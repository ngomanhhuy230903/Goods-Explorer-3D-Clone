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

            int reserved = _reservedSlots.TryGetValue(trayKey, out var r) ? r : 0;
            int needed = targetTray.RemainingCount - reserved;

            if (needed <= 0)
            {
                Debug.Log($"[Magnet] tray#{trayKey} đang được fill đủ bởi lần trước, bỏ qua.");
                yield break;
            }

            Debug.Log($"[Magnet] tray#{trayKey} foodID={foodID} cần={needed} (reserved={reserved})");

            var foods = CollectFoodsFromFoodTrays(foodID, needed);
            if (foods.Count == 0)
            {
                Debug.LogWarning("[Magnet] Không tìm thấy food trong FoodTray nào!");
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
                // FIX: Spawn tại vị trí của FoodTray (không phải Vector3.zero)
                food = SpawnFoodFromData(entry.Data, entry.SpawnWorldPosition);
                if (food == null)
                {
                    ReleaseReservation(trayKey);
                    yield break;
                }
                // FIX: Data đã được RemovePendingFood ngay lúc collect, không cần gọi lại ở đây
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

                    ReleaseReservation(trayKey);

                    DOVirtual.DelayedCall(0.35f, () =>
                    {
                        if (food != null)
                            PoolManager.Instance.ReturnFood(food.FoodID, food.gameObject);
                    }, false);
                });
        }

        // ── Core Fix: chỉ lấy food từ FoodTray, không lấy lung tung ──────────

        /// <summary>
        /// Thu thập food từ các FoodTray theo thứ tự ưu tiên:
        /// 1. Layer 0 (interactive, sẵn sàng)
        /// 2. Layer 1 (greyed-out, vẫn spawned)
        /// 3. Pending layer 2+ (chưa spawn — sẽ tạo GameObject tại vị trí FoodTray)
        ///
        /// FIX: Với pending food, RemovePendingFood NGAY TẠI ĐÂY để trừ khỏi FoodTray.TotalFoodCount
        ///      trước khi animation bắt đầu, tránh race condition.
        /// </summary>
        private List<FoodEntry> CollectFoodsFromFoodTrays(int foodID, int count)
        {
            var result = new List<FoodEntry>();

            // Lấy tất cả FoodTray đang active trong scene
            var foodTrays = _gridSpawner.GetCellContainer()
                .GetComponentsInChildren<FoodTray>(includeInactive: false);

            // ── Pass 1: Layer 0 ───────────────────────────────────────────────
            foreach (var tray in foodTrays)
            {
                if (result.Count >= count) break;

                var layer0Anchors = tray.GetLayer0Anchors();
                // Duyệt qua item trong layer 0 của tray này
                // (dùng TopItem + GetAllActiveFoods filtered by tray + layer)
                var activeFoods = _gridSpawner.GetAllActiveFoods();
                foreach (var f in activeFoods)
                {
                    if (result.Count >= count) break;
                    if (f.OwnerTray != tray) continue;
                    if (f.FoodID != foodID) continue;
                    if (f.LayerIndex != 0) continue;

                    result.Add(new FoodEntry
                    {
                        FoodItem = f,
                        Data = f.Data,
                        LayerIndex = 0,
                        OwnerTray = tray,
                        SpawnWorldPosition = f.transform.position
                    });
                }
            }

            // ── Pass 2: Layer 1 ───────────────────────────────────────────────
            if (result.Count < count)
            {
                var activeFoods = _gridSpawner.GetAllActiveFoods();
                foreach (var tray in foodTrays)
                {
                    if (result.Count >= count) break;

                    foreach (var f in activeFoods)
                    {
                        if (result.Count >= count) break;
                        if (f.OwnerTray != tray) continue;
                        if (f.FoodID != foodID) continue;
                        if (f.LayerIndex != 1) continue;

                        // Không lấy item đã được chọn ở pass 1
                        bool alreadyPicked = false;
                        foreach (var e in result)
                            if (e.FoodItem == f) { alreadyPicked = true; break; }
                        if (alreadyPicked) continue;

                        result.Add(new FoodEntry
                        {
                            FoodItem = f,
                            Data = f.Data,
                            LayerIndex = 1,
                            OwnerTray = tray,
                            SpawnWorldPosition = f.transform.position
                        });
                    }
                }
            }

            // ── Pass 3: Pending layer 2+ ──────────────────────────────────────
            // FIX: Spawn tại vị trí FoodTray, xoá ngay khỏi _pendingLayers
            if (result.Count < count)
            {
                foreach (var tray in foodTrays)
                {
                    if (result.Count >= count) break;

                    // GetPendingFoodsOfType trả về list data match foodID trong toàn bộ pending layers
                    var pendingMatches = tray.GetPendingFoodsOfType(foodID);
                    foreach (var data in pendingMatches)
                    {
                        if (result.Count >= count) break;
                        if (data == null) continue;

                        // FIX: Xoá khỏi FoodTray.pendingLayers NGAY LÚC NÀY
                        // để TotalFoodCount và MaxFoodCapacity phản ánh đúng
                        bool removed = tray.RemovePendingFood(data);
                        if (!removed)
                        {
                            Debug.LogWarning($"[Magnet] Không remove được pending data {data.foodName} khỏi tray {tray.TrayID}");
                            continue;
                        }

                        // Vị trí spawn = vị trí của FoodTray (center) để food xuất hiện đúng chỗ
                        Vector3 spawnPos = tray.transform.position;

                        result.Add(new FoodEntry
                        {
                            FoodItem = null,        // chưa có GameObject, sẽ spawn trong AnimateSingleFood
                            Data = data,
                            LayerIndex = 2,
                            OwnerTray = tray,
                            SpawnWorldPosition = spawnPos
                        });
                    }
                }
            }

            return result;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void ReleaseReservation(int trayKey)
        {
            if (!_reservedSlots.TryGetValue(trayKey, out var cur)) return;
            int next = cur - 1;
            if (next <= 0) _reservedSlots.Remove(trayKey);
            else _reservedSlots[trayKey] = next;
        }

        /// <summary>
        /// FIX: Spawn tại spawnWorldPosition (vị trí FoodTray) thay vì Vector3.zero.
        /// </summary>
        private FoodItem SpawnFoodFromData(FoodItemData data, Vector3 spawnWorldPosition)
        {
            if (data == null) return null;
            var go = PoolManager.Instance.GetFood(data.foodID, spawnWorldPosition);
            if (go == null) return null;
            var food = go.GetComponent<FoodItem>();
            if (food == null) return null;
            food.Initialize(data, layerIndex: 0);
            return food;
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
            /// <summary>
            /// Vị trí world để spawn food (với pending layer: vị trí của FoodTray).
            /// </summary>
            public Vector3 SpawnWorldPosition;
        }
    }
}