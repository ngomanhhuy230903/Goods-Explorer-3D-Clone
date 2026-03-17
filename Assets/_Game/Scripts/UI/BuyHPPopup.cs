using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using FoodMatch.Data;
using FoodMatch.Managers;

namespace FoodMatch.UI
{
    /// <summary>
    /// Popup mua HP bằng coin hoặc xem ads.
    /// - Hiển thị giá coin = reviveCost từ PlayerCurrencyConfig.
    /// - Đếm ngược thời gian hồi tim tiếp theo.
    /// - Mua bằng coin / xem ads (placeholder) / đóng popup.
    ///
    /// Setup:
    ///   1. Tạo GameObject BuyHPPopup trong Canvas.
    ///   2. Gắn script này vào.
    ///   3. Kéo các field vào Inspector.
    ///   4. Gọi BuyHPPopup.Instance.Show() từ HPBarUI hoặc UIManager khi HP == 0.
    /// </summary>
    public class BuyHPPopup : MonoBehaviour
    {
        public static BuyHPPopup Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("─── Popup Root ─────────────────────────")]
        [SerializeField] private GameObject popupRoot;

        [Tooltip("Panel con bên trong — dùng để animate scale.")]
        [SerializeField] private RectTransform popupPanel;

        [Header("─── Content ───────────────────────────")]
        [SerializeField] private TextMeshProUGUI timerText;      // "19:45" hoặc "Full"
        [SerializeField] private TextMeshProUGUI coinCostText;   // "30 🪙"

        [Header("─── Buttons ────────────────────────────")]
        [SerializeField] private Button buyCoinButton;
        [SerializeField] private Button watchAdsButton;
        [SerializeField] private Button closeButton;

        [Header("─── Config ─────────────────────────────")]
        [Tooltip("PlayerCurrencyConfig SO — kéo vào để đọc reviveCost.")]
        [SerializeField] private PlayerCurrencyConfig currencyConfig;

