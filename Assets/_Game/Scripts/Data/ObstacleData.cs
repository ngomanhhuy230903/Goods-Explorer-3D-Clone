// ObstacleData.cs
using System;
using UnityEngine;

namespace FoodMatch.Data
{
    /// <summary>
    /// Base class cho tất cả obstacle data.
    /// Thêm obstacle mới: tạo class kế thừa ObstacleData.
    /// </summary>
    [Serializable]
    public abstract class ObstacleData
    {
        [Tooltip("Bật/tắt obstacle này trong level.")]
        public bool isEnabled = true;

        /// <summary>Validate data trước khi load level.</summary>
        public virtual bool IsValid() => true;
        public abstract string ObstacleName { get; }
        /// <summary>Tên obstacle để debug/editor.</summary>
        /// <summary>
        /// Deep copy toàn bộ data — dùng khi Apply Preset vào LevelConfig
        /// để tránh nhiều LevelConfig share cùng 1 reference.
        /// </summary>
        public abstract ObstacleData Clone();
    }
}