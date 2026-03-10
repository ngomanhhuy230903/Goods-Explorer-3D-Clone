using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Food;
using FoodMatch.Order;
using FoodMatch.Tray;
using FoodMatch.Data;

namespace FoodMatch.Items
{
    [Booster]
    public class MagnetBooster : IBooster
    {
        public string BoosterName => "Magnet";

        private OrderQueue _orderQueue;
        private FoodGridSpawner _gridSpawner;
        private MonoBehaviour _runner;

        private const float FlyDuration = 0.55f;
        private const float JumpPower = 3.5f;
        private const float StaggerDelay = 0.18f;
        private const float RestoreColorDuration = 0.2f;

        public void Initialize(BoosterContext ctx)
        {
            _orderQueue = ctx.OrderQueue;
            _gridSpawner = ctx.FoodGridSpawner;
            _runner = ctx.CoroutineRunner;
        }

        public bool CanExecute()
        {
            if (_orderQueue == null || _gridSpawner == null) return false;
            return FindOldestActiveTray() != null;
        }

        public void Execute() => _runner.StartCoroutine(MagnetRoutine());

        private IEnumerator MagnetRoutine()
        {
            var targetTray = FindOldestActiveTray();
            if (targetTray == null) yield break;

            int foodID = targetTray.FoodID;
            int needed = targetTray.RemainingCount;

            Debug.Log($"[Magnet] tray#{targetTray.TrayIndex} foodID={foodID} cần={needed}");

            var foods = CollectFoodsFromTray(foodID, needed);
            if (foods.Count == 0)
            {
                Debug.LogWarning("[Magnet] Không tìm thấy food!");
                yield break;
            }

            // ── Cache slotIndex + scale TRƯỚC khi bất kỳ animation nào chạy ──
            // DeliveredCount chưa tăng trong lúc nhiều food bay song song
            // → phải tính slot từng cái một tại đây
            int baseDelivered = targetTray.OrderData.DeliveredCount;

            for (int i = 0; i < foods.Count; i++)
            {
                var entry = foods[i];
                int slot = Mathf.Clamp(baseDelivered + i, 0, targetTray.OrderData.TotalRequired - 1);
                float delay = i * StaggerDelay;

                // ── Dùng ĐÚNG công thức scale của FoodFlowController ──────────
                // FoodFlowController.ExecuteOrderCommand:
                //   targetScale = prefabScale * orderSlotScaleMultiplier
                // → gọi FoodFlowController.Instance.GetOrderTargetScale() để lấy
                //   cùng giá trị, thay vì tự tính lại (dễ lệch nếu multiplier thay đổi)
                Vector3 cachedScale = FoodFlowController.Instance != null
                    ? FoodFlowController.Instance.GetOrderTargetScale(entry.Data)
                    : entry.Data.prefab.transform.localScale * 0.85f; // fallback

                _runner.StartCoroutine(
                    AnimateSingleFood(entry, targetTray, slot, cachedScale, delay));
            }

            yield return new WaitForSeconds(
                (foods.Count - 1) * StaggerDelay + FlyDuration + 0.6f);

            Debug.Log("[Magnet] Done.");
        }

