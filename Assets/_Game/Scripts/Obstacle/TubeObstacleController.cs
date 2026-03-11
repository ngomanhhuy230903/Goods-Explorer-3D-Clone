// TubeObstacleController.cs
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Obstacle
{
    public class TubeObstacleController : ObstacleController<TubeObstacleData>
    {
        protected override void OnInitialize(TubeObstacleData data)
        {
            // TODO: Spawn data.tubeCount ống tại vị trí cố định
            // TODO: Mỗi ống tạo Queue<FoodItem> với data.GetFoodCountForTube(i) items
            // TODO: Hiển thị item đầu tiên, ẩn các item còn lại
            Debug.Log($"[TubeObstacle] Init — {data.tubeCount} tubes");
        }

        protected override void OnReset()
        {
            // TODO: Destroy tất cả ống và food trong đó
            Debug.Log("[TubeObstacle] Reset");
        }

        /// <summary>
        /// Gọi khi player lấy food từ đầu ống.
        /// TODO: Dequeue, hiện item tiếp theo, ẩn các item sau.
        /// </summary>
        public void OnFoodTaken(int tubeIndex)
        {
            // TODO: implement
        }
    }
}