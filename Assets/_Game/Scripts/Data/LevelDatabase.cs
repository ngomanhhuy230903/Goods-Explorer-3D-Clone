using System.Collections.Generic;
using UnityEngine;

namespace FoodMatch.Data
{
    /// <summary>
    /// ScriptableObject trung tâm chứa TOÀN BỘ levels.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LevelDatabase",
        menuName = "FoodMatch/Level Database",
        order = 0)]
    public class LevelDatabase : ScriptableObject
    {
        [Header("─── All Levels ──────────────────────")]
        [Tooltip("Kéo thả toàn bộ LevelConfig SO vào đây theo thứ tự.")]
        public List<LevelConfig> levels = new List<LevelConfig>();

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Lấy LevelConfig theo levelIndex (1-based).
        /// Trả về null nếu không tìm thấy.
        /// </summary>
        public LevelConfig GetLevel(int levelIndex)
        {
            // levelIndex là 1-based, list là 0-based
            int listIndex = levelIndex - 1;

            if (listIndex < 0 || listIndex >= levels.Count)
            {
                Debug.LogError($"[LevelDatabase] Không tìm thấy level {levelIndex}. " +
                               $"Hiện có {levels.Count} levels.");
                return null;
            }

            return levels[listIndex];
        }

        /// <summary>
        /// Tổng số level hiện có trong database.
        /// </summary>
        public int TotalLevels => levels.Count;

        /// <summary>
        /// Kiểm tra xem levelIndex có hợp lệ không.
        /// </summary>
        public bool IsValidLevel(int levelIndex)
        {
            return levelIndex >= 1 && levelIndex <= levels.Count;
        }

        /// <summary>
        /// Trả về level tiếp theo sau levelIndex hiện tại.
        /// Trả về null nếu đây là level cuối.
        /// </summary>
        public LevelConfig GetNextLevel(int currentLevelIndex)
        {
            int next = currentLevelIndex + 1;
            return IsValidLevel(next) ? GetLevel(next) : null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// [Editor Only] Tự động sort lại list theo levelIndex.
        /// </summary>
        [ContextMenu("Sort Levels By Index")]
        private void SortLevels()
        {
            levels.Sort((a, b) => a.levelIndex.CompareTo(b.levelIndex));
            Debug.Log("[LevelDatabase] Đã sort xong các level theo levelIndex.");
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// [Editor Only] Log ra danh sách tất cả levels để kiểm tra.
        /// </summary>
        [ContextMenu("Print All Levels Info")]
        private void PrintAllLevels()
        {
            Debug.Log($"[LevelDatabase] Tổng cộng {levels.Count} levels:");
            foreach (var lvl in levels)
            {
                if (lvl == null)
                {
                    Debug.LogWarning("  → Có 1 slot NULL trong danh sách!");
                    continue;
                }
                Debug.Log($"  Level {lvl.levelIndex:D2}: " +
                          $"{lvl.GetDisplayName()} | " +
                          $"Foods: {lvl.totalFoodCount} | " +
                          $"Layers: {lvl.layerCount} | " +
                          $"Customers: {lvl.maxActiveOrders}");
            }
        }
#endif
    }
}