using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace FoodMatch.Order
{
    /// <summary>
    /// 1 ô slot trong OrderTray UI — là điểm neo để food 3D bay đến.
    /// KHÔNG chứa Image icon. Chỉ cung cấp WorldPosition làm đích bay
    /// và hiển thị checkmark/highlight khi trạng thái thay đổi.
    /// </summary>
    public class OrderSlotUI : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Visual Feedback ─────────────────")]
        [Tooltip("GameObject dấu tích — bật khi slot đã nhận đủ món.")]
        [SerializeField] private GameObject checkmarkObject;

        [Tooltip("Image viền highlight — bật khi slot đang chờ nhận món.")]
        [SerializeField] private GameObject highlightObject;

        [Tooltip("Scale mà food phải DOScale về khi bay đến slot này.")]
        [SerializeField] private Vector3 foodTargetScale = new Vector3(0.5f, 0.5f, 0.5f);

        // ─── Runtime ──────────────────────────────────────────────────────────
        public bool IsDelivered { get; private set; }

        /// <summary>Scale đích để FoodAnimator DOScale về khi bay đến đây.</summary>
        public Vector3 FoodTargetScale => foodTargetScale;

        /// <summary>Vị trí World — đích bay cho FoodItem.</summary>
        public Vector3 WorldPosition => transform.position;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            checkmarkObject?.SetActive(false);
            highlightObject?.SetActive(true);
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Slot đã nhận đủ món — hiện checkmark, tắt highlight.
        /// Gọi bởi OrderTray.ConfirmDelivery() sau khi food đến nơi.
        /// </summary>
        public void MarkDelivered()
        {
            if (IsDelivered) return;
            IsDelivered = true;

            highlightObject?.SetActive(false);

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

        /// <summary>Hiệu ứng nảy nhẹ khi food vừa chạm vào slot.</summary>
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
            highlightObject?.SetActive(true);
        }
    }
}