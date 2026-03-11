using System.Collections.Generic;
using UnityEngine;

namespace FoodMatch.Items
{
    /// <summary>
    /// Database SO chứa toàn bộ BoosterData.
    /// LevelManager/BoosterManager tra cứu ở đây.
    /// Thêm booster mới: tạo file C# + [Booster] + tạo SO BoosterData + kéo vào database.
    /// </summary>
    [CreateAssetMenu(fileName = "BoosterDatabase", menuName = "FoodMatch/Booster Database")]
    public class BoosterDatabase : ScriptableObject
    {
        [SerializeField] private List<BoosterData> boosters = new();

        public IReadOnlyList<BoosterData> Boosters => boosters;

        /// <summary>Tra cứu nhanh theo boosterName (khớp với IBooster.BoosterName).</summary>
        public BoosterData GetByName(string boosterName) =>
            boosters.Find(b => b.boosterName == boosterName);

        /// <summary>Tất cả booster đã unlock theo level hiện tại.</summary>
        public List<BoosterData> GetUnlocked(int currentLevel)
        {
            var result = new List<BoosterData>();
            foreach (var b in boosters)
                if (b.IsUnlocked(currentLevel))
                    result.Add(b);
            return result;
        }

        /// <summary>Booster vừa được mở khóa đúng tại level này.</summary>
        public List<BoosterData> GetNewlyUnlocked(int level)
        {
            var result = new List<BoosterData>();
            foreach (var b in boosters)
                if (b.requiredLevel == level)
                    result.Add(b);
            return result;
        }

#if UNITY_EDITOR
        [ContextMenu("Sort by Required Level")]
        private void Sort()
        {
            boosters.Sort((a, b) => a.requiredLevel.CompareTo(b.requiredLevel));
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ContextMenu("Validate: Check boosterName duplicates")]
        private void Validate()
        {
            var seen = new HashSet<string>();
            foreach (var b in boosters)
            {
                if (string.IsNullOrEmpty(b.boosterName))
                    Debug.LogError($"[BoosterDatabase] '{b.name}' chưa điền boosterName!");
                else if (!seen.Add(b.boosterName))
                    Debug.LogError($"[BoosterDatabase] Trùng boosterName: '{b.boosterName}'");
            }
            Debug.Log("[BoosterDatabase] Validate xong.");
        }
#endif
    }
}