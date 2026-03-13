// FoodInteractionHandler.cs
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using FoodMatch.Food;
using FoodMatch.Tray;
using FoodMatch.Obstacle;

namespace FoodMatch.Food
{
    /// <summary>
    /// Xử lý tap/click trên FoodItem.
    ///
    /// FIX — stale ConveyorFoodOwner sau Reset/GoHome:
    ///   Khi food trả về pool, FoodItem.OnDespawn() chạy nhưng ConveyorFoodOwner
    ///   vẫn còn reference cũ → lần play mới food vào case 1 dù không thuộc conveyor
    ///   → TryPopItem fail → _isProcessing không reset → food bị treo.
    ///
    ///   Fix:
    ///   1. Validate OwnerConveyorTray còn sống (gameObject != null && activeInHierarchy)
    ///      trước khi dùng — nếu stale → fall-through sang case 2/3.
    ///   2. Reset _isProcessing trong OnDisable() để pool recycle sạch.
    ///   3. ConveyorTray.ResetTray() và ConveyorTray.OnDespawn() gọi
    ///      ClearConveyorOwnerOnAllFood() để null ref trên từng FoodItem.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FoodInteractionHandler : MonoBehaviour,
                                          IPointerClickHandler,
                                          IPointerDownHandler,
                                          IPointerUpHandler
    {
        private FoodItem _foodItem;
        private bool _isProcessing = false;
        private Vector3 _originalScale;

        private void Awake()
        {
            _foodItem = GetComponent<FoodItem>() ?? GetComponentInParent<FoodItem>();
            _originalScale = transform.localScale;
        }

        /// <summary>
        /// Reset state khi GameObject bị disable (trả về pool / despawn).
        /// Đảm bảo _isProcessing không còn lock khi food được tái sử dụng.
        /// </summary>
        private void OnDisable()
        {
            _isProcessing = false;
            // Reset scale về original phòng trường hợp DOTween bị kill giữa chừng
            transform.DOKill();
            transform.localScale = _originalScale;
        }

        private void OnEnable()
        {
            // Cập nhật lại _originalScale khi được lấy từ pool
            // (vì prefab scale có thể khác scale trong pool container)
            if (_foodItem != null && _foodItem.Data?.prefab != null)
                _originalScale = _foodItem.Data.prefab.transform.localScale;
        }

        private void NotifyTrayInteraction()
        {
            FoodGridSpawner spawner = null;
            if (_foodItem != null && _foodItem.OwnerTray != null)
                spawner = _foodItem.OwnerTray.GetComponentInChildren<FoodGridSpawner>();
            if (spawner == null)
                spawner = FindObjectOfType<FoodGridSpawner>();
            spawner?.NotifyInteraction();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isProcessing) return;
            NotifyTrayInteraction();
            transform.DOKill();
            transform.DOScale(_originalScale * 0.88f, 0.08f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isProcessing) return;
            transform.DOKill();
            transform.DOScale(_originalScale, 0.1f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            transform.DOKill();
            transform.DOScale(_originalScale, 0.12f).SetEase(Ease.OutBack).SetUpdate(true);
            HandleTap();
        }

        private void HandleTap()
        {
            if (_isProcessing) return;
            if (_foodItem == null) return;
            if (FoodFlowController.Instance == null) return;

            if (_foodItem.LayerIndex > 0)
            {
                _foodItem.PlayLockedBounce();
                return;
            }

            // ── CASE 1: Food từ ConveyorTray ─────────────────────────────────
            var conveyorOwner = GetComponent<ConveyorFoodOwner>()
                             ?? GetComponentInParent<ConveyorFoodOwner>();

            if (conveyorOwner != null && conveyorOwner.OwnerConveyorTray != null)
            {
                var conveyorTray = conveyorOwner.OwnerConveyorTray;

                // ── FIX: Validate tray còn sống trước khi dùng ───────────────
                // Sau Reset/GoHome, tray có thể đã bị Destroy hoặc trả về pool
                // (inactive). Nếu stale → clear ref và fall-through sang case 2/3.
                bool trayAlive = conveyorTray != null
                                 && conveyorTray.gameObject != null
                                 && conveyorTray.gameObject.activeInHierarchy;

                if (!trayAlive)
                {
                    // Stale reference — clear để không lặp lại lần sau
                    conveyorOwner.OwnerConveyorTray = null;
                    // Fall-through: xử lý như FoodTray hoặc BackupTray bên dưới
                }
                else
                {
                    FoodItem popped = conveyorTray.TryPopItem(_foodItem);
                    if (popped == null) return;

                    // Clear TRƯỚC khi gọi HandleFoodTapped
                    conveyorOwner.OwnerConveyorTray = null;

                    _isProcessing = true;
                    FoodFlowController.Instance.HandleFoodTapped(popped, () =>
                    {
                        _isProcessing = false;
                    }, keepScale: true);
                    return;
                }
            }

            // ── CASE 2: Food từ FoodTray ──────────────────────────────────────
            if (_foodItem.OwnerTray != null)
            {
                _isProcessing = true;
                FoodFlowController.Instance.HandleFoodTapped(_foodItem, () =>
                {
                    _isProcessing = false;
                });
                return;
            }

            // ── CASE 3: BackupTray food ───────────────────────────────────────
            _isProcessing = true;
            FoodFlowController.Instance.HandleFoodTapped(_foodItem, () =>
            {
                _isProcessing = false;
            });
        }
    }
}