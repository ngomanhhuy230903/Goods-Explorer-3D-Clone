using System;
using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Core;

namespace FoodMatch.Managers
{
    /// <summary>
    /// Quản lý hệ thống HP.
    /// - Hồi HP mỗi N giây (theo thời gian mạng nếu có, máy nếu offline).
    /// - Trừ HP khi thua / bỏ ván.
    /// - Lắng nghe GameState để tự động trừ HP đúng lúc.
    /// Attach lên cùng GameObject với GameManager (hoặc một Manager root riêng).
    /// </summary>
    public class HPManager : MonoBehaviour
    {
        public static HPManager Instance { get; private set; }

        [Header("Config (kéo SO vào đây)")]
        [SerializeField] private PlayerCurrencyConfig config;

        // ─── Runtime State ────────────────────────────────────────────────────
        public int CurrentHP { get; private set; }
        public int MaxHP => config != null ? config.maxHP : 5;

        /// <summary>
        /// Thời gian còn lại (giây) trước khi hồi 1 HP tiếp theo.
        /// Bằng 0 khi HP đã đầy.
        /// </summary>
        public float SecondsUntilNextRegen { get; private set; }

        // Timestamp UTC (giây) lúc bắt đầu đếm hồi cho HP đầu tiên đang thiếu.
        private long _regenStartTimestamp;
        private bool _isRegening;

        // Tránh trừ HP 2 lần trong cùng 1 ván (Lose + quit callback)
        private bool _hpDeductedThisSession;

        // ─── Events ───────────────────────────────────────────────────────────
        /// <summary>Raised mỗi khi HP thay đổi. Params: (current, max).</summary>
        public static event Action<int, int> OnHPChanged;

        /// <summary>Raised khi hết HP (current == 0).</summary>
        public static event Action OnHPEmpty;

        /// <summary>Raised khi HP đầy trở lại.</summary>
        public static event Action OnHPFull;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            LoadAndRecalculate();
            GameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDestroy()
        {
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void Update()
        {
            if (_isRegening)
                TickRegen();
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>Kiểm tra xem người chơi có đủ HP để bắt đầu ván không.</summary>
        public bool HasHPToPlay() => CurrentHP >= 1;

        /// <summary>
        /// Trừ HP (dùng khi thua hoặc quit).
        /// Gọi từ bên ngoài nếu cần, nhưng thường tự động qua GameState.
        /// </summary>
        public void DeductHP(int amount = 1)
        {
            if (_hpDeductedThisSession) return;
            _hpDeductedThisSession = true;

            bool wasFull = CurrentHP >= MaxHP;
            CurrentHP = Mathf.Max(0, CurrentHP - amount);

            if (wasFull && CurrentHP < MaxHP)
                StartRegenTimer();

            Save();
            OnHPChanged?.Invoke(CurrentHP, MaxHP);

            if (CurrentHP <= 0)
                OnHPEmpty?.Invoke();

            Debug.Log($"[HPManager] HP trừ {amount} → {CurrentHP}/{MaxHP}");
        }

        /// <summary>Cộng HP (ví dụ: mua thêm bằng coin, xem ads).</summary>
        public void AddHP(int amount = 1)
        {
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
            if (CurrentHP >= MaxHP)
                StopRegenTimer();
            Save();
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
            if (CurrentHP >= MaxHP)
                OnHPFull?.Invoke();
        }

        // ─── Internal ─────────────────────────────────────────────────────────

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Play:
                    _hpDeductedThisSession = false; // reset flag khi bắt đầu ván mới
                    break;

                    // Lose: KHÔNG trừ HP ở đây.
                    // Người chơi còn cơ hội revive (popup 1, 2).
                    // HP chỉ bị trừ khi xác nhận bỏ ván:
                    //   • Retry   → GameResultUI.OnClickRetry()        → DeductHP()
                    //   • Go Home → GameResultUI.OnClickCloseToHome()  → DeductHP()
            }
        }

