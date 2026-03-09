using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;
using FoodMatch.Tray;
using FoodMatch.Order;

namespace FoodMatch.Food
{
    /// <summary>
    /// SINGLETON — Điều phối luồng tap food → bay → OrderTray hoặc BackupTray.
    ///
    /// THAY ĐỔI:
    ///   1. Lắng nghe EventBus.OnNewOrderActive → tự động scan BackupTray tìm match
    ///      và fly food từ BackupTray lên OrderTray (không cần player tap).
    ///   2. Scale = prefabScale × multiplier (có thể chỉnh trong Inspector).
    ///      VD: prefab scale (10,10,10) × 0.85 = (8.5,8.5,8.5).
    ///   3. BackupTray snap food về đúng anchor sau khi bay xong.
    ///
    /// FIX CS1061: tray.State → tray.CurrentStateId (OrderTrayStateId enum)
    /// </summary>
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
        [Tooltip("prefabScale × multiplier = scale cuối trong OrderTray slot.\n"
               + "VD: prefab=(10,10,10) × 0.85 → target=(8.5,8.5,8.5).")]
        [SerializeField] private float orderSlotScaleMultiplier = 0.85f;

        [Tooltip("prefabScale × multiplier = scale cuối trong BackupTray slot.\n"
               + "VD: prefab=(10,10,10) × 0.70 → target=(7,7,7).")]
        [SerializeField] private float backupSlotScaleMultiplier = 0.70f;

        [Header("─── Auto-Match Config ─────────────────")]
        [Tooltip("Delay (giây) giữa mỗi food tự động bay từ BackupTray lên OrderTray.\n"
               + "Tránh nhiều food bay cùng lúc gây rối mắt.")]
        [SerializeField] private float autoMatchStaggerDelay = 0.25f;

        [Header("─── VFX ─────────────────────────────")]
        [SerializeField] private GameObject sparkleVFXPrefab;

        // ─── Runtime Dependencies ─────────────────────────────────────────────
        private OrderQueue _orderQueue;
        private BackupTray _backupTray;
        private bool _isReady = false;
        private bool _isAutoMatching = false;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable() => EventBus.OnNewOrderActive += HandleNewOrderActive;
        private void OnDisable() => EventBus.OnNewOrderActive -= HandleNewOrderActive;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ─── Dependency Injection ─────────────────────────────────────────────

        public void Inject(OrderQueue orderQueue, BackupTray backupTray)
        {
            _orderQueue = orderQueue;
            _backupTray = backupTray;
            _isReady = true;
            Debug.Log("[FoodFlowController] Inject thành công.");
        }

        public void ResetDependencies()
        {
            _orderQueue = null;
            _backupTray = null;
            _isReady = false;
            _isAutoMatching = false;
        }

        // ─── Public API ───────────────────────────────────────────────────────

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

            var ownerTray = foodItem.OwnerTray;

            if (ownerTray == null)
            {
                HandleBackupFoodTapped(foodItem, onComplete);
                return;
            }

            FoodItem poppedItem = ownerTray.TryPopItem(foodItem);
            if (poppedItem == null) { onComplete?.Invoke(); return; }

            var matchResult = _orderQueue.TryMatchFood(poppedItem.Data.foodID);

