using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Core;
using FoodMatch.Food;
using FoodMatch.Order;
using FoodMatch.Tray;
using FoodMatch.Obstacle;
using FoodMatch.Items;

namespace FoodMatch.Level
{
    /// <summary>
    /// Điều phối toàn bộ vòng đời 1 level.
    ///
    /// Reset policy:
    ///   • Win          → ResetAllSystems() (dọn dẹp để load level tiếp)
    ///   • Lose         → KHÔNG reset — game state giữ nguyên để người chơi revive
    ///   • Retry/Restart → RequestLoadLevel() → LoadLevel() → ResetAllSystems() bên trong
    ///   • Revive       → GameResultUI.ChangeState(Play) trực tiếp, không qua đây
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

        [Header("─── Obstacles ────────────────────────")]
        [SerializeField] private ObstacleManager obstacleManager;

        // ─── Runtime ──────────────────────────────────────────────────────────
        public LevelConfig CurrentConfig { get; private set; }
        public int CurrentLevelIndex { get; private set; } = 1;

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
            switch (state)
            {
                case GameState.LoadLevel:
                    LoadLevel(CurrentLevelIndex);
                    break;

                case GameState.Win:
                    // Win: reset để chuẩn bị load level tiếp (Next Level hoặc về Menu)
                    ResetAllSystems();
                    break;

                    // Lose: KHÔNG làm gì cả.
                    // Game state (BackupTray, Grid, OrderQueue, FoodTray) giữ nguyên.
                    // GameResultUI sẽ quyết định bước tiếp:
                    //   • Revive → ChangeState(Play) — chơi tiếp nguyên trạng
                    //   • Retry  → RestartCurrentLevel() → LoadLevel() → ResetAllSystems()
                    //   • Home   → ChangeState(Menu)
            }
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public void RequestLoadLevel(int levelIndex)
        {
            CurrentLevelIndex = levelIndex;
            GameManager.Instance.ChangeState(GameState.LoadLevel);
        }

        /// <summary>
        /// Retry: reset toàn bộ và load lại level hiện tại từ đầu.
        /// Đây là con đường DUY NHẤT trigger ResetAllSystems() sau khi Lose.
        /// </summary>
        public void RestartCurrentLevel() => RequestLoadLevel(CurrentLevelIndex);

        /// <summary>
        /// Gọi khi người chơi thoát ván thua về Home (không retry, không revive).
        /// Reset sạch toàn bộ game objects mà không load level mới.
        /// </summary>
        public void CleanupForHome()
        {
            ResetAllSystems();
            Debug.Log("[LevelManager] CleanupForHome: đã reset toàn bộ hệ thống.");
        }

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
            ResetAllSystems();               // reset trước khi spawn mọi thứ

            InitBackupTray(config);          // 1. Backup tray
            InitFoodGrid(config);            // 2. Tạo grid (async)
            InitOrderQueue(config);          // 3. Sinh SharedFoodList

            // 4. Obstacles reserve TRƯỚC — tubes & conveyor rút food ra khỏi SharedFoodList
            obstacleManager?.InitializeObstacles(config);

            // 5. FoodTraySpawner đăng ký callback — khi grid xong sẽ lấy phần CÒN LẠI
            InitFoodTraySpawner(config);

            InjectFoodFlowController();      // 6.
            InitProgressTracker(config);     // 7.

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
        /// </summary>
        private void InitOrderQueue(LevelConfig config)
        {
            if (orderQueue == null) { Debug.LogWarning("[LevelManager] OrderQueue chưa gán!"); return; }
            orderQueue.Initialize(config);
        }

        /// <summary>
        /// Bước 5: FoodTraySpawner tự lấy SharedFoodList từ OrderQueue
        /// trong callback OnGridSpawnComplete.
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

        /// <summary>
        /// Dọn sạch toàn bộ hệ thống về trạng thái trống.
        /// CHỈ gọi bên trong LoadLevel() — không gọi trực tiếp từ ngoài.
        /// </summary>
        private void ResetAllSystems()
        {
            BoosterManager.Instance?.ResetAllBoosterSessions();
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