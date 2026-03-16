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

        /// <summary>
        /// Context được lưu lại để GameResultUI có thể lấy khi cần
        /// tạo ClearTrayBooster trực tiếp lúc revive (không tốn quantity).
        /// </summary>
        public BoosterContext Context { get; private set; }

        private void Start()
        {
            if (BoosterManager.Instance == null)
            {
                Debug.LogError("[BoosterInstaller] BoosterManager chưa có Instance!");
                return;
            }

            Context = new BoosterContext(
                orderQueue,
                foodGridSpawner,
                backupTray,
                backupTraySpawner,
                foodBuffer,
                coroutineRunner: this
            );

            BoosterManager.Instance.AutoRegisterAll(Context);
        }
    }
}