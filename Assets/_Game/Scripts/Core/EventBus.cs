using System;
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Core
{
    /// <summary>
    /// Event Bus trung tâm - Observer Pattern.
    /// Tất cả script đều giao tiếp qua đây thay vì gọi nhau trực tiếp.
    /// </summary>
    public static class EventBus
    {
        // ─── Food Events ──────────────────────────────────────────────────────
        public static event Action<FoodItemData> OnFoodSelected;
        public static event Action<FoodItemData, int> OnFoodMatchedCustomer;
        public static event Action<FoodItemData> OnFoodSentToBackup;

        // ─── Order Events ─────────────────────────────────────────────────────
        public static event Action<int> OnOrderCompleted;
        public static event Action<int> OnOrderLeft;
        public static event Action OnAllOrdersCompleted;

        /// <summary>
        /// Raised khi 1 OrderTray mới trở thành Active (sau enter animation).
        /// FoodFlowController lắng nghe để tự động scan BackupTray tìm match.
        /// Param: foodID của order mới đó.
        /// </summary>
        public static event Action<int> OnNewOrderActive;

        // ─── Tray Events ──────────────────────────────────────────────────────
        public static event Action<int, int> OnBackupTrayWarning;
        public static event Action OnBackupTrayFull;
        public static event Action<int> OnBackupTrayExpanded;

        // ─── Game State Events ────────────────────────────────────────────────
        public static event Action<int> OnLevelStarted;
        public static event Action<int> OnLevelWin;
        public static event Action<int> OnLevelLose;
        public static event Action OnGamePaused;
        public static event Action OnGameResumed;

        // ─── Booster Events ───────────────────────────────────────────────────
        public static event Action<string> OnBoosterActivated;

        // ─── Raise Helpers ────────────────────────────────────────────────────
        public static void RaiseFoodSelected(FoodItemData food) => OnFoodSelected?.Invoke(food);
        public static void RaiseFoodMatched(FoodItemData food, int id) => OnFoodMatchedCustomer?.Invoke(food, id);
        public static void RaiseFoodToBackup(FoodItemData food) => OnFoodSentToBackup?.Invoke(food);
        public static void RaiseOrderCompleted(int trayIndex) => OnOrderCompleted?.Invoke(trayIndex);
        public static void RaiseOrderLeft(int trayIndex) => OnOrderLeft?.Invoke(trayIndex);
        public static void RaiseAllOrdersCompleted() => OnAllOrdersCompleted?.Invoke();

        /// <summary>Gọi bởi OrderTray khi state chuyển sang Active (enter animation xong).</summary>
        public static void RaiseNewOrderActive(int foodID) => OnNewOrderActive?.Invoke(foodID);

        public static void RaiseBackupExpanded(int newCapacity) => OnBackupTrayExpanded?.Invoke(newCapacity);
        public static void RaiseBackupWarning(int current, int max) => OnBackupTrayWarning?.Invoke(current, max);
        public static void RaiseBackupFull() => OnBackupTrayFull?.Invoke();
        public static void RaiseLevelStarted(int index) => OnLevelStarted?.Invoke(index);
        public static void RaiseLevelWin(int index) => OnLevelWin?.Invoke(index);
        public static void RaiseLevelLose(int index) => OnLevelLose?.Invoke(index);
        public static void RaiseGamePaused() => OnGamePaused?.Invoke();
        public static void RaiseGameResumed() => OnGameResumed?.Invoke();
        public static void RaiseBoosterActivated(string name) => OnBoosterActivated?.Invoke(name);

        public static void ClearAllEvents()
        {
            OnFoodSelected = null;
            OnFoodMatchedCustomer = null;
            OnFoodSentToBackup = null;
            OnOrderCompleted = null;
            OnOrderLeft = null;
            OnAllOrdersCompleted = null;
            OnNewOrderActive = null;
            OnBackupTrayWarning = null;
            OnBackupTrayFull = null;
            OnBackupTrayExpanded = null;
            OnLevelStarted = null;
            OnLevelWin = null;
            OnLevelLose = null;
            OnGamePaused = null;
            OnGameResumed = null;
            OnBoosterActivated = null;

            Debug.Log("[EventBus] Tất cả events đã được dọn sạch.");
        }
    }
}