namespace FoodMatch.Core
{
    /// <summary>
    /// Implement interface này trên các Component cần biết
    /// khi nào mình được lấy ra / trả vào pool.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Gọi ngay khi object được Get() từ pool – dùng thay Awake/Start.</summary>
        void OnSpawn();

        /// <summary>Gọi ngay trước khi object được Return() vào pool – dùng để cleanup.</summary>
        void OnDespawn();
    }
}