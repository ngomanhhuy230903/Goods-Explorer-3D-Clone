using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using FoodMatch.Core;

namespace FoodMatch.UI
{
    /// <summary>
    /// HIERARCHY BẮT BUỘC:
    ///
    ///   [SettingsManager]          ← Empty GameObject, script SettingsUI.cs nằm ĐÂY
    ///       └─ [SettingsPopup]     ← kéo vào field settingsPopup, SetActive FALSE trong Editor
    ///
    ///   [HUD]
    ///       └─ [BtnSettings]       ← kéo vào field btnSettings
    ///                                 XÓA SẠCH Unity Events trên button này
    ///
    /// QUIT FLOW:
    ///   SettingsUI.OnClickQuit()
    ///     → đóng SettingsPopup
    ///     → gọi GameResultUI.Instance.ShowQuitWarning(onCancelled: mở lại settings)
    ///     → GameResultUI tự lo overlay + popup (trong PopupCanvas sortingOrder 999)
    ///       → render trên 3D objects tự nhiên, không cần ẩn gì
    ///     → Xác nhận: HPDeduct + CleanupForHome → Menu  (xử lý trong GameResultUI)
    ///     → Hủy: callback → SettingsUI mở lại settingsPopup
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Popup — phải SetActive FALSE trong Editor")]
        [SerializeField] private GameObject settingsPopup;

