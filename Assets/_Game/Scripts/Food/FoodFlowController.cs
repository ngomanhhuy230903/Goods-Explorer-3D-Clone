using System;
using System.Collections;
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
    /// THAY ĐỔI SO VỚI VERSION CŨ:
    ///   - KHÔNG dùng [SerializeField] cho OrderQueue và BackupTray
    ///   - LevelManager gọi Inject() sau khi spawn các object runtime
    ///   - Dùng Dependency Injection thay vì kéo tay trong Inspector
    /// </summary>
    public class FoodFlowController : MonoBehaviour
    {
        // ─── Singleton ────────────────────────────────────────────────────────
        public static FoodFlowController Instance { get; private set; }

        // ─── Inspector: CHỈ chứa config thuần túy, KHÔNG phải runtime refs ────
        [Header("─── Jump Config ─────────────────────")]
        [SerializeField] private float orderJumpPower = 3.5f;
        [SerializeField] private int orderJumpCount = 1;
        [SerializeField] private float orderJumpDuration = 0.55f;
        [SerializeField] private float backupJumpPower = 2f;
        [SerializeField] private float backupJumpDuration = 0.4f;

        [Header("─── Scale Multipliers ─────────────────")]
        [Tooltip("prefabScale × multiplier = scale trong OrderTray slot.\n"
               + "VD: prefab=(180,180,180), mult=0.85 → target=(153,153,153).\n"
               + "KHÔNG phải set scale = 0.85 mà là NHÂN với scale gốc.")]
        [SerializeField] private float orderSlotScaleMultiplier = 0.85f;
        [SerializeField] private float backupSlotScaleMultiplier = 0.70f;

        [Header("─── VFX ─────────────────────────────")]
        [Tooltip("Để trống nếu dùng PoolManager.GetSparkle().")]
        [SerializeField] private GameObject sparkleVFXPrefab;

        // ─── Runtime Dependencies ─────────────────────────────────────────────
        // Inject sau khi LevelManager spawn các object này
        private OrderQueue _orderQueue;
        private BackupTray _backupTray;
        private bool _isReady = false;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── Dependency Injection ─────────────────────────────────────────────

        /// <summary>
        /// Gọi bởi LevelManager SAU KHI đã spawn OrderQueue và BackupTray runtime.
        ///
        /// Ví dụ trong LevelManager:
        ///   var oq = Instantiate(orderQueuePrefab).GetComponent&lt;OrderQueue&gt;();
        ///   var bt = Instantiate(backupTrayPrefab).GetComponent&lt;BackupTray&gt;();
        ///   FoodFlowController.Instance.Inject(oq, bt);
        /// </summary>
        public void Inject(OrderQueue orderQueue, BackupTray backupTray)
        {
            _orderQueue = orderQueue;
            _backupTray = backupTray;
            _isReady = true;
            Debug.Log("[FoodFlowController] Inject thành công.");
        }

        /// <summary>Gọi khi reset / load level mới.</summary>
        public void ResetDependencies()
        {
            _orderQueue = null;
            _backupTray = null;
            _isReady = false;
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Gọi bởi FoodInteractionHandler khi player tap vào food.
        /// onComplete: callback trả _isProcessing = false.
        /// </summary>
        public void HandleFoodTapped(FoodItem foodItem, Action onComplete = null)
        {
            if (!_isReady)
            {
                Debug.LogError("[FoodFlowController] Chưa Inject! " +
                               "Gọi Inject(orderQueue, backupTray) từ LevelManager trước.");
                onComplete?.Invoke();
                return;
            }

            if (foodItem == null || foodItem.Data == null)
            {
                onComplete?.Invoke();
                return;
            }

            // ── 1. Phân biệt food đang ở FoodTray hay BackupTray ─────────
            var ownerTray = foodItem.OwnerTray;

            if (ownerTray == null)
            {
                // Food đang nằm trong BackupTray
                HandleBackupFoodTapped(foodItem, onComplete);
                return;
            }

            // ── 2. Tách khỏi FoodTray ─────────────────────────────────────
            FoodItem poppedItem = ownerTray.TryPopItem(foodItem);
            if (poppedItem == null)
            {
                // Locked — PlayLockedBounce đã gọi bên trong TryPopItem
                onComplete?.Invoke();
                return;
            }

            // ── 3. Match OrderTray ────────────────────────────────────────
            var matchResult = _orderQueue.TryMatchFood(poppedItem.Data.foodID);

            if (matchResult.IsMatch)
                FlyToOrderSlot(poppedItem, matchResult.Tray, matchResult.SlotIndex, onComplete);
            else
                FlyToBackupTray(poppedItem, onComplete);
        }

        // ─── Bay lên OrderTray ────────────────────────────────────────────────

        private void FlyToOrderSlot(FoodItem food, OrderTray orderTray,
                                     int slotIndex, Action onComplete)
        {
            // Scale đích = prefabScale × multiplier — KHÔNG set giá trị tuyệt đối
            Vector3 prefabScale = food.Data.prefab.transform.localScale;
            Vector3 targetScale = prefabScale * orderSlotScaleMultiplier;
            Vector3 targetPos = orderTray.GetNextSlotWorldPosition();

            DisableCollider(food);

            Sequence seq = DOTween.Sequence();

            // Arc jump đến slot
            seq.Append(
                food.transform
                    .DOJump(targetPos, orderJumpPower, orderJumpCount, orderJumpDuration)
                    .SetEase(Ease.OutQuad)
            );

            // Scale thu về targetScale song song trong lúc bay
            seq.Join(
                food.transform
                    .DOScale(targetScale, orderJumpDuration * 0.8f)
                    .SetEase(Ease.InOutSine)
            );

            seq.OnComplete(() =>
            {
                // Snap chính xác
                food.transform.position = targetPos;
                food.transform.localScale = targetScale;

                // VFX lấp lánh
                SpawnSparkleVFX(targetPos);

                // Checkmark + order tracking
                orderTray.ConfirmDelivery(slotIndex);

                // Trả về pool sau khi checkmark animation chạy
                DOVirtual.DelayedCall(0.35f,
                    () => PoolManager.Instance.ReturnFood(food.FoodID, food.gameObject), false);

                onComplete?.Invoke();
            });

            seq.SetUpdate(false);
            seq.Play();
        }

        // ─── Bay vào BackupTray ───────────────────────────────────────────────

        private void FlyToBackupTray(FoodItem food, Action onComplete)
        {
            if (!_backupTray.HasFreeSlot())
            {
                Debug.Log("[FoodFlowController] BackupTray đầy → THUA!");
                EventBus.RaiseBackupFull();
                // Trả food về tray cũ hoặc pool tuỳ design
                PoolManager.Instance.ReturnFood(food.FoodID, food.gameObject);
                onComplete?.Invoke();
                return;
            }

            Vector3 prefabScale = food.Data.prefab.transform.localScale;
            Vector3 targetScale = prefabScale * backupSlotScaleMultiplier;
            Vector3 targetPos = _backupTray.GetNextSlotWorldPosition();

            DisableCollider(food);

            Sequence seq = DOTween.Sequence();

            seq.Append(
                food.transform
                    .DOJump(targetPos, backupJumpPower, 1, backupJumpDuration)
                    .SetEase(Ease.OutQuad)
            );
            seq.Join(
                food.transform
                    .DOScale(targetScale, backupJumpDuration * 0.8f)
                    .SetEase(Ease.InOutSine)
            );

            seq.OnComplete(() =>
            {
                food.transform.position = targetPos;
                food.transform.localScale = targetScale;

                _backupTray.ReceiveFood(food);
                EventBus.RaiseFoodToBackup(food.Data);

                onComplete?.Invoke();
            });

            seq.SetUpdate(false);
            seq.Play();
        }

        // ─── Tap food trong BackupTray ────────────────────────────────────────

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
            // Ưu tiên dùng Pool nếu có
            var pooled = PoolManager.Instance?.GetSparkle(worldPos);
            if (pooled != null)
            {
                StartCoroutine(ReturnVFXAfterDelay(pooled, 1.5f));
                return;
            }

            // Fallback: Instantiate trực tiếp
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