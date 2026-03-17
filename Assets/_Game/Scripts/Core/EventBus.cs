using System;
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Core
{
    public static class EventBus
    {
        // ─── Food Events ──────────────────────────────────────────────────────
        public static event Action<FoodItemData> OnFoodSelected;
        public static event Action<FoodItemData, int> OnFoodMatchedCustomer;
        public static event Action<FoodItemData> OnFoodSentToBackup;
        public static event Action<int> OnBufferFoodReady;

        // ─── Order Events ─────────────────────────────────────────────────────
        public static event Action<int> OnOrderCompleted;
        public static event Action<int> OnOrderLeft;
        public static event Action OnAllOrdersCompleted;
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
        public static event Action<string> OnBoosterUnlocked;
        public static event Action<string> OnBoosterOutOfStock;
        public static event Action<string> OnBoosterPurchased;

        // ─── HP Events ────────────────────────────────────────────────────────
        public static event Action<int, int> OnHPChanged;
        public static event Action OnHPEmpty;
        public static event Action OnHPFull;

        // ─── Coin Events ──────────────────────────────────────────────────────
        public static event Action<long> OnCoinChanged;
        public static event Action<long> OnInsufficientCoins;

        // ─── Conveyor Events ──────────────────────────────────────────────────
        public static event Action<int> OnConveyorFoodCollected;

        // ─── Shop Events ──────────────────────────────────────────────────────
        /// <summary>Yêu cầu mở Shop từ bất kỳ nơi nào không có reference tới ShopManager.</summary>
        public static event Action OnOpenShop;

        /// <summary>Yêu cầu đóng Shop.</summary>
        public static event Action OnCloseShop;

        // ─── Raise Helpers ────────────────────────────────────────────────────
        public static void RaiseFoodSelected(FoodItemData food) => OnFoodSelected?.Invoke(food);
        public static void RaiseFoodMatched(FoodItemData food, int id) => OnFoodMatchedCustomer?.Invoke(food, id);
        public static void RaiseFoodToBackup(FoodItemData food) => OnFoodSentToBackup?.Invoke(food);
        public static void RaiseOrderCompleted(int trayIndex) => OnOrderCompleted?.Invoke(trayIndex);
        public static void RaiseOrderLeft(int trayIndex) => OnOrderLeft?.Invoke(trayIndex);
        public static void RaiseAllOrdersCompleted() => OnAllOrdersCompleted?.Invoke();
        public static void RaiseNewOrderActive(int foodID) => OnNewOrderActive?.Invoke(foodID);
        public static void RaiseBackupExpanded(int newCapacity) => OnBackupTrayExpanded?.Invoke(newCapacity);
        public static void RaiseBackupWarning(int current, int max) => OnBackupTrayWarning?.Invoke(current, max);
        public static void RaiseBackupFull() => OnBackupTrayFull?.Invoke();
        public static void RaiseLevelStarted(int index) => OnLevelStarted?.Invoke(index);
        public static void RaiseLevelWin(int index) => OnLevelWin?.Invoke(index);
        public static void RaiseLevelLose(int index) => OnLevelLose?.Invoke(index);
        public static void RaiseGamePaused() => OnGamePaused?.Invoke();
        public static void RaiseGameResumed() => OnGameResumed?.Invoke();
        public static void RaiseBufferFoodReady(int foodID) => OnBufferFoodReady?.Invoke(foodID);
        public static void RaiseBoosterActivated(string name) => OnBoosterActivated?.Invoke(name);
        public static void RaiseBoosterUnlocked(string name) => OnBoosterUnlocked?.Invoke(name);
        public static void RaiseBoosterOutOfStock(string name) => OnBoosterOutOfStock?.Invoke(name);
        public static void RaiseBoosterPurchased(string name) => OnBoosterPurchased?.Invoke(name);
        public static void RaiseConveyorFoodCollected(int foodID) => OnConveyorFoodCollected?.Invoke(foodID);
        public static void RaiseHPChanged(int current, int max) => OnHPChanged?.Invoke(current, max);
        public static void RaiseHPEmpty() => OnHPEmpty?.Invoke();
        public static void RaiseHPFull() => OnHPFull?.Invoke();
        public static void RaiseCoinChanged(long amount) => OnCoinChanged?.Invoke(amount);
        public static void RaiseInsufficientCoins(long shortfall) => OnInsufficientCoins?.Invoke(shortfall);
        public static void RaiseOpenShop() => OnOpenShop?.Invoke();
        public static void RaiseCloseShop() => OnCloseShop?.Invoke();

        // ─── Cleanup ──────────────────────────────────────────────────────────
        public static void ClearAllEvents()
        {
            OnFoodSelected = null;
            OnFoodMatchedCustomer = null;
            OnFoodSentToBackup = null;
            OnBufferFoodReady = null;
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
            OnBoosterUnlocked = null;
            OnBoosterOutOfStock = null;
            OnBoosterPurchased = null;
            OnConveyorFoodCollected = null;
            OnHPChanged = null;
            OnHPEmpty = null;
            OnHPFull = null;
            OnCoinChanged = null;
            OnInsufficientCoins = null;
            OnOpenShop = null;
            OnCloseShop = null;
            Debug.Log("[EventBus] Tất cả events đã được dọn sạch.");
        }
    }
}