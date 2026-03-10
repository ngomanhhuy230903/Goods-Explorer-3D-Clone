using UnityEngine;
using TMPro;
using FoodMatch.Level;
using FoodMatch.Managers;
using FoodMatch.Core; // Cần thêm thư viện này để lấy GameManager và GameState

namespace FoodMatch.UI
{
    public class LevelTextDisplay : MonoBehaviour
    {
        public enum DisplayType
        {
            CurrentPlayingLevel,
            SavedLevel
        }

        [Header("─── UI References ─────────────────────")]
        [SerializeField] private TextMeshProUGUI levelText;

        [Header("─── Settings ──────────────────────────")]
        [SerializeField] private DisplayType displayType = DisplayType.CurrentPlayingLevel;
        [SerializeField] private string prefix = "Level ";

        // Đăng ký sự kiện khi UI được bật
        private void OnEnable()
        {
            if (GameManager.Instance != null) // Đảm bảo GameManager tồn tại
            {
                GameManager.OnGameStateChanged += HandleGameStateChanged;
            }
            UpdateLevelText();
        }

        // Hủy đăng ký sự kiện khi UI bị tắt/xóa để tránh lỗi bộ nhớ
        private void OnDisable()
        {
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void Start()
        {
            UpdateLevelText();
        }

        // Tự động được gọi mỗi khi GameState thay đổi
        private void HandleGameStateChanged(GameState state)
        {
            // Cập nhật lại text ngay khi bắt đầu LoadLevel hoặc vào Play
            if (state == GameState.LoadLevel || state == GameState.Play)
            {
                UpdateLevelText();
            }
        }

        public void UpdateLevelText()
        {
            if (levelText == null) return;

            int levelToDisplay = 1;

            if (displayType == DisplayType.CurrentPlayingLevel)
            {
                if (LevelManager.Instance != null)
                {
                    levelToDisplay = LevelManager.Instance.CurrentLevelIndex;
                }
            }
            else if (displayType == DisplayType.SavedLevel)
            {
                levelToDisplay = SaveManager.CurrentLevel;
            }

            levelText.text = prefix + levelToDisplay.ToString();
        }
    }
}