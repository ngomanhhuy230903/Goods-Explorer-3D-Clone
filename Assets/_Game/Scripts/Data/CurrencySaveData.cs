using System;

namespace FoodMatch.Data
{
    /// <summary>
    /// Dữ liệu HP được lưu qua SaveManager.
    /// Không serialize trực tiếp – dùng wrapper CurrencySaveData.
    /// </summary>
    [Serializable]
    public class HPSaveData
    {
        /// <summary>HP hiện tại.</summary>
        public int currentHP;

        /// <summary>
        /// Thời điểm bắt đầu đếm hồi HP (Unix timestamp UTC, tính bằng giây).
        /// Khi currentHP == maxHP → bằng -1 (không cần đếm).
        /// </summary>
        public long regenStartTimestamp;

        public HPSaveData(int hp, long timestamp)
        {
            currentHP = hp;
            regenStartTimestamp = timestamp;
        }
    }

    /// <summary>
    /// Dữ liệu Coin được mã hóa lưu vào file JSON.
    /// </summary>
    [Serializable]
    public class CoinSaveData
    {
        /// <summary>Số coin hiện tại (raw, trước khi mã hóa).</summary>
        public long coins;

        /// <summary>Checksum đơn giản để detect giả mạo.</summary>
        public string checksum;

        public CoinSaveData(long coins)
        {
            this.coins = coins;
            this.checksum = ComputeChecksum(coins);
        }

        public bool IsValid() => checksum == ComputeChecksum(coins);

        private static string ComputeChecksum(long value)
        {
            // XOR đơn giản với salt, đủ để chống chỉnh sửa thủ công
            const ulong SALT = 0xDEADBEEF_CAFEBABE;
            long hashed = (long)((ulong)value ^ SALT);
            return hashed.ToString("X16");
        }
    }

    /// <summary>
    /// Wrapper tổng hợp cả HP và Coin để SaveManager lưu một file duy nhất.
    /// </summary>
    [Serializable]
    public class CurrencySaveData
    {
        public HPSaveData hp;
        public CoinSaveData coin;
    }
}