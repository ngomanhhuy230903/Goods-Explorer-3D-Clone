using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using FoodMatch.Data;
using FoodMatch.Items;
using FoodMatch.Managers;

namespace FoodMatch.UI
{
    /// <summary>
    /// Popup mua booster khi qty == 0.
    /// Gắn lên BoosterArea (cùng GameObject với BoosterAreaSpawner).
    /// </summary>
    public class BoosterPurchasePopup : MonoBehaviour
    {
        public static BoosterPurchasePopup Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("─── Popup Root ─────────────────────────")]
        [Tooltip("Kéo GameObject BoosterPurchasePopup vào đây.")]
        [SerializeField] private GameObject popupRoot;

        [Tooltip("RectTransform panel con bên trong — dùng để animate scale.")]
        [SerializeField] private RectTransform popupPanel;

        [Header("─── Content ───────────────────────────")]
        [SerializeField] private Image boosterIcon;
        [SerializeField] private TextMeshProUGUI boosterNameText;
        [SerializeField] private TextMeshProUGUI boosterDescText;
        [SerializeField] private TextMeshProUGUI costText;

        [Header("─── Buttons ────────────────────────────")]
        [Tooltip("Mua bằng coin.")]
        [SerializeField] private Button buyButton;

        [Tooltip("Xem ads nhận free 1 lượt (placeholder — bấm = nhận luôn).")]
        [SerializeField] private Button watchAdsButton;

        [Tooltip("Đóng popup, trở về màn chơi.")]
        [SerializeField] private Button closeButton;

        [Header("─── Animation ──────────────────────────")]
        [SerializeField] private float popupScaleDuration = 0.45f;
        [SerializeField] private Ease popupScaleEase = Ease.OutBack;
        [SerializeField] private float popupDelay = 0.15f;

        // ── Runtime ───────────────────────────────────────────────────────────
        private BoosterData _currentData;

