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

            // Strategy factory — swap không cần sửa thêm dòng nào khác
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
                // 2a. Tạo OrderDeliveryCommand (slot đã reserved trong TryMatchFoodWithReservation)
                var cmd = new OrderDeliveryCommand(food, matchResult.Tray, matchResult.SlotIndex);
                ExecuteOrderCommand(cmd, onComplete);
            }
            else
            {
                // 2b. Thử reserve slot trong BackupTray
                int backupSlot = _backupTray.TryReserveNextSlot(instanceId);

                if (backupSlot < 0)
                {
                    // BackupTray đầy → THUA
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

            // Validate tray còn Active không (có thể đã complete giữa chừng)
            if (orderTray == null || orderTray.CurrentStateId != OrderTrayStateId.Active)
            {
                cmd.Cancel();
                // Fallback: thử lại với tray khác hoặc về BackupTray
                BuildAndExecuteDeliveryCommand(food, onComplete);
                return;
            }

            Vector3 prefabScale = food.Data.prefab.transform.localScale;
            Vector3 targetScale = prefabScale * orderSlotScaleMultiplier;
            Vector3 targetPos = orderTray.GetSlotWorldPosition(slotIndex);

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
                // Re-validate: tray có thể complete từ food khác bay vào trước
                if (orderTray.CurrentStateId != OrderTrayStateId.Active)
                {
                    cmd.MarkFailed();
                    PoolManager.Instance.ReturnFood(food.FoodID, food.gameObject);
                    onComplete?.Invoke();
                    return;
                }

                SpawnSparkleVFX(targetPos);
                orderTray.ConfirmDelivery(slotIndex);
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
                _backupTray.ReceiveFood(food, slotIndex); // confirm vào đúng slot đã reserved
                cmd.MarkCompleted(); // release reservation từ registry
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

                // Reserve slot ngay tại đây — nếu không còn slot thì không add vào queue
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

                // Validate tray trước khi chạy
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

                bool done = false;
                ExecuteOrderCommand(cmd, () => done = true);

                yield return new WaitUntil(() => done);
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

            // Reserve slot order ngay lập tức
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

        /// <summary>
        /// Loop lấy TẤT CẢ food cùng foodID từ buffer lên order.
        /// Dùng while loop thay vì raise event nhiều lần.
        /// </summary>
        private IEnumerator SendBufferFoodCoroutine(int foodID)
        {
            while (_foodBuffer != null && _foodBuffer.HasFoodOfType(foodID))
            {
                // Kiểm tra còn order cần foodID này không
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

                bool done = false;
                var cmd = new OrderDeliveryCommand(food, matchResult.Tray, matchResult.SlotIndex);
                ExecuteOrderCommand(cmd, () => done = true);

                yield return new WaitUntil(() => done);
                yield return new WaitForSeconds(autoMatchStaggerDelay);
            }
        }

        #endregion
        private void Log(string msg) => Debug.Log(msg);
    }
}