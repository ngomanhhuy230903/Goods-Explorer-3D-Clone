using UnityEngine;
using DG.Tweening;

namespace FoodMatch.Order
{
    /// <summary>
    /// 1 slot trong khay order — là 1 Transform 3D làm điểm neo.
    /// Food item sẽ DOMove/DOJump đến đây và DOScale về đúng kích thước.
    /// Gắn vào các child GameObject "Slot_0", "Slot_1", "Slot_2" trong OrderTray prefab.
    /// </summary>
    public class OrderSlot3D : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Slot Config ─────────────────────")]
        [Tooltip("Scale chuẩn của food khi đứng trong slot này.\n"
               + "Kéo thử trong Scene để căn cho vừa khay.")]
        [SerializeField] private Vector3 foodScaleInSlot = new Vector3(0.5f, 0.5f, 0.5f);

        [Header("─── Checkmark UI (tuỳ chọn) ─────────")]
        [Tooltip("GameObject chứa checkmark/tick hiển thị khi slot đã được giao.\n"
               + "Có thể là UI Canvas hoặc SpriteRenderer. Để trống = không dùng.")]
        [SerializeField] private GameObject checkmarkObject;

        // ─── Runtime ──────────────────────────────────────────────────────────
        public bool IsDelivered { get; private set; } = false;

        /// <summary>Scale mà food phải DOTween về khi bay vào slot này.</summary>
        public Vector3 FoodScaleInSlot => foodScaleInSlot;

        /// <summary>Vị trí World của slot — đích bay cho food.</summary>
        public Vector3 WorldPosition => transform.position;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            // Ẩn checkmark lúc đầu
            if (checkmarkObject != null)
                checkmarkObject.SetActive(false);
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Đánh dấu slot đã nhận món — hiện checkmark, tắt highlight.
        /// Gọi bởi OrderTray.ConfirmDelivery() sau khi food đến nơi.
        /// </summary>
        public void MarkDelivered()
        {
            if (IsDelivered) return;
            IsDelivered = true;
            // Hiện checkmark với animation scale
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

        /// <summary>
        /// Animation nảy khi food vừa đến nơi (gọi đồng thời với MarkDelivered).
        /// </summary>
        public void PlayReceiveAnimation()
        {
            transform.DOKill();
            transform
                .DOPunchScale(Vector3.one * 0.3f, 0.3f, 5, 0.4f)
                .SetUpdate(true);
        }

        /// <summary>Reset slot về trạng thái chờ (khi trả OrderTray về pool).</summary>
        public void Reset()
        {
            IsDelivered = false;

            if (checkmarkObject != null)
                checkmarkObject.SetActive(false);
            transform.DOKill();
            transform.localScale = Vector3.one;
        }
    }
}