            if (matchResult.IsMatch)
                FlyToOrderSlot(poppedItem, matchResult.Tray, matchResult.SlotIndex, onComplete);
            else
                FlyToBackupTray(poppedItem, onComplete);
        }

        // ─── Auto-Match: BackupTray → OrderTray ──────────────────────────────

        private void HandleNewOrderActive(int newOrderFoodID)
        {
            if (!_isReady || _backupTray == null || _orderQueue == null) return;

            var allBackupFoods = _backupTray.GetAllFoods();
            if (allBackupFoods == null || allBackupFoods.Count == 0) return;

            var matchQueue = new Queue<(FoodItem food, OrderTray tray, int slotIndex)>();

            foreach (var food in allBackupFoods)
            {
                if (food == null || food.Data == null) continue;
                var matchResult = _orderQueue.TryMatchFood(food.Data.foodID);
                if (matchResult.IsMatch)
                    matchQueue.Enqueue((food, matchResult.Tray, matchResult.SlotIndex));
            }

            if (matchQueue.Count == 0) return;

            if (!_isAutoMatching)
                StartCoroutine(AutoMatchCoroutine(matchQueue));
        }

        private IEnumerator AutoMatchCoroutine(
            Queue<(FoodItem food, OrderTray tray, int slotIndex)> matchQueue)
        {
            _isAutoMatching = true;

            while (matchQueue.Count > 0)
            {
                var (food, tray, slotIndex) = matchQueue.Dequeue();

                if (food == null || food.Data == null) continue;

                // ── FIX CS1061: dùng CurrentStateId (OrderTrayStateId) thay vì State (OrderState) ──
                if (tray == null || tray.CurrentStateId != OrderTrayStateId.Active) continue;

                if (!_backupTray.TryRemoveFood(food)) continue;

                var recheck = _orderQueue.TryMatchFood(food.Data.foodID);
                int validSlot = recheck.IsMatch ? recheck.SlotIndex : slotIndex;
                OrderTray validTray = recheck.IsMatch ? recheck.Tray : tray;

                bool done = false;
                FlyToOrderSlot(food, validTray, validSlot, () => done = true);

                yield return new WaitUntil(() => done);
                yield return new WaitForSeconds(autoMatchStaggerDelay);
            }

            _isAutoMatching = false;
        }

        // ─── Bay lên OrderTray ────────────────────────────────────────────────

        private void FlyToOrderSlot(FoodItem food, OrderTray orderTray,
                                    int slotIndex, Action onComplete)
        {
            Vector3 prefabScale = food.Data.prefab.transform.localScale;
            Vector3 targetScale = prefabScale * orderSlotScaleMultiplier;
            Vector3 targetPos = orderTray.GetNextSlotWorldPosition();

            DisableCollider(food);

            DOTween.Sequence()
                .Append(food.transform
                    .DOJump(targetPos, orderJumpPower, orderJumpCount, orderJumpDuration)
                    .SetEase(Ease.OutQuad))
                .Join(food.transform
                    .DOScale(targetScale, orderJumpDuration * 0.8f)
                    .SetEase(Ease.InOutSine))
                .OnComplete(() =>
                {
                    food.transform.position = targetPos;
                    food.transform.localScale = targetScale;

                    SpawnSparkleVFX(targetPos);
                    orderTray.ConfirmDelivery(slotIndex);

                    DOVirtual.DelayedCall(0.35f,
                        () => PoolManager.Instance.ReturnFood(food.FoodID, food.gameObject),
                        false);

                    onComplete?.Invoke();
                })
                .SetUpdate(false)
                .Play();
        }

        // ─── Bay vào BackupTray ───────────────────────────────────────────────

        private void FlyToBackupTray(FoodItem food, Action onComplete)
        {
            if (!_backupTray.HasFreeSlot())
            {
                Debug.Log("[FoodFlowController] BackupTray đầy → THUA!");
                EventBus.RaiseBackupFull();
                PoolManager.Instance.ReturnFood(food.FoodID, food.gameObject);
                onComplete?.Invoke();
                return;
            }

            Vector3 prefabScale = food.Data.prefab.transform.localScale;
            Vector3 targetScale = prefabScale * backupSlotScaleMultiplier;
            Vector3 targetPos = _backupTray.GetNextSlotWorldPosition();

            DisableCollider(food);

            DOTween.Sequence()
                .Append(food.transform
                    .DOJump(targetPos, backupJumpPower, 1, backupJumpDuration)
                    .SetEase(Ease.OutQuad))
                .Join(food.transform
                    .DOScale(targetScale, backupJumpDuration * 0.8f)
                    .SetEase(Ease.InOutSine))
                .OnComplete(() =>
                {
                    food.transform.position = targetPos;
                    food.transform.localScale = targetScale;

                    _backupTray.ReceiveFood(food);
                    EventBus.RaiseFoodToBackup(food.Data);

                    onComplete?.Invoke();
                })
                .SetUpdate(false)
                .Play();
        }

        // ─── Tap food trong BackupTray (player chủ động) ──────────────────────

        private void HandleBackupFoodTapped(FoodItem food, Action onComplete)
        {
            var matchResult = _orderQueue.TryMatchFood(food.Data.foodID);

            if (matchResult.IsMatch)
            {
                _backupTray.TryRemoveFood(food);
                FlyToOrderSlot(food, matchResult.Tray, matchResult.SlotIndex, onComplete);
            }
            else
            {
                food.PlayLockedBounce();
                onComplete?.Invoke();
            }
        }

        // ─── VFX ─────────────────────────────────────────────────────────────

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

        private static void DisableCollider(FoodItem food)
        {
            var col = food.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }
}