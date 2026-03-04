using System;
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Core
{
    /// <summary>
    /// Event Bus trung tâm - Observer Pattern.
    /// Tất cả script đều giao tiếp qua đây thay vì gọi nhau trực tiếp.
    /// Dùng: EventBus.OnFoodSelected?.Invoke(foodItem);
    /// </summary>
    public static class EventBus
    {
        // ─── Food Events ──────────────────────────────────────────────────────
        /// <summary>Khi người chơi chạm/click vào 1 món ăn.</summary>
        public static event Action<FoodItemData> OnFoodSelected;

        /// <summary>Khi 1 món ăn khớp đúng với order của khách.</summary>
        public static event Action<FoodItemData, int> OnFoodMatchedCustomer; // food, customerID

        /// <summary>Khi 1 món ăn bị đẩy vào khay thừa (không match).</summary>
        public static event Action<FoodItemData> OnFoodSentToBackup;

        // ─── Customer Events ──────────────────────────────────────────────────
        /// <summary>Khi 1 khách hàng đã nhận đủ toàn bộ order.</summary>
        public static event Action<int> OnCustomerOrderComplete; // customerID

        /// <summary>Khi 1 khách hàng mới bước vào.</summary>
        public static event Action<int> OnCustomerArrived; // customerID

        // ─── Tray Events ──────────────────────────────────────────────────────
        /// <summary>Khi khay thừa gần đầy (còn 1-2 ô trống). Trigger cảnh báo.</summary>
        public static event Action<int, int> OnBackupTrayWarning; // currentCount, maxCapacity

        /// <summary>Khi khay thừa ĐẦY hoàn toàn → THUA.</summary>
        public static event Action OnBackupTrayFull;

        /// <summary>Khi item booster +1 khay được dùng thành công.</summary>
        public static event Action<int> OnBackupTrayExpanded; // newCapacity

        // ─── Game State Events ────────────────────────────────────────────────
        /// <summary>Bắt đầu load một level mới.</summary>
        public static event Action<int> OnLevelStarted; // levelIndex

        /// <summary>Người chơi THẮNG level.</summary>
        public static event Action<int> OnLevelWin; // levelIndex

        /// <summary>Người chơi THUA level.</summary>
        public static event Action<int> OnLevelLose; // levelIndex

        /// <summary>Người chơi bấm Pause.</summary>
        public static event Action OnGamePaused;

        /// <summary>Người chơi bấm Resume.</summary>
        public static event Action OnGameResumed;

        // ─── Booster Events ───────────────────────────────────────────────────
        /// <summary>Khi 1 booster được kích hoạt.</summary>
        public static event Action<string> OnBoosterActivated; // boosterType name

        // ─── Invoke Helpers ───────────────────────────────────────────────────
        // Các hàm static để gọi event an toàn (null-check tự động)

        public static void RaiseFoodSelected(FoodItemData food)
            => OnFoodSelected?.Invoke(food);

        public static void RaiseFoodMatched(FoodItemData food, int customerID)
            => OnFoodMatchedCustomer?.Invoke(food, customerID);

        public static void RaiseFoodToBackup(FoodItemData food)
            => OnFoodSentToBackup?.Invoke(food);

        public static void RaiseCustomerOrderComplete(int customerID)
            => OnCustomerOrderComplete?.Invoke(customerID);

        public static void RaiseCustomerArrived(int customerID)
            => OnCustomerArrived?.Invoke(customerID);

        public static void RaiseBackupWarning(int current, int max)
            => OnBackupTrayWarning?.Invoke(current, max);

        public static void RaiseBackupFull()
            => OnBackupTrayFull?.Invoke();

        public static void RaiseLevelStarted(int index)
            => OnLevelStarted?.Invoke(index);

        public static void RaiseLevelWin(int index)
            => OnLevelWin?.Invoke(index);

        public static void RaiseLevelLose(int index)
            => OnLevelLose?.Invoke(index);

        public static void RaiseGamePaused()
            => OnGamePaused?.Invoke();

        public static void RaiseGameResumed()
            => OnGameResumed?.Invoke();

        public static void RaiseBoosterActivated(string boosterName)
            => OnBoosterActivated?.Invoke(boosterName);

        /// <summary>
        /// Dọn sạch toàn bộ subscriber khi load scene mới.
        /// Gọi hàm này trong GameManager.OnDestroy() hoặc khi reset game.
        /// </summary>
        public static void ClearAllEvents()
        {
            OnFoodSelected = null;
            OnFoodMatchedCustomer = null;
            OnFoodSentToBackup = null;
            OnCustomerOrderComplete = null;
            OnCustomerArrived = null;
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