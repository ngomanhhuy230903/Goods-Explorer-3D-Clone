// ConveyorObstacleData.cs
using System;
using UnityEngine;

namespace FoodMatch.Data
{
    /// <summary>
    /// Obstacle 3: Băng chuyền (Conveyor Belt).
    /// - Nhiều băng chuyền nhỏ nối đuôi nhau, mỗi băng chứa 1 food.
    /// - Food chạy từ trái → phải, wrap lại từ đầu.
    /// - Chỉ cần bật/tắt và set số lượng băng chuyền + food per conveyor.
    /// </summary>
    [Serializable]
    public class ConveyorObstacleData : ObstacleData
    {
        public override string ObstacleName => "Conveyor Belt";

        [Header("Conveyor Settings")]
        [Tooltip("Số lượng băng chuyền nhỏ (mỗi cái chứa 1 food).")]
        [Range(5,10)]
        public int conveyorCount = 4;

        [Tooltip("Số food trên mỗi băng chuyền.")]
        [Range(3, 6)]
        public int foodPerConveyor = 3;

        [Tooltip("Tốc độ di chuyển của băng chuyền (units/second).")]
        [Range(20, 80)]
        public float speed = 40f;

        /// <summary>Tổng số food trên tất cả băng chuyền.</summary>
        public int TotalFoodCount => conveyorCount * foodPerConveyor;

        public override ObstacleData Clone() => new ConveyorObstacleData
        {
            isEnabled = isEnabled,
            conveyorCount = conveyorCount,
            foodPerConveyor = foodPerConveyor,
            speed = speed
        };
    }
}