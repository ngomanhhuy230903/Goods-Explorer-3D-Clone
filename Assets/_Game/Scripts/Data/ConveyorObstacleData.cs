// ConveyorObstacleData.cs
using System;
using UnityEngine;

namespace FoodMatch.Data
{
    /// <summary>
    /// Obstacle 3: Băng chuyền (Conveyor Belt).
    /// - Food chạy từ trái → phải, loop lại
    /// - Chỉ cần bật/tắt và set số lượng food
    /// </summary>
    [Serializable]
    public class ConveyorObstacleData : ObstacleData
    {
        public override string ObstacleName => "Conveyor Belt";

        [Header("Conveyor Settings")]
        [Tooltip("Số lượng food xuất hiện trên băng chuyền.")]
        [Min(1)]
        public int foodCount = 6;

        [Tooltip("Tốc độ di chuyển của băng chuyền (units/second).")]
        [Min(0.1f)]
        public float speed = 2f;

        public override ObstacleData Clone() => new ConveyorObstacleData
        {
            isEnabled = isEnabled,
            foodCount = foodCount,
            speed = speed
        };
    }
}