        /// <summary>Trừ HP khi người chơi xác nhận bỏ ván (Retry hoặc Go Home).</summary>
        public void DeductHPOnQuit()
        {
            DeductHP(config != null ? config.hpCostOnQuit : 1);
        }

        // ─── Regen Timer ──────────────────────────────────────────────────────

        private void StartRegenTimer()
        {
            _regenStartTimestamp = GetCurrentTimestamp();
            _isRegening = true;
            RecalcSecondsUntilRegen();
        }

        private void StopRegenTimer()
        {
            _isRegening = false;
            _regenStartTimestamp = -1;
            SecondsUntilNextRegen = 0;
        }

        private void TickRegen()
        {
            long now = GetCurrentTimestamp();
            float interval = config != null ? config.hpRegenIntervalSeconds : 1200f;
            int regenAmount = config != null ? config.hpRegenAmount : 1;

            long elapsed = now - _regenStartTimestamp;
            int hpToRegen = (int)(elapsed / interval);

            if (hpToRegen > 0)
            {
                int newHP = Mathf.Min(MaxHP, CurrentHP + hpToRegen * regenAmount);
                if (newHP != CurrentHP)
                {
                    CurrentHP = newHP;
                    // Cập nhật timestamp để tính từ điểm đã hồi
                    _regenStartTimestamp += (long)(hpToRegen * interval);
                    Save();
                    OnHPChanged?.Invoke(CurrentHP, MaxHP);
                    Debug.Log($"[HPManager] HP hồi → {CurrentHP}/{MaxHP}");
                }

                if (CurrentHP >= MaxHP)
                {
                    StopRegenTimer();
                    OnHPFull?.Invoke();
                    return;
                }
            }

            RecalcSecondsUntilRegen();
        }

        private void RecalcSecondsUntilRegen()
        {
            float interval = config != null ? config.hpRegenIntervalSeconds : 1200f;
            long now = GetCurrentTimestamp();
            long elapsed = now - _regenStartTimestamp;
            SecondsUntilNextRegen = Mathf.Max(0f, interval - (elapsed % (long)interval));
        }

        // ─── Load / Save ──────────────────────────────────────────────────────

        private void LoadAndRecalculate()
        {
            var data = CurrencySaveManager.LoadHP();
            if (data == null)
            {
                // Lần đầu chạy
                CurrentHP = MaxHP;
                _regenStartTimestamp = -1;
                _isRegening = false;
                Save();
                OnHPChanged?.Invoke(CurrentHP, MaxHP);
                return;
            }

            CurrentHP = Mathf.Clamp(data.currentHP, 0, MaxHP);
            _regenStartTimestamp = data.regenStartTimestamp;

            if (CurrentHP < MaxHP && _regenStartTimestamp > 0)
            {
                // Tính HP đã hồi trong lúc offline/tắt app
                float interval = config != null ? config.hpRegenIntervalSeconds : 1200f;
                int regenAmount = config != null ? config.hpRegenAmount : 1;
                long elapsed = GetCurrentTimestamp() - _regenStartTimestamp;
                int hpToRegen = (int)(elapsed / interval);

                if (hpToRegen > 0)
                {
                    CurrentHP = Mathf.Min(MaxHP, CurrentHP + hpToRegen * regenAmount);
                    _regenStartTimestamp += (long)(hpToRegen * interval);
                }

                _isRegening = CurrentHP < MaxHP;
                if (!_isRegening) StopRegenTimer();
                else RecalcSecondsUntilRegen();
            }
            else
            {
                _isRegening = false;
            }

            Save();
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
            Debug.Log($"[HPManager] Loaded HP={CurrentHP}/{MaxHP}, regen={_isRegening}");
        }

        private void Save()
        {
            CurrencySaveManager.SaveHP(new HPSaveData(CurrentHP, _regenStartTimestamp));
        }

        // ─── Timestamp Utility ────────────────────────────────────────────────

        /// <summary>
        /// Trả về Unix timestamp UTC (giây).
        /// Dùng DateTime.UtcNow – sẽ tích hợp NTP sau nếu cần.
        /// </summary>
        private static long GetCurrentTimestamp()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}