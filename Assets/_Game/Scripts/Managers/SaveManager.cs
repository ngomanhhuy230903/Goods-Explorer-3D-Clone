using UnityEngine;
using FoodMatch.Core;

namespace FoodMatch.Managers
{
    /// <summary>
    /// Quản lý lưu/đọc dữ liệu người chơi qua PlayerPrefs.
    /// </summary>
    public static class SaveManager
    {
        // ─── Level Progress ───────────────────────────────────────────────────

        /// <summary>Level hiện tại người chơi đang ở (1-based).</summary>
        public static int CurrentLevel
        {
            get => PlayerPrefs.GetInt(GameConstants.PREF_CURRENT_LEVEL, 1);
            set
            {
                PlayerPrefs.SetInt(GameConstants.PREF_CURRENT_LEVEL, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Mở khóa level tiếp theo nếu vượt qua level hiện tại.</summary>
        public static void UnlockNextLevel(int completedLevel)
        {
            int unlocked = PlayerPrefs.GetInt(GameConstants.PREF_CURRENT_LEVEL, 1);
            if (completedLevel >= unlocked)
            {
                CurrentLevel = completedLevel + 1;
                Debug.Log($"[SaveManager] Đã mở khóa Level {completedLevel + 1}");
            }
        }
        // ─── Settings ─────────────────────────────────────────────────────────

        public static bool IsSoundOn
        {
            get => PlayerPrefs.GetInt(GameConstants.PREF_SOUND_ON, 1) == 1;
            set => PlayerPrefs.SetInt(GameConstants.PREF_SOUND_ON, value ? 1 : 0);
        }

        public static bool IsMusicOn
        {
            get => PlayerPrefs.GetInt(GameConstants.PREF_MUSIC_ON, 1) == 1;
            set => PlayerPrefs.SetInt(GameConstants.PREF_MUSIC_ON, value ? 1 : 0);
        }

        public static bool IsVibrationOn
        {
            get => PlayerPrefs.GetInt(GameConstants.PREF_VIBRATION_ON, 1) == 1;
            set => PlayerPrefs.SetInt(GameConstants.PREF_VIBRATION_ON, value ? 1 : 0);
        }

        // ─── Utility ──────────────────────────────────────────────────────────

        /// <summary>
        /// Xóa toàn bộ dữ liệu (dùng khi debug hoặc Reset game).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void ResetAllData()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.LogWarning("[SaveManager] Đã xóa toàn bộ save data!");
        }
    }
}