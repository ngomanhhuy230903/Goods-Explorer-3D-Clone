namespace FoodMatch.Items
{
    public interface IBooster
    {
        string BoosterName { get; }
        /// <summary>
        /// Inject dependencies sau khi tạo instance qua Reflection.
        /// Mỗi booster chỉ lấy đúng thứ mình cần từ context.
        /// </summary>
        void Initialize(BoosterContext context);
        bool CanExecute();
        void Execute();

        /// <summary>
        /// Reset trạng thái per-session. Gọi mỗi khi level mới bắt đầu (LoadLevel / Restart / GoHome).
        /// Default no-op — chỉ override khi booster có session state cần reset (vd: ExtraTray).
        /// </summary>
        void ResetSession() { }
    }
}