        private IEnumerator AnimateSingleFood(
            FoodEntry entry, OrderTray targetTray,
            int slotIndex, Vector3 targetScale, float delay)
        {
            yield return new WaitForSeconds(delay);

            FoodItem food = entry.FoodItem;

            if (food == null)
            {
                // Layer 2 pending → spawn mới từ pool
                food = SpawnFoodFromData(entry.Data);
                if (food == null) yield break;
                entry.OwnerTray?.RemovePendingFood(entry.Data);
            }
            else
            {
                bool removed = food.OwnerTray != null
                    ? food.OwnerTray.ForceRemoveFromAnyLayer(food)
                    : false;

                if (!removed && food.OwnerTray != null)
                {
                    Debug.LogWarning($"[Magnet] Không remove được {food.name}");
                    yield break;
                }

                // Food ở layer 1 (xám/nhỏ) → restore visual về layer 0 trước khi bay
                if (entry.LayerIndex >= 1)
                {
                    food.SetLayerVisual(0);
                    // Restore về prefab scale gốc rồi mới scale về targetScale khi bay
                    food.transform
                        .DOScale(food.Data.prefab.transform.localScale, RestoreColorDuration)
                        .SetEase(Ease.OutBack);
                    yield return new WaitForSeconds(RestoreColorDuration);
                }
            }

            Vector3 targetPos = targetTray.GetSlotWorldPosition(slotIndex);

            food.transform.DOKill();

            // Scale thu nhỏ về đúng kích cỡ order slot TRONG LÚC BAY
            // (giống hệt FoodFlowController.ExecuteOrderCommand)
            food.transform
                .DOScale(targetScale, FlyDuration * 0.6f)
                .SetEase(Ease.InOutSine);

            food.transform
                .DOJump(targetPos, JumpPower, 1, FlyDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (food == null || targetTray == null) return;

                    // Hard-set để tránh sub-frame drift
                    food.transform.localScale = targetScale;
                    targetTray.ConfirmDelivery(slotIndex);

                    DOVirtual.DelayedCall(0.35f, () =>
                    {
                        if (food != null)
                            PoolManager.Instance.ReturnFood(food.FoodID, food.gameObject);
                    }, false);
                });
        }

        private FoodItem SpawnFoodFromData(FoodItemData data)
        {
            if (data == null) return null;
            var go = PoolManager.Instance.GetFood(data.foodID, Vector3.zero);
            if (go == null) return null;
            var food = go.GetComponent<FoodItem>();
            if (food == null) return null;
            food.Initialize(data, layerIndex: 0);
            return food;
        }

        private List<FoodEntry> CollectFoodsFromTray(int foodID, int count)
        {
            var result = new List<FoodEntry>();
            var allFoods = _gridSpawner.GetAllActiveFoods();

            foreach (var f in allFoods)   // Layer 0 ưu tiên
            {
                if (result.Count >= count) break;
                if (f.OwnerTray == null || f.FoodID != foodID || f.LayerIndex != 0) continue;
                result.Add(new FoodEntry { FoodItem = f, Data = f.Data, LayerIndex = 0, OwnerTray = f.OwnerTray });
            }

            if (result.Count < count)
            {
                foreach (var f in allFoods)   // Layer 1
                {
                    if (result.Count >= count) break;
                    if (f.OwnerTray == null || f.FoodID != foodID || f.LayerIndex != 1) continue;
                    result.Add(new FoodEntry { FoodItem = f, Data = f.Data, LayerIndex = 1, OwnerTray = f.OwnerTray });
                }
            }

            if (result.Count < count)
            {
                var pending = _gridSpawner.GetPendingFoodsOfType(foodID);
                foreach (var data in pending)
                {
                    if (result.Count >= count) break;
                    result.Add(new FoodEntry { FoodItem = null, Data = data, LayerIndex = 2, OwnerTray = FindTrayWithPending(data) });
                }
            }

            return result;
        }

        private FoodTray FindTrayWithPending(FoodItemData data)
        {
            var trays = _gridSpawner.GetCellContainer()
                .GetComponentsInChildren<FoodTray>(includeInactive: false);
            foreach (var tray in trays)
                if (tray.GetPendingFoodsOfType(data.foodID).Contains(data))
                    return tray;
            return null;
        }

        private OrderTray FindOldestActiveTray()
        {
            OrderTray oldest = null;
            int minIndex = int.MaxValue;
            foreach (var tray in _orderQueue.GetActiveTrays())
            {
                if (tray == null) continue;
                if (tray.CurrentStateId != OrderTrayStateId.Active) continue;
                if (tray.RemainingCount <= 0) continue;
                if (tray.TrayIndex < minIndex) { minIndex = tray.TrayIndex; oldest = tray; }
            }
            return oldest;
        }

        private struct FoodEntry
        {
            public FoodItem FoodItem;
            public FoodItemData Data;
            public int LayerIndex;
            public FoodTray OwnerTray;
        }
    }
}