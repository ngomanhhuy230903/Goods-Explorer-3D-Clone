using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;
using FoodMatch.UI;

namespace FoodMatch.Items
{
    /// <summary>
    /// Gán vào prefab BoosterSlot.
    /// Hiển thị icon, tên, quantity, lock state.
    ///
    /// Logic click:
    ///   - Booster bị lock → không làm gì (button disabled).
    ///   - Booster đã unlock, qty > 0 → UseBooster bình thường.
    ///   - Booster đã unlock, qty == 0 → mở BoosterPurchasePopup.
    ///
    /// v3: Thêm trigger BoosterPurchasePopup khi qty == 0.
    ///     Lắng nghe EventBus.OnBoosterPurchased để refresh sau khi mua.
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

        /// <summary>
        /// (Optional) Badge hiện dấu "+" hoặc icon giỏ hàng khi qty == 0 và có thể mua.
        /// Nếu không dùng thì để trống.
        /// </summary>
        [Header("─── Buy Hint (optional) ─────────────")]
        [Tooltip("GameObject hiện dấu '+' hoặc icon mua khi qty = 0. Để trống nếu không dùng.")]
        [SerializeField] private GameObject buyHintBadge;

        private BoosterData _data;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void OnEnable()
        {
            EventBus.OnBoosterActivated += HandleBoosterActivated;
            EventBus.OnBoosterPurchased += HandleBoosterPurchased;
        }

        private void OnDisable()
        {
            EventBus.OnBoosterActivated -= HandleBoosterActivated;
            EventBus.OnBoosterPurchased -= HandleBoosterPurchased;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        /// <summary>
        /// Fire từ BoosterManager sau khi booster hoàn thành hiệu ứng.
        /// Refresh lại slot tương ứng.
        /// </summary>
        private void HandleBoosterActivated(string boosterName)
        {
            if (_data == null || boosterName != _data.boosterName) return;
            RefreshQuantity();
        }

        /// <summary>
        /// Fire từ BoosterPurchasePopup sau khi mua thành công.
        /// Refresh lại slot vừa được thêm hàng.
        /// </summary>
        private void HandleBoosterPurchased(string boosterName)
        {
            if (_data == null || boosterName != _data.boosterName) return;
            RefreshQuantity();

            // Animation nhỏ báo hiệu slot vừa được nạp hàng
            transform.DOKill();
            transform
                .DOScale(Vector3.one * 1.15f, 0.18f).SetEase(Ease.OutBack)
                .OnComplete(() => transform.DOScale(Vector3.one, 0.12f));
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

            // Hiện buy hint badge nếu hết hàng nhưng có thể mua
            if (buyHintBadge != null)
                buyHintBadge.SetActive(unlocked && qty == 0 && data.IsPurchasable);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);

                // Khi qty = 0 nhưng đã unlock và có thể mua → vẫn interactable
                // để dẫn vào popup mua hàng
                bool canTap = unlocked && (qty > 0 || data.IsPurchasable);
                button.interactable = canTap;
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
            bool unlocked = _data.IsUnlocked(FoodMatch.Managers.SaveManager.CurrentLevel);

            if (quantityBadge != null) quantityBadge.SetActive(unlocked);
            if (quantityText != null) quantityText.text = qty.ToString();

            // Buy hint badge
            if (buyHintBadge != null)
                buyHintBadge.SetActive(unlocked && !hasStock && _data.IsPurchasable);

            // Icon dim khi hết hàng
            if (iconImage != null)
                iconImage.color = (unlocked && hasStock) ? Color.white : new Color(0.4f, 0.4f, 0.4f);

            // Button: vẫn bật nếu có thể mua qua popup
            if (button != null)
            {
                bool canTap = unlocked && (hasStock || _data.IsPurchasable);
                button.interactable = canTap;
            }
        }

        // ── Click handler ─────────────────────────────────────────────────────

        private void OnClick()
        {
            if (_data == null) return;

            // Animation nhấn
            transform.DOKill();
            transform
                .DOScale(Vector3.one * 0.88f, 0.07f)
                .OnComplete(() =>
                    transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack));

            int qty = BoosterInventory.GetQuantity(_data);

            // ── Trường hợp hết hàng → mở popup mua ──────────────────────────
            if (qty == 0)
            {
                if (_data.IsPurchasable)
                {
                    if (BoosterPurchasePopup.Instance != null)
                        BoosterPurchasePopup.Instance.Show(_data);
                    else
                        Debug.LogWarning("[BoosterSlotView] BoosterPurchasePopup.Instance is null. " +
                                         "Hãy đặt BoosterPurchasePopup vào scene.");
                }
                // Nếu không thể mua (coinCost = 0, hết hàng hoàn toàn) → không làm gì
                return;
            }

            // ── Trường hợp còn hàng → dùng booster bình thường ───────────────
            if (BoosterManager.Instance == null) return;

            // Disable button NGAY để chặn double-tap
            if (button != null) button.interactable = false;

            bool wasBusy = BoosterManager.Instance.IsBusy;
            BoosterManager.Instance.UseBooster(_data.boosterName);

            // Nếu UseBooster bị reject (busy), restore button
            if (wasBusy)
            {
                if (button != null) button.interactable = qty > 0;
            }
            // Nếu thành công → button disabled, sẽ re-enable khi HandleBoosterActivated về.
        }
    }
}