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

        // ── Anti-spam: track reserved slots per tray ──────────────────────────
        // Key: TrayIndex, Value: số slot đã được reserve (đang bay, chưa Confirm)
        private static readonly Dictionary<int, int> _reservedSlots = new();

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

            int trayKey = targetTray.TrayIndex;
            int foodID = targetTray.FoodID;

            // ── Tính số slot còn THỰC SỰ cần (trừ đi slot đang bay) ──────────
            int reserved = _reservedSlots.TryGetValue(trayKey, out var r) ? r : 0;
            int needed = targetTray.RemainingCount - reserved;

            if (needed <= 0)
            {
                Debug.Log($"[Magnet] tray#{trayKey} đang được fill đủ bởi lần trước, bỏ qua.");
                yield break;
            }

            Debug.Log($"[Magnet] tray#{trayKey} foodID={foodID} cần={needed} (reserved={reserved})");

            var foods = CollectFoodsFromTray(foodID, needed);
            if (foods.Count == 0)
            {
                Debug.LogWarning("[Magnet] Không tìm thấy food!");
                yield break;
            }

            // ── Reserve NGAY trước khi animation chạy ────────────────────────
            _reservedSlots[trayKey] = reserved + foods.Count;

            int baseDelivered = targetTray.OrderData.DeliveredCount + reserved;

            for (int i = 0; i < foods.Count; i++)
            {
                var entry = foods[i];
                int slot = Mathf.Clamp(baseDelivered + i, 0, targetTray.OrderData.TotalRequired - 1);
                float delay = i * StaggerDelay;

                Vector3 cachedScale = FoodFlowController.Instance != null
                    ? FoodFlowController.Instance.GetOrderTargetScale(entry.Data)
                    : entry.Data.prefab.transform.localScale * 0.85f;

                _runner.StartCoroutine(
                    AnimateSingleFood(entry, targetTray, slot, cachedScale, delay, trayKey));
            }

            yield return new WaitForSeconds(
                (foods.Count - 1) * StaggerDelay + FlyDuration + 0.6f);

            Debug.Log("[Magnet] Done.");
        }

        private IEnumerator AnimateSingleFood(
            FoodEntry entry, OrderTray targetTray,
            int slotIndex, Vector3 targetScale, float delay, int trayKey)
        {
            yield return new WaitForSeconds(delay);

            FoodItem food = entry.FoodItem;

            if (food == null)
            {
                food = SpawnFoodFromData(entry.Data);
                if (food == null)
                {
                    ReleaseReservation(trayKey); // trả lại reservation nếu spawn thất bại
                    yield break;
                }
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
                    ReleaseReservation(trayKey);
                    yield break;
                }

                if (entry.LayerIndex >= 1)
                {
                    food.SetLayerVisual(0);
                    food.transform
                        .DOScale(food.Data.prefab.transform.localScale, RestoreColorDuration)
                        .SetEase(Ease.OutBack);
                    yield return new WaitForSeconds(RestoreColorDuration);
                }
            }

            Vector3 targetPos = targetTray.GetSlotWorldPosition(slotIndex);

            food.transform.DOKill();

            food.transform
                .DOScale(targetScale, FlyDuration * 0.6f)
                .SetEase(Ease.InOutSine);

            food.transform
                .DOJump(targetPos, JumpPower, 1, FlyDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (food == null || targetTray == null)
                    {
                        ReleaseReservation(trayKey);
                        return;
                    }

                    food.transform.localScale = targetScale;
                    targetTray.ConfirmDelivery(slotIndex);

                    // ── Giải phóng reservation SAU KHI ConfirmDelivery ────────
                    ReleaseReservation(trayKey);

                    DOVirtual.DelayedCall(0.35f, () =>
                    {
                        if (food != null)
                            PoolManager.Instance.ReturnFood(food.FoodID, food.gameObject);
                    }, false);
                });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void ReleaseReservation(int trayKey)
        {
            if (!_reservedSlots.TryGetValue(trayKey, out var cur)) return;
            int next = cur - 1;
            if (next <= 0) _reservedSlots.Remove(trayKey);
            else _reservedSlots[trayKey] = next;
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

            foreach (var f in allFoods)
            {
                if (result.Count >= count) break;
                if (f.OwnerTray == null || f.FoodID != foodID || f.LayerIndex != 0) continue;
                result.Add(new FoodEntry { FoodItem = f, Data = f.Data, LayerIndex = 0, OwnerTray = f.OwnerTray });
            }

            if (result.Count < count)
            {
                foreach (var f in allFoods)
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