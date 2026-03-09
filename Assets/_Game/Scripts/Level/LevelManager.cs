using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Core;
using FoodMatch.Food;
using FoodMatch.Order;
using FoodMatch.Tray;

namespace FoodMatch.Level
{
    /// <summary>
    /// Điều phối toàn bộ vòng đời 1 level.
    /// Gắn vào GameObject "LevelManager" trong Scene Game.
    ///
    /// THỨ TỰ INIT QUAN TRỌNG:
    ///   1. InitOrderQueue        → sinh SharedFoodList (nguồn sự thật duy nhất)
    ///   2. InitFoodTraySpawner   → inject SharedFoodList, rồi mới spawn
    ///   3. Inject FoodFlowController
    /// Đảm bảo FoodTray và OrderTray luôn dùng cùng số lượng từng loại food.
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

        [Tooltip("Spawn food vào các anchor positions trong FoodTray — chạy SAU foodGridSpawner.")]
        [SerializeField] private FoodTraySpawner foodTraySpawner;

        // ─── Runtime ──────────────────────────────────────────────────────────
        public LevelConfig CurrentConfig { get; private set; }
        public int CurrentLevelIndex { get; private set; } = 1;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            GameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.LoadLevel)
                LoadLevel(CurrentLevelIndex);
            else if (state == GameState.Win || state == GameState.Lose)
            {
                // Cleanup ngay khi Win/Lose để food biến mất trước khi popup hiện
                FoodFlowController.Instance?.ResetDependencies();
                backupTray?.ClearAllFood();
                foodTraySpawner?.ClearFood();
                orderQueue?.Reset();
                foodGridSpawner?.ClearGrid();
            }
        }

        /// <summary>
        /// Ẩn/cleanup tất cả gameplay objects khi Win/Lose.
        /// KHÔNG reset hoàn toàn — chỉ ẩn visual. Reset thật sự xảy ra ở ResetAllSystems().
        /// </summary>
        private void CleanupGameplayObjects()
        {
            // Stop food fly animations
            FoodFlowController.Instance?.ResetDependencies();

            // Return food về pool (ẩn khỏi màn hình)
            backupTray?.ClearAllFood();
            foodTraySpawner?.ClearFood();
            foodGridSpawner?.ClearGrid();
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

            // ── Init theo thứ tự phụ thuộc ────────────────────────────────

            InitBackupTray(config);      // BackupTray sẵn sàng nhận food
            InitFoodGrid(config);        // Tạo hình khối polygon 3D

            // !! QUAN TRỌNG: OrderQueue phải init TRƯỚC FoodTraySpawner !!
            // OrderQueue.Initialize() → sinh SharedFoodList
            // FoodTraySpawner cần SharedFoodList để spawn đúng số lượng từng loại
            InitOrderQueue(config);

            // Inject SharedFoodList vào FoodTraySpawner, rồi mới spawn
            InitFoodTraySpawner(config);

            // ── INJECT vào FoodFlowController ─────────────────────────────
            InjectFoodFlowController();

            InitProgressTracker(config);

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

        private void InitOrderQueue(LevelConfig config)
        {
            if (orderQueue == null) { Debug.LogWarning("[LevelManager] OrderQueue chưa gán!"); return; }
            orderQueue.Initialize(config);
            // Sau bước này, orderQueue.SharedFoodList đã sẵn sàng
        }

        /// <summary>
        /// Inject SharedFoodList từ OrderQueue vào FoodTraySpawner TRƯỚC khi spawn.
        /// Đảm bảo 2 hệ thống dùng cùng 1 nguồn food list → số lượng từng loại khớp nhau.
        /// </summary>
        private void InitFoodTraySpawner(LevelConfig config)
        {
            if (foodTraySpawner == null) { Debug.LogWarning("[LevelManager] FoodTraySpawner chưa gán!"); return; }

            // Inject canonical food list từ OrderQueue (đã init ở bước trước)
            if (orderQueue != null && orderQueue.SharedFoodList != null)
            {
                foodTraySpawner.SetFoodList(orderQueue.SharedFoodList);
            }
            else
            {
                Debug.LogWarning("[LevelManager] SharedFoodList null — FoodTraySpawner sẽ fallback về random. " +
                                 "Kiểm tra InitOrderQueue() đã chạy trước chưa.");
            }

            foodTraySpawner.SpawnFood(config);
        }

        /// <summary>
        /// Inject OrderQueue và BackupTray đã init vào FoodFlowController.
        /// </summary>
        private void InjectFoodFlowController()
        {
            if (FoodFlowController.Instance == null)
            {
                Debug.LogError("[LevelManager] FoodFlowController.Instance là null! " +
                               "Đảm bảo FoodFlowController đã có trong Scene Game.");
                return;
            }

            if (orderQueue == null || backupTray == null)
            {
                Debug.LogError("[LevelManager] Không thể Inject: orderQueue hoặc backupTray null!");
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
            backupTray?.ClearAllFood();
            foodTraySpawner?.ClearFood();  // ← THÊM VÀO ĐÂY, trước ClearGrid
            orderQueue?.Reset();
            foodGridSpawner?.ClearGrid();  // ← SAU CÙNG
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