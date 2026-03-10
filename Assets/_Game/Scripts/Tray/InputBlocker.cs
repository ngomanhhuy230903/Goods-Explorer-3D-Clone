using System;
using System.Collections.Generic;
using UnityEngine;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Hệ thống block/unblock input dựa trên counter.
    /// Nhiều caller có thể block cùng lúc — input chỉ mở lại khi TẤT CẢ đã Unblock.
    ///
    /// Cách dùng:
    ///   InputBlocker.Block("SomeCaller");
    ///   // ... chờ animation xong ...
    ///   InputBlocker.Unblock("SomeCaller");
    ///
    /// Ở input handler (FoodSelector, v.v.) chỉ cần check:
    ///   if (InputBlocker.IsBlocked) return;
    /// </summary>
    public static class InputBlocker
    {
        private static readonly HashSet<string> _blockers = new HashSet<string>();

        /// <summary>True khi đang có ít nhất 1 blocker active.</summary>
        public static bool IsBlocked => _blockers.Count > 0;

        /// <summary>Raised khi trạng thái block thay đổi. bool = isBlocked.</summary>
        public static event Action<bool> OnBlockStateChanged;

        /// <summary>
        /// Đặt block với key định danh.
        /// Gọi nhiều lần với cùng key không tính double.
        /// </summary>
        public static void Block(string key)
        {
            bool wasBlocked = _blockers.Count > 0;
            _blockers.Add(key);
            if (!wasBlocked)
            {
                OnBlockStateChanged?.Invoke(true);
                Debug.Log($"[InputBlocker] BLOCKED by '{key}'.");
            }
        }

        /// <summary>
        /// Gỡ block cho key.
        /// Nếu không còn blocker nào thì mở input.
        /// </summary>
        public static void Unblock(string key)
        {
            _blockers.Remove(key);
            if (_blockers.Count == 0)
            {
                OnBlockStateChanged?.Invoke(false);
                Debug.Log($"[InputBlocker] UNBLOCKED — '{key}' removed. Input open.");
            }
            else
            {
                Debug.Log($"[InputBlocker] '{key}' removed. Still blocked by: {string.Join(", ", _blockers)}");
            }
        }

        /// <summary>
        /// Force clear toàn bộ blocker — dùng khi reset/load level.
        /// </summary>
        public static void ForceUnblockAll()
        {
            if (_blockers.Count > 0)
            {
                _blockers.Clear();
                OnBlockStateChanged?.Invoke(false);
                Debug.Log("[InputBlocker] Force unblocked all.");
            }
        }
    }
}