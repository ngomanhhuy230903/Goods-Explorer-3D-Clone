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
    ///   2. UseBooster()      → check _isBusy → check unlock + quantity → CanExecute() → Execute()
    ///   3. NotifyBoosterCompleted() → mỗi IBooster.Execute() BẮT BUỘC gọi khi xong
    ///      consumed=true  → trừ quantity (booster thực sự có tác dụng)
    ///      consumed=false → không trừ (không có tray/food, không làm gì)
    ///   4. OnLevelUp() → unlock booster mới + grant initialQuantity
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

        /// <summary>
        /// Tên booster đang chờ consume — sẽ bị trừ quantity trong NotifyBoosterCompleted(consumed=true).
        /// </summary>
        private string _pendingConsumeBoosterName = null;

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
            _pendingConsumeBoosterName = null;

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
        /// Quantity CHỈ bị trừ sau khi booster thực sự có tác dụng (trong NotifyBoosterCompleted).
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
                Debug.LogError($"[BoosterManager] Không có BoosterData cho '{boosterName}'. " +
                               "Kiểm tra: 1) Database SO đã gán chưa? 2) boosterName có khớp không?");
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

            // CanExecute() kiểm tra TRƯỚC khi lock.
            // Nếu fail → không lock, không consume, button giữ nguyên trạng thái.
            if (!booster.CanExecute())
            {
                Debug.Log($"[BoosterManager] '{boosterName}' CanExecute = false, không thực hiện.");
                return;
            }

            // Tất cả guard đã pass → lock và execute.
            // Quantity sẽ bị trừ trong NotifyBoosterCompleted(consumed=true).
            _isBusy = true;
            _pendingConsumeBoosterName = boosterName;
            booster.Execute();

            Debug.Log($"[BoosterManager] Đang thực thi '{boosterName}'. Qty hiện tại (chưa trừ): {BoosterInventory.GetQuantity(data)}");
        }

        /// <summary>
        /// BẮT BUỘC gọi từ mỗi IBooster.Execute() khi hiệu ứng đã hoàn thành.
        /// Giải phóng lock và fire event để UI refresh.
        ///
        /// consumed = true  → booster thực sự có tác dụng → trừ 1 quantity.
        /// consumed = false → không có tray/food phù hợp → không trừ quantity.
        ///
        /// Nếu quên gọi → _isBusy kẹt true → game không dùng được booster nào nữa.
        /// </summary>
        public void NotifyBoosterCompleted(string boosterName, bool consumed = true)
        {
            if (consumed && _pendingConsumeBoosterName == boosterName)
            {
                var data = database?.GetByName(boosterName);
                if (data != null)
                {
                    BoosterInventory.TryConsume(data);
                    Debug.Log($"[BoosterManager] Consumed 1x '{boosterName}'. Còn lại: {BoosterInventory.GetQuantity(data)}");
                }
            }
            else if (!consumed)
            {
                Debug.Log($"[BoosterManager] '{boosterName}' không có tác dụng → không trừ quantity.");
            }

            _pendingConsumeBoosterName = null;
            _isBusy = false;
            EventBus.RaiseBoosterActivated(boosterName);
            Debug.Log($"[BoosterManager] '{boosterName}' completed. Lock released. consumed={consumed}");
        }

        /// <summary>
        /// Reset busy flag khi game kết thúc (Win/Lose) để tránh kẹt lock.
        /// Gọi từ GameManager.HandleGameStateChanged khi state = Win | Lose.
        /// KHÔNG trừ quantity vì booster chưa hoàn thành.
        /// </summary>
        public void ForceReleaseLock()
        {
            if (_isBusy)
            {
                _pendingConsumeBoosterName = null;
                _isBusy = false;
                Debug.LogWarning("[BoosterManager] ForceReleaseLock called — lock cleared, quantity NOT consumed.");
            }
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

        /// <summary>
        /// Gọi mỗi khi bắt đầu level mới (LoadLevel / Restart / GoHome → Play).
        /// Reset per-session state của tất cả booster (vd: ExtraTray _usedThisSession).
        /// Cũng giải phóng busy lock phòng trường hợp coroutine bị interrupt.
        /// </summary>
        public void ResetAllBoosterSessions()
        {
            // Release lock nếu đang kẹt (level kết thúc giữa chừng animation)
            _isBusy = false;
            _pendingConsumeBoosterName = null;

            foreach (var booster in _registry.Values)
                booster.ResetSession();

            Debug.Log("[BoosterManager] All booster sessions reset.");
        }

        public void UnregisterAll()
        {
            _registry.Clear();
            _isBusy = false;
            _pendingConsumeBoosterName = null;
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