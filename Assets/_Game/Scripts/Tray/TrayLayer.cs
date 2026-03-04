using System.Collections.Generic;
using UnityEngine;
using FoodMatch.Food;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Quản lý 1 tầng (depth layer) trong khay chính.
    /// Mỗi TrayLayer chứa nhiều TraySlot xếp theo lưới rows x columns.
    /// </summary>
    public class TrayLayer : MonoBehaviour
    {
        // ─── Runtime Data ─────────────────────────────────────────────────────
        private TraySlot[,] _slots;  // [row, col]
        private int _rows;
        private int _columns;

        /// <summary>Index của tầng này trong FoodTray (0 = trên cùng)</summary>
        public int LayerDepthIndex { get; private set; }

        /// <summary>
        /// Lấy danh sách tất cả slot còn item ở layer 0 (có thể tương tác).
        /// </summary>
        public List<TraySlot> GetActiveSlotsAtTop()
        {
            var result = new List<TraySlot>();
            if (_slots == null) return result;

            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _columns; c++)
                    if (_slots[r, c] != null && !_slots[r, c].IsEmpty)
                        result.Add(_slots[r, c]);

            return result;
        }

        /// <summary>Tổng số item còn lại trong tầng này.</summary>
        public int TotalRemainingItems()
        {
            int count = 0;
            if (_slots == null) return count;

            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _columns; c++)
                    if (_slots[r, c] != null)
                        count += _slots[r, c].RemainingLayers;

            return count;
        }

        /// <summary>
        /// Gọi sau khi tạo xong lưới slot.
        /// </summary>
        public void Setup(TraySlot[,] slots, int rows, int columns, int depthIndex)
        {
            _slots = slots;
            _rows = rows;
            _columns = columns;
            LayerDepthIndex = depthIndex;
        }

        /// <summary>Lấy slot tại vị trí (row, col).</summary>
        public TraySlot GetSlot(int row, int col)
        {
            if (row < 0 || row >= _rows || col < 0 || col >= _columns)
                return null;
            return _slots[row, col];
        }
    }
}