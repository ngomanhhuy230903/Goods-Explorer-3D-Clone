using UnityEngine;
using TMPro; // Chú ý: Cần sử dụng TextMeshPro
using FoodMatch.Level;
using FoodMatch.Managers;
using FoodMatch.Core;

namespace FoodMatch.UI
{
    /// <summary>
    /// Cập nhật đồng loạt toàn bộ Text hiển thị Level trên UI dựa theo GameState.
    /// </summary>
    public class LevelUITextController : MonoBehaviour
    {
        [Header(" UI References")]
        [SerializeField] private TextMeshProUGUI homeLevelText;
        [SerializeField] private TextMeshProUGUI inGameLevelText;
        [SerializeField] private TextMeshProUGUI winLevelText;
        [SerializeField] private TextMeshProUGUI loseLevelText;

        [Header(" Settings")]
        [SerializeField] private string prefix = "Level ";

        private void OnEnable()
        {
            GameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void Start()
        {
            // Khởi tạo text ngay khi game mới bật (State = Init/Menu)
            if (GameManager.Instance != null)
            {
                UpdateTexts();
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            // Cập nhật lại Text mỗi khi đổi State (LoadLevel, Play, Win, Lose, v.v.)
            UpdateTexts();
        }

        private void UpdateTexts()
        {
            // 1. Panel Home: Cần hiển thị Level đã lưu (sắp chơi) từ SaveManager
            if (homeLevelText != null)
            {
                homeLevelText.text = $"{prefix}{SaveManager.CurrentLevel}";
            }

            // 2. InGame / Popup Win / Popup Lose: Cần hiển thị Level ĐANG CHƠI từ LevelManager
            // Check null an toàn trong trường hợp LevelManager chưa kịp khởi tạo
            int playingLevel = LevelManager.Instance != null
                ? LevelManager.Instance.CurrentLevelIndex
                : SaveManager.CurrentLevel;

            string playingLevelString = $"{prefix}{playingLevel}";

            if (inGameLevelText != null) inGameLevelText.text = playingLevelString;
            if (winLevelText != null) winLevelText.text = playingLevelString;
            if (loseLevelText != null) loseLevelText.text = playingLevelString;
        }
    }
}