        [Header("Graphics")]
        [SerializeField] private Button btnLow;
        [SerializeField] private Button btnMedium;
        [SerializeField] private Button btnHigh;
        [SerializeField] private Color graphicsActiveColor = new Color(1f, 0.82f, 0.1f);
        [SerializeField] private Color graphicsInactiveColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);

        [Header("Sound FX")]
        [SerializeField] private Slider soundFxSlider;
        [SerializeField] private Image soundFxIcon;
        [SerializeField] private Sprite soundOnSprite;
        [SerializeField] private Sprite soundOffSprite;

        [Header("Music")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Image musicIcon;
        [SerializeField] private Sprite musicOnSprite;
        [SerializeField] private Sprite musicOffSprite;

        [Header("Vibration Toggle")]
        [SerializeField] private Button vibrationToggleBtn;
        [SerializeField] private Image vibrationBgImage;
        [SerializeField] private RectTransform vibrationHandle;
        [SerializeField] private Image vibrationHandleImage;
        [SerializeField] private Sprite toggleBgOnSprite;
        [SerializeField] private Sprite toggleBgOffSprite;
        [SerializeField] private Sprite toggleHandleOnSprite;
        [SerializeField] private Sprite toggleHandleOffSprite;
        [SerializeField] private float handleOnX = 40f;
        [SerializeField] private float handleOffX = -40f;
        [SerializeField] private float toggleAnimDuration = 0.2f;

        [Header("Buttons")]
        [SerializeField] private Button btnResume;
        [SerializeField] private Button btnClose;
        [SerializeField] private Button btnQuit;
        [SerializeField] private Button btnSettings;

        [Header("Animation")]
        [SerializeField] private float panelScaleDuration = 0.35f;
        [SerializeField] private Ease panelOpenEase = Ease.OutBack;
        [SerializeField] private Ease panelCloseEase = Ease.InBack;

        // ─── Keys ─────────────────────────────────────────────────────────────
        private const string KEY_GRAPHICS = "Settings_Graphics";
        private const string KEY_SOUND = "Settings_SoundFx";
        private const string KEY_MUSIC = "Settings_Music";
        private const string KEY_VIBRATION = "Settings_Vibration";

        // ─── Singleton ────────────────────────────────────────────────────────
        public static SettingsUI Instance { get; private set; }

        // ─── Runtime ──────────────────────────────────────────────────────────
        private int _currentGraphics = 1;
        private bool _isOpen = false;
        private bool _vibrationOn = true;
        private GameState _stateBeforeOpen = GameState.None;

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (settingsPopup != null) settingsPopup.SetActive(false);

            LoadSettings();
            BindButtons();
        }

        // ─── Bind ─────────────────────────────────────────────────────────────

        private void BindButtons()
        {
            btnLow?.onClick.AddListener(() => OnGraphicsChanged(0));
            btnMedium?.onClick.AddListener(() => OnGraphicsChanged(1));
            btnHigh?.onClick.AddListener(() => OnGraphicsChanged(2));

            soundFxSlider?.onValueChanged.AddListener(OnSoundFxChanged);
            musicSlider?.onValueChanged.AddListener(OnMusicChanged);
            vibrationToggleBtn?.onClick.AddListener(OnVibrationClicked);

            btnResume?.onClick.AddListener(ResumeGame);
            btnClose?.onClick.AddListener(ResumeGame);
            btnQuit?.onClick.AddListener(OnClickQuit);
            btnSettings?.onClick.AddListener(OnClickSettings);
        }

        // ─── Open / Close ─────────────────────────────────────────────────────

        public void OnClickSettings()
        {
            Debug.Log($"[SettingsUI] OnClickSettings — _isOpen={_isOpen}");
            if (_isOpen) CloseSettings();
            else OpenSettings();
        }

        public void OpenSettings()
        {
            Debug.Log($"[SettingsUI] OpenSettings — _isOpen={_isOpen}");
            if (_isOpen) return;
            _isOpen = true;

            _stateBeforeOpen = GameManager.Instance != null
                ? GameManager.Instance.CurrentState
                : GameState.None;
            bool isInsideGame = (_stateBeforeOpen == GameState.Play || _stateBeforeOpen == GameState.Pause);
            if (btnResume != null) btnResume.gameObject.SetActive(isInsideGame);
            if (btnQuit != null) btnQuit.gameObject.SetActive(isInsideGame);
            if (_stateBeforeOpen == GameState.Play)
            {
                GameManager.Instance.ForceChangeState(GameState.Pause);
                Time.timeScale = 0f;
            }

            settingsPopup.SetActive(true);
            settingsPopup.transform.DOKill();
            settingsPopup.transform.localScale = Vector3.zero;
            settingsPopup.transform
                .DOScale(Vector3.one, panelScaleDuration)
                .SetEase(panelOpenEase)
                .SetUpdate(true);
        }

        public void CloseSettings()
        {
            Debug.Log($"[SettingsUI] CloseSettings — _isOpen={_isOpen}");
            if (!_isOpen) return;
            SaveSettings();
            _isOpen = false;

            settingsPopup.transform.DOKill();
            settingsPopup.transform
                .DOScale(Vector3.zero, panelScaleDuration * 0.8f)
                .SetEase(panelCloseEase)
                .SetUpdate(true)
                .OnComplete(() => settingsPopup.SetActive(false));
        }

        // ─── Resume ───────────────────────────────────────────────────────────

        private void ResumeGame()
        {
            GameState stateToRestore = _stateBeforeOpen;
            CloseSettings();

            if (GameManager.Instance != null && stateToRestore == GameState.Play)
            {
                Time.timeScale = 1f;
                GameManager.Instance.ForceChangeState(GameState.Play);
            }

            _stateBeforeOpen = GameState.None;
            EventBus.RaiseGameResumed();
        }

        // ─── Quit ─────────────────────────────────────────────────────────────

        private void OnClickQuit()
        {
            // Đóng settings popup trước
            // Không gọi CloseSettings() vì cần giữ _isOpen=true và _stateBeforeOpen
            // để nếu player hủy quit thì resume lại đúng state
            SaveSettings();

            settingsPopup.transform.DOKill();
            settingsPopup.transform
                .DOScale(Vector3.zero, panelScaleDuration * 0.8f)
                .SetEase(panelCloseEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    settingsPopup.SetActive(false);

                    // Delegate hoàn toàn cho GameResultUI:
                    // - overlay dim
                    // - popup nằm trong PopupCanvas (sortingOrder 999)
                    //   → tự render trên 3D objects, không cần ẩn gì
                    // - xử lý Confirm (giống lose3_CloseBtn) và Cancel
                    if (GameResultUI.Instance != null)
                    {
                        GameResultUI.Instance.ShowQuitWarning(onCancelled: OnQuitCancelled);
                    }
                    else
                    {
                        // Fallback nếu không có GameResultUI (ví dụ đang ở Home)
                        // Thoát thẳng về Menu không cần cảnh báo
                        _isOpen = false;
                        _stateBeforeOpen = GameState.None;
                        Time.timeScale = 1f;
                        GameManager.Instance?.ChangeState(GameState.Menu);
                    }
                });
        }

        /// <summary>
        /// Callback từ GameResultUI khi player bấm X/Hủy trong QuitWarningPopup.
        /// Mở lại settings popup và resume đúng trạng thái cũ.
        /// </summary>
        private void OnQuitCancelled()
        {
            // _isOpen vẫn còn true từ trước, _stateBeforeOpen vẫn còn — mở lại bình thường
            settingsPopup.SetActive(true);
            settingsPopup.transform.DOKill();
            settingsPopup.transform.localScale = Vector3.zero;
            settingsPopup.transform
                .DOScale(Vector3.one, panelScaleDuration)
                .SetEase(panelOpenEase)
                .SetUpdate(true);
        }

        // ─── Graphics ─────────────────────────────────────────────────────────

        private void OnGraphicsChanged(int level)
        {
            _currentGraphics = level;
            RefreshGraphicsUI(level);
            QualitySettings.SetQualityLevel(level, true);
            SaveSettings();
        }

        private void RefreshGraphicsUI(int activeLevel)
        {
            SetButtonColor(btnLow, activeLevel == 0);
            SetButtonColor(btnMedium, activeLevel == 1);
            SetButtonColor(btnHigh, activeLevel == 2);
        }

        private void SetButtonColor(Button btn, bool isActive)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = isActive ? graphicsActiveColor : graphicsInactiveColor;
            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null) txt.color = isActive ? Color.white : new Color(0.8f, 0.8f, 0.8f, 0.6f);
            var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null) tmp.color = isActive ? Color.white : new Color(0.8f, 0.8f, 0.8f, 0.6f);
        }

        // ─── Sound / Music ────────────────────────────────────────────────────

        private void OnSoundFxChanged(float value)
        {
            if (soundFxIcon != null)
                soundFxIcon.sprite = value <= 0f ? soundOffSprite : soundOnSprite;
            ApplyAudioVolumes(value, musicSlider != null ? musicSlider.value : 1f);
            SaveSettings();
        }

        private void OnMusicChanged(float value)
        {
            if (musicIcon != null)
                musicIcon.sprite = value <= 0f ? musicOffSprite : musicOnSprite;
            ApplyAudioVolumes(soundFxSlider != null ? soundFxSlider.value : 1f, value);
            SaveSettings();
        }

        private void RefreshSoundIcon(float v)
        {
            if (soundFxIcon != null) soundFxIcon.sprite = v <= 0f ? soundOffSprite : soundOnSprite;
        }

        private void RefreshMusicIcon(float v)
        {
            if (musicIcon != null) musicIcon.sprite = v <= 0f ? musicOffSprite : musicOnSprite;
        }

        private void ApplyAudioVolumes(float sfx, float music)
        {
            // audioMixer.SetFloat("SFX_Volume",   Mathf.Log10(Mathf.Max(sfx,   0.0001f)) * 20f);
            // audioMixer.SetFloat("Music_Volume", Mathf.Log10(Mathf.Max(music, 0.0001f)) * 20f);
            // SoundManager.Instance?.SetSFXVolume(sfx);
            // SoundManager.Instance?.SetMusicVolume(music);
            AudioListener.volume = sfx;
        }

        // ─── Vibration ────────────────────────────────────────────────────────

        private void OnVibrationClicked()
        {
            _vibrationOn = !_vibrationOn;
            RefreshVibrationToggle(instant: false);
            SaveSettings();
        }

        private void RefreshVibrationToggle(bool instant)
        {
            float targetX = _vibrationOn ? handleOnX : handleOffX;

            if (vibrationBgImage != null) vibrationBgImage.sprite = _vibrationOn ? toggleBgOnSprite : toggleBgOffSprite;
            if (vibrationHandleImage != null) vibrationHandleImage.sprite = _vibrationOn ? toggleHandleOnSprite : toggleHandleOffSprite;
            if (vibrationHandle == null) return;

            if (vibrationToggleBtn != null &&
                vibrationHandle.gameObject == vibrationToggleBtn.gameObject)
            {
                Debug.LogWarning("[SettingsUI] vibrationHandle trỏ vào Toggle cha!");
                return;
            }

            vibrationHandle.DOKill();
            if (instant)
                vibrationHandle.anchoredPosition = new Vector2(targetX, vibrationHandle.anchoredPosition.y);
            else
                vibrationHandle.DOAnchorPosX(targetX, toggleAnimDuration).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        // ─── Load / Save ──────────────────────────────────────────────────────

        private void LoadSettings()
        {
            _currentGraphics = PlayerPrefs.GetInt(KEY_GRAPHICS, 1);
            float soundVal = PlayerPrefs.GetFloat(KEY_SOUND, 0.7f);
            float musicVal = PlayerPrefs.GetFloat(KEY_MUSIC, 0.5f);
            _vibrationOn = PlayerPrefs.GetInt(KEY_VIBRATION, 1) == 1;

            if (soundFxSlider != null) soundFxSlider.value = soundVal;
            if (musicSlider != null) musicSlider.value = musicVal;

            RefreshVibrationToggle(instant: true);
            RefreshGraphicsUI(_currentGraphics);
            RefreshSoundIcon(soundVal);
            RefreshMusicIcon(musicVal);
            ApplyAudioVolumes(soundVal, musicVal);
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetInt(KEY_GRAPHICS, _currentGraphics);
            PlayerPrefs.SetFloat(KEY_SOUND, soundFxSlider != null ? soundFxSlider.value : 1f);
            PlayerPrefs.SetFloat(KEY_MUSIC, musicSlider != null ? musicSlider.value : 1f);
            PlayerPrefs.SetInt(KEY_VIBRATION, _vibrationOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool IsVibrationEnabled() =>
            PlayerPrefs.GetInt(KEY_VIBRATION, 1) == 1;

        // ─── Cleanup ──────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            if (settingsPopup != null) settingsPopup.transform.DOKill();
        }
    }
}