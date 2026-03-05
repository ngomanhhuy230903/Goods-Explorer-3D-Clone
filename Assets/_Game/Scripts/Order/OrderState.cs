namespace FoodMatch.Order
{
    /// <summary>Các trạng thái của 1 order tray.</summary>
    public enum OrderState
    {
        Idle,        // Chưa được khởi tạo (đang trong pool)
        Active,      // Đang hiển thị, chờ nhận món
        Receiving,   // Đang nhận món (animation đang chạy, khóa input tạm)
        Completed,   // Đã nhận đủ 3 món, chuẩn bị rời màn hình
        Leaving      // Animation rời màn hình đang chạy
    }
}