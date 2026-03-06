using UnityEngine;
using DG.Tweening;
using FoodMatch.Data;

namespace FoodMatch.Order
{
    /// <summary>
    /// 1 ô slot trong OrderTray UI.
    /// - Là điểm neo đích để food 3D bay đến từ grid.
    /// - Spawn food 3D icon từ FoodItemData.prefab để hiển thị loại food cần order.
    /// </summary>
    public class OrderSlotUI : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Visual Feedback ─────────────────")]
        [Tooltip("GameObject dấu tích — bật khi slot đã nhận đủ món.")]
        [SerializeField] private GameObject checkmarkObject;

        [Tooltip("Offset local để căn chỉnh food icon trong slot (thường để 0).")]
        [SerializeField] private Vector3 foodLocalOffset = Vector3.zero;

        // ─── Runtime ──────────────────────────────────────────────────────────
        public bool IsDelivered { get; private set; }

        // Scale gốc của prefab — lưu lại khi spawn để dùng cho animation và FoodTargetScale
        private Vector3 _prefabOriginalScale = Vector3.one;

        /// <summary>Scale đích để FoodAnimator DOScale về khi food bay đến đây (= scale gốc prefab).</summary>
        public Vector3 FoodTargetScale => _prefabOriginalScale;

        /// <summary>Vị trí World — đích bay cho FoodItem từ grid.</summary>
        public Vector3 WorldPosition => transform.position;

        private GameObject _foodIcon;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            checkmarkObject?.SetActive(false);
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Spawn food 3D icon từ FoodItemData.prefab để hiển thị loại food cần order.
        /// Gọi bởi OrderTray.Initialize().
        /// </summary>
        public void ShowFoodIcon(FoodItemData foodData)
        {
            ClearFoodIcon();

            if (foodData == null || foodData.prefab == null)
            {
                Debug.LogWarning("[OrderSlotUI] FoodItemData hoặc prefab bị null!");
                return;
            }

            // Spawn prefab 3D làm con của slot transform
            _foodIcon = Instantiate(foodData.prefab, transform);
            _foodIcon.transform.localPosition = foodLocalOffset;
            _foodIcon.transform.localRotation = Quaternion.identity;

            // Lưu scale gốc của prefab TRƯỚC khi zero để dùng cho animation và FoodTargetScale
            _prefabOriginalScale = _foodIcon.transform.localScale;
            _foodIcon.transform.localScale = Vector3.zero;

            // Disable physics — icon chỉ để nhìn, không tương tác
            foreach (var col in _foodIcon.GetComponentsInChildren<Collider>())
                col.enabled = false;
            foreach (var rb in _foodIcon.GetComponentsInChildren<Rigidbody>())
                rb.isKinematic = true;

            // Pop-in animation về đúng scale gốc của prefab
            _foodIcon.transform
                .DOScale(_prefabOriginalScale, 0.3f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        /// <summary>
        /// Slot nhận được food — ẩn icon, hiện checkmark.
        /// Gọi bởi OrderTray.ConfirmDelivery().
        /// </summary>
        public void MarkDelivered()
        {
            if (IsDelivered) return;
            IsDelivered = true;

            // Scale out icon rồi destroy
            if (_foodIcon != null)
            {
                var icon = _foodIcon;
                _foodIcon = null;
                icon.transform
                    .DOScale(Vector3.zero, 0.15f)
                    .SetEase(Ease.InBack)
                    .SetUpdate(true)
                    .OnComplete(() => Destroy(icon));
            }

            // Hiện checkmark
            if (checkmarkObject != null)
            {
                checkmarkObject.SetActive(true);
                checkmarkObject.transform.localScale = Vector3.zero;
                checkmarkObject.transform
                    .DOScale(1f, 0.25f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true);
            }
        }

        /// <summary>Nảy nhẹ khi food chạm vào slot.</summary>
        public void PlayReceiveAnimation()
        {
            transform.DOKill();
            transform
                .DOPunchScale(Vector3.one * 0.3f, 0.3f, 5, 0.4f)
                .SetUpdate(true);
        }

        /// <summary>Reset về trạng thái ban đầu khi trả OrderTray về pool.</summary>
        public void ResetSlot()
        {
            IsDelivered = false;
            transform.DOKill();
            transform.localScale = Vector3.one;
            checkmarkObject?.SetActive(false);
            ClearFoodIcon();
        }

        // ─────────────────────────────────────────────────────────────────────
        private void ClearFoodIcon()
        {
            if (_foodIcon == null) return;
            Destroy(_foodIcon);
            _foodIcon = null;
        }
    }
}