using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace FoodMatch.Items
{
    /// <summary>
    /// Gán vào prefab BoosterSlot.
    /// Hiển thị icon, tên, quantity, lock state.
    /// Click → dùng booster nếu còn số lượng.
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

        // ─────────────────────────────────────────────────────────────────────

        public void Bind(BoosterData data)
        {
            _data = data;

            // ── Unlock check: dùng level trực tiếp, KHÔNG chỉ dựa PlayerPrefs ──
            // SyncUnlocksByLevel có thể chưa chạy khi panel spawn → check cả 2
            int currentLevel = FoodMatch.Managers.SaveManager.CurrentLevel;
            bool unlocked = data.IsUnlocked(currentLevel);

            // Nếu đủ level nhưng chưa có PlayerPrefs key → grant ngay
            if (unlocked) BoosterInventory.UnlockAndGrant(data);

            int qty = BoosterInventory.GetQuantity(data);

            // Icon — tô xám nếu locked HOẶC hết hàng
            if (iconImage != null)
            {
                iconImage.sprite = data.icon;
                bool dim = !unlocked || qty == 0;
                iconImage.color = dim ? new Color(0.4f, 0.4f, 0.4f) : Color.white;
            }

            // NameText không cần — ẩn nếu có
            if (nameText != null) nameText.gameObject.SetActive(false);

            // Quantity badge — luôn hiện khi đã unlock
            if (quantityBadge != null) quantityBadge.SetActive(unlocked);
            if (quantityText != null) quantityText.text = qty.ToString();

            // Lock overlay
            if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
            if (lockLevelText != null) lockLevelText.text = $"Lv.{data.requiredLevel}";

            if (selectBorder != null) selectBorder.SetActive(false);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);
                // Disable nếu hết hàng, nhưng vẫn hiện số 0
                button.interactable = unlocked && qty > 0;
            }
        }

        /// <summary>Overload tương thích BoosterCollectionPanel (bỏ qua currentLevel, dùng BoosterInventory).</summary>
        public void Bind(BoosterData data, int currentLevel, System.Action<BoosterData> onSelected)
        {
            Bind(data);
            // onSelected gọi lại từ panel nếu cần — wrap vào button
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
                DG.Tweening.DOTween.To(() => cg.alpha, x => cg.alpha = x, 0f, 0.3f)
                    .OnComplete(() => lockOverlay.SetActive(false));
            }
            transform.DOKill();
            transform
                .DOScale(Vector3.one * 1.2f, 0.2f).SetEase(Ease.OutBack)
                .OnComplete(() => transform.DOScale(Vector3.one, 0.15f));

            // Refresh toàn bộ sau unlock
            if (_data != null) Bind(_data);
        }

        public void RefreshQuantity()
        {
            if (_data == null) return;
            int qty = BoosterInventory.GetQuantity(_data);
            bool hasStock = qty > 0;

            // Luôn hiện badge + số lượng (kể cả khi = 0)
            if (quantityBadge != null) quantityBadge.SetActive(true);
            if (quantityText != null) quantityText.text = qty.ToString();

            // Xám icon + disable button khi hết
            if (button != null) button.interactable = hasStock;
            if (iconImage != null)
                iconImage.color = hasStock ? Color.white : new Color(0.5f, 0.5f, 0.5f);
        }

        private void OnClick()
        {
            if (_data == null || BoosterManager.Instance == null) return;

            // Animation nhấn
            transform.DOKill();
            transform
                .DOScale(Vector3.one * 0.88f, 0.07f)
                .OnComplete(() =>
                    transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack));

            // Execute — TryConsume chạy bên trong UseBooster (đồng bộ)
            BoosterManager.Instance.UseBooster(_data.boosterName);

            // Refresh ngay sau — quantity đã bị trừ tại đây
            RefreshQuantity();
        }
    }
}