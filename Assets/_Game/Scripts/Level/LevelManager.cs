using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Core;
using FoodMatch.Order;
using FoodMatch.Tray;

namespace FoodMatch.Level
{
    /// <summary>
    /// Điều phối toàn bộ vòng đời 1 level:
    ///   1. Nhận lệnh load từ GameManager
    ///   2. Đọc LevelConfig từ LevelDatabase
    ///   3. Khởi tạo OrderQueue, BackupTray, FoodTray
    ///   4. Lắng nghe Win/Lose từ LevelProgressTracker
    /// Gắn vào GameObject "LevelManager" trong Scene Game.
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
  //    [SerializeField] private FoodGridSpawner foodGridSpawner;
        [SerializeField] private LevelProgressTracker progressTracker;

        // ─── Runtime ──────────────────────────────────────────────────────────
        public LevelConfig CurrentConfig { get; private set; }
        public int CurrentLevelIndex { get; private set; } = 1;
        [Header("─── Spawners ────────────────────────")]
        [SerializeField] private BackupTraySpawner backupTraySpawner;
        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
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

        // ─── Game State Handler ───────────────────────────────────────────────

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.LoadLevel:
                    LoadLevel(CurrentLevelIndex);
                    break;

                case GameState.Play:
                    // LoadLevel tự gọi StartLevel,
                    // nhưng nếu GameManager skip LoadLevel thẳng vào Play thì xử lý ở đây
                    if (CurrentConfig == null)
                        LoadLevel(CurrentLevelIndex);
                    break;
            }
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Set level cần chơi rồi báo GameManager chuyển sang LoadLevel.
        /// Gọi từ UI khi player bấm nút Play hoặc Next Level.
        /// </summary>
        public void RequestLoadLevel(int levelIndex)
        {
            CurrentLevelIndex = levelIndex;
            GameManager.Instance.ChangeState(GameState.LoadLevel);
        }

        /// <summary>
        /// Chơi lại level hiện tại.
        /// </summary>
        public void RestartCurrentLevel()
        {
            RequestLoadLevel(CurrentLevelIndex);
        }

        /// <summary>
        /// Qua level tiếp theo.
        /// </summary>
        public void LoadNextLevel()
        {
            var next = levelDatabase.GetNextLevel(CurrentLevelIndex);
            if (next != null)
                RequestLoadLevel(next.levelIndex);
            else
            {
                // Hết game — về menu
                GameManager.Instance.ChangeState(GameState.Menu);
            }
        }

        // ─── Core Load Flow ───────────────────────────────────────────────────

        private void LoadLevel(int levelIndex)
        {
            // 1. Lấy config
            var config = levelDatabase.GetLevel(levelIndex);
            if (config == null || !config.IsValid())
            {
                Debug.LogError($"[LevelManager] LevelConfig {levelIndex} không hợp lệ!");
                return;
            }

            CurrentConfig = config;
            Debug.Log($"[LevelManager] Đang load Level {levelIndex}: {config.GetDisplayName()}");

            // 2. Reset các hệ thống cũ
            ResetAllSystems();

            // 3. Khởi tạo theo thứ tự phụ thuộc:
            //    BackupTray → FoodGrid → OrderQueue → ProgressTracker
            InitBackupTray(config);
          //InitFoodGrid(config);
            InitOrderQueue(config);
            InitProgressTracker(config);

            // 4. Chuyển sang Play
            GameManager.Instance.ChangeState(GameState.Play);

            Debug.Log($"[LevelManager] Level {levelIndex} sẵn sàng!");
        }

        // ─── System Init ──────────────────────────────────────────────────────

        private void InitBackupTray(LevelConfig config)
        {
            if (backupTray == null)
            {
                Debug.LogWarning("[LevelManager] BackupTray chưa được gán!");
                return;
            }

            if (backupTraySpawner != null)
            {
                // BackupTraySpawner lo cả việc sinh slot lẫn gọi ResetTray bên trong
                backupTraySpawner.SpawnSlots(config.backupTrayCapacity);
            }
            else
            {
                // Fallback: không có spawner thì dùng trực tiếp như cũ
                backupTray.ResetTray(config.backupTrayCapacity);
            }
        }

        //private void InitFoodGrid(LevelConfig config)
        //{
        //    if (foodGridSpawner == null)
        //    {
        //        Debug.LogWarning("[LevelManager] FoodGridSpawner chưa được gán!");
        //        return;
        //    }
        //    foodGridSpawner.SpawnGrid(config);
        //}

        private void InitOrderQueue(LevelConfig config)
        {
            if (orderQueue == null)
            {
                Debug.LogWarning("[LevelManager] OrderQueue chưa được gán!");
                return;
            }
            orderQueue.Initialize(config);
        }

        private void InitProgressTracker(LevelConfig config)
        {
            if (progressTracker == null)
            {
                Debug.LogWarning("[LevelManager] LevelProgressTracker chưa được gán!");
                return;
            }
            progressTracker.Initialize(config);
        }

        private void ResetAllSystems()
        {
            orderQueue?.Reset();
            backupTray?.ResetTray(5);
         // foodGridSpawner?.ClearGrid();
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