using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Core;
using FoodMatch.Managers;

namespace FoodMatch.Level
{
    /// <summary>
    /// Theo dõi tiến trình trong 1 level đang chạy.
    ///   - Đếm số order đã hoàn thành
    ///   - Lắng nghe BackupTray đầy → Lose
    ///   - Lắng nghe AllOrdersCompleted → Win
    /// Gắn vào cùng GameObject "LevelManager".
    /// </summary>
    public class LevelProgressTracker : MonoBehaviour
    {
        // ─── Runtime State ────────────────────────────────────────────────────
        private LevelConfig _currentConfig;
        private int _totalOrders;
        private int _ordersCompleted;
        private int _foodDelivered;
        private bool _isLevelOver;

        // ─── Public Properties ────────────────────────────────────────────────
        public int OrdersCompleted => _ordersCompleted;
        public int TotalOrders => _totalOrders;
        public int FoodDelivered => _foodDelivered;
        public bool IsLevelOver => _isLevelOver;

        public float Progress => _totalOrders == 0
            ? 0f
            : (float)_ordersCompleted / _totalOrders;

        // ─── Init ─────────────────────────────────────────────────────────────

        public void Initialize(LevelConfig config)
        {
            _currentConfig = config;
            _isLevelOver = false;
            _foodDelivered = 0;
            _ordersCompleted = 0;

            _totalOrders = config.totalFoodCount / GameConstants.FOOD_SET_SIZE;

            Debug.Log($"[LevelProgressTracker] Level {config.levelIndex} bắt đầu. " +
                      $"Tổng món: {config.totalFoodCount} | " +
                      $"Tổng order: {_totalOrders}");

            UnsubscribeEvents();
            SubscribeEvents();
        }

        // ─── Events ───────────────────────────────────────────────────────────

        private void SubscribeEvents()
        {
            // Dùng đúng tên event trong EventBus hiện tại
            EventBus.OnAllOrdersCompleted += HandleAllOrdersCompleted;
            EventBus.OnBackupTrayFull += HandleBackupFull;
            EventBus.OnFoodMatchedCustomer += HandleFoodMatched;
            EventBus.OnOrderCompleted += HandleOrderCompleted;
        }

        private void UnsubscribeEvents()
        {
            EventBus.OnAllOrdersCompleted -= HandleAllOrdersCompleted;
            EventBus.OnBackupTrayFull -= HandleBackupFull;
            EventBus.OnFoodMatchedCustomer -= HandleFoodMatched;
            EventBus.OnOrderCompleted -= HandleOrderCompleted;
        }

        // ─── Handlers ─────────────────────────────────────────────────────────

        private void HandleFoodMatched(FoodItemData food, int orderIndex)
        {
            if (_isLevelOver) return;
            _foodDelivered++;
        }

        private void HandleOrderCompleted(int trayIndex)
        {
            if (_isLevelOver) return;
            _ordersCompleted++;

            Debug.Log($"[LevelProgressTracker] Order {trayIndex} hoàn thành. " +
                      $"{_ordersCompleted}/{_totalOrders}");
        }

        /// <summary>
        /// OrderQueue báo tất cả order xong → Win.
        /// </summary>
        private void HandleAllOrdersCompleted()
        {
            if (_isLevelOver) return;
            TriggerWin();
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

            SaveManager.UnlockNextLevel(_currentConfig.levelIndex);

            // Delay nhỏ để animation kịp chạy trước khi show popup
            Invoke(nameof(RaiseWinEvent), GameConstants.WIN_SEQUENCE_DELAY);
        }

        private void RaiseWinEvent()
        {
            EventBus.RaiseLevelWin(_currentConfig.levelIndex);
        }

        private void TriggerLose()
        {
            if (_isLevelOver) return;
            _isLevelOver = true;

            Debug.Log($"[LevelProgressTracker] LOSE! Level {_currentConfig.levelIndex}.");
            EventBus.RaiseLevelLose(_currentConfig.levelIndex);
        }

        // ─── Cleanup ──────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }
    }
}