        [Header("─── Animation ──────────────────────────")]
        [SerializeField] private float scaleDuration = 0.45f;
        [SerializeField] private Ease scaleEaseIn = Ease.OutBack;
        [SerializeField] private Ease scaleEaseOut = Ease.InBack;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            if (popupRoot != null) popupRoot.SetActive(false);
        }

        private void Start()
        {
            if (buyCoinButton != null) buyCoinButton.onClick.AddListener(OnBuyCoinClicked);
            if (watchAdsButton != null) watchAdsButton.onClick.AddListener(OnWatchAdsClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);

            // Hiện giá ngay khi Start (giá không đổi trong session)
            RefreshCostUI();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (popupRoot != null && popupRoot.activeSelf)
                TickTimer();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Mở popup mua HP.</summary>
        public void Show()
        {
            RefreshHPUI();
            RefreshCostUI();
            AnimateIn();
        }

        /// <summary>Đóng popup.</summary>
        public void Hide()
        {
            AnimateOut();
        }

        // ── UI Refresh ────────────────────────────────────────────────────────

        private void RefreshHPUI()
        {
            if (HPManager.Instance == null) return;

            int cur = HPManager.Instance.CurrentHP;
            int max = HPManager.Instance.MaxHP;

            // Nếu đã đầy HP thì không cần mua → hiện thông báo
            if (cur >= max)
            {
                if (timerText != null) timerText.text = "Full";
                if (buyCoinButton != null) buyCoinButton.interactable = false;
                if (watchAdsButton != null) watchAdsButton.interactable = false;
            }
            else
            {
                if (buyCoinButton != null) buyCoinButton.interactable = CanAfford();
                if (watchAdsButton != null) watchAdsButton.interactable = true;
            }
        }

        /// <summary>
        /// Cập nhật giá coin = reviveCost từ PlayerCurrencyConfig.
        /// </summary>
        private void RefreshCostUI()
        {
            if (currencyConfig == null) return;

            int cost = currencyConfig.reviveCost;

            if (coinCostText != null)
                coinCostText.text = cost > 0 ? $"{cost}" : "Free";

            // Disable mua coin nếu reviveCost == 0 (feature bị tắt)
            if (buyCoinButton != null && cost == 0)
                buyCoinButton.interactable = false;
        }

        // ── Timer ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Gọi mỗi frame khi popup đang mở — cập nhật đồng hồ hồi tim.
        /// </summary>
        private void TickTimer()
        {
            if (HPManager.Instance == null) return;

            if (HPManager.Instance.CurrentHP >= HPManager.Instance.MaxHP)
            {
                if (timerText != null) timerText.text = "Full";
                return;
            }

            if (timerText != null)
                timerText.text = FormatTime(HPManager.Instance.SecondsUntilNextRegen);
        }

        // ── Button Handlers ───────────────────────────────────────────────────

        private void OnBuyCoinClicked()
        {
            if (currencyConfig == null || HPManager.Instance == null) return;

            int cost = currencyConfig.reviveCost;
            if (cost <= 0)
            {
                // reviveCost == 0 → grant free
                GrantHP();
                return;
            }

            if (CoinManager.Instance == null)
            {
                Debug.LogError("[BuyHPPopup] CoinManager.Instance is null!");
                return;
            }

            bool success = CoinManager.Instance.SpendCoins(cost);
            if (success)
            {
                Debug.Log($"[BuyHPPopup] Mua HP bằng {cost} coin thành công.");
                GrantHP();
            }
            else
            {
                // Không đủ coin → rung nút
                ShakeButton(buyCoinButton);
                Debug.Log("[BuyHPPopup] Không đủ coin để mua HP.");
            }
        }

        /// <summary>
        /// Xem ads → nhận +1 HP.
        /// TODO: thay placeholder bằng AdsManager.ShowRewardedAd(onSuccess: ...).
        /// </summary>
        private void OnWatchAdsClicked()
        {
            // ── [ADS PLACEHOLDER] ──────────────────────────────────────────
            // TODO: AdsManager.ShowRewardedAd(onSuccess: () =>
            // {
            //     GrantHP();
            // });
            Debug.Log("[BuyHPPopup] [ADS PLACEHOLDER] Nhận +1 HP từ ads.");
            GrantHP();
            // ── End placeholder ─────────────────────────────────────────────
        }

        // ── Grant HP Flow ─────────────────────────────────────────────────────

        private void GrantHP()
        {
            if (HPManager.Instance == null) return;

            HPManager.Instance.AddHP(1);
            Debug.Log($"[BuyHPPopup] +1 HP. Hiện tại: {HPManager.Instance.CurrentHP}/{HPManager.Instance.MaxHP}");

            // Refresh UI ngay
            RefreshHPUI();

            // Bounce nhỏ rồi đóng
            var animTarget = popupPanel != null ? (Transform)popupPanel : popupRoot.transform;
            animTarget.DOKill();
            animTarget
                .DOScale(Vector3.one * 1.08f, 0.12f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() =>
                    animTarget.DOScale(Vector3.one, 0.1f)
                              .SetUpdate(true)
                              .OnComplete(() =>
                                  DOVirtual.DelayedCall(0.25f, Hide, ignoreTimeScale: true)));
        }

        // ── Animation ─────────────────────────────────────────────────────────

        private void AnimateIn()
        {
            if (popupRoot == null) return;

            var t = popupPanel != null ? (Transform)popupPanel : popupRoot.transform;
            // Kill tween cũ trước, reset scale rồi mới SetActive + animate
            t.DOKill();
            t.localScale = Vector3.zero;
            popupRoot.SetActive(true);

            t.DOScale(Vector3.one, scaleDuration)
             .SetEase(scaleEaseIn)
             .SetUpdate(true);
        }

        private void AnimateOut()
        {
            if (popupRoot == null) return;

            var t = popupPanel != null ? (Transform)popupPanel : popupRoot.transform;
            t.DOKill();
            t.DOScale(Vector3.zero, scaleDuration * 0.6f)
             .SetEase(scaleEaseOut)
             .SetUpdate(true)
             .OnComplete(() =>
             {
                 popupRoot.SetActive(false);
                 // Reset scale để lần Show() tiếp không bắt đầu từ zero
                 t.localScale = Vector3.one;
             });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private bool CanAfford()
        {
            if (currencyConfig == null || CoinManager.Instance == null) return false;
            return CoinManager.Instance.CurrentCoins >= currencyConfig.reviveCost;
        }

        private void ShakeButton(Button btn)
        {
            if (btn == null) return;
            btn.transform.DOKill();
            btn.transform
               .DOShakePosition(0.3f, new Vector3(8f, 0f, 0f), 20)
               .SetRelative()
               .SetUpdate(true);
        }

        // "1200s" → "20:00",  "65s" → "01:05"
        private static string FormatTime(float totalSeconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(totalSeconds));
            int m = total / 60;
            int s = total % 60;
            return $"{m:00}:{s:00}";
        }
    }
}