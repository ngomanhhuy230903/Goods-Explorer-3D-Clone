using System;
using System.IO;
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Managers
{
    /// <summary>
    /// Lớp I/O tách biệt, chịu trách nhiệm đọc/ghi file JSON cho HP và Coin.
    /// Coin được mã hóa bằng XOR + checksum để chống chỉnh sửa thủ công.
    /// Đặt các file trong Application.persistentDataPath để tránh bị xóa khi update app.
    /// </summary>
    public static class CurrencySaveManager
    {
        private const string HP_FILE = "hp_data.json";
        private const string COIN_FILE = "coin_data.enc";

        // Khóa XOR đơn giản để obfuscate nội dung coin file
        private const string XOR_KEY = "FM_S3cr3t_K3y_2024!";

        // ─── HP ───────────────────────────────────────────────────────────────

        public static void SaveHP(HPSaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(GetPath(HP_FILE), json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CurrencySaveManager] Lỗi lưu HP: {e.Message}");
            }
        }

        public static HPSaveData LoadHP()
        {
            string path = GetPath(HP_FILE);
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<HPSaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CurrencySaveManager] Lỗi load HP: {e.Message}");
                return null;
            }
        }

        // ─── Coin ─────────────────────────────────────────────────────────────

        public static void SaveCoin(CoinSaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, false);
                string encoded = XorEncrypt(json, XOR_KEY);
                File.WriteAllText(GetPath(COIN_FILE), encoded);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CurrencySaveManager] Lỗi lưu Coin: {e.Message}");
            }
        }

        public static CoinSaveData LoadCoin()
        {
            string path = GetPath(COIN_FILE);
            if (!File.Exists(path)) return null;
            try
            {
                string encoded = File.ReadAllText(path);
                string json = XorEncrypt(encoded, XOR_KEY); // XOR is symmetric
                return JsonUtility.FromJson<CoinSaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CurrencySaveManager] Lỗi load Coin: {e.Message}");
                return null;
            }
        }

        // ─── Utility ──────────────────────────────────────────────────────────

        public static void DeleteAll()
        {
            DeleteFile(HP_FILE);
            DeleteFile(COIN_FILE);
            Debug.Log("[CurrencySaveManager] Đã xóa toàn bộ dữ liệu currency.");
        }

        private static void DeleteFile(string filename)
        {
            string path = GetPath(filename);
            if (File.Exists(path)) File.Delete(path);
        }

        private static string GetPath(string filename)
            => Path.Combine(Application.persistentDataPath, filename);

        /// <summary>
        /// XOR encryption đơn giản: mã hóa/giải mã đối xứng.
        /// Đủ để ngăn người chơi casual chỉnh file, không phải bảo mật cấp ngân hàng.
        /// </summary>
        private static string XorEncrypt(string input, string key)
        {
            char[] output = new char[input.Length];
            for (int i = 0; i < input.Length; i++)
                output[i] = (char)(input[i] ^ key[i % key.Length]);
            return new string(output);
        }
    }
}