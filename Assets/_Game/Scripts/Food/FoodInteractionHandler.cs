using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using FoodMatch.Food;
using FoodMatch.Tray;

namespace FoodMatch.Food
{
    /// <summary>
    /// Xử lý tap/click trên FoodItem.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FoodInteractionHandler : MonoBehaviour,
                                          IPointerClickHandler,
                                          IPointerDownHandler,
                                          IPointerUpHandler
    {
        // ─── Cache ────────────────────────────────────────────────────────────
        private FoodItem _foodItem;
        private bool _isProcessing = false;
        private Vector3 _originalScale;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _foodItem = GetComponent<FoodItem>() ?? GetComponentInParent<FoodItem>();
            _originalScale = transform.localScale;
        }

        // ─── Helper: thông báo tray dừng auto-rotate ──────────────────────────
        private void NotifyTrayInteraction()
        {
            // Tìm FoodGridSpawner từ OwnerTray nếu có, hoặc tìm trong scene
            FoodGridSpawner spawner = null;

            if (_foodItem != null && _foodItem.OwnerTray != null)
                spawner = _foodItem.OwnerTray.GetComponentInChildren<FoodGridSpawner>();

            if (spawner == null)
                spawner = FindObjectOfType<FoodGridSpawner>();

            spawner?.NotifyInteraction();
        }

        // ─── IPointerDownHandler — press feedback ─────────────────────────────
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isProcessing) return;

            // Ngay khi chạm → dừng auto-rotate
            NotifyTrayInteraction();

            transform.DOKill();
            transform.DOScale(_originalScale * 0.88f, 0.08f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        // ─── IPointerUpHandler — release mà không click (drag) ───────────────
        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isProcessing) return;
            transform.DOKill();
            transform.DOScale(_originalScale, 0.1f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        // ─── IPointerClickHandler — logic chính ──────────────────────────────
        public void OnPointerClick(PointerEventData eventData)
        {
            // Restore scale trước
            transform.DOKill();
            transform.DOScale(_originalScale, 0.12f).SetEase(Ease.OutBack).SetUpdate(true);

            HandleTap();
        }

        // ─── Core ─────────────────────────────────────────────────────────────
        private void HandleTap()
        {
            if (_isProcessing) return;

            if (_foodItem == null)
            {
                Debug.LogWarning("[FoodInteractionHandler] _foodItem null!");
                return;
            }

            if (FoodFlowController.Instance == null)
            {
                Debug.LogWarning("[FoodInteractionHandler] FoodFlowController.Instance null!");
                return;
            }

            // Layer > 0: locked — chỉ bounce, không xử lý
            if (_foodItem.LayerIndex > 0)
            {
                _foodItem.PlayLockedBounce();
                return;
            }

            _isProcessing = true;
            FoodFlowController.Instance.HandleFoodTapped(_foodItem, () =>
            {
                _isProcessing = false;
            });
        }
    }
}