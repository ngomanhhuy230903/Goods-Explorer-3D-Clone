using UnityEngine;
using FoodMatch.Food;
using FoodMatch.Order;
using FoodMatch.Tray;

namespace FoodMatch.Items
{
    /// <summary>
    /// Duy nhất 1 MonoBehaviour liên quan đến booster trong scene.
    /// Chỉ thu thập references → tạo BoosterContext → gọi AutoRegisterAll.
    /// Không chứa bất kỳ logic booster nào.
    /// </summary>
    public class BoosterInstaller : MonoBehaviour
    {
        [Header("─── Dependencies ───────────────────")]
        [SerializeField] private OrderQueue orderQueue;
        [SerializeField] private FoodGridSpawner foodGridSpawner;
        [SerializeField] private BackupTray backupTray;
        [SerializeField] private BackupTraySpawner backupTraySpawner;
        [SerializeField] private FoodBuffer foodBuffer;

        private void Start()
        {
            if (BoosterManager.Instance == null)
            {
                Debug.LogError("[BoosterInstaller] BoosterManager chưa có Instance!");
                return;
            }

            var context = new BoosterContext(
                orderQueue,
                foodGridSpawner,
                backupTray,
                backupTraySpawner,
                foodBuffer,
                coroutineRunner: this   // MonoBehaviour để chạy Coroutine
            );

            BoosterManager.Instance.AutoRegisterAll(context);
        }
    }
}