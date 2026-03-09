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

        [Header("─── Order Pool ────────────────────")]
        [SerializeField] private GameObject orderPrefab;
        [SerializeField] private int orderPreloadCount = 5;

        // ─── Internal dictionaries ───────────────────────────────────────────
        // Key: foodID → Pool tương ứng
        private readonly Dictionary<int, ObjectPool> _foodPools
            = new Dictionary<int, ObjectPool>();

        private ObjectPool _sparklePool;
        private ObjectPool _checkmarkPool;
        private ObjectPool _orderPool;

        // Container transforms (giữ Hierarchy gọn gàng)
        private Transform _foodContainer;
        private Transform _vfxContainer;
        private Transform _orderContainer;

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
        }

        private void Start()
        {
            InitContainers();
            InitFoodPools();
            InitVFXPools();
            InitOrderPool();
        }

        // ─── Init helpers ────────────────────────────────────────────────────

        private void InitContainers()
        {
            _foodContainer = CreateContainer("Pool_Foods");
            _vfxContainer = CreateContainer("Pool_VFX");
            _orderContainer = CreateContainer("Pool_Orders");
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

        private void InitOrderPool()
        {
            if (orderPrefab != null)
                _orderPool = new ObjectPool(orderPrefab, _orderContainer, orderPreloadCount);
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

        public GameObject GetOrder(Vector3 pos)
            => _orderPool?.Get(pos) ?? null;

        public void ReturnOrder(GameObject obj)
            => _orderPool?.Return(obj);
    }
}