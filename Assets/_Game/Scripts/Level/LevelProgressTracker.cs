using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Core;
using FoodMatch.Managers;

namespace FoodMatch.Level
{
    /// <summary>
    /// Theo dõi tiến trình trong 1 level đang chạy.
    /// - Đếm số khách đã hoàn thành order
    /// - Đếm số món còn lại trên khay
    /// - Phát sự kiện Win/Lose khi điều kiện thỏa mãn
    /// Gắn vào GameObject "LevelManager" trong Scene Game.
    /// </summary>
    public class LevelProgressTracker : MonoBehaviour
    {
        // ─── Runtime State ────────────────────────────────────────────────────
        private LevelConfig _currentLevel;
        private int _totalCustomers;
        private int _customersCompleted;
        private int _totalFoodOnTray;
        private int _foodDelivered;
        private bool _isLevelOver;

        // ─── Public Readonly Properties ───────────────────────────────────────
        public int CustomersCompleted => _customersCompleted;
        public int TotalCustomers => _totalCustomers;
        public int FoodDelivered => _foodDelivered;
        public bool IsLevelOver => _isLevelOver;

        /// <summary>Tiến độ hoàn thành level (0f -> 1f).</summary>
        public float Progress =>
            _totalCustomers == 0 ? 0f :
            (float)_customersCompleted / _totalCustomers;

        // ─── Initialization ───────────────────────────────────────────────────

        /// <summary>
        /// Khởi tạo tracker với config của level hiện tại.
        /// Gọi từ LevelManager sau khi load level xong.
        /// </summary>
        public void Initialize(LevelConfig config)
        {
            _currentLevel = config;
            _totalFoodOnTray = config.totalFoodCount;
            _isLevelOver = false;
            _foodDelivered = 0;
            _customersCompleted = 0;

            // Tính tổng số khách: tổng món / 3 món mỗi khách
            _totalCustomers = config.totalFoodCount / GameConstants.FOOD_SET_SIZE;

            Debug.Log($"[LevelProgressTracker] Level {config.levelIndex} bắt đầu. " +
                      $"Tổng món: {_totalFoodOnTray} | " +
                      $"Tổng khách: {_totalCustomers}");

            SubscribeEvents();
        }

        // ─── Event Subscription ───────────────────────────────────────────────

        private void SubscribeEvents()
        {
            EventBus.OnCustomerOrderComplete += HandleCustomerComplete;
            EventBus.OnBackupTrayFull += HandleBackupFull;
            EventBus.OnFoodMatchedCustomer += HandleFoodMatched;
        }

        private void UnsubscribeEvents()
        {
            EventBus.OnCustomerOrderComplete -= HandleCustomerComplete;
            EventBus.OnBackupTrayFull -= HandleBackupFull;
            EventBus.OnFoodMatchedCustomer -= HandleFoodMatched;
        }

        // ─── Event Handlers ───────────────────────────────────────────────────

        private void HandleFoodMatched(FoodItemData food, int customerID)
        {
            if (_isLevelOver) return;
            _foodDelivered++;
        }

        private void HandleCustomerComplete(int customerID)
        {
            if (_isLevelOver) return;

            _customersCompleted++;

            Debug.Log($"[LevelProgressTracker] Khách {customerID} hoàn thành. " +
                      $"{_customersCompleted}/{_totalCustomers}");

            // Kiểm tra Win
            if (_customersCompleted >= _totalCustomers)
            {
                TriggerWin();
            }
        }

        private void HandleBackupFull()
        {
            if (_isLevelOver) return;
            TriggerLose();
        }

        // ─── Win / Lose ───────────────────────────────────────────────────────

        private void TriggerWin()
        {
            if (_isLevelOver) return;
            _isLevelOver = true;

            // Mở khóa level tiếp theo
            SaveManager.UnlockNextLevel(_currentLevel.levelIndex);

            // Delay nhỏ trước khi phát event để animation kịp chạy
            Invoke(nameof(RaiseWinEvent), GameConstants.WIN_SEQUENCE_DELAY);
        }

        private void RaiseWinEvent()
        {
            EventBus.RaiseLevelWin(_currentLevel.levelIndex);
        }

        private void TriggerLose()
        {
            if (_isLevelOver) return;
            _isLevelOver = true;

            Debug.Log($"[LevelProgressTracker] LOSE! Level {_currentLevel.levelIndex}.");
            EventBus.RaiseLevelLose(_currentLevel.levelIndex);
        }

        // ─── Cleanup ──────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        // ─── Debug ────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        [ContextMenu("Debug: Force Win")]
        private void DebugForceWin() => TriggerWin();

        [ContextMenu("Debug: Force Lose")]
        private void DebugForceLose() => TriggerLose();

        [ContextMenu("Debug: Print Status")]
        private void DebugPrintStatus()
        {
            Debug.Log($"[LevelProgressTracker] Status:\n" +
                      $"  Level: {_currentLevel?.levelIndex}\n" +
                      $"  Khách: {_customersCompleted}/{_totalCustomers}\n" +
                      $"  Món đã giao: {_foodDelivered}/{_totalFoodOnTray}\n" +
                      $"  Progress: {Progress:P0}\n" +
                      $"  IsOver: {_isLevelOver}");
        }
#endif
    }
}