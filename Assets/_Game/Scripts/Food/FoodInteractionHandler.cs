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
    /// Hoạt động cho cả FoodTray VÀ ConveyorTray:
    ///   - FoodTray food: item.OwnerTray != null → FoodTray.TryPopItem()
    ///   - ConveyorTray food: ConveyorFoodOwner component → ConveyorTray.TryPopItem()
    ///   - BackupTray food: OwnerTray == null && không có ConveyorFoodOwner
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

            // CASE 1: Food từ ConveyorTray
            var conveyorOwner = GetComponent<ConveyorFoodOwner>()
                             ?? GetComponentInParent<ConveyorFoodOwner>();

            if (conveyorOwner != null && conveyorOwner.OwnerConveyorTray != null)
            {
                var conveyorTray = conveyorOwner.OwnerConveyorTray;
                FoodItem popped = conveyorTray.TryPopItem(_foodItem);
                if (popped == null) return;

                // ✅ Clear TRƯỚC khi gọi HandleFoodTapped để OwnerTray == null
                // → FoodFlowController sẽ gọi BuildAndExecuteDeliveryCommand trực tiếp
                conveyorOwner.OwnerConveyorTray = null;

                _isProcessing = true;
                FoodFlowController.Instance.HandleFoodTapped(popped, () =>
                {
                    _isProcessing = false;
                }, keepScale: true);
                return;
            }

            // CASE 2: Food từ FoodTray
            if (_foodItem.OwnerTray != null)
            {
                // ✅ KHÔNG pop ở đây — để HandleFoodTapped tự pop bên trong
                _isProcessing = true;
                FoodFlowController.Instance.HandleFoodTapped(_foodItem, () =>
                {
                    _isProcessing = false;
                });
                return;
            }

            // CASE 3: BackupTray food
            _isProcessing = true;
            FoodFlowController.Instance.HandleFoodTapped(_foodItem, () =>
            {
                _isProcessing = false;
            });
        }
    }
}