using UnityEngine;
using FoodMatch.Managers;

namespace FoodMatch.Items
{
    /// <summary>
    /// Quản lý QUANTITY của từng booster qua PlayerPrefs.
    /// 
    /// Tách biệt hoàn toàn với execution logic (BoosterManager)
    /// và definition (BoosterData SO).
    /// 
    /// Pattern: static class giống SaveManager để truy cập từ mọi nơi.
    /// </summary>
    public static class BoosterInventory
    {
        // ── Quantity ──────────────────────────────────────────────────────────

        public static int GetQuantity(BoosterData data)
        {
            if (data == null) return 0;
            return PlayerPrefs.GetInt(data.QuantityPrefKey, 0);
        }

        public static void SetQuantity(BoosterData data, int value)
        {
            if (data == null) return;
            int clamped = Mathf.Clamp(value, 0, data.maxQuantity);
            PlayerPrefs.SetInt(data.QuantityPrefKey, clamped);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Tiêu thụ 1 lượt dùng. Trả về true nếu thành công.
        /// </summary>
        public static bool TryConsume(BoosterData data, int amount = 1)
        {
            if (data == null) return false;
            int current = GetQuantity(data);
            if (current < amount) return false;
            SetQuantity(data, current - amount);
            return true;
        }

        /// <summary>
        /// Thêm số lượng (mua, nhận reward). Trả về số lượng sau khi thêm.
        /// </summary>
        public static int Add(BoosterData data, int amount)
        {
            if (data == null) return 0;
            int newQty = Mathf.Min(GetQuantity(data) + amount, data.maxQuantity);
            SetQuantity(data, newQty);
            return newQty;
        }

        public static bool HasAny(BoosterData data) => GetQuantity(data) > 0;

        // ── Unlock State ──────────────────────────────────────────────────────

        /// <summary>
        /// Đánh dấu booster đã được unlock và grant initialQuantity.
        /// Chỉ gọi 1 lần khi người chơi đạt requiredLevel.
        /// </summary>
        public static void UnlockAndGrant(BoosterData data)
        {
            if (data == null) return;
            bool alreadyUnlocked = PlayerPrefs.GetInt(data.UnlockedPrefKey, 0) == 1;
            if (alreadyUnlocked) return;

            PlayerPrefs.SetInt(data.UnlockedPrefKey, 1);
            Add(data, data.initialQuantity);
            Debug.Log($"[BoosterInventory] Unlock '{data.boosterName}' → +{data.initialQuantity} (total={GetQuantity(data)})");
        }

        public static bool IsEverUnlocked(BoosterData data) =>
            data != null && PlayerPrefs.GetInt(data.UnlockedPrefKey, 0) == 1;

        // ── Bulk ops ──────────────────────────────────────────────────────────

        /// <summary>
        /// Gọi khi load game: unlock tất cả booster đáng được unlock theo level hiện tại.
        /// Safe to call multiple times (idempotent).
        /// </summary>
        public static void SyncUnlocksByLevel(BoosterDatabase database, int currentLevel)
        {
            if (database == null) return;
            foreach (var data in database.Boosters)
                if (data.IsUnlocked(currentLevel))
                    UnlockAndGrant(data);
        }

        /// <summary>Debug only.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void ResetAll(BoosterDatabase database)
        {
            if (database == null) return;
            foreach (var data in database.Boosters)
            {
                PlayerPrefs.DeleteKey(data.QuantityPrefKey);
                PlayerPrefs.DeleteKey(data.UnlockedPrefKey);
            }
            PlayerPrefs.Save();
            Debug.LogWarning("[BoosterInventory] Reset all booster data.");
        }
    }
}