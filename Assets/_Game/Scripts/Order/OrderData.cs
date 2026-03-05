using FoodMatch.Data;

namespace FoodMatch.Order
{
    /// <summary>
    /// Dữ liệu runtime của 1 order.
    /// Mỗi order yêu cầu đúng 1 loại món x 3 lần.
    /// </summary>
    public class OrderData
    {
        // ─── Properties ───────────────────────────────────────────────────────
        public int FoodID { get; private set; }
        public FoodItemData FoodData { get; private set; }
        public int TotalRequired { get; private set; }  // luôn = 3
        public int DeliveredCount { get; private set; }  // số món đã giao

        public bool IsCompleted => DeliveredCount >= TotalRequired;
        public int RemainingCount => TotalRequired - DeliveredCount;

        // ─── Constructor ──────────────────────────────────────────────────────
        public OrderData(FoodItemData foodData, int totalRequired = 3)
        {
            FoodData = foodData;
            FoodID = foodData.foodID;
            TotalRequired = totalRequired;
            DeliveredCount = 0;
        }

        // ─── Methods ──────────────────────────────────────────────────────────

        /// <summary>Giao 1 món. Trả về true nếu thành công.</summary>
        public bool Deliver()
        {
            if (IsCompleted) return false;
            DeliveredCount++;
            return true;
        }

        /// <summary>Reset về 0 (dùng khi trả object về pool).</summary>
        public void Reset()
        {
            DeliveredCount = 0;
        }
    }
}