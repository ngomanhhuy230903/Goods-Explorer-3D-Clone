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
    }
}