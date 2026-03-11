using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;
using FoodMatch.Tray;
using FoodMatch.Order;
using FoodMatch.Flow;

namespace FoodMatch.Food
{
    public class FoodFlowController : MonoBehaviour
    {
        // ─── Singleton ────────────────────────────────────────────────────────
        public static FoodFlowController Instance { get; private set; }

        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Jump Config ─────────────────────")]
        [SerializeField] private float orderJumpPower = 3.5f;
        [SerializeField] private int orderJumpCount = 1;
        [SerializeField] private float orderJumpDuration = 0.55f;
        [SerializeField] private float backupJumpPower = 2f;
        [SerializeField] private float backupJumpDuration = 0.4f;

        [Header("─── Scale Multipliers ─────────────────")]
        [Tooltip("prefabScale × multiplier = scale trong OrderTray slot")]
        [SerializeField] private float orderSlotScaleMultiplier = 0.85f;
        [Tooltip("prefabScale × multiplier = scale trong BackupTray slot")]
        [SerializeField] private float backupSlotScaleMultiplier = 0.70f;

        [Header("─── Premium VFX Strategy ──────────────")]
        [Tooltip("Bật để dùng ArcPunchFlyStrategy thay JumpFlyStrategy cho OrderTray")]
        [SerializeField] private bool usePremiumFlyEffect = false;

        [Header("─── Auto-Match Config ─────────────────")]
        [Tooltip("Delay giữa mỗi food auto-bay từ BackupTray → OrderTray")]
        [SerializeField] private float autoMatchStaggerDelay = 0.25f;

        [Header("─── VFX ─────────────────────────────")]
        [SerializeField] private GameObject sparkleVFXPrefab;
        [Header("─── Buffer ───────────────────────────")]
        [SerializeField] private FoodMatch.Tray.FoodBuffer _foodBuffer;

        // ─── Runtime Dependencies ─────────────────────────────────────────────
        private OrderQueue _orderQueue;
        private BackupTray _backupTray;
        private bool _isReady = false;
        private bool _isAutoMatching = false;

        // ─── Strategies (khởi tạo theo usePremiumFlyEffect) ───────────────────
        private IFlyStrategy _orderFlyStrategy;
        private IFlyStrategy _backupFlyStrategy;

        // ─────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _orderFlyStrategy = FlyStrategyFactory.CreateOrderStrategy(usePremiumFlyEffect);
            _backupFlyStrategy = FlyStrategyFactory.CreateBackupStrategy();
        }

