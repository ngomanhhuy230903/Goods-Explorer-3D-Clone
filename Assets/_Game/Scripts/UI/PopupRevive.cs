using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using FoodMatch.Managers;
using FoodMatch.Core;

namespace FoodMatch.UI
{
    /// <summary>
    /// Popup xuất hiện khi người chơi thua, cho phép:
    /// 1. Hồi sinh bằng coin  (TryRevive)
    /// 2. Hồi sinh bằng xem ads (placeholder)
    /// 3. Về menu (kết thúc ván, không trừ thêm HP vì đã trừ ở Lose state)
    /// 
    /// Gắn vào Canvas Popup, kích hoạt từ PopupLose hoặc GameResultUI.
    /// </summary>
    public class PopupRevive : MonoBehaviour
    {
        [Header("Coin Revive")]
        [SerializeField] private Button reviveWithCoinButton;
        [SerializeField] private TMP_Text reviveCostText;
        [SerializeField] private GameObject notEnoughCoinIndicator;

        [Header("Ads Revive")]
        [SerializeField] private Button reviveWithAdsButton;
        [Tooltip("Hiện/ẩn nút ads tuỳ theo ads có sẵn sàng không.")]
        [SerializeField] private GameObject adsNotAvailableOverlay;

        [Header("Quit")]
        [SerializeField] private Button quitButton;

        [Header("Countdown (tự đóng sau N giây nếu không chọn)")]
        [SerializeField] private bool enableCountdown = true;
        [SerializeField] private float countdownSeconds = 10f;
        [SerializeField] private Image countdownFillImage;
        [SerializeField] private TMP_Text countdownText;

        private float _remainingTime;
        private bool _resolved; // tránh xử lý 2 lần

        // Callback về cho caller (PopupLose hoặc GameResultUI)
        public System.Action OnReviveConfirmed;
        public System.Action OnQuitConfirmed;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────

        private void OnEnable()
        {
            _resolved = false;
            _remainingTime = countdownSeconds;
            RefreshUI();
        }

        private void Update()
        {
            if (!enableCountdown || _resolved) return;
            _remainingTime -= Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(_remainingTime / countdownSeconds);
            if (countdownFillImage) countdownFillImage.fillAmount = t;
            if (countdownText) countdownText.text = Mathf.CeilToInt(_remainingTime).ToString();

            if (_remainingTime <= 0)
                HandleQuit();
        }

        private void Start()
        {
            reviveWithCoinButton?.onClick.AddListener(HandleReviveWithCoin);
            reviveWithAdsButton?.onClick.AddListener(HandleReviveWithAds);
            quitButton?.onClick.AddListener(HandleQuit);
        }

        // ─── Button Handlers ──────────────────────────────────────────────────

        private void HandleReviveWithCoin()
        {
            if (_resolved) return;
            if (CoinManager.Instance == null) return;

            if (!CoinManager.Instance.TryRevive())
            {
                // Không đủ coin – flash indicator
                if (notEnoughCoinIndicator != null)
                {
                    notEnoughCoinIndicator.SetActive(true);
                    notEnoughCoinIndicator.transform
                        .DOShakePosition(0.4f, 10f, 15)
                        .OnComplete(() => notEnoughCoinIndicator.SetActive(false));
                }
                return;
            }

            _resolved = true;
            OnReviveConfirmed?.Invoke();
            gameObject.SetActive(false);
        }

        private void HandleReviveWithAds()
        {
            if (_resolved) return;
            // TODO: Tích hợp Ads SDK thật. Hiện tại giả lập ads thành công.
            Debug.Log("[PopupRevive] Ads revive – giả lập xem ads thành công.");
            SimulateAdsWatched();
        }

        private void SimulateAdsWatched()
        {
            // Hồi sinh miễn phí khi xem ads (không tốn coin)
            _resolved = true;
            OnReviveConfirmed?.Invoke();
            gameObject.SetActive(false);
        }

        private void HandleQuit()
        {
            if (_resolved) return;
            _resolved = true;
            OnQuitConfirmed?.Invoke();
            gameObject.SetActive(false);
        }

        // ─── UI Refresh ───────────────────────────────────────────────────────

        private void RefreshUI()
        {
            if (CoinManager.Instance == null) return;

            long cost = CoinManager.Instance.GetReviveCost();
            long coins = CoinManager.Instance.CurrentCoins;

            if (reviveCostText) reviveCostText.text = cost.ToString();

            bool canAfford = coins >= cost;
            if (reviveWithCoinButton) reviveWithCoinButton.interactable = canAfford;
            if (notEnoughCoinIndicator) notEnoughCoinIndicator.SetActive(!canAfford);

            // Ads: luôn hiện (khi tích hợp thật sẽ check availability)
            if (adsNotAvailableOverlay) adsNotAvailableOverlay.SetActive(false);
        }
    }
}