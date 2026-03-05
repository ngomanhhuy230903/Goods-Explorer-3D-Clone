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
    ///   3. Khởi tạo OrderQueue, BackupTray, FoodGridSpawner
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
        [SerializeField] private FoodGridSpawner foodGridSpawner;   // ← bỏ comment
        [SerializeField] private LevelProgressTracker progressTracker;

        [Header("─── Spawners ────────────────────────")]
        [SerializeField] private BackupTraySpawner backupTraySpawner;

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

        // ─── Game State Handler ───────────────────────────────────────────────

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.LoadLevel)
                LoadLevel(CurrentLevelIndex);
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
            // 1. Lấy config
            var config = levelDatabase.GetLevel(levelIndex);
            if (config == null || !config.IsValid())
            {
                Debug.LogError($"[LevelManager] LevelConfig {levelIndex} không hợp lệ!");
                return;
            }

            CurrentConfig = config;
            Debug.Log($"[LevelManager] Load Level {levelIndex}: {config.GetDisplayName()}");

            // 2. Reset hệ thống cũ
            ResetAllSystems();

            // 3. Khởi tạo theo thứ tự phụ thuộc
            InitBackupTray(config);
            InitFoodGrid(config);        // ← spawn grid ngay tại đây
            InitOrderQueue(config);
            InitProgressTracker(config);

            // 4. Chuyển sang Play — UIManager sẽ hiện panelGame
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
                backupTray.ResetTray(config.backupTrayCapacity);
        }

        private void InitFoodGrid(LevelConfig config)
        {
            if (foodGridSpawner == null)
            {
                Debug.LogWarning("[LevelManager] FoodGridSpawner chưa gán!");
                return;
            }
            foodGridSpawner.SpawnGrid(config);
        }

        private void InitOrderQueue(LevelConfig config)
        {
            if (orderQueue == null) { Debug.LogWarning("[LevelManager] OrderQueue chưa gán!"); return; }
            orderQueue.Initialize(config);
        }

        private void InitProgressTracker(LevelConfig config)
        {
            if (progressTracker == null) { Debug.LogWarning("[LevelManager] ProgressTracker chưa gán!"); return; }
            progressTracker.Initialize(config);
        }

        private void ResetAllSystems()
        {
            orderQueue?.Reset();
            backupTray?.ResetTray(5);
            foodGridSpawner?.ClearGrid();   // ← clear grid cũ trước khi spawn mới
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