namespace FoodMatch.Order
{
    /// <summary>
    /// Kết quả trả về từ OrderQueue.TryMatchFood().
    /// Dùng struct để tránh GC Alloc.
    /// </summary>
    public readonly struct MatchResult
    {
        public bool IsMatch { get; }
        public OrderTray Tray { get; }
        public int SlotIndex { get; }

        private MatchResult(bool isMatch, OrderTray tray, int slotIndex)
        {
            IsMatch = isMatch;
            Tray = tray;
            SlotIndex = slotIndex;
        }

        /// <summary>Tạo kết quả match thành công.</summary>
        public static MatchResult Matched(OrderTray tray, int slotIndex)
            => new MatchResult(true, tray, slotIndex);

        /// <summary>Tạo kết quả không match → food về BackupTray.</summary>
        public static MatchResult NoMatch()
            => new MatchResult(false, null, -1);
    }
}