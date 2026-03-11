using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using FoodMatch.Core;
using FoodMatch.Managers;

namespace FoodMatch.Items
{
    /// <summary>
    /// BoosterManager v3 — thêm global busy lock chống spam.
    ///
    /// Flow:
    ///   1. AutoRegisterAll() → scan assembly, tạo IBooster instances
    ///   2. UseBooster()      → check _isBusy → check unlock + quantity → Execute() → TryConsume()
    ///   3. NotifyBoosterCompleted() → mỗi IBooster.Execute() BẮT BUỘC gọi khi xong
    ///   4. OnLevelUp()       → unlock booster mới + grant initialQuantity
    /// </summary>
    public class BoosterManager : MonoBehaviour
    {
        public static BoosterManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("─── Data ────────────────────────────")]
        [SerializeField] private BoosterDatabase database;

        // ── Runtime ───────────────────────────────────────────────────────────
        private readonly Dictionary<string, IBooster> _registry = new();

        /// <summary>
        /// Global lock: true khi có booster đang thực thi.
        /// Mọi UseBooster() mới đều bị chặn cho đến khi booster hiện tại
        /// gọi NotifyBoosterCompleted().
        /// </summary>
        private bool _isBusy = false;

        public BoosterDatabase Database => database;

        /// <summary>True khi có booster đang thực thi — để BoosterSlotView restore button nếu bị reject.</summary>
        public bool IsBusy => _isBusy;

        // ── Unity ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Gọi từ BoosterInstaller.Start() sau khi đủ references.
        /// </summary>
        public void AutoRegisterAll(BoosterContext context)
        {
            _registry.Clear();
            _isBusy = false;

            var boosterInterface = typeof(IBooster);
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.GetCustomAttribute<BoosterAttribute>() == null) continue;
                if (!boosterInterface.IsAssignableFrom(type)) continue;
                if (type.IsAbstract || type.IsInterface) continue;

                if (Activator.CreateInstance(type) is not IBooster booster)
                {
                    Debug.LogWarning($"[BoosterManager] Không tạo được instance: {type.Name}");
                    continue;
                }

                booster.Initialize(context);

                if (_registry.ContainsKey(booster.BoosterName))
                {
                    Debug.LogWarning($"[BoosterManager] Trùng tên: '{booster.BoosterName}'");
                    continue;
                }

                if (database != null && database.GetByName(booster.BoosterName) == null)
                    Debug.LogWarning($"[BoosterManager] '{booster.BoosterName}' chưa có BoosterData SO!");

