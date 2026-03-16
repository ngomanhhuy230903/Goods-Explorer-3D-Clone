using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FoodMatch.Data;
using FoodMatch.Managers;

namespace FoodMatch.UI
{
    /// <summary>
    /// UI slot cho 1 booster trong shop/HUD, cho phép mua thêm bằng coin.
    /// Gắn lên prefab BoosterSlot trong HUD hoặc Shop Panel.
    /// 
    /// Workflow:
    ///   BoosterShopUI.Setup(boosterData) → hiện icon, giá, số lượng
    ///   Nhấn Buy → CoinManager.TryPurchaseBooster() → cộng quantity vào BoosterInventory
    /// </summary>
    public class BoosterShopUI : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private TMP_Text quantityText;       // "x3"
        [SerializeField] private TMP_Text costText;           // "40 🪙"
        [SerializeField] private Button buyButton;
        [SerializeField] private GameObject notPurchasableOverlay; // ẩn nút nếu coinCost = 0
        [SerializeField] private GameObject cantAffordOverlay;     // highlight khi không đủ coin

        private BoosterData _data;

        // ─── Public API ───────────────────────────────────────────────────────

        public void Setup(BoosterData data)
        {
            _data = data;

            if (iconImage) iconImage.sprite = data.icon;
            if (displayNameText) displayNameText.text = data.displayName;
            if (costText) costText.text = data.IsPurchasable
                                                  ? $"{data.coinCost} 🪙"
                                                  : "Free";

            RefreshQuantity();
            RefreshAffordability();

            bool purchasable = data.IsPurchasable;
            if (buyButton) buyButton.gameObject.SetActive(purchasable);
            if (notPurchasableOverlay) notPurchasableOverlay.SetActive(!purchasable);
        }

        // ─── Unity ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            buyButton?.onClick.AddListener(HandleBuy);
            CoinManager.OnCoinChanged += _ => RefreshAffordability();
        }

        private void OnDisable()
        {
            buyButton?.onClick.RemoveListener(HandleBuy);
            CoinManager.OnCoinChanged -= _ => RefreshAffordability();
        }

        // ─── Handlers ─────────────────────────────────────────────────────────

        private void HandleBuy()
        {
            if (_data == null || !_data.IsPurchasable) return;

            bool success = CoinManager.Instance.TryPurchaseBooster(_data.boosterName, _data.coinCost);
            if (!success) return;

            // Cộng quantity vào BoosterInventory (BoosterManager lưu qua PlayerPrefs)
            int current = PlayerPrefs.GetInt(_data.QuantityPrefKey, 0);
            int newQty = Mathf.Min(current + _data.coinPurchaseAmount, _data.maxQuantity);
            PlayerPrefs.SetInt(_data.QuantityPrefKey, newQty);
            PlayerPrefs.Save();

            RefreshQuantity();
            RefreshAffordability();

            Debug.Log($"[BoosterShopUI] Đã mua {_data.boosterName} x{_data.coinPurchaseAmount}. Số dư: {newQty}");
        }

        private void RefreshQuantity()
        {
            if (_data == null || quantityText == null) return;
            int qty = PlayerPrefs.GetInt(_data.QuantityPrefKey, 0);
            quantityText.text = $"x{qty}";
        }

        private void RefreshAffordability()
        {
            if (_data == null || CoinManager.Instance == null) return;
            bool canAfford = CoinManager.Instance.CurrentCoins >= _data.coinCost;
            if (buyButton) buyButton.interactable = canAfford;
            if (cantAffordOverlay) cantAffordOverlay.SetActive(!canAfford && _data.IsPurchasable);
        }
    }
}