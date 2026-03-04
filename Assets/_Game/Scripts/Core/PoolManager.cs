using System.Collections.Generic;
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Core
{
    /// <summary>
    /// Singleton quản lý toàn bộ các pool.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        [Header("─── Food Item Pools ─────────────────")]
        [Tooltip("Kéo tất cả FoodItemData vào đây. Pool sẽ tự tạo theo prefab.")]
        [SerializeField] private List<FoodItemData> allFoodData;
        [SerializeField] private int foodPreloadPerType = 10;

        [Header("─── VFX Pools ────────────────────────")]
        //[SerializeField] private GameObject sparklePrefab;
        //[SerializeField] private int sparklePreloadCount = 15;

        [SerializeField] private GameObject checkmarkPrefab;
        [SerializeField] private int checkmarkPreloadCount = 10;

        [Header("─── Customer Pool ────────────────────")]
        [SerializeField] private GameObject customerPrefab;
        [SerializeField] private int customerPreloadCount = 5;

        // ─── Internal dictionaries ───────────────────────────────────────────
        // Key: foodID → Pool tương ứng
        private readonly Dictionary<int, ObjectPool> _foodPools
            = new Dictionary<int, ObjectPool>();

        private ObjectPool _sparklePool;
        private ObjectPool _checkmarkPool;
        private ObjectPool _customerPool;

        // Container transforms (giữ Hierarchy gọn gàng)
        private Transform _foodContainer;
        private Transform _vfxContainer;
        private Transform _customerContainer;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // KHÔNG dùng DontDestroyOnLoad vì Pool gắn với Scene Game
        }

        private void Start()
        {
            InitContainers();
            InitFoodPools();
            InitVFXPools();
            InitCustomerPool();
        }

        // ─── Init helpers ────────────────────────────────────────────────────

        private void InitContainers()
        {
            _foodContainer = CreateContainer("Pool_Foods");
            _vfxContainer = CreateContainer("Pool_VFX");
            _customerContainer = CreateContainer("Pool_Customers");
        }

        private Transform CreateContainer(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            return go.transform;
        }

        private void InitFoodPools()
        {
            if (allFoodData == null) return;

            foreach (var data in allFoodData)
            {
                if (data == null || data.prefab == null)
                {
                    Debug.LogWarning("[PoolManager] FoodItemData null hoặc thiếu prefab – bỏ qua.");
                    continue;
                }

                if (_foodPools.ContainsKey(data.foodID))
                {
                    Debug.LogWarning($"[PoolManager] FoodID {data.foodID} bị trùng! Kiểm tra lại ScriptableObjects.");
                    continue;
                }

                var pool = new ObjectPool(data.prefab, _foodContainer, foodPreloadPerType);
                _foodPools.Add(data.foodID, pool);
            }

            Debug.Log($"[PoolManager] Đã khởi tạo {_foodPools.Count} food pools.");
        }

        private void InitVFXPools()
        {
            //if (sparklePrefab != null)
            //    _sparklePool = new ObjectPool(sparklePrefab, _vfxContainer, sparklePreloadCount);

            if (checkmarkPrefab != null)
                _checkmarkPool = new ObjectPool(checkmarkPrefab, _vfxContainer, checkmarkPreloadCount);
        }

        private void InitCustomerPool()
        {
            if (customerPrefab != null)
                _customerPool = new ObjectPool(customerPrefab, _customerContainer, customerPreloadCount);
        }

        // ─── Public API ──────────────────────────────────────────────────────

        /// <summary>Lấy 1 food GameObject từ pool theo foodID.</summary>
        public GameObject GetFood(int foodID, Vector3 position = default)
        {
            if (_foodPools.TryGetValue(foodID, out var pool))
                return pool.Get(position);

            Debug.LogError($"[PoolManager] Không tìm thấy pool cho foodID = {foodID}!");
            return null;
        }

        /// <summary>Trả 1 food GameObject về pool.</summary>
        public void ReturnFood(int foodID, GameObject obj)
        {
            if (_foodPools.TryGetValue(foodID, out var pool))
                pool.Return(obj);
            else
                Destroy(obj); // Fallback an toàn
        }

        public GameObject GetSparkle(Vector3 pos)
            => _sparklePool?.Get(pos) ?? null;

        public void ReturnSparkle(GameObject obj)
            => _sparklePool?.Return(obj);

        public GameObject GetCheckmark(Vector3 pos)
            => _checkmarkPool?.Get(pos) ?? null;

        public void ReturnCheckmark(GameObject obj)
            => _checkmarkPool?.Return(obj);

        public GameObject GetCustomer(Vector3 pos)
            => _customerPool?.Get(pos) ?? null;

        public void ReturnCustomer(GameObject obj)
            => _customerPool?.Return(obj);
    }
}