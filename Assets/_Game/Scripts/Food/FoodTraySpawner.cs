using System.Collections.Generic;
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Gắn cùng GameObject với FoodGridSpawner.
    ///
    /// THAY ĐỔI: Không tự generate food list nữa.
    /// Nhận canonical food list từ OrderQueue (qua LevelManager) để đảm bảo
    /// số lượng từng loại food trong FoodTray khớp hoàn toàn với OrderQueue.
    ///
    /// Đăng ký OnSpawnComplete — được invoke SAU KHI animation cell CUỐI CÙNG xong
    /// → lúc đó tất cả anchor.position đã đúng → SpawnFoods chạy bình thường.
    /// </summary>
    public class FoodTraySpawner : MonoBehaviour
    {
        [Header("─── Spawn Animation ─────────────────────")]
        [SerializeField] private float trayStaggerDelay = 0.06f;

        [Header("─── Debug ───────────────────────────")]
        [SerializeField] private bool verboseLog = true;

        private readonly List<FoodTray> _trays = new();
        private LevelConfig _pendingConfig;

        /// <summary>
        /// Canonical food list nhận từ OrderQueue.
        /// Nếu null → fallback về random (không khuyến khích).
        /// </summary>
        private IReadOnlyList<FoodItemData> _canonicalFoodList;

        private FoodGridSpawner _gridSpawner;

        private void Awake()
        {
            _gridSpawner = GetComponent<FoodGridSpawner>();
            if (_gridSpawner == null)
                Debug.LogError("[FoodTraySpawner] Không tìm thấy FoodGridSpawner!");
        }

        /// <summary>
        /// Gọi TRƯỚC SpawnFood() để truyền canonical food list từ OrderQueue.
        /// LevelManager chịu trách nhiệm gọi theo đúng thứ tự:
        ///   1. orderQueue.Initialize(config)       → sinh SharedFoodList
        ///   2. foodTraySpawner.SetFoodList(...)    → inject list vào spawner
        ///   3. foodTraySpawner.SpawnFood(config)   → bắt đầu spawn
        /// </summary>
        public void SetFoodList(IReadOnlyList<FoodItemData> canonicalFoodList)
        {
            _canonicalFoodList = canonicalFoodList;
            Log($"SetFoodList: nhận {canonicalFoodList?.Count ?? 0} foods từ OrderQueue.");
        }

        public void SpawnFood(LevelConfig config)
        {
            _pendingConfig = config;
            if (_gridSpawner == null) return;

            _gridSpawner.OnSpawnComplete -= OnGridSpawnComplete;
            _gridSpawner.OnSpawnComplete += OnGridSpawnComplete;
        }

        public void ClearFood()
        {
            if (_gridSpawner != null)
                _gridSpawner.OnSpawnComplete -= OnGridSpawnComplete;

            foreach (var tray in _trays)
                tray?.ClearTray();
            _trays.Clear();
            _pendingConfig = null;
            _canonicalFoodList = null;
        }

        // ─────────────────────────────────────────────────────────────────────

        private void OnGridSpawnComplete()
        {
            _gridSpawner.OnSpawnComplete -= OnGridSpawnComplete;
            if (_pendingConfig == null) return;

            Transform neutralContainer = _gridSpawner.GetNeutralContainer();
            if (neutralContainer == null)
            {
                Debug.LogError("[FoodTraySpawner] GetNeutralContainer() trả về null!");
                return;
            }

            _trays.Clear();
            _trays.AddRange(
                _gridSpawner.GetCellContainer().GetComponentsInChildren<FoodTray>());

            if (_trays.Count == 0)
            {
                Debug.LogError("[FoodTraySpawner] Không tìm thấy FoodTray!");
                return;
            }

            Log($"Tìm thấy {_trays.Count} FoodTray.");

            // ── Dùng canonical list từ OrderQueue, fallback về random nếu null ──
            List<FoodItemData> foodList;
            if (_canonicalFoodList != null && _canonicalFoodList.Count > 0)
            {
                // Copy ra list mới để shuffle mà không ảnh hưởng canonical list gốc
                foodList = new List<FoodItemData>(_canonicalFoodList);
                ShuffleList(foodList); // Shuffle lại để vị trí spawn trong tray ngẫu nhiên
                Log($"Dùng canonical food list: {foodList.Count} items.");
            }
            else
            {
                Debug.LogWarning("[FoodTraySpawner] _canonicalFoodList null! " +
                                 "Fallback về random. Gọi SetFoodList() trước SpawnFood().");
                foodList = GenerateRandomFoodList(_pendingConfig);
            }

            var distribution = DistributeToTrays(foodList);

            for (int i = 0; i < _trays.Count; i++)
            {
                _trays[i].SpawnFoods(
                    distribution[i],
                    trayID: i,
                    neutralContainer: neutralContainer,
                    globalDelay: i * trayStaggerDelay);

                Log($"Tray[{i}] → {distribution[i].Count} foods");
            }

            Log($"Spawn xong: {foodList.Count} food → {_trays.Count} trays.");
            _pendingConfig = null;
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fallback: chỉ dùng khi không có canonical list.
        /// Kết quả sẽ không khớp với OrderQueue — tránh dùng trong production.
        /// </summary>
        private List<FoodItemData> GenerateRandomFoodList(LevelConfig config)
        {
            var result = new List<FoodItemData>();
            var foods = config.availableFoods;
            if (foods == null || foods.Count == 0) return result;

            for (int i = 0; i < config.totalFoodCount; i++)
                result.Add(foods[Random.Range(0, foods.Count)]);

            ShuffleList(result);
            return result;
        }

        private List<List<FoodItemData>> DistributeToTrays(List<FoodItemData> foodList)
        {
            int trayCount = _trays.Count;
            var result = new List<List<FoodItemData>>();
            for (int i = 0; i < trayCount; i++)
                result.Add(new List<FoodItemData>());

            if (foodList.Count == 0) return result;

            int foodIdx = 0, remaining = foodList.Count;

            // Đảm bảo mỗi tray có ít nhất 2 food trước
            for (int i = 0; i < trayCount && remaining > 0; i++)
            {
                int give = Mathf.Min(2, remaining);
                for (int k = 0; k < give; k++)
                    result[i].Add(foodList[foodIdx++]);
                remaining -= give;
            }

            // Phân phối phần còn lại vào các tray chưa đầy
            while (remaining > 0)
            {
                var available = new List<int>();
                for (int i = 0; i < trayCount; i++)
                    if (result[i].Count < _trays[i].TotalAnchorCapacity)
                        available.Add(i);

                if (available.Count == 0)
                {
                    Debug.LogWarning($"[FoodTraySpawner] Hết capacity! {remaining} food còn lại.");
                    break;
                }

                result[available[Random.Range(0, available.Count)]].Add(foodList[foodIdx++]);
                remaining--;
            }

            return result;
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void Log(string msg)
        {
            if (verboseLog) Debug.Log($"[FoodTraySpawner] {msg}");
        }
    }
}