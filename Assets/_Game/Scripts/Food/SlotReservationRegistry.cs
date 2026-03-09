using System.Collections.Generic;
using UnityEngine;

namespace FoodMatch.Core
{
    /// <summary>
    /// Registry lưu trữ trạng thái "đặt chỗ" của từng slot trong OrderTray và BackupTray.
    /// Slot được reserve ngay khi bắt đầu animation bay → tránh collision.
    ///
    /// KEY INSIGHT: Tách biệt "reserved" (đang bay) vs "confirmed" (đã đến nơi).
    /// </summary>
    public class SlotReservationRegistry
    {
        // ─── Singleton ────────────────────────────────────────────────────────
        private static SlotReservationRegistry _instance;
        public static SlotReservationRegistry Instance =>
            _instance ??= new SlotReservationRegistry();

        // ─── Data Structures ──────────────────────────────────────────────────

        // key: (trayId, slotIndex) → foodItemId đang giữ slot đó
        private readonly Dictionary<(int trayId, int slotIndex), int> _orderSlotReservations
            = new Dictionary<(int trayId, int slotIndex), int>();

        // key: slotIndex trong BackupTray → foodItemId
        private readonly Dictionary<int, int> _backupSlotReservations
            = new Dictionary<int, int>();

        // ─── Order Tray Slot API ───────────────────────────────────────────────

        /// <summary>
        /// Thử reserve slot trong OrderTray. Trả về true nếu thành công.
        /// Thread-safe trong context Unity single-thread.
        /// </summary>
        public bool TryReserveOrderSlot(int trayId, int slotIndex, int foodItemId)
        {
            var key = (trayId, slotIndex);
            if (_orderSlotReservations.ContainsKey(key))
            {
                Debug.Log($"[SlotRegistry] OrderTray[{trayId}] slot[{slotIndex}] đã bị reserve bởi food#{_orderSlotReservations[key]}");
                return false;
            }

            _orderSlotReservations[key] = foodItemId;
            Debug.Log($"[SlotRegistry] RESERVE OrderTray[{trayId}] slot[{slotIndex}] → food#{foodItemId}");
            return true;
        }

        public void ReleaseOrderSlot(int trayId, int slotIndex)
        {
            var key = (trayId, slotIndex);
            if (_orderSlotReservations.Remove(key))
                Debug.Log($"[SlotRegistry] RELEASE OrderTray[{trayId}] slot[{slotIndex}]");
        }

        public bool IsOrderSlotReserved(int trayId, int slotIndex)
            => _orderSlotReservations.ContainsKey((trayId, slotIndex));

        /// <summary>Đếm số slot đang được reserve trong 1 tray.</summary>
        public int GetReservedCountForTray(int trayId)
        {
            int count = 0;
            foreach (var key in _orderSlotReservations.Keys)
                if (key.trayId == trayId) count++;
            return count;
        }

        // ─── Backup Tray Slot API ─────────────────────────────────────────────

        public bool TryReserveBackupSlot(int slotIndex, int foodItemId)
        {
            if (_backupSlotReservations.ContainsKey(slotIndex)) return false;
            _backupSlotReservations[slotIndex] = foodItemId;
            Debug.Log($"[SlotRegistry] RESERVE BackupSlot[{slotIndex}] → food#{foodItemId}");
            return true;
        }

        public void ReleaseBackupSlot(int slotIndex)
        {
            if (_backupSlotReservations.Remove(slotIndex))
                Debug.Log($"[SlotRegistry] RELEASE BackupSlot[{slotIndex}]");
        }

        public bool IsBackupSlotReserved(int slotIndex)
            => _backupSlotReservations.ContainsKey(slotIndex);

        // ─── Cleanup ──────────────────────────────────────────────────────────

        public void ClearAll()
        {
            _orderSlotReservations.Clear();
            _backupSlotReservations.Clear();
            Debug.Log("[SlotRegistry] Cleared all reservations.");
        }

        public void ClearOrderTray(int trayId)
        {
            var toRemove = new List<(int, int)>();
            foreach (var key in _orderSlotReservations.Keys)
                if (key.trayId == trayId) toRemove.Add(key);
            foreach (var key in toRemove)
                _orderSlotReservations.Remove(key);
        }
    }
}