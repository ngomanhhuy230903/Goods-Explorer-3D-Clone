using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using FoodMatch.Core;
using FoodMatch.Managers;

namespace FoodMatch.Items
{
    /// <summary>
    /// BoosterManager v2 — giữ nguyên Reflection auto-scan.
    /// Thêm: tích hợp BoosterDatabase (SO) + BoosterInventory (quantity).
    /// 
    /// Flow:
    ///   1. AutoRegisterAll() → scan assembly, tạo IBooster instances
    ///   2. UseBooster()      → check unlock + check quantity → Execute() → TryConsume()
    ///   3. OnLevelUp()       → unlock booster mới + grant initialQuantity
    /// </summary>
    public class BoosterManager : MonoBehaviour
    {
        public static BoosterManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("─── Data ────────────────────────────")]
        [SerializeField] private BoosterDatabase database;

        // ── Runtime ───────────────────────────────────────────────────────────
        private readonly Dictionary<string, IBooster> _registry = new();

        // Expose để BoosterButtonUI đọc quantity mà không cần ref database
        public BoosterDatabase Database => database;

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

                // Validate: phải có BoosterData SO tương ứng
                if (database != null && database.GetByName(booster.BoosterName) == null)
                    Debug.LogWarning($"[BoosterManager] '{booster.BoosterName}' chưa có BoosterData SO!");

                _registry[booster.BoosterName] = booster;
                Debug.Log($"[BoosterManager] Registered: {booster.BoosterName}");
            }

            // Sync unlock state theo level hiện tại
            if (database != null)
                BoosterInventory.SyncUnlocksByLevel(database, SaveManager.CurrentLevel);

            Debug.Log($"[BoosterManager] Tổng: {_registry.Count} boosters.");
        }

        /// <summary>
        /// Gọi từ BoosterButtonUI khi người chơi nhấn nút booster.
        /// </summary>
        public void UseBooster(string boosterName)
        {
            if (!_registry.TryGetValue(boosterName, out var booster))
            {
                Debug.LogError($"[BoosterManager] Không tìm thấy: '{boosterName}'");
                return;
            }

            // ── Lookup SO (bắt buộc có — log lỗi rõ nếu thiếu) ──────────────
            var data = database?.GetByName(boosterName);
            if (data == null)
            {
                Debug.LogError($"[BoosterManager] Không tìm thấy BoosterData SO cho '{boosterName}'. " +
                               $"Kiểm tra: 1) Database SO đã gán vào BoosterManager chưa? " +
                               $"2) boosterName trong SO có khớp chính xác không?");
                return; // bắt buộc return — không thể consume nếu không có SO
            }

            // ── Check unlock ──────────────────────────────────────────────────
            if (!BoosterInventory.IsEverUnlocked(data))
            {
                Debug.Log($"[BoosterManager] '{boosterName}' chưa được mở khóa.");
                return;
            }

            // ── Check quantity ────────────────────────────────────────────────
            if (!BoosterInventory.HasAny(data))
            {
                Debug.Log($"[BoosterManager] '{boosterName}' hết lượt dùng.");
                EventBus.RaiseBoosterOutOfStock(boosterName);
                return;
            }

            // ── Check game-state condition ────────────────────────────────────
            if (!booster.CanExecute())
            {
                Debug.Log($"[BoosterManager] '{boosterName}' không thể dùng lúc này.");
                return;
            }

            // ── Execute → consume → notify ────────────────────────────────────
            booster.Execute();
            BoosterInventory.TryConsume(data);
            EventBus.RaiseBoosterActivated(boosterName);
            Debug.Log($"[BoosterManager] Used '{boosterName}'. Còn lại: {BoosterInventory.GetQuantity(data)}");
        }

        /// <summary>
        /// Gọi khi người chơi lên level (từ LevelManager / GameManager).
        /// Unlock booster mới + grant initialQuantity.
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
        /// Fire EventBus.OnBoosterQuantityChanged để UI refresh ngay không cần restart.
        /// </summary>
        public void AddBoosterQuantity(string boosterName, int amount)
        {
            var data = database?.GetByName(boosterName);
            if (data == null)
            {
                Debug.LogWarning($"[BoosterManager] Không tìm thấy SO cho: '{boosterName}'");
                return;
            }
            BoosterInventory.UnlockAndGrant(data); // idempotent — safe gọi nhiều lần
            int newQty = BoosterInventory.Add(data, amount);
            // Fire activated event để BoosterAreaSpawner/SlotView refresh ngay
            EventBus.RaiseBoosterActivated(boosterName);
            Debug.Log($"[BoosterManager] +{amount} '{boosterName}'. Tổng: {newQty}");
        }

        /// <summary>Query quantity để BoosterButtonUI hiển thị badge.</summary>
        public int GetQuantity(string boosterName)
        {
            var data = database?.GetByName(boosterName);
            return data != null ? BoosterInventory.GetQuantity(data) : 0;
        }

        public void UnregisterAll() => _registry.Clear();

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
#endif
    }
}