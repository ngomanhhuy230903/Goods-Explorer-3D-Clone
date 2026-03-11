// LockObstacleController.cs
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Obstacle
{
    public class LockObstacleController : ObstacleController<LockObstacleData>
    {
        protected override void OnInitialize(LockObstacleData data)
        {
            // TODO: Random chọn data.lockedTrayCount trays để khóa
            // TODO: Gán HP cho từng tray (data.GetHpForTray(i))
            // TODO: Attach lock sprite lên các tray đó
            Debug.Log($"[LockObstacle] Init — {data.lockedTrayCount} trays locked");
        }

        protected override void OnReset()
        {
            // TODO: Gỡ tất cả lock sprite, reset HP
            Debug.Log("[LockObstacle] Reset");
        }

        /// <summary>
        /// Gọi khi 1 order hoàn thành.
        /// TODO: Chọn random 1 tray đang bị khóa, trừ 1 HP, nếu HP=0 unlock tray đó.
        /// </summary>
        public void OnOrderCompleted()
        {
            // TODO: implement
        }
    }
}