        // ── Unity ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            if (popupRoot != null) popupRoot.SetActive(false);
        }

        private void Start()
        {
            // Reparent popupRoot vào PopupCanvas của GameResultUI
            // (gọi trong Start để đảm bảo GameResultUI.Awake đã chạy xong)
            ReparentToPopupCanvas();

            if (buyButton != null) buyButton.onClick.AddListener(OnBuyClicked);
            if (watchAdsButton != null) watchAdsButton.onClick.AddListener(OnWatchAdsClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Reparent ──────────────────────────────────────────────────────────

        /// <summary>
        /// Reparent popupRoot vào PopupCanvas của GameResultUI.
        /// Giữ nguyên anchor/size/scale như khi thiết kế trong MainCanvas.
        /// </summary>
        private void ReparentToPopupCanvas()
        {
            if (popupRoot == null) return;
            if (GameResultUI.Instance == null)
            {
                Debug.LogWarning("[BoosterPurchasePopup] GameResultUI.Instance chưa có — không reparent được.");
                return;
            }

            GameResultUI.Instance.ReparentToPopupCanvas(popupRoot);
            Debug.Log("[BoosterPurchasePopup] popupRoot đã được reparent vào PopupCanvas.");
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Mở popup cho booster cụ thể.
        /// Gọi từ BoosterSlotView khi qty == 0 và booster đã unlock.
        /// </summary>
        public void Show(BoosterData data)
        {
            if (data == null) return;
            _currentData = data;

            if (boosterIcon != null) boosterIcon.sprite = data.icon;
            if (boosterNameText != null) boosterNameText.text = data.displayName;
            if (boosterDescText != null) boosterDescText.text = data.description;

            if (costText != null)
                costText.text = data.IsPurchasable ? $"{data.coinCost}" : "Miễn phí";

            RefreshBuyButtonState();
            if (watchAdsButton != null) watchAdsButton.interactable = true;

            // Fade overlay qua GameResultUI rồi animate popup vào
            if (GameResultUI.Instance != null)
                GameResultUI.Instance.ShowOverlayThen(() => AnimatePopupIn(popupRoot));
            else
                AnimatePopupIn(popupRoot);
        }

        /// <summary>Đóng popup, trở về màn hình chơi bình thường.</summary>
        public void Hide()
        {
            AnimatePopupOut(popupRoot, () =>
            {
                if (GameResultUI.Instance != null)
                    GameResultUI.Instance.HideOverlay();
            });

            _currentData = null;
        }

        // ── Animate (cùng pattern GameResultUI) ──────────────────────────────

        private void AnimatePopupIn(GameObject target)
        {
            if (target == null) return;
            target.SetActive(true);

            // Scale animate trên popupPanel (con), không phải root
            var animTarget = popupPanel != null ? popupPanel.transform : target.transform;
            animTarget.localScale = Vector3.zero;

            DOVirtual.DelayedCall(popupDelay, () =>
                animTarget
                    .DOScale(Vector3.one, popupScaleDuration)
                    .SetEase(popupScaleEase)
                    .SetUpdate(true),
                ignoreTimeScale: true);
        }

        private void AnimatePopupOut(GameObject target, System.Action onDone = null)
        {
            if (target == null) { onDone?.Invoke(); return; }

            var animTarget = popupPanel != null ? popupPanel.transform : target.transform;
            animTarget
                .DOScale(Vector3.zero, popupScaleDuration * 0.6f)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    target.SetActive(false);
                    onDone?.Invoke();
                });
        }

        // ── Button Handlers ───────────────────────────────────────────────────

        private void OnBuyClicked()
        {
            if (_currentData == null) return;

            // Booster miễn phí → grant luôn, không trừ coin
            if (!_currentData.IsPurchasable)
            {
                BoosterInventory.Add(_currentData, _currentData.coinPurchaseAmount);
                Debug.Log($"[BoosterPurchasePopup] Grant free '{_currentData.boosterName}' x{_currentData.coinPurchaseAmount}");
                OnGrantSuccess();
                return;
            }

            if (CoinManager.Instance == null)
            {
                Debug.LogError("[BoosterPurchasePopup] CoinManager.Instance is null!");
                return;
            }

            bool success = CoinManager.Instance.SpendCoins(_currentData.coinCost);
            if (success)
            {
                BoosterInventory.Add(_currentData, _currentData.coinPurchaseAmount);
                Debug.Log($"[BoosterPurchasePopup] Mua '{_currentData.boosterName}' " +
                          $"x{_currentData.coinPurchaseAmount} giá {_currentData.coinCost} coin.");
                OnGrantSuccess();
            }
            else
            {
                // Không đủ coin → rung nút
                if (buyButton != null)
                {
                    buyButton.transform.DOKill();
                    buyButton.transform
                             .DOShakePosition(0.3f, new Vector3(8f, 0f, 0f), 20)
                             .SetRelative()
                             .SetUpdate(true);
                }
                Debug.Log($"[BoosterPurchasePopup] Không đủ coin để mua '{_currentData.boosterName}'.");
            }
        }

        /// <summary>
        /// Xem ads → nhận free 1 lượt.
        /// TODO: thay bằng AdsManager.ShowRewardedAd(onSuccess: ...) khi tích hợp.
        /// </summary>
        private void OnWatchAdsClicked()
        {
            if (_currentData == null) return;

            // ── [ADS PLACEHOLDER] bấm = nhận luôn ─────────────────────────
            // TODO: wrap bằng:
            //   AdsManager.ShowRewardedAd(onSuccess: () =>
            //   {
            //       BoosterInventory.Add(_currentData, 1);
            //       OnGrantSuccess();
            //   });
            Debug.Log($"[BoosterPurchasePopup] [ADS PLACEHOLDER] Nhận free '{_currentData.boosterName}' x1.");
            BoosterInventory.Add(_currentData, 1);
            OnGrantSuccess();
            // ── End placeholder ────────────────────────────────────────────
        }

        // ── Shared Grant Flow ─────────────────────────────────────────────────

        private void OnGrantSuccess()
        {
            // Notify BoosterSlotView refresh UI ngay (trước khi đóng)
            Core.EventBus.RaiseBoosterPurchased(_currentData.boosterName);

            // Bounce nhỏ → đóng
            var animTarget = popupPanel != null ? popupPanel.transform : popupRoot.transform;
            animTarget.DOKill();
            animTarget
                .DOScale(Vector3.one * 1.08f, 0.12f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() =>
                    animTarget.DOScale(Vector3.one, 0.1f)
                              .SetUpdate(true)
                              .OnComplete(() =>
                                  DOVirtual.DelayedCall(0.2f, Hide, ignoreTimeScale: true)));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void RefreshBuyButtonState()
        {
            if (buyButton == null || _currentData == null) return;
            if (!_currentData.IsPurchasable) { buyButton.interactable = true; return; }
            long coins = CoinManager.Instance != null ? CoinManager.Instance.CurrentCoins : 0;
            buyButton.interactable = coins >= _currentData.coinCost;
        }
    }
}