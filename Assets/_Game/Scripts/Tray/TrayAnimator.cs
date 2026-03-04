using UnityEngine;
using DG.Tweening;
using FoodMatch.Food;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Xử lý toàn bộ animation DOTween của FoodTray.
    /// Tách riêng để giữ FoodTray.cs tập trung vào logic.
    /// </summary>
    public class TrayAnimator : MonoBehaviour
    {
        [SerializeField] private Transform trayRoot;
        [SerializeField] private float spinDuration = 0.5f;
        [SerializeField] private float shrinkDuration = 0.4f;

        /// <summary>
        /// Animation win: xoay → thu nhỏ → ẩn.
        /// Gọi khi tất cả order được hoàn thành.
        /// </summary>
        public void PlayWinAnimation(System.Action onComplete = null)
        {
            if (trayRoot == null) return;

            Sequence seq = DOTween.Sequence();

            // Xoay 360 độ
            seq.Append(trayRoot
                .DORotate(new Vector3(0, 0, 360f), spinDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.InOutQuad));

            // Thu nhỏ về 0
            seq.Append(trayRoot
                .DOScale(Vector3.zero, shrinkDuration)
                .SetEase(Ease.InBack));

            seq.OnComplete(() =>
            {
                trayRoot.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// Animation layer shift: item mới được đẩy lên từ dưới.
        /// Gọi trên từng FoodItem khi layer bên dưới được kích hoạt.
        /// </summary>
        public static void PlayLayerShiftIn(FoodItem item)
        {
            if (item == null) return;

            // Bắt đầu từ scale nhỏ hơn rồi phóng to lên
            item.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            item.transform
                .DOScale(item.Data?.normalScale ?? Vector3.one, 0.3f)
                .SetEase(Ease.OutBack);
        }

        /// <summary>
        /// Punch scale nhẹ khi item được thêm vào backup tray.
        /// </summary>
        public static void PlayBackupLand(Transform target)
        {
            if (target == null) return;
            target.DOPunchScale(Vector3.one * 0.2f, 0.25f, 5, 0.5f);
        }
    }
}