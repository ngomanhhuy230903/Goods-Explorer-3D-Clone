using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using FoodMatch.Level;
using FoodMatch.Managers;
using FoodMatch.Items;
using FoodMatch.Tray;
using System.Collections;

namespace FoodMatch.UI
{
    /// <summary>
    /// Gắn vào GameObject "GameResultUI" — con của MainCanvas.
    ///
    /// ══ WIN FLOW ══
    ///   Overlay → PopupWin
    ///     • Title text
    ///     • Coin reward text  (+N)
    ///     • [x2 Ads]   → DoubleWinReward() → GoHome luôn
    ///     • [Next Level] → LoadNextLevel() → GoHome luôn
    ///
    /// ══ LOSE FLOW ══
    ///   Overlay → LosePopup1 (Revive cơ hội 1)
    ///     • [Dùng vàng]  → TryRevive() → ClearTrayBooster.Execute() → resume Play
    ///     • [Xem Ads]    → ClearTrayBooster.Execute() → resume Play (miễn phí)
    ///     • [Đóng]       → ẩn P1, hiện LosePopup2
    ///   LosePopup2 (Revive cơ hội 2)
    ///     • [Dùng vàng]  → TryRevive() → ClearTrayBooster.Execute() → resume Play
    ///     • [Xem Ads]    → ClearTrayBooster.Execute() → resume Play (miễn phí)
    ///     • [Đóng]       → ẩn P2, hiện LosePopup3
    ///   LosePopup3 (Final)
    ///     • [Retry]      → RestartCurrentLevel()
    ///     • [Đóng]       → fade loading → GameState.Menu
    /// </summary>
    public class GameResultUI : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════════
        // INSPECTOR
        // ═══════════════════════════════════════════════════════════════════════

        [Header("─── Overlay ─────────────────────────")]
        [SerializeField] private Image overlayDim;
        [SerializeField] [Range(0f, 1f)] private float overlayTargetAlpha = 0.6f;
        [SerializeField] private float overlayFadeDuration = 0.35f;

        // ── WIN ───────────────────────────────────────────────────────────────
        [Header("─── Win: Popup ──────────────────────")]
        [SerializeField] private GameObject popupWin;
        [SerializeField] private TMP_Text winTitleText;        // "Hoàn thành!" / tùy
        [SerializeField] private TMP_Text winCoinRewardText;   // "+50"
        [SerializeField] private Button winBtn_NextLevel;

        [Header("─── Win: Double Reward ─────────────")]
        [Tooltip("1 button x2 Ads. Sau khi click (hoặc next level) → GoHome luôn.")]
        [SerializeField] private TMP_Text winDoubleRewardText; // "+100" (= reward × mult)
        [SerializeField] private Button winBtn_DoubleAds;

        // ── LOSE — Popup 1 ────────────────────────────────────────────────────
        [Header("─── Lose: Popup 1 (Revive #1) ───────")]
        [SerializeField] private GameObject losePopup1;
        [SerializeField] private TMP_Text lose1_CoinCostText;  // giá revive bằng vàng
        [SerializeField] private Button lose1_ReviveCoinBtn; // dùng vàng → revive
        [SerializeField] private Button lose1_ReviveAdsBtn;  // xem ads → revive miễn phí
        [SerializeField] private Button lose1_CloseBtn;      // → popup 2

        // ── LOSE — Popup 2 ────────────────────────────────────────────────────
        [Header("─── Lose: Popup 2 (Revive #2) ───────")]
        [SerializeField] private GameObject losePopup2;
        [SerializeField] private TMP_Text lose2_CoinCostText;
        [SerializeField] private Button lose2_ReviveCoinBtn;
        [SerializeField] private Button lose2_ReviveAdsBtn;
        [SerializeField] private Button lose2_CloseBtn;      // → popup 3

        // ── LOSE — Popup 3 ────────────────────────────────────────────────────
        [Header("─── Lose: Popup 3 (Final) ─────────")]
        [SerializeField] private GameObject losePopup3;
        [SerializeField] private Button lose3_RetryBtn;
        [SerializeField] private Button lose3_CloseBtn;      // loading → home

        // ── Loading overlay (dùng chung, fade khi về home) ───────────────────
        [Header("─── Loading Panel ───────────────────")]
        [Tooltip("CanvasGroup của panel Loading (dùng lại panelLoading từ UIManager nếu có).")]
        [SerializeField] private CanvasGroup loadingPanel;
        [SerializeField] private float loadingFadeDuration = 0.35f;

