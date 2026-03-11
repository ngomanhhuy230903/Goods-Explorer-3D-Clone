// ObstacleController.cs
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Obstacle
{
    /// <summary>
    /// Base MonoBehaviour cho tất cả obstacle controllers.
    /// Implement Initialize() và Reset() trong subclass.
    /// </summary>
    public abstract class ObstacleController<TData> : MonoBehaviour
        where TData : ObstacleData
    {
        protected TData Data { get; private set; }
        protected bool IsInitialized { get; private set; }

        public void Initialize(TData data)
        {
            Data = data;
            IsInitialized = true;
            OnInitialize(data);
        }

        public void Reset()
        {
            IsInitialized = false;
            Data = null;
            OnReset();
        }

        /// <summary>Override để thực hiện logic init cụ thể.</summary>
        protected abstract void OnInitialize(TData data);

        /// <summary>Override để dọn dẹp khi level kết thúc.</summary>
        protected abstract void OnReset();
    }
}