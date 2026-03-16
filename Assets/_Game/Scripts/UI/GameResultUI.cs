using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using FoodMatch.Level;
using FoodMatch.Managers;

namespace FoodMatch.UI
{
    /// <summary>
    /// Gắn vào GameObject "GameResultUI" — con của MainCanvas.
    /// Tích hợp đầy đủ: HP display, Coin display, Revive (coin + ads), Double Reward (ads).
    ///
    /// Flow LOSE:
    ///   Overlay mờ → popupLose hiện → người chơi chọn:
    ///     [Revive Coin]  → trừ coin, ẩn popupLose, resume game
    ///     [Revive Ads]   → xem ads, ẩn popupLose, resume game  (free)
    ///     [Try Again]    → restart level
    ///     [Go Home]      → về menu
    ///
    /// Flow WIN:
    ///   Overlay mờ → popupWin hiện → coin reward đã cộng tự động qua CoinManager
    ///     [Double x2 Ads] → xem ads → nhân đôi coin bonus
    ///     [Go Home]       → về menu
    /// </summary>
    public class GameResultUI : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("─── Overlay ─────────────────────────")]
        [SerializeField] private Image overlayDim;
        [SerializeField] [Range(0f, 1f)] private float overlayTargetAlpha = 0.6f;
        [SerializeField] private float overlayFadeDuration = 0.35f;

        [Header("─── Popups ───────────────────────────")]
        [SerializeField] private GameObject popupLose;
        [SerializeField] private GameObject popupWin;

        // ── Lose Panel UI ─────────────────────────────────────────────────────
        [Header("─── Lose: Main buttons ──────────────")]
        [SerializeField] private Button loseBtn_GoHome;
        [SerializeField] private Button loseBtn_TryAgain;

        [Header("─── Lose: Revive Panel ─────────────")]
        [Tooltip("Panel chứa 2 nút revive + countdown. Ẩn/hiện cùng popupLose.")]
        [SerializeField] private GameObject revivePanel;
        [SerializeField] private Button reviveBtn_Coin;
        [SerializeField] private TMP_Text reviveCoinCostText;
        [SerializeField] private Button reviveBtn_Ads;
        [SerializeField] private GameObject reviveAdsUnavailableOverlay;
        [Tooltip("Hiện khi không đủ coin để revive.")]
        [SerializeField] private GameObject notEnoughCoinIndicator;

        [Header("─── Lose: HP Display ────────────────")]
        [Tooltip("(Tuỳ chọn) Text hiện HP còn lại sau khi thua.")]
        [SerializeField] private TMP_Text loseHP_Text;

        [Header("─── Lose: Countdown ─────────────────")]
        [SerializeField] private bool enableReviveCountdown = true;
        [SerializeField] private float reviveCountdownSeconds = 10f;
        [SerializeField] private Image reviveCountdownFill;
        [SerializeField] private TMP_Text reviveCountdownText;

        // ── Win Panel UI ──────────────────────────────────────────────────────
        [Header("─── Win: Main buttons ───────────────")]
        [SerializeField] private Button winBtn_GoHome;
        [SerializeField] private Button winBtn_NextLevel;

        [Header("─── Win: Coin Reward ──────────────────")]
        [SerializeField] private TMP_Text winCoinRewardText;    // "+50 🪙"
        [SerializeField] private TMP_Text winDoubleRewardText;  // "+100 🪙"
        [SerializeField] private Button winBtn_DoubleAds;
        [SerializeField] private Button winBtn_SkipDouble;
        [Tooltip("Panel chứa 2 nút double/skip. Ẩn sau khi đã double hoặc skip.")]
        [SerializeField] private GameObject doubleRewardPanel;

        // ── Shared Coin Display ───────────────────────────────────────────────
        [Header("─── Shared: Coin & HP display ────────")]
        [Tooltip("TMP hiện tổng coin hiện tại (dùng chung Win/Lose).")]
        [SerializeField] private TMP_Text totalCoinText;
        [SerializeField] private TMP_Text totalHPText; // "HP: 3/5"

        // ─── 3D Objects ───────────────────────────────────────────────────────
        [Header("─── 3D Objects cần ẩn khi Win/Lose ───")]
        [SerializeField] private GameObject foodGridObject;
        [SerializeField] private GameObject[] extraObjectsToHide;

        // ─── Animation ────────────────────────────────────────────────────────
        [Header("─── Animation ────────────────────────")]
        [SerializeField] private float popupScaleDuration = 0.45f;
        [SerializeField] private Ease popupScaleEase = Ease.OutBack;
        [SerializeField] private float popupDelay = 0.15f;

        // ─── Runtime ──────────────────────────────────────────────────────────
        private bool _isShowing;
        private bool _reviveResolved;
        private bool _doubleResolved;
        private float _reviveRemainingTime;
        private Canvas _popupCanvas;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            BuildPopupCanvas();
            HideAll();
            BindButtons();
        }

        private void OnEnable()
        {
            GameManager.OnGameStateChanged += HandleStateChanged;
            CoinManager.OnCoinChanged += RefreshCoinDisplay;
            HPManager.OnHPChanged += RefreshHPDisplay;
        }

        private void OnDisable()
        {
            GameManager.OnGameStateChanged -= HandleStateChanged;
            CoinManager.OnCoinChanged -= RefreshCoinDisplay;
            HPManager.OnHPChanged -= RefreshHPDisplay;
        }

        private void Update()
        {
            TickReviveCountdown();
        }

        // ─── PopupCanvas (luôn trên 3D) ───────────────────────────────────────

        private void BuildPopupCanvas()
        {
            var go = new GameObject("PopupCanvas");
            DontDestroyOnLoad(go);

            _popupCanvas = go.AddComponent<Canvas>();
            _popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _popupCanvas.sortingOrder = 999;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            ReparentToPopupCanvas(overlayDim != null ? overlayDim.gameObject : null);
            ReparentToPopupCanvas(popupLose);
            ReparentToPopupCanvas(popupWin);

            Debug.Log("[GameResultUI] PopupCanvas (Overlay) đã được tạo.");
        }

        private void ReparentToPopupCanvas(GameObject target)
        {
            if (target == null || _popupCanvas == null) return;
            var rt = target.GetComponent<RectTransform>();
            if (rt == null) return;

            Vector2 anchorMin = rt.anchorMin;
            Vector2 anchorMax = rt.anchorMax;
            Vector2 offsetMin = rt.offsetMin;
            Vector2 offsetMax = rt.offsetMax;
            Vector2 anchoredPos = rt.anchoredPosition;
            Vector2 sizeDelta = rt.sizeDelta;
            Vector3 localScale = rt.localScale;

            rt.SetParent(_popupCanvas.transform, false);

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            rt.localScale = localScale;
        }

        // ─── State Handler ────────────────────────────────────────────────────

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Win:
                    SetGameObjectsVisible(false);
                    ShowResult(isWin: true);
                    break;

                case GameState.Lose:
                    SetGameObjectsVisible(false);
                    ShowResult(isWin: false);
                    break;

                case GameState.LoadLevel:
                case GameState.Menu:
                    ForceHideAll();
                    break;
            }
        }

        // ─── Button Binding ───────────────────────────────────────────────────

        private void BindButtons()
        {
            // Lose
            loseBtn_GoHome?.onClick.AddListener(OnClickGoHome);
            loseBtn_TryAgain?.onClick.AddListener(OnClickTryAgain);
            reviveBtn_Coin?.onClick.AddListener(OnClickReviveWithCoin);
            reviveBtn_Ads?.onClick.AddListener(OnClickReviveWithAds);

            // Win
            winBtn_GoHome?.onClick.AddListener(OnClickGoHome);
            winBtn_NextLevel?.onClick.AddListener(OnClickNextLevel);
            winBtn_DoubleAds?.onClick.AddListener(OnClickDoubleRewardAds);
            winBtn_SkipDouble?.onClick.AddListener(OnClickSkipDouble);
        }

        // ─── Core Show Flow ───────────────────────────────────────────────────

        private void ShowResult(bool isWin)
        {
            if (_isShowing) return;
            _isShowing = true;
            Time.timeScale = 0f;

            if (overlayDim != null)
            {
                overlayDim.gameObject.SetActive(true);
                var c = overlayDim.color; c.a = 0f; overlayDim.color = c;
                overlayDim.DOFade(overlayTargetAlpha, overlayFadeDuration)
                    .SetUpdate(true)
                    .OnComplete(() => ShowPopup(isWin));
            }
            else
            {
                ShowPopup(isWin);
            }
        }

        private void ShowPopup(bool isWin)
        {
            if (isWin)
                SetupWinPopup();
            else
                SetupLosePopup();

            GameObject target = isWin ? popupWin : popupLose;
            if (target == null)
            {
                Debug.LogError($"[GameResultUI] Popup {(isWin ? "Win" : "Lose")} chưa gán!");
                return;
            }

            target.SetActive(true);
            target.transform.localScale = Vector3.zero;

            DOVirtual.DelayedCall(popupDelay, () =>
            {
                target.transform
                    .DOScale(Vector3.one, popupScaleDuration)
                    .SetEase(popupScaleEase)
                    .SetUpdate(true);
            }, ignoreTimeScale: true);
        }

        // ─── Lose Setup ───────────────────────────────────────────────────────

        private void SetupLosePopup()
        {
            _reviveResolved = false;
            _reviveRemainingTime = reviveCountdownSeconds;

            // HP sau khi thua (HPManager đã trừ tại GameState.Lose)
            RefreshHPDisplay(
                HPManager.Instance != null ? HPManager.Instance.CurrentHP : 0,
                HPManager.Instance != null ? HPManager.Instance.MaxHP : 5);

            // Revive panel
            RefreshRevivePanel();

            if (revivePanel != null) revivePanel.SetActive(true);
        }

        private void RefreshRevivePanel()
        {
            if (CoinManager.Instance == null) return;
            long cost = CoinManager.Instance.GetReviveCost();
            long coins = CoinManager.Instance.CurrentCoins;

            if (reviveCoinCostText) reviveCoinCostText.text = cost.ToString();

            bool canAfford = coins >= cost;
            if (reviveBtn_Coin) reviveBtn_Coin.interactable = canAfford;
            if (notEnoughCoinIndicator) notEnoughCoinIndicator.SetActive(!canAfford);
            if (reviveAdsUnavailableOverlay) reviveAdsUnavailableOverlay.SetActive(false);
        }

        // ─── Win Setup ────────────────────────────────────────────────────────

        private void SetupWinPopup()
        {
            _doubleResolved = false;

            if (CoinManager.Instance != null)
            {
                long reward = CoinManager.Instance.GetWinReward();
                float mult = CoinManager.Instance.GetAdsMultiplier();
                long doubled = (long)(reward * mult);

                if (winCoinRewardText) winCoinRewardText.text = $"+{reward} 🪙";
                if (winDoubleRewardText) winDoubleRewardText.text = $"+{doubled} 🪙";
            }

            if (doubleRewardPanel) doubleRewardPanel.SetActive(true);

            RefreshCoinDisplay(CoinManager.Instance != null ? CoinManager.Instance.CurrentCoins : 0);
        }

        // ─── Revive Countdown ─────────────────────────────────────────────────

        private void TickReviveCountdown()
        {
            if (!enableReviveCountdown || _reviveResolved) return;
            if (popupLose == null || !popupLose.activeSelf) return;

            _reviveRemainingTime -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_reviveRemainingTime / reviveCountdownSeconds);

            if (reviveCountdownFill) reviveCountdownFill.fillAmount = t;
            if (reviveCountdownText) reviveCountdownText.text = Mathf.CeilToInt(_reviveRemainingTime).ToString();

            if (_reviveRemainingTime <= 0f)
                OnClickTryAgain(); // hết giờ → tự retry
        }

        // ─── Lose Button Callbacks ────────────────────────────────────────────

        private void OnClickReviveWithCoin()
        {
            if (_reviveResolved) return;
            if (CoinManager.Instance == null) return;

            if (!CoinManager.Instance.TryRevive())
            {
                if (notEnoughCoinIndicator != null)
                {
                    notEnoughCoinIndicator.SetActive(true);
                    notEnoughCoinIndicator.transform
                        .DOShakePosition(0.4f, 10f, 15)
                        .SetUpdate(true)
                        .OnComplete(() => notEnoughCoinIndicator.SetActive(false));
                }
                return;
            }
            ConfirmRevive();
        }

        private void OnClickReviveWithAds()
        {
            if (_reviveResolved) return;
            // TODO: thay bằng Ads SDK thật
            Debug.Log("[GameResultUI] Revive Ads – giả lập thành công.");
            ConfirmRevive();
        }

        /// <summary>Hồi sinh thành công – ẩn popup và resume game.</summary>
        private void ConfirmRevive()
        {
            _reviveResolved = true;
            CleanupAndResume();
            // Không đổi GameState – game tiếp tục từ điểm thua
            // LevelManager / GameController sẽ lắng nghe riêng nếu cần reset grid
            GameManager.Instance?.ChangeState(GameState.Play);
            Debug.Log("[GameResultUI] Revive thành công → GameState.Play");
        }

        // ─── Win Button Callbacks ─────────────────────────────────────────────

        private void OnClickDoubleRewardAds()
        {
            if (_doubleResolved) return;
            // TODO: thay bằng Ads SDK thật
            Debug.Log("[GameResultUI] Double Reward Ads – giả lập thành công.");
            CoinManager.Instance?.DoubleWinReward();
            _doubleResolved = true;
            if (doubleRewardPanel) doubleRewardPanel.SetActive(false);

            // Animate coin text để feedback
            if (totalCoinText != null)
                totalCoinText.transform.DOPunchScale(Vector3.one * 0.25f, 0.35f, 5, 0.5f).SetUpdate(true);
        }

        private void OnClickSkipDouble()
        {
            _doubleResolved = true;
            if (doubleRewardPanel) doubleRewardPanel.SetActive(false);
        }

        private void OnClickNextLevel()
        {
            CleanupAndResume();
            LevelManager.Instance?.LoadNextLevel();
        }

        // ─── Shared Button Callbacks ──────────────────────────────────────────

        public void OnClickGoHome()
        {
            CleanupAndResume();
            GameManager.Instance?.ChangeState(GameState.Menu);
        }

        public void OnClickTryAgain()
        {
            CleanupAndResume();
            LevelManager.Instance?.RestartCurrentLevel();
        }

        // ─── Display Refresh ──────────────────────────────────────────────────

        private void RefreshCoinDisplay(long coins)
        {
            if (totalCoinText != null)
                totalCoinText.text = FormatCoins(coins);
        }

        private void RefreshHPDisplay(int current, int max)
        {
            if (loseHP_Text != null)
                loseHP_Text.text = $"❤️ {current}/{max}";
            if (totalHPText != null)
                totalHPText.text = $"❤️ {current}/{max}";
        }

        private static string FormatCoins(long amount)
        {
            if (amount >= 1_000_000) return $"{amount / 1_000_000f:0.#}M 🪙";
            if (amount >= 1_000) return $"{amount / 1_000f:0.#}K 🪙";
            return $"{amount} 🪙";
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private void SetGameObjectsVisible(bool visible)
        {
            if (foodGridObject != null) foodGridObject.SetActive(visible);
            if (extraObjectsToHide == null) return;
            foreach (var obj in extraObjectsToHide)
                if (obj != null) obj.SetActive(visible);
        }

        private void CleanupAndResume()
        {
            Time.timeScale = 1f;
            _isShowing = false;
            HideAll();
        }

        private void HideAll()
        {
            popupLose?.SetActive(false);
            popupWin?.SetActive(false);
            if (revivePanel != null) revivePanel.SetActive(false);
            if (doubleRewardPanel != null) doubleRewardPanel.SetActive(false);

            if (overlayDim == null) return;
            overlayDim.DOKill();
            var c = overlayDim.color; c.a = 0f; overlayDim.color = c;
            overlayDim.gameObject.SetActive(false);
        }

        private void ForceHideAll()
        {
            Time.timeScale = 1f;
            _isShowing = false;
            HideAll();
        }

        private void OnDestroy()
        {
            if (_popupCanvas != null)
                Destroy(_popupCanvas.gameObject);
        }
    }
}