        private void OnEnable()
        {
            EventBus.OnNewOrderActive += HandleNewOrderActive;
            EventBus.OnBufferFoodReady += HandleBufferFoodReady;
        }
        private void OnDisable()
        {
            EventBus.OnNewOrderActive -= HandleNewOrderActive;
            EventBus.OnBufferFoodReady -= HandleBufferFoodReady;
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Dependency Injection

        public void Inject(OrderQueue orderQueue, BackupTray backupTray)
        {
            _orderQueue = orderQueue;
            _backupTray = backupTray;
            _isReady = true;
            Debug.Log("[FoodFlowController] Inject OK.");
        }

        public void ResetDependencies()
        {
            _orderQueue = null;
            _backupTray = null;
            _isReady = false;
            _isAutoMatching = false;
            SlotReservationRegistry.Instance.ClearAll();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>Gọi bởi FoodInteractionHandler khi player tap vào food.</summary>
        public void HandleFoodTapped(FoodItem foodItem, Action onComplete = null)
        {
            if (!_isReady)
            {
                Debug.LogError("[FoodFlowController] Chưa Inject!");
                onComplete?.Invoke();
                return;
            }

            if (foodItem == null || foodItem.Data == null)
            {
                onComplete?.Invoke();
                return;
            }

            // Food trong BackupTray (ownerTray == null)
            if (foodItem.OwnerTray == null)
            {
                HandleBackupFoodTapped(foodItem, onComplete);
                return;
            }

            // Pop food ra khỏi FoodTray trước
            FoodItem poppedItem = foodItem.OwnerTray.TryPopItem(foodItem);
            if (poppedItem == null) { onComplete?.Invoke(); return; }

            // Tạo và execute command pipeline
            BuildAndExecuteDeliveryCommand(poppedItem, onComplete);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Command Pipeline (Factory + Command Pattern)

        /// <summary>
        /// CORE LOGIC: Match → Reserve slot ngay → Tạo Command → Execute.
        /// Reservation xảy ra synchronously trước animation → tránh race condition.
        /// </summary>
        private void BuildAndExecuteDeliveryCommand(FoodItem food, Action onComplete)
        {
            int instanceId = food.GetInstanceID();

            // 1. Thử match vào OrderTray (kèm reserve slot ngay lập tức)
            var matchResult = _orderQueue.TryMatchFoodWithReservation(food.Data.foodID, instanceId);

            if (matchResult.IsMatch)
            {
                var cmd = new OrderDeliveryCommand(food, matchResult.Tray, matchResult.SlotIndex);
                ExecuteOrderCommand(cmd, onComplete);
            }
            else
            {
                // 2b. Thử reserve slot trong BackupTray
                int backupSlot = _backupTray.TryReserveNextSlot(instanceId);

                if (backupSlot < 0)
                {
                    Debug.Log("[FoodFlowController] BackupTray đầy → THUA!");
                    EventBus.RaiseBackupFull();
                    PoolManager.Instance.ReturnFood(food.FoodID, food.gameObject);
                    onComplete?.Invoke();
                    return;
                }

                var cmd = new BackupDeliveryCommand(food, backupSlot);
                ExecuteBackupCommand(cmd, onComplete);
            }
        }

        // ─── Execute Order Command ─────────────────────────────────────────────

        private void ExecuteOrderCommand(OrderDeliveryCommand cmd, Action onComplete)
        {
            cmd.Execute(null); // mark Executing

            var food = cmd.Food;
            var orderTray = cmd.TargetTray;
            int slotIndex = cmd.SlotIndex;

            // Validate tray còn Active không
            if (orderTray == null || orderTray.CurrentStateId != OrderTrayStateId.Active)
            {
                cmd.Cancel();
                BuildAndExecuteDeliveryCommand(food, onComplete);
                return;
            }

            Vector3 prefabScale = food.Data.prefab.transform.localScale;
            Vector3 targetScale = prefabScale * orderSlotScaleMultiplier;
            Vector3 targetPos = orderTray.GetSlotWorldPosition(slotIndex);

            // ─── [FIX] PreConfirmDelivery NGAY TRƯỚC KHI BAY ────────────────
            // Đánh dấu slot là đã delivered về mặt logic (OrderData.DeliveredCount++).
            // Food thứ 2 cùng type bay đồng thời sẽ thấy DeliveredCount đã tăng
            // → TryMatchAndReserve sẽ cấp slot TIẾP THEO thay vì trùng slot này.
            orderTray.PreConfirmDelivery(slotIndex);
            // ────────────────────────────────────────────────────────────────

            var orderConfig = new FlyConfig
            {
                jumpPower = orderJumpPower,
                jumpCount = orderJumpCount,
                duration = orderJumpDuration,
                easeMove = DG.Tweening.Ease.OutQuad,
                easeScale = DG.Tweening.Ease.InOutSine
            };

            _orderFlyStrategy.Execute(food, targetPos, targetScale, orderConfig, () =>
            {
                // Re-validate: tray vẫn phải tồn tại (không bị despawn giữa chừng)
                if (orderTray == null)
                {
                    cmd.MarkFailed();
                    PoolManager.Instance.ReturnFood(food.FoodID, food.gameObject);
                    onComplete?.Invoke();
                    return;
                }

                SpawnSparkleVFX(targetPos);

                // [FIX] Chỉ chạy VISUAL (animation slot icon, transition Completed nếu đủ)
                // Logic đã được confirm ở PreConfirmDelivery phía trên.
                orderTray.FinalizeDeliveryVisual(slotIndex);

                cmd.MarkCompleted(); // release reservation

                DOVirtual.DelayedCall(0.35f,
                    () => PoolManager.Instance.ReturnFood(food.FoodID, food.gameObject),
                    false);

                onComplete?.Invoke();
            });
        }

        // ─── Execute Backup Command ────────────────────────────────────────────

        private void ExecuteBackupCommand(BackupDeliveryCommand cmd, Action onComplete)
        {
            cmd.Execute(null); // mark Executing

            var food = cmd.Food;
            int slotIndex = cmd.SlotIndex;

            Vector3 prefabScale = food.Data.prefab.transform.localScale;
            Vector3 targetScale = prefabScale * backupSlotScaleMultiplier;
            Vector3 targetPos = _backupTray.GetSlotWorldPosition(slotIndex);

            var backupConfig = new FlyConfig
            {
                jumpPower = backupJumpPower,
                jumpCount = 1,
                duration = backupJumpDuration,
                easeMove = DG.Tweening.Ease.OutQuad,
                easeScale = DG.Tweening.Ease.InOutSine
            };

            _backupFlyStrategy.Execute(food, targetPos, targetScale, backupConfig, () =>
            {
                _backupTray.ReceiveFood(food, slotIndex);
                cmd.MarkCompleted();
                EventBus.RaiseFoodToBackup(food.Data);

                onComplete?.Invoke();
            });
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Auto-Match: BackupTray → OrderTray (Observer response)

        /// <summary>
        /// Observer callback khi OrderQueue kích hoạt order mới.
        /// Scan BackupTray tìm matching food và auto-fly lên OrderTray.
        /// </summary>
        private void HandleNewOrderActive(int newOrderFoodID)
        {
            if (!_isReady || _backupTray == null || _orderQueue == null) return;

            var allBackupFoods = _backupTray.GetAllFoods();
            if (allBackupFoods == null || allBackupFoods.Count == 0) return;

            // Build command queue: reserve slot NGAY cho từng food match được
            var commandQueue = new Queue<OrderDeliveryCommand>();

            foreach (var food in allBackupFoods)
            {
                if (food == null || food.Data == null) continue;

                var matchResult = _orderQueue.TryMatchFoodWithReservation(
                    food.Data.foodID, food.GetInstanceID());

                if (!matchResult.IsMatch) continue;

                var cmd = new OrderDeliveryCommand(food, matchResult.Tray, matchResult.SlotIndex);
                commandQueue.Enqueue(cmd);
            }

            if (commandQueue.Count == 0) return;

            StartCoroutine(AutoMatchCoroutine(commandQueue));
        }

        private IEnumerator AutoMatchCoroutine(Queue<OrderDeliveryCommand> commandQueue)
        {
            _isAutoMatching = true;

            while (commandQueue.Count > 0)
            {
                var cmd = commandQueue.Dequeue();

                if (cmd.Food == null || cmd.Food.Data == null)
                {
                    cmd.Cancel();
                    continue;
                }

                if (cmd.TargetTray == null ||
                    cmd.TargetTray.CurrentStateId != OrderTrayStateId.Active)
                {
                    cmd.Cancel();
                    continue;
                }

                // Remove khỏi BackupTray trước khi bay
                if (!_backupTray.TryRemoveFood(cmd.Food))
                {
                    cmd.Cancel();
                    continue;
                }

                // [FIX] Không cần WaitUntil(done) nữa vì PreConfirmDelivery
                // đánh dấu slot ngay lập tức trong ExecuteOrderCommand.
                // Các food bay song song sẽ nhận slot khác nhau nhờ reservation.
                // Chỉ cần stagger delay để animation không chồng chéo nhau.
                ExecuteOrderCommand(cmd, null);

                yield return new WaitForSeconds(autoMatchStaggerDelay);
            }

            _isAutoMatching = false;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Backup Food Tap (Player chủ động tap food trong BackupTray)

        private void HandleBackupFoodTapped(FoodItem food, Action onComplete)
        {
            int instanceId = food.GetInstanceID();

            var matchResult = _orderQueue.TryMatchFoodWithReservation(
                food.Data.foodID, instanceId);

            if (matchResult.IsMatch)
            {
                _backupTray.TryRemoveFood(food);
                var cmd = new OrderDeliveryCommand(food, matchResult.Tray, matchResult.SlotIndex);
                ExecuteOrderCommand(cmd, onComplete);
            }
            else
            {
                food.PlayLockedBounce();
                onComplete?.Invoke();
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region VFX

        private void SpawnSparkleVFX(Vector3 worldPos)
        {
            var pooled = PoolManager.Instance?.GetSparkle(worldPos);
            if (pooled != null)
            {
                StartCoroutine(ReturnVFXAfterDelay(pooled, 1.5f));
                return;
            }

            if (sparkleVFXPrefab != null)
                Destroy(Instantiate(sparkleVFXPrefab, worldPos, Quaternion.identity), 2f);
        }

        private IEnumerator ReturnVFXAfterDelay(GameObject vfx, float delay)
        {
            yield return new WaitForSeconds(delay);
            PoolManager.Instance?.ReturnSparkle(vfx);
        }

        #endregion

        #region Buffer → OrderTray Auto-Send

        private void HandleBufferFoodReady(int foodID)
        {
            if (!_isReady || _foodBuffer == null || _orderQueue == null) return;
            if (!_foodBuffer.HasFoodOfType(foodID)) return;

            StartCoroutine(SendBufferFoodCoroutine(foodID));
        }

        private IEnumerator SendBufferFoodCoroutine(int foodID)
        {
            while (_foodBuffer != null && _foodBuffer.HasFoodOfType(foodID))
            {
                int instanceId = -(foodID * 10000 + (System.Environment.TickCount & 0x7FFF));
                var matchResult = _orderQueue.TryMatchFoodWithReservation(foodID, instanceId);

                if (!matchResult.IsMatch)
                {
                    Log($"[Buffer] Không còn slot cho foodID={foodID}");
                    yield break;
                }

                var food = _foodBuffer.TakeFood(foodID);
                if (food == null)
                {
                    matchResult.Tray?.ReleaseSlotReservation(matchResult.SlotIndex);
                    yield break;
                }

                var cmd = new OrderDeliveryCommand(food, matchResult.Tray, matchResult.SlotIndex);

                // [FIX] Tương tự AutoMatchCoroutine — không wait animation xong
                ExecuteOrderCommand(cmd, null);

                yield return new WaitForSeconds(autoMatchStaggerDelay);
            }
        }

        #endregion

        private void Log(string msg) => Debug.Log(msg);

        public Vector3 GetOrderTargetScale(FoodItemData data)
            => data.prefab.transform.localScale * orderSlotScaleMultiplier;
    }
}