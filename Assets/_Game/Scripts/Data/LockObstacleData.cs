// LockObstacleData.cs
using UnityEngine;

namespace FoodMatch.Data
{
    /// <summary>
    /// Data cho Obstacle 1: FoodTray Lock.
    /// Cấu hình số lượng tray bị khóa và HP của từng khóa.
    /// </summary>
    [System.Serializable]
    public class LockObstacleData : ObstacleData
    {
        public override string ObstacleName => "FoodTray Lock";

        [Header("─── Lock Settings ────────────────────")]
        [Tooltip("Số lượng FoodTray bị khóa ngẫu nhiên lúc bắt đầu level.")]
        [Min(1)]
        public int lockedTrayCount = 2;

        [Tooltip("HP mặc định của mỗi khóa (số order cần hoàn thành để phá 1 khóa).")]
        [Min(1)]
        public int defaultLockHp = 3;

        [Tooltip("Override HP riêng cho từng tray theo index (để trống = dùng defaultLockHp).\n" +
                 "VD: [3, 5, 2] → tray 0 HP=3, tray 1 HP=5, tray 2 HP=2.")]
        public int[] perTrayHpOverride = System.Array.Empty<int>();

        // ─── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Lấy HP cho tray thứ i.
        /// Nếu có override tại index i → dùng override, ngược lại dùng defaultLockHp.
        /// </summary>
        public int GetHpForTray(int trayIndex)
        {
            if (perTrayHpOverride != null
                && trayIndex < perTrayHpOverride.Length
                && perTrayHpOverride[trayIndex] > 0)
                return perTrayHpOverride[trayIndex];

            return defaultLockHp;
        }

        // ─── Validation ───────────────────────────────────────────────────────

        public override bool IsValid()
        {
            if (lockedTrayCount <= 0)
            {
                UnityEngine.Debug.LogError("[LockObstacleData] lockedTrayCount phải >= 1!");
                return false;
            }
            if (defaultLockHp <= 0)
            {
                UnityEngine.Debug.LogError("[LockObstacleData] defaultLockHp phải >= 1!");
                return false;
            }
            return true;
        }

        public override ObstacleData Clone()
        {
            var clonedOverrides = perTrayHpOverride != null
                ? (int[])perTrayHpOverride.Clone()
                : System.Array.Empty<int>();

            return new LockObstacleData
            {
                isEnabled = this.isEnabled,
                lockedTrayCount = this.lockedTrayCount,
                defaultLockHp = this.defaultLockHp,
                perTrayHpOverride = clonedOverrides
            };
        }
    }
}