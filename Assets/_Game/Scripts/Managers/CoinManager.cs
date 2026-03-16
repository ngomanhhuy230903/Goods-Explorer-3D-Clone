using System;
using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Core;

namespace FoodMatch.Managers
{
    /// <summary>
    /// Quản lý hệ thống Coin.
    /// - Coin lưu dưới dạng JSON mã hóa (checksum).
    /// - Thưởng coin khi thắng, nhân đôi khi xem ads.
    /// - Tiêu coin mua booster hoặc hồi sinh.
    /// </summary>
    public class CoinManager : MonoBehaviour
    {
        public static CoinManager Instance { get; private set; }

        [Header("Config (kéo SO vào đây)")]
        [SerializeField] private PlayerCurrencyConfig config;

        // ─── Runtime State ────────────────────────────────────────────────────
        public long CurrentCoins { get; private set; }

        // Lưu tạm coin thưởng ván vừa thắng để nhân đôi nếu xem ads
        private long _pendingWinReward;

        // ─── Events ───────────────────────────────────────────────────────────
        /// <summary>Raised khi số coin thay đổi. Param: số coin mới.</summary>
        public static event Action<long> OnCoinChanged;

        /// <summary>Raised khi không đủ coin. Param: số coin thiếu.</summary>
        public static event Action<long> OnInsufficientCoins;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            Load();
            GameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDestroy()
        {
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>Thêm coin (win reward, gift, v.v.).</summary>
        public void AddCoins(long amount)
        {
            if (amount <= 0) return;
            CurrentCoins += amount;
            Save();
            OnCoinChanged?.Invoke(CurrentCoins);
            Debug.Log($"[CoinManager] +{amount} coin → {CurrentCoins}");
        }

        /// <summary>
        /// Trừ coin nếu đủ. Trả về true nếu thành công.
        /// </summary>
        public bool SpendCoins(long amount)
        {
            if (amount <= 0) return true;
            if (CurrentCoins < amount)
            {
                OnInsufficientCoins?.Invoke(amount - CurrentCoins);
                Debug.Log($"[CoinManager] Không đủ coin. Cần {amount}, có {CurrentCoins}.");
                return false;
            }
            CurrentCoins -= amount;
            Save();
            OnCoinChanged?.Invoke(CurrentCoins);
            Debug.Log($"[CoinManager] -{amount} coin → {CurrentCoins}");
            return true;
        }

        /// <summary>
        /// Gọi sau khi thắng ván để nhận coin thưởng bình thường.
        /// Coin sẽ được lưu vào _pendingWinReward để có thể nhân đôi bằng DoubleReward().
        /// </summary>
        public void ClaimWinReward()
        {
            long reward = config != null ? config.coinRewardOnWin : 50;
            _pendingWinReward = reward;
            AddCoins(reward);
        }

        /// <summary>
        /// Nhân đôi coin thưởng ván vừa thắng (gọi sau khi xem ads thành công).
        /// Chỉ cộng thêm phần chênh lệch (= reward * multiplier - reward đã cộng).
        /// </summary>
        public void DoubleWinReward()
        {
            if (_pendingWinReward <= 0)
            {
                Debug.LogWarning("[CoinManager] DoubleWinReward: không có pending reward.");
                return;
            }

            float multiplier = config != null ? config.adsCoinMultiplier : 2f;
            long bonus = (long)(_pendingWinReward * multiplier) - _pendingWinReward;
            _pendingWinReward = 0;

            AddCoins(bonus);
            Debug.Log($"[CoinManager] Double reward: +{bonus} coin (x{multiplier})");
        }

        /// <summary>
        /// Thử mua booster bằng coin.
        /// Trả về true nếu đủ tiền và đã trừ.
        /// </summary>
        public bool TryPurchaseBooster(string boosterName, long cost)
        {
            if (!SpendCoins(cost)) return false;
            Debug.Log($"[CoinManager] Mua booster [{boosterName}] giá {cost} coin.");
            return true;
        }

        /// <summary>
        /// Thử hồi sinh khi thua ván.
        /// Nếu đủ coin → trừ và trả true, caller tự xử lý revive logic.
        /// </summary>
        public bool TryRevive()
        {
            long cost = config != null ? config.reviveCost : 30;
            if (!SpendCoins(cost)) return false;
            Debug.Log($"[CoinManager] Hồi sinh thành công. Trừ {cost} coin.");
            return true;
        }

        public long GetReviveCost() => config != null ? config.reviveCost : 30;
        public long GetWinReward() => config != null ? config.coinRewardOnWin : 50;
        public float GetAdsMultiplier() => config != null ? config.adsCoinMultiplier : 2f;

        // ─── Internal ─────────────────────────────────────────────────────────

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.Win)
                ClaimWinReward();
        }

        // ─── Load / Save ──────────────────────────────────────────────────────

        private void Load()
        {
            var data = CurrencySaveManager.LoadCoin();
            if (data == null || !data.IsValid())
            {
                if (data != null)
                    Debug.LogWarning("[CoinManager] Checksum không hợp lệ! Reset về 0.");
                CurrentCoins = 0;
                Save();
            }
            else
            {
                CurrentCoins = data.coins;
            }
            OnCoinChanged?.Invoke(CurrentCoins);
            Debug.Log($"[CoinManager] Loaded coins={CurrentCoins}");
        }

        private void Save()
        {
            CurrencySaveManager.SaveCoin(new CoinSaveData(CurrentCoins));
        }
    }
}