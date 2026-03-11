using UnityEngine;

namespace FoodMatch.Items
{
    /// <summary>
    /// ScriptableObject định nghĩa 1 booster.
    /// boosterName phải KHỚP CHÍNH XÁC với IBooster.BoosterName của class tương ứng.
    /// 
    /// SO chịu trách nhiệm:  unlock condition + visual + config
    /// SaveManager chịu trách nhiệm: quantity runtime
    /// BoosterManager chịu trách nhiệm: execution logic
    /// </summary>
    [CreateAssetMenu(fileName = "BoosterData_", menuName = "FoodMatch/Booster Data")]
    public class BoosterData : ScriptableObject
    {
        [Header("─── Identity (phải khớp IBooster.BoosterName) ───")]
        [Tooltip("Phải trùng với BoosterName trong class IBooster.")]
        public string boosterName;

        [Header("─── Unlock ──────────────────────────")]
        [Tooltip("Người chơi đạt level này thì mở khóa booster.")]
        public int requiredLevel = 1;

        [Header("─── Visual ──────────────────────────")]
        public Sprite icon;
        public string displayName;
        [TextArea] public string description;

        [Header("─── Quantity Config ─────────────────")]
        [Tooltip("Số lượng ban đầu khi mở khóa lần đầu.")]
        public int initialQuantity = 1;

        [Tooltip("Số lượng tối đa có thể giữ.")]
        public int maxQuantity = 999;

        [Tooltip("Số lượng nhận thêm mỗi khi mua/reward.")]
        public int rewardQuantity = 1;

        // ── Runtime helpers ───────────────────────────────────────────────────

        /// <summary>Key lưu vào PlayerPrefs: "Booster_Qty_AddSlot"</summary>
        public string QuantityPrefKey => $"Booster_Qty_{boosterName}";

        /// <summary>Key lưu vào PlayerPrefs để biết đã unlock chưa.</summary>
        public string UnlockedPrefKey => $"Booster_Unlocked_{boosterName}";

        public bool IsUnlocked(int currentLevel) => currentLevel >= requiredLevel;
    }
}