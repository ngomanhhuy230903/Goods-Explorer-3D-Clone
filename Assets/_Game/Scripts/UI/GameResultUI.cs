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
    public class GameResultUI : MonoBehaviour
    {
        // ─── Singleton ────────────────────────────────────────────────────────
        public static GameResultUI Instance { get; private set; }

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
        [SerializeField] private TMP_Text winTitleText;
        [SerializeField] private TMP_Text winCoinRewardText;
        [SerializeField] private Button winBtn_NextLevel;

        [Header("─── Win: Double Reward ─────────────")]
        [SerializeField] private TMP_Text winDoubleRewardText;
        [SerializeField] private Button winBtn_DoubleAds;

        // ── LOSE — Popup 1 ────────────────────────────────────────────────────
        [Header("─── Lose: Popup 1 (Revive #1) ───────")]
        [SerializeField] private GameObject losePopup1;
        [SerializeField] private TMP_Text lose1_CoinCostText;
        [SerializeField] private Button lose1_ReviveCoinBtn;
        [SerializeField] private Button lose1_ReviveAdsBtn;
        [SerializeField] private Button lose1_CloseBtn;

        // ── LOSE — Popup 2 ────────────────────────────────────────────────────
        [Header("─── Lose: Popup 2 (Revive #2) ───────")]
        [SerializeField] private GameObject losePopup2;
        [SerializeField] private TMP_Text lose2_CoinCostText;
        [SerializeField] private Button lose2_ReviveCoinBtn;
        [SerializeField] private Button lose2_ReviveAdsBtn;
        [SerializeField] private Button lose2_CloseBtn;

        // ── LOSE — Popup 3 ────────────────────────────────────────────────────
        [Header("─── Lose: Popup 3 (Final) ─────────")]
        [SerializeField] private GameObject losePopup3;
        [SerializeField] private Button lose3_RetryBtn;
        [SerializeField] private Button lose3_CloseBtn;

        // ── QUIT WARNING ──────────────────────────────────────────────────────
        [Header("─── Quit Warning Popup ───────────────")]
        [Tooltip("Popup cảnh báo thoát. Nằm trong MainCanvas, script tự ReparentToPopupCanvas.")]
        [SerializeField] private GameObject quitWarningPopup;
        [Tooltip("Xác nhận thoát — giống lose3_CloseBtn: trừ HP + CleanupForHome + về Menu")]
        [SerializeField] private Button quitConfirmBtn;
        [Tooltip("Hủy — đóng popup, gọi callback để SettingsUI mở lại settings")]
        [SerializeField] private Button quitCancelBtn;

        // ── Loading overlay ───────────────────────────────────────────────────
        [Header("─── Loading Panel ───────────────────")]
        [SerializeField] private CanvasGroup loadingPanel;
        [SerializeField] private float loadingFadeDuration = 0.35f;

        // ── Revive Dependencies ───────────────────────────────────────────────
        [Header("─── Revive Dependencies ──────────────")]
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
        private System.Action _onQuitCancelled;

        // ═══════════════════════════════════════════════════════════════════════
        // UNITY LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

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
            ReparentToPopupCanvas(quitWarningPopup);

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
            winBtn_NextLevel?.onClick.AddListener(OnClickNextLevel);
            winBtn_DoubleAds?.onClick.AddListener(OnClickDoubleRewardAds);

            lose1_ReviveCoinBtn?.onClick.AddListener(() => OnClickReviveCoin(1));
            lose1_ReviveAdsBtn?.onClick.AddListener(() => OnClickReviveAds(1));
            lose1_CloseBtn?.onClick.AddListener(() => TransitionLosePopup(from: 1, to: 2));

            lose2_ReviveCoinBtn?.onClick.AddListener(() => OnClickReviveCoin(2));
            lose2_ReviveAdsBtn?.onClick.AddListener(() => OnClickReviveAds(2));
            lose2_CloseBtn?.onClick.AddListener(() => TransitionLosePopup(from: 2, to: 3));

            lose3_RetryBtn?.onClick.AddListener(OnClickRetry);
            lose3_CloseBtn?.onClick.AddListener(OnClickCloseToHome);

            quitConfirmBtn?.onClick.AddListener(OnClickQuitConfirm);
            quitCancelBtn?.onClick.AddListener(OnClickQuitCancel);
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
        // QUIT WARNING — PUBLIC API
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gọi từ SettingsUI khi player bấm Quit.
        /// Hiện overlay + QuitWarningPopup (trong PopupCanvas sortingOrder 999)
        /// → tự nhiên render trên 3D objects, không cần ẩn gì thêm.
        /// </summary>
        /// <param name="onCancelled">Callback khi player bấm X/Hủy — SettingsUI mở lại settings.</param>
        public void ShowQuitWarning(System.Action onCancelled)
        {
            _onQuitCancelled = onCancelled;

            if (overlayDim != null)
            {
                overlayDim.gameObject.SetActive(true);
                var c = overlayDim.color; c.a = 0f; overlayDim.color = c;
                overlayDim.DOFade(overlayTargetAlpha, overlayFadeDuration)
                    .SetUpdate(true)
                    .OnComplete(() => AnimatePopupIn(quitWarningPopup));
            }
            else
            {
                AnimatePopupIn(quitWarningPopup);
            }
        }

        // ── Xác nhận thoát — giống hệt OnClickCloseToHome ────────────────────

        private void OnClickQuitConfirm()
        {
            HPManager.Instance?.DeductHPOnQuit();

            HideQuitWarningInstant();
            Time.timeScale = 1f;

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

        // ── Hủy thoát — trả callback về SettingsUI ───────────────────────────

        private void OnClickQuitCancel()
        {
            AnimatePopupOut(quitWarningPopup, () =>
            {
                // Fade out overlay
                if (overlayDim != null)
                {
                    overlayDim.DOFade(0f, overlayFadeDuration)
                        .SetUpdate(true)
                        .OnComplete(() => overlayDim.gameObject.SetActive(false));
                }

                // Báo lại SettingsUI để mở lại settings popup
                _onQuitCancelled?.Invoke();
                _onQuitCancelled = null;
            });
        }

        private void HideQuitWarningInstant()
        {
            if (quitWarningPopup != null)
            {
                quitWarningPopup.transform.DOKill();
                quitWarningPopup.SetActive(false);
            }
            if (overlayDim != null)
            {
                overlayDim.DOKill();
                var c = overlayDim.color; c.a = 0f; overlayDim.color = c;
                overlayDim.gameObject.SetActive(false);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // WIN / LOSE FLOW
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
            if (isWin) { SetupWinPopup(); AnimatePopupIn(popupWin); }
            else { SetupLosePopup1(); AnimatePopupIn(losePopup1); }
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

        // ── Win ───────────────────────────────────────────────────────────────

        private void SetupWinPopup()
        {
            _doubleResolved = false;
            if (CoinManager.Instance == null) return;
            long reward = CoinManager.Instance.GetWinReward();
            float mult = CoinManager.Instance.GetAdsMultiplier();
            long doubled = (long)(reward * mult);
            if (winCoinRewardText) winCoinRewardText.text = $"+{reward}";
            if (winDoubleRewardText) winDoubleRewardText.text = $"+{doubled}";
        }

        private void OnClickDoubleRewardAds()
        {
            if (_doubleResolved) return;
            Debug.Log("[GameResultUI] Double Reward Ads – giả lập.");
            CoinManager.Instance?.DoubleWinReward();
            _doubleResolved = true;
            CleanupAndResume();
            GameManager.Instance?.ChangeState(GameState.Menu);
        }

        private void OnClickNextLevel()
        {
            CleanupAndResume();
            LevelManager.Instance?.LoadNextLevel();
        }

        // ── Lose ──────────────────────────────────────────────────────────────

        private void SetupLosePopup1() => RefreshReviveCoinButton(lose1_ReviveCoinBtn, lose1_CoinCostText);
        private void SetupLosePopup2() => RefreshReviveCoinButton(lose2_ReviveCoinBtn, lose2_CoinCostText);

        private void RefreshReviveCoinButton(Button btn, TMP_Text costText)
        {
            if (CoinManager.Instance == null) return;
            long cost = CoinManager.Instance.GetReviveCost();
            long coins = CoinManager.Instance.CurrentCoins;
            if (costText) costText.text = cost.ToString();
            if (btn) btn.interactable = coins >= cost;
        }

        private void TransitionLosePopup(int from, int to)
        {
            AnimatePopupOut(GetLosePopup(from), () =>
            {
                if (to == 2) SetupLosePopup2();
                AnimatePopupIn(GetLosePopup(to));
            });
        }

        private GameObject GetLosePopup(int index) => index switch
        {
            1 => losePopup1,
            2 => losePopup2,
            3 => losePopup3,
            _ => null
        };

        private void OnClickReviveCoin(int popupIndex)
        {
            if (CoinManager.Instance == null) return;
            if (!CoinManager.Instance.TryRevive()) return;
            ConfirmRevive(popupIndex, byAds: false);
        }

        private void OnClickReviveAds(int popupIndex)
        {
            Debug.Log($"[GameResultUI] Revive Ads popup {popupIndex} – giả lập.");
            ConfirmRevive(popupIndex, byAds: true);
        }

        private void ConfirmRevive(int popupIndex, bool byAds)
        {
            Debug.Log($"[GameResultUI] Revive popup {popupIndex} byAds={byAds}.");
            CleanupAndResume();
            StartCoroutine(ReviveRoutine());
        }

        private IEnumerator ReviveRoutine()
        {
            if (boosterInstaller != null && boosterInstaller.Context != null)
            {
                var context = boosterInstaller.Context;
                var clearTray = new ClearTrayBooster();
                clearTray.Initialize(context);
                if (clearTray.CanExecute())
                {
                    int foodCount = context.BackupTray.OccupiedCount;
                    float waitTime = (foodCount - 1) * 0.08f + 0.4f + 0.15f;
                    clearTray.Execute();
                    yield return new WaitForSeconds(waitTime);
                }
            }
            else
            {
                Debug.LogWarning("[GameResultUI] boosterInstaller chưa gán.");
            }
            GameManager.Instance?.ChangeState(GameState.Play);
        }

        private void OnClickRetry()
        {
            HPManager.Instance?.DeductHPOnQuit();
            CleanupAndResume();
            LevelManager.Instance?.RestartCurrentLevel();
        }

        private void OnClickCloseToHome()
        {
            HPManager.Instance?.DeductHPOnQuit();
            CleanupAndResume();
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
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        public void OnClickGoHome()
        {
            CleanupAndResume();
            GameManager.Instance?.ChangeState(GameState.Menu);
        }

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
            quitWarningPopup?.SetActive(false);

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