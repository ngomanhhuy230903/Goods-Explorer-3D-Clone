// ObstaclePreset.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Data
{
    /// <summary>
    /// Template obstacle tái sử dụng được.
    /// Tạo asset: Create > FoodMatch > Obstacle Preset
    /// VD: "Preset_EasyLock", "Preset_HardTubes", "Preset_FullCombo"
    /// </summary>
    [CreateAssetMenu(
        fileName = "ObstaclePreset_",
        menuName = "FoodMatch/Obstacle Preset",
        order = 3)]
    public class ObstaclePreset : ScriptableObject
    {
        [TextArea(1, 2)]
        [Tooltip("Mô tả preset này dùng cho loại level nào.")]
        public string description;

        [SerializeReference]
        public List<ObstacleData> obstacles = new List<ObstacleData>();

        /// <summary>
        /// Deep clone toàn bộ obstacles — dùng khi apply vào LevelConfig.
        /// Đảm bảo LevelConfig không share reference với preset.
        /// </summary>
        public List<ObstacleData> CloneObstacles()
            => obstacles?.Select(o => o?.Clone()).Where(o => o != null).ToList()
               ?? new List<ObstacleData>();
    }
}