// TubeObstacleData.cs
using System;
using UnityEngine;

namespace FoodMatch.Data
{
    /// <summary>
    /// Obstacle 2: Ống chứa food (Queue).
    /// - Mỗi ống chứa N food, chỉ thấy 1 food đầu
    /// - Khi food đầu được lấy → food tiếp theo lộ ra
    /// </summary>
    [Serializable]
    public class TubeObstacleData : ObstacleData
    {
        public override string ObstacleName => "Food Tube";

        [Header("Tube Settings")]
        [Tooltip("Số ống spawn trong level.")]
        [Range(1, 4)]
        public int tubeCount = 2;

        [Tooltip("Số food mặc định trong mỗi ống.")]
        [Range(1, 10)]
        public int defaultFoodPerTube = 4;

        [Tooltip("Override số food cho từng ống theo index. Để trống = dùng default.")]
        public int[] perTubeFoodCount = Array.Empty<int>();

        public int GetFoodCountForTube(int tubeIndex)
        {
            if (perTubeFoodCount != null
                && tubeIndex < perTubeFoodCount.Length
                && perTubeFoodCount[tubeIndex] > 0)
                return perTubeFoodCount[tubeIndex];
            return defaultFoodPerTube;
        }

        public int GetTotalFoodCount()
        {
            int total = 0;
            for (int i = 0; i < tubeCount; i++)
                total += GetFoodCountForTube(i);
            return total;
        }

        public override bool IsValid()
        {
            if (tubeCount <= 0)
            {
                Debug.LogError("[TubeObstacleData] tubeCount phải >= 1");
                return false;
            }
            return true;
        }

        public override ObstacleData Clone() => new TubeObstacleData
        {
            isEnabled = isEnabled,
            tubeCount = tubeCount,
            defaultFoodPerTube = defaultFoodPerTube,
            perTubeFoodCount = (int[])perTubeFoodCount?.Clone()
        };
    }
}