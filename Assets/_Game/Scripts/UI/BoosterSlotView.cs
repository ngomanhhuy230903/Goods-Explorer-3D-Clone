using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using FoodMatch.Core;

namespace FoodMatch.Items
{
    /// <summary>
    /// Gán vào prefab BoosterSlot.
    /// Hiển thị icon, tên, quantity, lock state.
    /// Click → UseBooster nếu còn số lượng VÀ không có booster nào đang chạy.
    ///
    /// v2: Bỏ RefreshQuantity() trong OnClick() — thay bằng lắng nghe
    ///     EventBus.OnBoosterActivated (fire sau khi booster THỰC SỰ hoàn thành).
    ///     Button bị disable ngay khi click, chỉ được enable lại khi event về.
    /// </summary>
    public class BoosterSlotView : MonoBehaviour
    {
        [Header("─── References (khớp hierarchy) ────")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private GameObject quantityBadge;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private TextMeshProUGUI lockLevelText;
        [SerializeField] private GameObject selectBorder;
        [SerializeField] private Button button;

        private BoosterData _data;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void OnEnable()
        {
            EventBus.OnBoosterActivated += HandleBoosterActivated;
        }

        private void OnDisable()
        {
            EventBus.OnBoosterActivated -= HandleBoosterActivated;
        }

        // ── Event handler ─────────────────────────────────────────────────────

        /// <summary>
        /// Fire từ BoosterManager.NotifyBoosterCompleted() — tức là SAU KHI
        /// booster đã thực hiện xong hiệu ứng, không phải lúc bắt đầu.
        /// Chỉ refresh slot của booster vừa hoàn thành.
        /// </summary>
        private void HandleBoosterActivated(string boosterName)
        {
            if (_data == null) return;
            if (boosterName != _data.boosterName) return;

            // Booster đã xong → refresh UI chính xác
            RefreshQuantity();
        }

        // ── Bind ──────────────────────────────────────────────────────────────

        public void Bind(BoosterData data)
        {
            _data = data;

            int currentLevel = FoodMatch.Managers.SaveManager.CurrentLevel;
            bool unlocked = data.IsUnlocked(currentLevel);

            if (unlocked) BoosterInventory.UnlockAndGrant(data);

            int qty = BoosterInventory.GetQuantity(data);

            if (iconImage != null)
            {
                iconImage.sprite = data.icon;
                bool dim = !unlocked || qty == 0;
                iconImage.color = dim ? new Color(0.4f, 0.4f, 0.4f) : Color.white;
            }

            if (nameText != null) nameText.gameObject.SetActive(false);

            if (quantityBadge != null) quantityBadge.SetActive(unlocked);
            if (quantityText != null) quantityText.text = qty.ToString();

            if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
            if (lockLevelText != null) lockLevelText.text = $"Lv.{data.requiredLevel}";

            if (selectBorder != null) selectBorder.SetActive(false);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);
                button.interactable = unlocked && qty > 0;
            }
        }

        /// <summary>Overload tương thích BoosterCollectionPanel.</summary>
        public void Bind(BoosterData data, int currentLevel, System.Action<BoosterData> onSelected)
        {
            Bind(data);
            if (button != null && onSelected != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    OnClick();
                    onSelected.Invoke(_data);
                });
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectBorder != null) selectBorder.SetActive(selected);
        }

        public void PlayUnlockAnimation()
        {
            if (lockOverlay != null)
            {
                var cg = lockOverlay.GetComponent<CanvasGroup>()
                         ?? lockOverlay.AddComponent<CanvasGroup>();
                DOTween.To(() => cg.alpha, x => cg.alpha = x, 0f, 0.3f)
                    .OnComplete(() => lockOverlay.SetActive(false));
            }
            transform.DOKill();
            transform
                .DOScale(Vector3.one * 1.2f, 0.2f).SetEase(Ease.OutBack)
                .OnComplete(() => transform.DOScale(Vector3.one, 0.15f));

            if (_data != null) Bind(_data);
        }

        public void RefreshQuantity()
        {
            if (_data == null) return;
            int qty = BoosterInventory.GetQuantity(_data);
            bool hasStock = qty > 0;

            if (quantityBadge != null) quantityBadge.SetActive(true);
            if (quantityText != null) quantityText.text = qty.ToString();

            // Re-enable button nếu còn hàng, disable nếu hết
            if (button != null) button.interactable = hasStock;
            if (iconImage != null)
                iconImage.color = hasStock ? Color.white : new Color(0.5f, 0.5f, 0.5f);
        }

        // ── Click handler ─────────────────────────────────────────────────────

        private void OnClick()
        {
            if (_data == null || BoosterManager.Instance == null) return;

            // Disable button NGAY để chặn double-tap trong khi BoosterManager
            // chưa kịp set _isBusy (tránh race condition 1 frame)
            if (button != null) button.interactable = false;

            // Animation nhấn
            transform.DOKill();
            transform
                .DOScale(Vector3.one * 0.88f, 0.07f)
                .OnComplete(() =>
                    transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack));

            // UseBooster có _isBusy guard bên trong.
            // Nếu bị reject (busy / hết hàng), cần re-enable button lại.
            bool wasBusy = BoosterManager.Instance.IsBusy;
            BoosterManager.Instance.UseBooster(_data.boosterName);

            // Nếu UseBooster bị reject (vẫn còn hàng nhưng busy), restore button
            if (wasBusy)
            {
                int qty = BoosterInventory.GetQuantity(_data);
                if (button != null) button.interactable = qty > 0;
            }
            // Nếu UseBooster thành công → button ở trạng thái disabled
            // và sẽ được re-enable trong HandleBoosterActivated khi booster xong.
        }
    }
}