        // ── Revive Dependencies ───────────────────────────────────────────────
        [Header("─── Revive Dependencies ──────────────")]
        [Tooltip("Kéo BoosterInstaller vào đây để lấy BoosterContext cho ClearTrayBooster khi revive.")]
        [SerializeField] private BoosterInstaller boosterInstaller;

        // ── 3D Objects ────────────────────────────────────────────────────────
        [Header("─── 3D Objects cần ẩn khi Win/Lose ──")]
        [SerializeField] private GameObject foodGridObject;
        [SerializeField] private GameObject[] extraObjectsToHide;

        // ── Animation ─────────────────────────────────────────────────────────
        [Header("─── Animation ────────────────────────")]
        [SerializeField] private float popupScaleDuration = 0.45f;
        [SerializeField] private Ease popupScaleEase = Ease.OutBack;
        [SerializeField] private float popupDelay = 0.15f;

        // ═══════════════════════════════════════════════════════════════════════
        // RUNTIME
        // ═══════════════════════════════════════════════════════════════════════

        private bool _isShowing;
        private bool _doubleResolved;
        private Canvas _popupCanvas;

        // ═══════════════════════════════════════════════════════════════════════
        // UNITY LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            BuildPopupCanvas();
            HideAll();
            BindButtons();
        }

        private void OnEnable() => GameManager.OnGameStateChanged += HandleStateChanged;
        private void OnDisable() => GameManager.OnGameStateChanged -= HandleStateChanged;

        // ═══════════════════════════════════════════════════════════════════════
        // POPUP CANVAS
        // ═══════════════════════════════════════════════════════════════════════

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
            ReparentToPopupCanvas(popupWin);
            ReparentToPopupCanvas(losePopup1);
            ReparentToPopupCanvas(losePopup2);
            ReparentToPopupCanvas(losePopup3);

            Debug.Log("[GameResultUI] PopupCanvas đã được tạo.");
        }

        private void ReparentToPopupCanvas(GameObject target)
        {
            if (target == null || _popupCanvas == null) return;
            var rt = target.GetComponent<RectTransform>();
            if (rt == null) return;

            var anchorMin = rt.anchorMin;
            var anchorMax = rt.anchorMax;
            var offsetMin = rt.offsetMin;
            var offsetMax = rt.offsetMax;
            var anchoredPos = rt.anchoredPosition;
            var sizeDelta = rt.sizeDelta;
            var localScale = rt.localScale;

            rt.SetParent(_popupCanvas.transform, false);

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            rt.localScale = localScale;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BUTTON BINDING
        // ═══════════════════════════════════════════════════════════════════════

        private void BindButtons()
        {
            // Win
            winBtn_NextLevel?.onClick.AddListener(OnClickNextLevel);
            winBtn_DoubleAds?.onClick.AddListener(OnClickDoubleRewardAds);

            // Lose P1
            lose1_ReviveCoinBtn?.onClick.AddListener(() => OnClickReviveCoin(1));
            lose1_ReviveAdsBtn?.onClick.AddListener(() => OnClickReviveAds(1));
            lose1_CloseBtn?.onClick.AddListener(() => TransitionLosePopup(from: 1, to: 2));

            // Lose P2
            lose2_ReviveCoinBtn?.onClick.AddListener(() => OnClickReviveCoin(2));
            lose2_ReviveAdsBtn?.onClick.AddListener(() => OnClickReviveAds(2));
            lose2_CloseBtn?.onClick.AddListener(() => TransitionLosePopup(from: 2, to: 3));

            // Lose P3
            lose3_RetryBtn?.onClick.AddListener(OnClickRetry);
            lose3_CloseBtn?.onClick.AddListener(OnClickCloseToHome);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // STATE HANDLER
        // ═══════════════════════════════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════════════════════════════
        // CORE SHOW FLOW
        // ═══════════════════════════════════════════════════════════════════════

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
            {
                SetupWinPopup();
                AnimatePopupIn(popupWin);
            }
            else
            {
                SetupLosePopup1();
                AnimatePopupIn(losePopup1);
            }
        }

        private void AnimatePopupIn(GameObject target)
        {
            if (target == null) { Debug.LogError("[GameResultUI] Popup target null!"); return; }
            target.SetActive(true);
            target.transform.localScale = Vector3.zero;
            DOVirtual.DelayedCall(popupDelay, () =>
                target.transform
                    .DOScale(Vector3.one, popupScaleDuration)
                    .SetEase(popupScaleEase)
                    .SetUpdate(true),
                ignoreTimeScale: true);
        }

        private void AnimatePopupOut(GameObject target, System.Action onDone = null)
        {
            if (target == null) { onDone?.Invoke(); return; }
            target.transform
                .DOScale(Vector3.zero, popupScaleDuration * 0.6f)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    target.SetActive(false);
                    onDone?.Invoke();
                });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // WIN SETUP
        // ═══════════════════════════════════════════════════════════════════════

        private void SetupWinPopup()
        {
            _doubleResolved = false;

            if (CoinManager.Instance != null)
            {
                long reward = CoinManager.Instance.GetWinReward();
                float mult = CoinManager.Instance.GetAdsMultiplier();
                long doubled = (long)(reward * mult);

                if (winCoinRewardText) winCoinRewardText.text = $"+{reward}";
                if (winDoubleRewardText) winDoubleRewardText.text = $"+{doubled}";
            }


        }

        // ═══════════════════════════════════════════════════════════════════════
        // WIN CALLBACKS
        // ═══════════════════════════════════════════════════════════════════════

        private void OnClickDoubleRewardAds()
        {
            if (_doubleResolved) return;
            // TODO: tích hợp Ads SDK thật — hiện tại giả lập thành công
            Debug.Log("[GameResultUI] Double Reward Ads – giả lập.");
            CoinManager.Instance?.DoubleWinReward();
            _doubleResolved = true;
            // x2 xong → về home luôn
            CleanupAndResume();
            GameManager.Instance?.ChangeState(GameState.Menu);
        }

        private void OnClickNextLevel()
        {
            CleanupAndResume();
            LevelManager.Instance?.LoadNextLevel();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // LOSE SETUP
        // ═══════════════════════════════════════════════════════════════════════

        private void SetupLosePopup1()
        {
            RefreshReviveCoinButton(lose1_ReviveCoinBtn, lose1_CoinCostText);
        }

        private void SetupLosePopup2()
        {
            RefreshReviveCoinButton(lose2_ReviveCoinBtn, lose2_CoinCostText);
        }

        /// <summary>Cập nhật text giá coin và interactable cho nút dùng vàng.</summary>
        private void RefreshReviveCoinButton(Button btn, TMP_Text costText)
        {
            if (CoinManager.Instance == null) return;
            long cost = CoinManager.Instance.GetReviveCost();
            long coins = CoinManager.Instance.CurrentCoins;
            if (costText) costText.text = cost.ToString();
            if (btn) btn.interactable = coins >= cost;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // LOSE TRANSITIONS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Animate từ popup (from) sang popup (to): 1→2, 2→3.</summary>
        private void TransitionLosePopup(int from, int to)
        {
            GameObject fromPopup = GetLosePopup(from);
            GameObject toPopup = GetLosePopup(to);

            AnimatePopupOut(fromPopup, () =>
            {
                if (to == 2) SetupLosePopup2();
                // Popup 3 không cần setup riêng
                AnimatePopupIn(toPopup);
            });
        }

        private GameObject GetLosePopup(int index) => index switch
        {
            1 => losePopup1,
            2 => losePopup2,
            3 => losePopup3,
            _ => null
        };

        // ═══════════════════════════════════════════════════════════════════════
        // LOSE CALLBACKS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Dùng vàng hồi sinh (popup 1 hoặc 2).
        /// Nếu đủ coin → trừ coin → ConfirmRevive.
        /// </summary>
        private void OnClickReviveCoin(int popupIndex)
        {
            if (CoinManager.Instance == null) return;
            if (!CoinManager.Instance.TryRevive()) return; // không đủ coin → button đã disabled, không xảy ra
            ConfirmRevive(popupIndex, byAds: false);
        }

        /// <summary>
        /// Xem ads để hồi sinh miễn phí (popup 1 hoặc 2).
        /// TODO: tích hợp Ads SDK thật — gọi ShowRewardedAd() rồi trong callback gọi ConfirmRevive.
        /// </summary>
        private void OnClickReviveAds(int popupIndex)
        {
            // TODO: thay dòng dưới bằng Ads SDK callback thật
            Debug.Log($"[GameResultUI] Revive Ads popup {popupIndex} – giả lập.");
            ConfirmRevive(popupIndex, byAds: true);
        }

        /// <summary>
        /// Xác nhận hồi sinh thành công (coin hoặc ads).
        /// Flow:
        ///   1. CleanupAndResume() → ẩn popup, Time.timeScale = 1
        ///   2. Tạo ClearTrayBooster trực tiếp (không qua BoosterManager — không tốn quantity)
        ///   3. Inject BoosterContext từ BoosterInstaller, gọi Execute()
        ///   4. StartCoroutine chờ animation xong → ChangeState(Play)
        /// </summary>
        private void ConfirmRevive(int popupIndex, bool byAds)
        {
            Debug.Log($"[GameResultUI] Revive popup {popupIndex} byAds={byAds} → ClearTray + Play.");
            CleanupAndResume(); // ẩn popup, Time.timeScale = 1

            StartCoroutine(ReviveRoutine());
        }

        private System.Collections.IEnumerator ReviveRoutine()
        {
            // Revive: chỉ dùng ClearTrayBooster để dọn BackupTray, không thay đổi
            // bất kỳ thứ gì khác trong game state. LevelManager KHÔNG được ResetAllSystems()
            // khi Lose — game state phải còn nguyên để chơi tiếp được.

            if (boosterInstaller != null && boosterInstaller.Context != null)
            {
                var context = boosterInstaller.Context;
                var clearTray = new ClearTrayBooster();
                clearTray.Initialize(context);

                if (clearTray.CanExecute())
                {
                    // Tính thời gian chờ animation: (N-1)*stagger + flyDuration + buffer
                    // StaggerDelay=0.08s, FlyDuration=0.4s — khớp với hằng số trong ClearTrayBooster
                    int foodCount = context.BackupTray.OccupiedCount;
                    float waitTime = (foodCount - 1) * 0.08f + 0.4f + 0.15f;

                    clearTray.Execute();
                    yield return new WaitForSeconds(waitTime);
                }
            }
            else
            {
                Debug.LogWarning("[GameResultUI] boosterInstaller chưa gán — revive không có ClearTray animation.");
            }

            GameManager.Instance?.ChangeState(GameState.Play);
        }

        // ── Popup 3 ──────────────────────────────────────────────────────────

        private void OnClickRetry()
        {
            // Người chơi từ chối revive và chọn retry → trừ HP
            HPManager.Instance?.DeductHPOnQuit();
            CleanupAndResume();
            LevelManager.Instance?.RestartCurrentLevel();
        }

        /// <summary>Đóng popup 3 → reset toàn bộ game objects → fade loading → về Home.</summary>
        private void OnClickCloseToHome()
        {
            // Người chơi từ chối revive và chọn về home → trừ HP
            HPManager.Instance?.DeductHPOnQuit();
            CleanupAndResume();

            // Reset toàn bộ hệ thống game (grid, tray, order...) trước khi về home.
            // Cần thiết vì LevelManager không còn reset khi Lose để cho phép revive.
            // ChangeState(LoadLevel) sẽ trigger LevelManager.LoadLevel() → ResetAllSystems()
            // nhưng ta không muốn load level — ta chỉ muốn reset sạch rồi về Menu.
            // Dùng LoadLevel nội bộ thông qua một state trung gian là cách sạch nhất:
            // gọi ResetAllSystems() trực tiếp qua LevelManager public method.
            LevelManager.Instance?.CleanupForHome();

            if (loadingPanel != null)
            {
                loadingPanel.gameObject.SetActive(true);
                loadingPanel.alpha = 0f;
                loadingPanel.DOFade(1f, loadingFadeDuration)
                    .OnComplete(() => GameManager.Instance?.ChangeState(GameState.Menu));
            }
            else
            {
                GameManager.Instance?.ChangeState(GameState.Menu);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SHARED CALLBACKS
        // ═══════════════════════════════════════════════════════════════════════

        public void OnClickGoHome()
        {
            CleanupAndResume();
            GameManager.Instance?.ChangeState(GameState.Menu);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════

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
            popupWin?.SetActive(false);
            losePopup1?.SetActive(false);
            losePopup2?.SetActive(false);
            losePopup3?.SetActive(false);

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