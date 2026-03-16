using UnityEngine;

namespace FoodMatch.Data
{
    /// <summary>
    /// ScriptableObject chứa toàn bộ cấu hình cho HP và Coin.
    ///
    /// Đặt tại: Assets/ScriptableObjects/Currency/PlayerCurrencyConfig.asset
    /// (Tạo folder Currency trong ScriptableObjects là hoàn toàn ổn và được khuyến khích.)
    ///
    /// Giá coin của từng Booster được cấu hình RIÊNG trong từng BoosterData SO
    /// (field coinCost) để mỗi booster có thể có giá khác nhau.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerCurrencyConfig",
                     menuName = "FoodMatch/Currency/Player Currency Config")]
    public class PlayerCurrencyConfig : ScriptableObject
    {
        [Header("─── HP Settings ───────────────────────────────────")]
        [Tooltip("HP tối đa người chơi có thể có.")]
        [Range(1, 20)]
        public int maxHP = 5;

        [Tooltip("Thời gian hồi 1 HP (giây). Mặc định 20 phút = 1200s.")]
        [Min(60)]
        public float hpRegenIntervalSeconds = 1200f;

        [Tooltip("HP hồi mỗi lần (thường là 1).")]
        [Range(1, 5)]
        public int hpRegenAmount = 1;

        [Tooltip("HP bị trừ khi thua ván đấu.")]
        [Range(1, 3)]
        public int hpCostOnLose = 1;

        [Tooltip("HP bị trừ khi bỏ ván giữa chừng (Quit).")]
        [Range(1, 3)]
        public int hpCostOnQuit = 1;

        [Header("─── Coin Settings ──────────────────────────────────")]
        [Tooltip("Coin thưởng khi thắng ván đấu bình thường.")]
        [Min(1)]
        public int coinRewardOnWin = 50;

        [Tooltip("Hệ số nhân coin khi xem ads (Double Reward).")]
        [Range(1f, 5f)]
        public float adsCoinMultiplier = 2f;

        [Tooltip("Chi phí coin để hồi sinh khi thua (Revive). 0 = vô hiệu tính năng.")]
        [Min(0)]
        public int reviveCost = 30;
    }
}