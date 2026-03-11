// ObstacleManager.cs
using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Core;

namespace FoodMatch.Obstacle
{
    /// <summary>
    /// Khởi tạo và quản lý vòng đời tất cả obstacles trong 1 level.
    /// Logic cụ thể của từng obstacle nằm trong controller riêng của nó.
    /// </summary>
    public class ObstacleManager : MonoBehaviour
    {
        public static ObstacleManager Instance { get; private set; }

        [Header("─── Obstacle Controllers ────────────")]
        [Tooltip("Controller cho Obstacle 1: FoodTray Lock")]
        [SerializeField] private LockObstacleController lockController;

        [Tooltip("Controller cho Obstacle 2: Food Tube")]
        [SerializeField] private TubeObstacleController tubeController;

        [Tooltip("Controller cho Obstacle 3: Conveyor Belt")]
        [SerializeField] private ConveyorObstacleController conveyorController;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Gọi từ LevelManager sau ResetAllSystems(), trước GameState.Play.
        /// </summary>
        public void InitializeObstacles(LevelConfig config)
        {
            if (config == null) return;

            InitLockObstacle(config);
            InitTubeObstacle(config);
            InitConveyorObstacle(config);
        }

        /// <summary>Reset toàn bộ obstacles khi level kết thúc.</summary>
        public void ResetObstacles()
        {
            lockController?.Reset();
            tubeController?.Reset();
            conveyorController?.Reset();
        }

        // ─── Private Init ─────────────────────────────────────────────────────

        private void InitLockObstacle(LevelConfig config)
        {
            if (lockController == null) return;

            var data = config.GetObstacle<LockObstacleData>();
            if (data != null)
            {
                lockController.gameObject.SetActive(true);
                lockController.Initialize(data);
                Debug.Log($"[ObstacleManager] Lock Obstacle ON — " +
                          $"{data.lockedTrayCount} trays, HP={data.defaultLockHp}");
            }
            else
            {
                lockController.gameObject.SetActive(false);
                Debug.Log("[ObstacleManager] Lock Obstacle OFF");
            }
        }

        private void InitTubeObstacle(LevelConfig config)
        {
            if (tubeController == null) return;

            var data = config.GetObstacle<TubeObstacleData>();
            if (data != null)
            {
                tubeController.gameObject.SetActive(true);
                tubeController.Initialize(data);
                Debug.Log($"[ObstacleManager] Tube Obstacle ON — " +
                          $"{data.tubeCount} tubes, total food={data.GetTotalFoodCount()}");
            }
            else
            {
                tubeController.gameObject.SetActive(false);
                Debug.Log("[ObstacleManager] Tube Obstacle OFF");
            }
        }

        private void InitConveyorObstacle(LevelConfig config)
        {
            if (conveyorController == null) return;

            var data = config.GetObstacle<ConveyorObstacleData>();
            if (data != null)
            {
                conveyorController.gameObject.SetActive(true);
                conveyorController.Initialize(data);
                Debug.Log($"[ObstacleManager] Conveyor Obstacle ON — " +
                          $"food={data.foodCount}, speed={data.speed}");
            }
            else
            {
                conveyorController.gameObject.SetActive(false);
                Debug.Log("[ObstacleManager] Conveyor Obstacle OFF");
            }
        }
    }
}