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

            for (int i = 0; i < foods.Count; i++)
            {
                var entry = foods[i];
                float delay = i * StaggerDelay;
                _runner.StartCoroutine(
                    AnimateSingleFood(entry, targetTray,
                        targetTray.OrderData.DeliveredCount + i, delay));
            }

            yield return new WaitForSeconds(
                (foods.Count - 1) * StaggerDelay + FlyDuration + 0.6f);

            Debug.Log("[Magnet] Done.");
        }

        private IEnumerator AnimateSingleFood(
            FoodEntry entry, OrderTray targetTray, int rawSlot, float delay)
        {
            yield return new WaitForSeconds(delay);

            FoodItem food = entry.FoodItem;

            if (food == null)
            {
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

                if (entry.LayerIndex >= 1)
                {
                    food.SetLayerVisual(0);
                    Vector3 prefabScaleRestore = food.Data.prefab.transform.localScale;
                    food.transform
                        .DOScale(prefabScaleRestore, RestoreColorDuration)
                        .SetEase(Ease.OutBack);
                    yield return new WaitForSeconds(RestoreColorDuration);
                }
            }

            int slotIndex = Mathf.Clamp(
                rawSlot, 0, targetTray.OrderData.TotalRequired - 1);

            Vector3 targetPos = targetTray.GetSlotWorldPosition(slotIndex);

            // ── Cache scale ĐÚNG tại thời điểm này ───────────────────────────────
            // GetNextSlotFoodScale() phụ thuộc DeliveredCount → cache ngay trước khi confirm
            // Scale = prefabScale * orderSlotScaleMultiplier (lấy từ OrderSlotUI)
            Vector3 targetScale = GetCorrectOrderSlotScale(food, targetTray, slotIndex);

            food.transform.DOKill();

            // Scale về targetScale TRONG LÚC BAY (không đợi OnComplete)
            food.transform
                .DOScale(targetScale, FlyDuration * 0.6f)
                .SetEase(Ease.InOutSine);

            food.transform
                .DOJump(targetPos, JumpPower, 1, FlyDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (food == null || targetTray == null) return;

            // Hard set scale đúng sau khi đến nơi
            food.transform.localScale = targetScale;
                    targetTray.ConfirmDelivery(slotIndex);

                    DOVirtual.DelayedCall(0.35f, () =>
                    {
                        if (food != null)
                            PoolManager.Instance.ReturnFood(food.FoodID, food.gameObject);
                    }, false);
                });
        }

        /// <summary>
        /// Tính scale đúng cho food khi nằm trong OrderTray slot.
        /// OrderSlotUI.FoodTargetScale đã nhân sẵn multiplier.
        /// Fallback: prefabScale * 0.85f nếu không lấy được.
        /// </summary>
        private Vector3 GetCorrectOrderSlotScale(FoodItem food, OrderTray targetTray, int slotIndex)
        {
            // Thử lấy từ OrderTray.GetNextSlotFoodScale() — đây là scale chuẩn
            Vector3 slotScale = targetTray.GetNextSlotFoodScale();

            // Validate: nếu scale quá nhỏ hoặc zero thì fallback
            if (slotScale.sqrMagnitude < 0.001f)
            {
                Vector3 prefabScale = food.Data?.prefab != null
                    ? food.Data.prefab.transform.localScale
                    : Vector3.one;
                return prefabScale * 0.85f;
            }

            return slotScale;
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

            // Layer 0
            foreach (var f in allFoods)
            {
                if (result.Count >= count) break;
                if (f.OwnerTray == null || f.FoodID != foodID) continue;
                if (f.LayerIndex == 0)
                    result.Add(new FoodEntry
                    { FoodItem = f, Data = f.Data, LayerIndex = 0, OwnerTray = f.OwnerTray });
            }

            // Layer 1
            if (result.Count < count)
            {
                foreach (var f in allFoods)
                {
                    if (result.Count >= count) break;
                    if (f.OwnerTray == null || f.FoodID != foodID) continue;
                    if (f.LayerIndex == 1)
                        result.Add(new FoodEntry
                        { FoodItem = f, Data = f.Data, LayerIndex = 1, OwnerTray = f.OwnerTray });
                }
            }

            // Layer 2+ (pending data)
            if (result.Count < count)
            {
                var pending = _gridSpawner.GetPendingFoodsOfType(foodID);
                foreach (var data in pending)
                {
                    if (result.Count >= count) break;
                    // Tìm tray chứa pending data này
                    var ownerTray = FindTrayWithPending(data);
                    result.Add(new FoodEntry
                    { FoodItem = null, Data = data, LayerIndex = 2, OwnerTray = ownerTray });
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
                if (tray.TrayIndex < minIndex)
                {
                    minIndex = tray.TrayIndex;
                    oldest = tray;
                }
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