using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Core;
using FoodMatch.Order;
using FoodMatch.Tray;

namespace FoodMatch.Level
{
    /// <summary>
    /// Điều phối toàn bộ vòng đời 1 level.
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

        private void OnEnable() => GameManager.OnGameStateChanged += HandleGameStateChanged;
        private void OnDisable() => GameManager.OnGameStateChanged -= HandleGameStateChanged;

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
            var config = levelDatabase.GetLevel(levelIndex);
            if (config == null || !config.IsValid())
            {
                Debug.LogError($"[LevelManager] LevelConfig {levelIndex} không hợp lệ!");
                return;
            }

            CurrentConfig = config;
            Debug.Log($"[LevelManager] Load Level {levelIndex}: {config.GetDisplayName()}");

            ResetAllSystems();

            InitBackupTray(config);
            InitFoodGrid(config);       // ← FoodGridSpawner: tạo hình khối
            InitFoodTraySpawner(config); // ← FoodTraySpawner: nhét food vào anchor
            InitOrderQueue(config);
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
                backupTray.ResetTray(config.backupTrayCapacity);
        }

        private void InitFoodGrid(LevelConfig config)
        {
            if (foodGridSpawner == null) { Debug.LogWarning("[LevelManager] FoodGridSpawner chưa gán!"); return; }
            foodGridSpawner.SpawnGrid(config);
        }

        private void InitFoodTraySpawner(LevelConfig config)
        {
            if (foodTraySpawner == null) { Debug.LogWarning("[LevelManager] FoodTraySpawner chưa gán!"); return; }
            foodTraySpawner.SpawnFood(config);
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
            foodGridSpawner?.ClearGrid();    // ← xóa hình khối cũ
            foodTraySpawner?.ClearFood();    // ← xóa food cũ trong các tray
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