                _registry[booster.BoosterName] = booster;
                Debug.Log($"[BoosterManager] Registered: {booster.BoosterName}");
            }

            if (database != null)
                BoosterInventory.SyncUnlocksByLevel(database, SaveManager.CurrentLevel);

            Debug.Log($"[BoosterManager] Tổng: {_registry.Count} boosters.");
        }

        /// <summary>
        /// Gọi từ BoosterSlotView khi người chơi nhấn nút booster.
        /// Bị chặn nếu có booster khác đang chạy (_isBusy = true).
        /// </summary>
        public void UseBooster(string boosterName)
        {
            // ── Global busy guard ─────────────────────────────────────────────
            if (_isBusy)
            {
                Debug.Log($"[BoosterManager] Đang bận (booster khác đang chạy), bỏ qua '{boosterName}'.");
                return;
            }

            if (!_registry.TryGetValue(boosterName, out var booster))
            {
                Debug.LogError($"[BoosterManager] Không tìm thấy: '{boosterName}'");
                return;
            }

            var data = database?.GetByName(boosterName);
            if (data == null)
            {
                Debug.LogError($"[BoosterManager] Không tìm thấy BoosterData SO cho '{boosterName}'. " +
                               "Kiểm tra: 1) Database SO đã gán vào BoosterManager chưa? " +
                               "2) boosterName trong SO có khớp chính xác không?");
                return;
            }

            if (!BoosterInventory.IsEverUnlocked(data))
            {
                Debug.Log($"[BoosterManager] '{boosterName}' chưa được mở khóa.");
                return;
            }

            if (!BoosterInventory.HasAny(data))
            {
                Debug.Log($"[BoosterManager] '{boosterName}' hết lượt dùng.");
                EventBus.RaiseBoosterOutOfStock(boosterName);
                return;
            }

            if (!booster.CanExecute())
            {
                Debug.Log($"[BoosterManager] '{boosterName}' không thể dùng lúc này.");
                return;
            }

            // ── Lock → Consume → Execute ──────────────────────────────────────
            // TryConsume sau khi đã qua hết guard để tránh trừ nhầm khi bị reject
            _isBusy = true;
            BoosterInventory.TryConsume(data);
            booster.Execute();
            // QUAN TRỌNG: Execute() phải gọi NotifyBoosterCompleted() khi hoàn thành!

            Debug.Log($"[BoosterManager] Using '{boosterName}'. Còn lại: {BoosterInventory.GetQuantity(data)}");
        }

        /// <summary>
        /// BẮT BUỘC gọi từ mỗi IBooster.Execute() khi hiệu ứng đã hoàn thành.
        /// Giải phóng lock và fire event để UI refresh.
        /// Nếu quên gọi → game bị kẹt, không dùng được booster nào nữa.
        /// </summary>
        public void NotifyBoosterCompleted(string boosterName)
        {
            _isBusy = false;
            EventBus.RaiseBoosterActivated(boosterName);
            Debug.Log($"[BoosterManager] '{boosterName}' completed. Lock released.");
        }

        /// <summary>
        /// Gọi khi người chơi lên level (từ LevelManager / GameManager).
        /// </summary>
        public void OnLevelUp(int newLevel)
        {
            if (database == null) return;
            var newlyUnlocked = database.GetNewlyUnlocked(newLevel);
            foreach (var data in newlyUnlocked)
            {
                BoosterInventory.UnlockAndGrant(data);
                EventBus.RaiseBoosterUnlocked(data.boosterName);
                Debug.Log($"[BoosterManager] New booster unlocked: {data.boosterName}");
            }
        }

        /// <summary>
        /// Thêm quantity (reward, mua IAP, debug...).
        /// </summary>
        public void AddBoosterQuantity(string boosterName, int amount)
        {
            var data = database?.GetByName(boosterName);
            if (data == null)
            {
                Debug.LogWarning($"[BoosterManager] Không tìm thấy SO cho: '{boosterName}'");
                return;
            }
            BoosterInventory.UnlockAndGrant(data);
            int newQty = BoosterInventory.Add(data, amount);
            EventBus.RaiseBoosterActivated(boosterName);
            Debug.Log($"[BoosterManager] +{amount} '{boosterName}'. Tổng: {newQty}");
        }

        public int GetQuantity(string boosterName)
        {
            var data = database?.GetByName(boosterName);
            return data != null ? BoosterInventory.GetQuantity(data) : 0;
        }

        public void UnregisterAll()
        {
            _registry.Clear();
            _isBusy = false;
        }

        /// <summary>
        /// Reset busy flag khi game kết thúc (Win/Lose) để tránh kẹt lock.
        /// Gọi từ GameManager.HandleGameStateChanged khi state = Win | Lose.
        /// </summary>
        public void ForceReleaseLock()
        {
            if (_isBusy)
            {
                _isBusy = false;
                Debug.LogWarning("[BoosterManager] ForceReleaseLock called.");
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Add 3 to ALL Boosters")]
        private void DebugAddAllBoosters()
        {
            if (database == null) { Debug.LogError("Database chưa gán!"); return; }
            foreach (var data in database.Boosters)
            {
                BoosterInventory.UnlockAndGrant(data);
                int qty = BoosterInventory.Add(data, 3);
                Debug.Log($"[DEBUG] '{data.boosterName}': tổng {qty}");
            }
        }

        [ContextMenu("Debug: Print All Quantities")]
        private void DebugPrintAll()
        {
            if (database == null) { Debug.LogError("Database chưa gán!"); return; }
            foreach (var data in database.Boosters)
            {
                bool unlocked = BoosterInventory.IsEverUnlocked(data);
                int qty = BoosterInventory.GetQuantity(data);
                Debug.Log($"[DEBUG] '{data.boosterName}': unlocked={unlocked}, qty={qty}");
            }
        }

        [ContextMenu("Debug: Reset ALL Booster Data (PlayerPrefs)")]
        private void DebugResetAll()
        {
            BoosterInventory.ResetAll(database);
            Debug.LogWarning("[DEBUG] Đã reset toàn bộ booster data.");
        }

        [ContextMenu("Debug: Force Release Lock")]
        private void DebugForceReleaseLock() => ForceReleaseLock();
#endif
    }
}