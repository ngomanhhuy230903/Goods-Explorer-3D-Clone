using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using FoodMatch.Managers;

namespace FoodMatch.UI
{
    /// <summary>
    /// Popup xuất hiện sau khi thắng ván, cho phép nhân đôi coin thưởng bằng ads.
    /// Gọi Show() từ PopupWin sau khi ClaimWinReward() đã được gọi.
    /// </summary>
    public class PopupDoubleReward : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text baseRewardText;    // "Bạn nhận được: 50 🪙"
        [SerializeField] private TMP_Text doubleRewardText;  // "Nhân đôi: 100 🪙"

        [Header("Buttons")]
        [SerializeField] private Button watchAdsButton;
        [SerializeField] private Button skipButton;

        [Header("Animation")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panelRect;

        public System.Action OnDoubleClaimDone;
        public System.Action OnSkipDone;

        private void Start()
        {
            watchAdsButton?.onClick.AddListener(HandleWatchAds);
            skipButton?.onClick.AddListener(HandleSkip);
        }

        private void OnEnable()
        {
            RefreshLabels();
            PlayShowAnimation();
        }

        // ─── Public ───────────────────────────────────────────────────────────

        public void Show()
        {
            gameObject.SetActive(true);
        }

        // ─── Handlers ─────────────────────────────────────────────────────────

        private void HandleWatchAds()
        {
            // TODO: Tích hợp Ads SDK thật. Hiện tại giả lập ads thành công.
            Debug.Log("[PopupDoubleReward] Ads – giả lập xem thành công.");
            SimulateAdsSuccess();
        }

        private void SimulateAdsSuccess()
        {
            CoinManager.Instance?.DoubleWinReward();
            PlayHideAnimation(() =>
            {
                gameObject.SetActive(false);
                OnDoubleClaimDone?.Invoke();
            });
        }

        private void HandleSkip()
        {
            PlayHideAnimation(() =>
            {
                gameObject.SetActive(false);
                OnSkipDone?.Invoke();
            });
        }

        // ─── UI ───────────────────────────────────────────────────────────────

        private void RefreshLabels()
        {
            if (CoinManager.Instance == null) return;
            long reward = CoinManager.Instance.GetWinReward();
            float mult = CoinManager.Instance.GetAdsMultiplier();
            long doubled = (long)(reward * mult);

            if (baseRewardText) baseRewardText.text = $"+{reward} 🪙";
            if (doubleRewardText) doubleRewardText.text = $"+{doubled} 🪙";
        }

        private void PlayShowAnimation()
        {
            if (canvasGroup)
            {
                canvasGroup.alpha = 0;
                canvasGroup.DOFade(1f, 0.3f);
            }
            if (panelRect)
            {
                panelRect.localScale = Vector3.one * 0.8f;
                panelRect.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
            }
        }

        private void PlayHideAnimation(System.Action onComplete)
        {
            if (canvasGroup)
                canvasGroup.DOFade(0f, 0.2f).OnComplete(() => onComplete?.Invoke());
            else
                onComplete?.Invoke();
        }
    }
}