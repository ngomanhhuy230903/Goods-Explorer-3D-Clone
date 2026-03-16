using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using FoodMatch.Core;

namespace FoodMatch.UI
{
    /// <summary>
    /// Gắn vào GameObject "SettingsUI" — popup cài đặt trong game.
    /// Quản lý: Graphics quality, Sound FX slider, Music slider, Vibration toggle,
    /// Quit (hiện popup cảnh báo), Resume / X (tiếp tục game).
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("─── Panel Root ──────────────────────────")]
        [SerializeField] private GameObject settingsPanel;

        // CanvasGroup tự động tạo — dùng để tắt blocksRaycasts ngay khi đóng
        // thay vì đợi tween scale về 0 xong mới SetActive(false)
        private CanvasGroup _panelCanvasGroup;

        [Header("─── Graphics Buttons ─────────────────────")]
        [SerializeField] private Button btnLow;
        [SerializeField] private Button btnMedium;
        [SerializeField] private Button btnHigh;

        [Tooltip("Màu nền khi button đang được chọn (vàng)")]
        [SerializeField] private Color graphicsActiveColor = new Color(1f, 0.82f, 0.1f);
        [Tooltip("Màu nền khi button không được chọn (xám mờ)")]
        [SerializeField] private Color graphicsInactiveColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);

        [Header("─── Sound FX ─────────────────────────────")]
        [SerializeField] private Slider soundFxSlider;
        [SerializeField] private Image soundFxIcon;
        [SerializeField] private Sprite soundOnSprite;
        [SerializeField] private Sprite soundOffSprite;

        [Header("─── Music ────────────────────────────────")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Image musicIcon;
        [SerializeField] private Sprite musicOnSprite;
        [SerializeField] private Sprite musicOffSprite;

        [Header("─── Vibration Custom Toggle ──────────────")]
        [Tooltip("GameObject cha của toggle (có Button component + Image background)")]
        [SerializeField] private Button vibrationToggleBtn;

        [Tooltip("Image của background toggle (cái thanh dài)")]
        [SerializeField] private Image vibrationBgImage;

        [Tooltip("RectTransform của handle (cái nút tròn)")]
        [SerializeField] private RectTransform vibrationHandle;

        [Tooltip("Image của handle (cái nút tròn)")]
        [SerializeField] private Image vibrationHandleImage;

        [Tooltip("Sprite background khi BẬT (màu cam/vàng)")]
        [SerializeField] private Sprite toggleBgOnSprite;

        [Tooltip("Sprite background khi TẮT (màu xám)")]
        [SerializeField] private Sprite toggleBgOffSprite;

        [Tooltip("Sprite handle khi BẬT")]
        [SerializeField] private Sprite toggleHandleOnSprite;

        [Tooltip("Sprite handle khi TẮT")]
        [SerializeField] private Sprite toggleHandleOffSprite;

        [Tooltip("Vị trí anchoredPosition X của handle khi BẬT (bên phải)")]
        [SerializeField] private float handleOnX = 40f;

        [Tooltip("Vị trí anchoredPosition X của handle khi TẮT (bên trái)")]
        [SerializeField] private float handleOffX = -40f;

        [SerializeField] private float toggleAnimDuration = 0.2f;

        // Runtime state
        private bool _vibrationOn = true;

        [Header("─── Buttons ─────────────────────────────")]
        [SerializeField] private Button btnResume;
        [SerializeField] private Button btnClose;   // dấu X
        [SerializeField] private Button btnQuit;
        [SerializeField] private Button btnSettings; // nút mở Settings (ở HUD)

        [Header("─── Quit Warning Popup ──────────────────")]
        [Tooltip("Popup cảnh báo thoát sẽ mất HP")]
        [SerializeField] private GameObject quitWarningPopup;

        [Header("─── Animation ────────────────────────────")]
        [SerializeField] private float panelScaleDuration = 0.35f;
        [SerializeField] private Ease panelOpenEase = Ease.OutBack;
        [SerializeField] private Ease panelCloseEase = Ease.InBack;

        // ─── Const Keys (PlayerPrefs) ─────────────────────────────────────────
        private const string KEY_GRAPHICS = "Settings_Graphics";   // 0=Low,1=Med,2=High
        private const string KEY_SOUND = "Settings_SoundFx";
        private const string KEY_MUSIC = "Settings_Music";
        private const string KEY_VIBRATION = "Settings_Vibration";

        // ─── Singleton ────────────────────────────────────────────────────────
        public static SettingsUI Instance { get; private set; }

        // ─── Runtime ──────────────────────────────────────────────────────────
        private int _currentGraphics = 1; // default Medium
        private bool _isOpen = false;
        private GameState _stateBeforeOpen = GameState.None; // state lúc mở settings

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Tự động thêm CanvasGroup vào settingsPanel nếu chưa có
            // CanvasGroup cho phép tắt blocksRaycasts ngay lập tức khi đóng panel
            // mà không cần đợi tween scale xong mới SetActive(false)
            if (settingsPanel != null)
            {
                _panelCanvasGroup = settingsPanel.GetComponent<CanvasGroup>();
                if (_panelCanvasGroup == null)
                    _panelCanvasGroup = settingsPanel.AddComponent<CanvasGroup>();
            }

            LoadSettings();
            BindButtons();
            settingsPanel?.SetActive(false);
            quitWarningPopup?.SetActive(false);
        }

        // ─── Load / Save ──────────────────────────────────────────────────────

        private void LoadSettings()
        {
            _currentGraphics = PlayerPrefs.GetInt(KEY_GRAPHICS, 1);

            float soundVal = PlayerPrefs.GetFloat(KEY_SOUND, 0.7f);
            float musicVal = PlayerPrefs.GetFloat(KEY_MUSIC, 0.5f);
            bool vibrationOn = PlayerPrefs.GetInt(KEY_VIBRATION, 1) == 1;

            if (soundFxSlider != null) soundFxSlider.value = soundVal;
            if (musicSlider != null) musicSlider.value = musicVal;

            _vibrationOn = vibrationOn;
            RefreshVibrationToggle(instant: true); // set ngay không animation

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

        private void OnClickSettings()
        {
            if (_isOpen)
            {
                CloseSettings();
            }
            else
            {
                OpenSettings();
            }
        }

        public void OpenSettings()
        {
            if (_isOpen) return;
            _isOpen = true;

            _stateBeforeOpen = GameManager.Instance != null
                ? GameManager.Instance.CurrentState
                : GameState.None;

            if (_stateBeforeOpen == GameState.Play)
            {
                GameManager.Instance.ChangeState(GameState.Pause);
                Time.timeScale = 0f;
            }

            settingsPanel?.SetActive(true);

            // Bật lại raycast ngay khi mở
            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.blocksRaycasts = true;
                _panelCanvasGroup.interactable = true;
            }

            if (settingsPanel != null)
            {
                settingsPanel.transform.localScale = Vector3.zero;
                settingsPanel.transform
                    .DOScale(Vector3.one, panelScaleDuration)
                    .SetEase(panelOpenEase)
                    .SetUpdate(true);
            }

            EventBus.RaiseGamePaused();
        }

        public void CloseSettings()
        {
            if (!_isOpen) return;

            SaveSettings();
            _isOpen = false;

            // Tắt raycast NGAY LẬP TỨC — không đợi tween xong
            // Đây là fix chính: panel vẫn visible trong lúc scale về 0
            // nhưng không block click nữa
            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.blocksRaycasts = false;
                _panelCanvasGroup.interactable = false;
            }

            if (settingsPanel != null)
            {
                settingsPanel.transform.DOKill();
                settingsPanel.transform
                    .DOScale(Vector3.zero, panelScaleDuration * 0.8f)
                    .SetEase(panelCloseEase)
                    .SetUpdate(true)
                    .OnComplete(() => settingsPanel.SetActive(false));
            }
        }

        // ─── Resume ───────────────────────────────────────────────────────────

        private void ResumeGame()
        {
            CloseSettings();

            // Trả về đúng state lúc mở settings, không hardcode
            if (GameManager.Instance != null)
            {
                // Chỉ restore timeScale và state nếu trước đó đang Play (đã bị pause)
                if (_stateBeforeOpen == GameState.Play)
                {
                    Time.timeScale = 1f;
                    GameManager.Instance.ChangeState(GameState.Play);
                }
                // Các state khác (Menu, Win, Lose…) không cần làm gì thêm
            }

            _stateBeforeOpen = GameState.None;
            EventBus.RaiseGameResumed();
        }

        // ─── Quit ─────────────────────────────────────────────────────────────

        private void OnClickQuit()
        {
            // Ẩn settings panel, hiện popup cảnh báo
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (quitWarningPopup != null)
            {
                quitWarningPopup.SetActive(true);
                quitWarningPopup.transform.localScale = Vector3.zero;
                quitWarningPopup.transform
                    .DOScale(Vector3.one, panelScaleDuration)
                    .SetEase(panelOpenEase)
                    .SetUpdate(true);
            }
        }

        /// <summary>
        /// Gọi từ QuitWarningPopup → nút "Xác nhận thoát".
        /// Sẽ mất HP, sau đó về Menu.
        /// </summary>
        public void ConfirmQuit()
        {
            quitWarningPopup?.SetActive(false);
            _isOpen = false;
            _stateBeforeOpen = GameState.None;
            Time.timeScale = 1f;

            EventBus.RaiseHPChanged(0, 0); // placeholder — thay bằng logic thực tế
            GameManager.Instance?.ChangeState(GameState.Menu);
        }

        /// <summary>
        /// Gọi từ QuitWarningPopup → nút "Hủy".
        /// Quay lại settings.
        /// </summary>
        public void CancelQuit()
        {
            quitWarningPopup?.SetActive(false);
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
                settingsPanel.transform.localScale = Vector3.zero;
                settingsPanel.transform
                    .DOScale(Vector3.one, panelScaleDuration)
                    .SetEase(panelOpenEase)
                    .SetUpdate(true);
            }
        }

        // ─── Graphics ─────────────────────────────────────────────────────────

        private void OnGraphicsChanged(int level)
        {
            _currentGraphics = level;
            RefreshGraphicsUI(level);
            ApplyGraphicsQuality(level);
            SaveSettings();
        }

        /// <summary>Button được chọn sáng vàng; còn lại xám mờ.</summary>
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
            if (img == null) return;
            img.color = isActive ? graphicsActiveColor : graphicsInactiveColor;

            // Text cũng làm tối nếu inactive
            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null) txt.color = isActive ? Color.white : new Color(0.8f, 0.8f, 0.8f, 0.6f);

            var tmpTxt = btn.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmpTxt != null) tmpTxt.color = isActive ? Color.white : new Color(0.8f, 0.8f, 0.8f, 0.6f);
        }

        private void ApplyGraphicsQuality(int level)
        {
            // Unity Quality Settings: index 0=Low, 1=Medium, 2=High (tuỳ setup project)
            QualitySettings.SetQualityLevel(level, true);
        }

        // ─── Sound FX ─────────────────────────────────────────────────────────

        private void OnSoundFxChanged(float value)
        {
            RefreshSoundIcon(value);
            ApplyAudioVolumes(value, musicSlider != null ? musicSlider.value : 1f);
            SaveSettings();
        }

        private void RefreshSoundIcon(float value)
        {
            if (soundFxIcon == null) return;
            soundFxIcon.sprite = value <= 0f ? soundOffSprite : soundOnSprite;
        }

        // ─── Music ────────────────────────────────────────────────────────────

        private void OnMusicChanged(float value)
        {
            RefreshMusicIcon(value);
            ApplyAudioVolumes(soundFxSlider != null ? soundFxSlider.value : 1f, value);
            SaveSettings();
        }

        private void RefreshMusicIcon(float value)
        {
            if (musicIcon == null) return;
            musicIcon.sprite = value <= 0f ? musicOffSprite : musicOnSprite;
        }

        // ─── Apply Audio ──────────────────────────────────────────────────────

        /// <summary>
        /// Áp dụng volume vào AudioMixer hoặc AudioListener.
        /// Nếu bạn dùng AudioMixer, thay thế phần này cho phù hợp.
        /// </summary>
        private void ApplyAudioVolumes(float sfxVolume, float musicVolume)
        {
            // Ví dụ dùng AudioMixer (uncomment nếu có):
            // audioMixer.SetFloat("SFX_Volume", Mathf.Log10(Mathf.Max(sfxVolume, 0.0001f)) * 20f);
            // audioMixer.SetFloat("Music_Volume", Mathf.Log10(Mathf.Max(musicVolume, 0.0001f)) * 20f);

            // Placeholder: gọi SoundManager/AudioManager nếu có
            // SoundManager.Instance?.SetSFXVolume(sfxVolume);
            // SoundManager.Instance?.SetMusicVolume(musicVolume);

            AudioListener.volume = sfxVolume; // fallback đơn giản
        }

        // ─── Vibration Custom Toggle ──────────────────────────────────────────

        private void OnVibrationClicked()
        {
            _vibrationOn = !_vibrationOn;
            RefreshVibrationToggle(instant: false);
            SaveSettings();
        }

        /// <summary>
        /// Cập nhật visual toggle: handle trượt trái/phải, đổi sprite bg và handle.
        /// </summary>
        /// <param name="instant">true = set ngay (không animation), dùng khi load lần đầu</param>
        private void RefreshVibrationToggle(bool instant)
        {
            float targetX = _vibrationOn ? handleOnX : handleOffX;

            // Đổi sprite background
            if (vibrationBgImage != null)
                vibrationBgImage.sprite = _vibrationOn ? toggleBgOnSprite : toggleBgOffSprite;

            // Đổi sprite handle
            if (vibrationHandleImage != null)
                vibrationHandleImage.sprite = _vibrationOn ? toggleHandleOnSprite : toggleHandleOffSprite;

            if (vibrationHandle == null) return;

            // Guard: handle phải là GameObject CON, không phải toggle cha
            if (vibrationToggleBtn != null &&
                vibrationHandle.gameObject == vibrationToggleBtn.gameObject)
            {
                Debug.LogWarning("[SettingsUI] vibrationHandle đang trỏ vào Toggle cha! " +
                                 "Hãy kéo Handle (GameObject con nút tròn) vào field này.");
                return;
            }

            // Kill tween cũ trước khi tạo tween mới
            vibrationHandle.DOKill();

            if (instant)
            {
                // Chỉ đổi X, giữ nguyên Y để handle không nhảy
                vibrationHandle.anchoredPosition =
                    new Vector2(targetX, vibrationHandle.anchoredPosition.y);
            }
            else
            {
                vibrationHandle
                    .DOAnchorPosX(targetX, toggleAnimDuration)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true);
            }
        }

        /// <summary>Truy vấn trạng thái vibration từ bên ngoài.</summary>
        public static bool IsVibrationEnabled()
        {
            return PlayerPrefs.GetInt(KEY_VIBRATION, 1) == 1;
        }

        // ─── Cleanup ──────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            DOTween.Kill(settingsPanel?.transform);
            DOTween.Kill(quitWarningPopup?.transform);
        }
    }
}