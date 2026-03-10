using FoodMatch.Food;
using FoodMatch.Order;
using FoodMatch.Tray;
using UnityEngine;

namespace FoodMatch.Items
{
    /// <summary>
    /// Dependency container — inject 1 lần từ BoosterManager.
    /// </summary>
    public class BoosterContext
    {
        public OrderQueue OrderQueue { get; }
        public FoodGridSpawner FoodGridSpawner { get; }
        public BackupTray BackupTray { get; }
        public BackupTraySpawner BackupTraySpawner { get; }
        public FoodBuffer FoodBuffer { get; }
        public MonoBehaviour CoroutineRunner { get; }

        public BoosterContext(
            OrderQueue orderQueue,
            FoodGridSpawner foodGridSpawner,
            BackupTray backupTray,
            BackupTraySpawner backupTraySpawner,
            FoodBuffer foodBuffer,
            MonoBehaviour coroutineRunner)
        {
            OrderQueue = orderQueue;
            FoodGridSpawner = foodGridSpawner;
            BackupTray = backupTray;
            BackupTraySpawner = backupTraySpawner;
            FoodBuffer = foodBuffer;
            CoroutineRunner = coroutineRunner;
        }
    }
}