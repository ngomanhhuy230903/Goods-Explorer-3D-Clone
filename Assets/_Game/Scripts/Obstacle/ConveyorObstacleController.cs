// ConveyorObstacleController.cs
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Obstacle
{
    public class ConveyorObstacleController : ObstacleController<ConveyorObstacleData>
    {
        protected override void OnInitialize(ConveyorObstacleData data)
        {
            // TODO: Spawn conveyor belt prefab
            // TODO: Spawn data.foodCount food items trên belt
            // TODO: Set tốc độ data.speed, bật movement loop (trái → phải → wrap)
            Debug.Log($"[ConveyorObstacle] Init — {data.foodCount} foods, speed={data.speed}");
        }

        protected override void OnReset()
        {
            // TODO: Dừng belt, destroy food items
            Debug.Log("[ConveyorObstacle] Reset");
        }
    }
}