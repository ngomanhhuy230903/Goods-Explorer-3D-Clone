using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using FoodMatch.Level;

namespace FoodMatch.UI
{
    /// <summary>
    /// Gắn vào GameObject "GameResultUI" — con của MainCanvas.
    ///
    /// FIX "Screen Space - Camera" bị 3D object che popup:
    ///   Tự động tạo "PopupCanvas" riêng với Render Mode = Screen Space - Overlay
    ///   và Sort Order = 999. PopupWin/PopupLose/OverlayDim được reparent vào đây
    ///   lúc runtime → luôn render TRÊN mọi thứ bao gồm cả object 3D.
    ///
    /// FIX ẩn 3D objects:
    ///   Gán foodGridObject trong Inspector → tự ẩn khi Win/Lose.
    /// </summary>
    public class GameResultUI : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Overlay ─────────────────────────")]
        [SerializeField] private Image overlayDim;
        [SerializeField][Range(0f, 1f)] private float overlayTargetAlpha = 0.6f;
        [SerializeField] private float overlayFadeDuration = 0.35f;

        [Header("─── Popups ───────────────────────────")]
        [SerializeField] private GameObject popupLose;
        [SerializeField] private GameObject popupWin;

        [Header("─── Buttons ─────────────────────────")]
        [SerializeField] private Button loseBtn_GoHome;
        [SerializeField] private Button loseBtn_TryAgain;
        [SerializeField] private Button winBtn_GoHome;

        [Header("─── 3D Objects cần ẩn khi Win/Lose ───")]
        [Tooltip("Kéo FoodGridSpawner GameObject vào đây.")]
        [SerializeField] private GameObject foodGridObject;
        [SerializeField] private GameObject[] extraObjectsToHide;

        [Header("─── Animation ────────────────────────")]
        [SerializeField] private float popupScaleDuration = 0.45f;
        [SerializeField] private Ease popupScaleEase = Ease.OutBack;
        [SerializeField] private float popupDelay = 0.15f;

        // ─── Runtime ──────────────────────────────────────────────────────────
        private bool _isShowing = false;
        private Canvas _popupCanvas = null; // Canvas Overlay riêng cho popup

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            BuildPopupCanvas();
            HideAll();
            BindButtons();
        }

        private void OnEnable() => GameManager.OnGameStateChanged += HandleStateChanged;
        private void OnDisable() => GameManager.OnGameStateChanged -= HandleStateChanged;

        // ─── Tạo Canvas Overlay riêng ─────────────────────────────────────────

        /// <summary>
        /// Tạo 1 Canvas "Screen Space - Overlay" độc lập với Sort Order = 999.
        /// Reparent OverlayDim, PopupLose, PopupWin vào Canvas này.
        /// → Luôn render trên 3D objects dù MainCanvas là Screen Space - Camera.
        /// </summary>
        private void BuildPopupCanvas()
        {
            // Tạo GameObject PopupCanvas ở root Scene (không phải con của MainCanvas)
            var go = new GameObject("PopupCanvas");
            DontDestroyOnLoad(go); // Tồn tại qua scene nếu cần

            _popupCanvas = go.AddComponent<Canvas>();
            _popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _popupCanvas.sortingOrder = 999; // Cao nhất, trên mọi thứ

            // CanvasScaler để UI scale đúng trên mọi độ phân giải
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // Chuẩn mobile portrait
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            // Reparent các popup và overlay vào PopupCanvas
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

            // Lưu anchor/size trước khi reparent
            Vector2 anchorMin = rt.anchorMin;
            Vector2 anchorMax = rt.anchorMax;
            Vector2 offsetMin = rt.offsetMin;
            Vector2 offsetMax = rt.offsetMax;
            Vector2 anchoredPos = rt.anchoredPosition;
            Vector2 sizeDelta = rt.sizeDelta;
            Vector3 localScale = rt.localScale;

            // Reparent
            rt.SetParent(_popupCanvas.transform, false);

            // Restore transform (SetParent với worldPositionStays=false reset anchor)
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

        // ─── Ẩn 3D Objects ───────────────────────────────────────────────────

        private void SetGameObjectsVisible(bool visible)
        {
            if (foodGridObject != null)
                foodGridObject.SetActive(visible);

            if (extraObjectsToHide == null) return;
            foreach (var obj in extraObjectsToHide)
                if (obj != null) obj.SetActive(visible);
        }

        // ─── Button Binding ───────────────────────────────────────────────────

        private void BindButtons()
        {
            loseBtn_GoHome?.onClick.AddListener(OnClickGoHome);
            loseBtn_TryAgain?.onClick.AddListener(OnClickTryAgain);
            winBtn_GoHome?.onClick.AddListener(OnClickGoHome);
        }

        // ─── Core Flow ────────────────────────────────────────────────────────

        private void ShowResult(bool isWin)
        {
            if (_isShowing) return;
            _isShowing = true;

            Time.timeScale = 0f;

            if (overlayDim != null)
            {
                overlayDim.gameObject.SetActive(true);
                var c = overlayDim.color;
                c.a = 0f;
                overlayDim.color = c;

                overlayDim
                    .DOFade(overlayTargetAlpha, overlayFadeDuration)
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

        // ─── Button Callbacks ─────────────────────────────────────────────────

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

        // ─── Cleanup ──────────────────────────────────────────────────────────

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

            if (overlayDim == null) return;
            overlayDim.DOKill();
            var c = overlayDim.color;
            c.a = 0f;
            overlayDim.color = c;
            overlayDim.gameObject.SetActive(false);
        }

        private void ForceHideAll()
        {
            Time.timeScale = 1f;
            _isShowing = false;
            HideAll();
        }

        // ─── Cleanup khi destroy ──────────────────────────────────────────────

        private void OnDestroy()
        {
            if (_popupCanvas != null)
                Destroy(_popupCanvas.gameObject);
        }
    }
}