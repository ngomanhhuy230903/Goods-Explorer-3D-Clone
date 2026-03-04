using UnityEngine;
using FoodMatch.Food;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Đại diện cho 1 ô (cell) trong khay đồ ăn.
    /// Mỗi slot chứa 1 stack các FoodItem xếp theo layer.
    /// </summary>
    public class TraySlot : MonoBehaviour
    {
        // ─── Config ───────────────────────────────────────────────────────────
        [Header("─── Slot Config ──────────────────────")]
        [Tooltip("Index hàng (row) của slot này trong lưới")]
        public int rowIndex;

        [Tooltip("Index cột (column) của slot này trong lưới")]
        public int columnIndex;

        // ─── Runtime Stack ────────────────────────────────────────────────────
        // Stack[0] = layer trên cùng (active)
        // Stack[1] = layer thứ 2 (xám)
        // Stack[2+] = ẩn
        private FoodItem[] _stack;
        private int _layerCount;

        /// <summary>Số layer đang còn item (chưa bị lấy)</summary>
        public int RemainingLayers { get; private set; }

        /// <summary>Item ở layer 0 (có thể tương tác). Null nếu slot trống.</summary>
        public FoodItem TopItem => (_stack != null && RemainingLayers > 0)
            ? _stack[0]
            : null;

        /// <summary>Slot không còn item nào.</summary>
        public bool IsEmpty => RemainingLayers == 0;

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Khởi tạo slot với mảng FoodItem theo thứ tự từ trên xuống dưới.
        /// items[0] = layer trên cùng, items[1] = layer kế tiếp, ...
        /// </summary>
        public void Initialize(FoodItem[] items)
        {
            _stack = items;
            _layerCount = items.Length;
            RemainingLayers = 0;

            // Đếm và set visual cho từng item
            for (int i = 0; i < _stack.Length; i++)
            {
                if (_stack[i] != null)
                {
                    _stack[i].OwnerSlot = this;
                    _stack[i].SetLayerVisual(i);
                    RemainingLayers++;
                }
            }
        }

        /// <summary>
        /// Lấy item ở layer 0 ra khỏi slot (gọi khi player chọn món).
        /// Tự động shift các layer còn lại lên.
        /// </summary>
        /// <returns>FoodItem vừa được lấy ra, hoặc null nếu slot trống.</returns>
        public FoodItem PopTopItem()
        {
            if (IsEmpty) return null;

            FoodItem topItem = _stack[0];
            topItem.OwnerSlot = null;

            // Shift stack lên 1
            for (int i = 0; i < _stack.Length - 1; i++)
            {
                _stack[i] = _stack[i + 1];
            }
            _stack[_stack.Length - 1] = null;
            RemainingLayers--;

            // Cập nhật visual sau khi shift
            RefreshVisuals();

            return topItem;
        }

        /// <summary>
        /// Cập nhật lại visual toàn bộ stack sau khi có thay đổi.
        /// Layer 0 sáng, Layer 1 xám, Layer 2+ ẩn.
        /// </summary>
        public void RefreshVisuals()
        {
            for (int i = 0; i < _stack.Length; i++)
            {
                if (_stack[i] == null) continue;

                int oldLayer = _stack[i].LayerIndex;
                _stack[i].SetLayerVisual(i);

                // Nếu item vừa được đẩy từ layer 1 lên layer 0 → play animation
                if (oldLayer == 1 && i == 0)
                    TrayAnimator.PlayLayerShiftIn(_stack[i]);
            }

        }

        /// <summary>
        /// Trả về item ở layer chỉ định (không lấy ra, chỉ xem).
        /// </summary>
        public FoodItem PeekAt(int layerIndex)
        {
            if (_stack == null || layerIndex >= _stack.Length) return null;
            return _stack[layerIndex];
        }
    }
}