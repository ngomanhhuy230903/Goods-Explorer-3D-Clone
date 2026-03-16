using UnityEngine;

namespace FoodMatch.Data
{
    /// <summary>
    /// ScriptableObject định nghĩa 1 booster.
    /// boosterName phải KHỚP CHÍNH XÁC với IBooster.BoosterName của class tương ứng.
    ///
    /// SO chịu trách nhiệm:  unlock condition + visual + config + GIÁ COIN
    /// SaveManager chịu trách nhiệm: quantity runtime
    /// BoosterManager chịu trách nhiệm: execution logic
    /// CoinManager chịu trách nhiệm: thanh toán khi mua
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

        [Header("─── Coin Cost ───────────────────────")]
        [Tooltip("Số coin cần để MUA thêm 1 lượt booster này.\n" +
                 "0 = booster miễn phí (không cho mua thêm bằng coin).")]
        [Min(0)]
        public int coinCost = 20;

        [Tooltip("Số lượng booster nhận được khi mua 1 lần bằng coin.\n" +
                 "Thường bằng rewardQuantity, nhưng có thể config riêng (ví dụ: mua 1 tặng thêm 1).")]
        [Min(1)]
        public int coinPurchaseAmount = 1;

        // ── Runtime helpers ───────────────────────────────────────────────────

        /// <summary>Key lưu vào PlayerPrefs: "Booster_Qty_AddSlot"</summary>
        public string QuantityPrefKey => $"Booster_Qty_{boosterName}";

        /// <summary>Key lưu vào PlayerPrefs để biết đã unlock chưa.</summary>
        public string UnlockedPrefKey => $"Booster_Unlocked_{boosterName}";

        public bool IsUnlocked(int currentLevel) => currentLevel >= requiredLevel;

        /// <summary>Có thể mua bằng coin không (coinCost > 0).</summary>
        public bool IsPurchasable => coinCost > 0;
    }
}