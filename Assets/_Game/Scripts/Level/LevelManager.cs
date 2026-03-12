using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Core;
using FoodMatch.Food;
using FoodMatch.Order;
using FoodMatch.Tray;
using FoodMatch.Obstacle;

namespace FoodMatch.Level
{
    /// <summary>
    /// Điều phối toàn bộ vòng đời 1 level.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Data ────────────────────────────")]
        [SerializeField] private LevelDatabase levelDatabase;

        [Header("─── Systems ─────────────────────────")]
        [SerializeField] private OrderQueue orderQueue;
        [SerializeField] private BackupTray backupTray;
        [SerializeField] private LevelProgressTracker progressTracker;

        [Header("─── Spawners ────────────────────────")]
        [SerializeField] private BackupTraySpawner backupTraySpawner;

        [Tooltip("Tạo hình khối polygon 3D — KHÔNG đụng vào.")]
        [SerializeField] private FoodGridSpawner foodGridSpawner;

        [Tooltip("Spawn food vào FoodTray SAU khi grid xong. Tự lấy SharedFoodList từ OrderQueue.")]
        [SerializeField] private FoodTraySpawner foodTraySpawner;

        // ─── Runtime ──────────────────────────────────────────────────────────
        public LevelConfig CurrentConfig { get; private set; }
        public int CurrentLevelIndex { get; private set; } = 1;
        [Header("─── Obstacles ────────────────────────")]
        [SerializeField] private ObstacleManager obstacleManager;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable() => GameManager.OnGameStateChanged += HandleGameStateChanged;
        private void OnDisable() => GameManager.OnGameStateChanged -= HandleGameStateChanged;

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.LoadLevel)
                LoadLevel(CurrentLevelIndex);
            else if (state == GameState.Win || state == GameState.Lose)
            {
                ResetAllSystems();
            }
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public void RequestLoadLevel(int levelIndex)
        {
            CurrentLevelIndex = levelIndex;
            GameManager.Instance.ChangeState(GameState.LoadLevel);
        }

        public void RestartCurrentLevel() => RequestLoadLevel(CurrentLevelIndex);

        public void LoadNextLevel()
        {
            var next = levelDatabase.GetNextLevel(CurrentLevelIndex);
            if (next != null) RequestLoadLevel(next.levelIndex);
            else GameManager.Instance.ChangeState(GameState.Menu);
        }

        // ─── Core Load Flow ───────────────────────────────────────────────────

        private void LoadLevel(int levelIndex)
        {
            var config = levelDatabase.GetLevel(levelIndex);
            if (config == null || !config.IsValid())
            {
                Debug.LogError($"[LevelManager] LevelConfig {levelIndex} không hợp lệ!");
                return;
            }

            CurrentConfig = config;
            Debug.Log($"[LevelManager] Load Level {levelIndex}: {config.GetDisplayName()}");

            ResetAllSystems();

            // ── Thứ tự init có phụ thuộc — KHÔNG đổi thứ tự ─────────────────

            InitBackupTray(config);   // 1. Backup tray sẵn sàng
            InitFoodGrid(config);     // 2. Tạo grid (async, callback khi xong)

            InitOrderQueue(config);   // 3. Sinh SharedFoodList — PHẢI trước FoodTraySpawner
            InitFoodTraySpawner(config); // 4. Đăng ký callback, sẽ lấy SharedFoodList khi grid xong

            InjectFoodFlowController(); // 5. Inject dependencies
            InitProgressTracker(config); // 6.
            obstacleManager?.InitializeObstacles(config);
            GameManager.Instance.ChangeState(GameState.Play);
            Debug.Log($"[LevelManager] Level {levelIndex} sẵn sàng!");
        }

        // ─── System Init ──────────────────────────────────────────────────────

        private void InitBackupTray(LevelConfig config)
        {
            if (backupTray == null) { Debug.LogWarning("[LevelManager] BackupTray chưa gán!"); return; }
            if (backupTraySpawner != null)
                backupTraySpawner.SpawnSlots(config.backupTrayCapacity);
            else
                backupTray.Initialize(config.backupTrayCapacity);
        }

        private void InitFoodGrid(LevelConfig config)
        {
            if (foodGridSpawner == null) { Debug.LogWarning("[LevelManager] FoodGridSpawner chưa gán!"); return; }
            foodGridSpawner.SpawnGrid(config);
        }

        /// <summary>
        /// Bước 3: OrderQueue.Initialize() sinh SharedFoodList.
        /// SharedFoodList = SOURCE OF TRUTH cho cả OrderTray và FoodTray.
        /// totalFood trong config đã được LevelGeneratorEditor tính đúng:
        ///   totalFood = (gridCapacity / (foodTypes×3)) × (foodTypes×3)
        /// → đảm bảo chia đều và không bao giờ vượt capacity FoodTray.
        /// </summary>
        private void InitOrderQueue(LevelConfig config)
        {
            if (orderQueue == null) { Debug.LogWarning("[LevelManager] OrderQueue chưa gán!"); return; }
            orderQueue.Initialize(config);
            // Sau dòng này: orderQueue.SharedFoodList đã có đầy đủ dữ liệu
        }

        /// <summary>
        /// Bước 4: FoodTraySpawner chỉ cần gọi SpawnFood().
        /// Nó sẽ tự lấy OrderQueue.Instance.SharedFoodList trong callback OnGridSpawnComplete.
        /// KHÔNG cần gọi SetFoodList() nữa — đã deprecated.
        /// </summary>
        private void InitFoodTraySpawner(LevelConfig config)
        {
            if (foodTraySpawner == null) { Debug.LogWarning("[LevelManager] FoodTraySpawner chưa gán!"); return; }

            foodTraySpawner.SpawnFood(config);
        }

        private void InjectFoodFlowController()
        {
            if (FoodFlowController.Instance == null)
            {
                Debug.LogError("[LevelManager] FoodFlowController.Instance là null!");
                return;
            }
            if (orderQueue == null || backupTray == null)
            {
                Debug.LogError("[LevelManager] orderQueue hoặc backupTray null!");
                return;
            }
            FoodFlowController.Instance.Inject(orderQueue, backupTray);
        }

        private void InitProgressTracker(LevelConfig config)
        {
            if (progressTracker == null) { Debug.LogWarning("[LevelManager] ProgressTracker chưa gán!"); return; }
            progressTracker.Initialize(config);
        }

        private void ResetAllSystems()
        {
            FoodFlowController.Instance?.ResetDependencies();
            FoodBuffer.Instance?.ForceReset();
            obstacleManager?.ResetObstacles();
            backupTray?.ClearAllFood();
            foodTraySpawner?.ClearFood();  // trước ClearGrid
            orderQueue?.Reset();
            foodGridSpawner?.ClearGrid();  // sau cùng
        }

        // ─── Debug ────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        [ContextMenu("Debug: Load Level 1")]
        private void DebugLoadLevel1() => RequestLoadLevel(1);

        [ContextMenu("Debug: Restart")]
        private void DebugRestart() => RestartCurrentLevel();
#endif
    }
}