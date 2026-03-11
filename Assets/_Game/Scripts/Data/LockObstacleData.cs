// LockObstacleData.cs
using System;
using UnityEngine;

namespace FoodMatch.Data
{
    /// <summary>
    /// Obstacle 1: Khóa FoodTray.
    /// - Số lượng tray bị khóa tuỳ chỉnh
    /// - Mỗi tray có HP riêng
    /// - Mỗi order hoàn thành → random phá 1 HP ở tray đang bị khóa
    /// </summary>
    [Serializable]
    public class LockObstacleData : ObstacleData
    {
        public override string ObstacleName => "FoodTray Lock";

        [Header("Lock Settings")]
        [Tooltip("Số FoodTray bị khóa (random chọn tray nào bị khóa lúc runtime).")]
        [Min(1)]
        public int lockedTrayCount = 1;

        [Tooltip("HP mặc định cho mỗi ổ khóa. Mỗi order hoàn thành -1 HP.")]
        [Min(1)]
        public int defaultLockHp = 3;

        [Tooltip("Override HP cho từng tray theo index. Để trống = dùng defaultLockHp.")]
        public int[] perTrayHpOverride = Array.Empty<int>();

        public int GetHpForTray(int trayIndex)
        {
            if (perTrayHpOverride != null
                && trayIndex < perTrayHpOverride.Length
                && perTrayHpOverride[trayIndex] > 0)
                return perTrayHpOverride[trayIndex];
            return defaultLockHp;
        }

        public override bool IsValid()
        {
            if (lockedTrayCount <= 0)
            {
                Debug.LogError("[LockObstacleData] lockedTrayCount phải >= 1");
                return false;
            }
            return true;
        }

        public override ObstacleData Clone() => new LockObstacleData
        {
            isEnabled = isEnabled,
            lockedTrayCount = lockedTrayCount,
            defaultLockHp = defaultLockHp,
            perTrayHpOverride = (int[])perTrayHpOverride?.Clone()
        };
    }
}