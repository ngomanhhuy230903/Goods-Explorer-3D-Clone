using System.Collections.Generic;
using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Order;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Gắn cùng GameObject với FoodGridSpawner.
    ///
    /// SOURCE OF TRUTH: FoodTray luôn lấy OrderQueue.Instance.SharedFoodList
    /// làm nguồn duy nhất — đảm bảo số lượng từng loại food trong FoodTray
    /// khớp 100% với OrderQueue, không phụ thuộc thứ tự gọi từ LevelManager.
    ///
    /// Thứ tự gọi từ LevelManager:
    ///   1. orderQueue.Initialize(config)     → sinh SharedFoodList
    ///   2. foodTraySpawner.SpawnFood(config) → đăng ký callback
    ///
    /// SetFoodList() giữ lại để tương thích ngược nhưng không cần gọi nữa.
    /// OnGridSpawnComplete tự lấy SharedFoodList từ OrderQueue.Instance.
    /// </summary>
    public class FoodTraySpawner : MonoBehaviour
    {
        [Header("─── Spawn Animation ─────────────────────")]
        [SerializeField] private float trayStaggerDelay = 0.06f;

        [Header("─── Debug ───────────────────────────")]
        [SerializeField] private bool verboseLog = true;

        private readonly List<FoodTray> _trays = new();
        private LevelConfig _pendingConfig;
        private FoodGridSpawner _gridSpawner;

        private void Awake()
        {
            _gridSpawner = GetComponent<FoodGridSpawner>();
            if (_gridSpawner == null)
                Debug.LogError("[FoodTraySpawner] Không tìm thấy FoodGridSpawner!");
        }

        /// <summary>
        /// [DEPRECATED - giữ lại để LevelManager cũ không bị compile error]
        /// Không cần gọi nữa. FoodTraySpawner tự lấy SharedFoodList
        /// từ OrderQueue.Instance trong callback.
        /// </summary>
        public void SetFoodList(IReadOnlyList<FoodItemData> canonicalFoodList)
        {
            Log("SetFoodList() được gọi — không cần thiết nữa. " +
                "FoodTraySpawner tự lấy SharedFoodList từ OrderQueue.Instance.");
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

            // ── SOURCE OF TRUTH: lấy trực tiếp từ OrderQueue.Instance ────────
            // Đây là cách duy nhất đảm bảo FoodTray và OrderTray
            // dùng chính xác cùng một danh sách với số lượng từng loại đồng nhất.
            List<FoodItemData> foodList = GetCanonicalFoodListCopy();
            if (foodList == null) return;

            // Shuffle để vị trí spawn ngẫu nhiên, nhưng TỔNG số lượng từng loại
            // vẫn giữ nguyên — khớp 100% với OrderQueue.
            ShuffleList(foodList);

            LogFoodDistribution(foodList);

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
        /// Lấy copy của OrderQueue.Instance.SharedFoodList.
        /// Copy để shuffle tự do mà không làm thay đổi source of truth.
        /// </summary>
        private List<FoodItemData> GetCanonicalFoodListCopy()
        {
            if (OrderQueue.Instance == null)
            {
                Debug.LogError("[FoodTraySpawner] OrderQueue.Instance là null! " +
                               "Đảm bảo OrderQueue tồn tại trong scene.");
                return null;
            }

            var shared = OrderQueue.Instance.SharedFoodList;
            if (shared == null || shared.Count == 0)
            {
                Debug.LogError("[FoodTraySpawner] OrderQueue.SharedFoodList trống! " +
                               "Gọi OrderQueue.Initialize(config) trước SpawnFood().");
                return null;
            }

            var copy = new List<FoodItemData>(shared);
            Log($"Lấy canonical list từ OrderQueue: {copy.Count} items.");
            return copy;
        }

        // ─────────────────────────────────────────────────────────────────────

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
                    Debug.LogWarning($"[FoodTraySpawner] Hết capacity! {remaining} food không được spawn.");
                    break;
                }

                result[available[Random.Range(0, available.Count)]].Add(foodList[foodIdx++]);
                remaining--;
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Log số lượng từng loại food để verify đồng nhất với OrderQueue.</summary>
        private void LogFoodDistribution(List<FoodItemData> foodList)
        {
            if (!verboseLog) return;
            var countMap = new Dictionary<FoodItemData, int>();
            foreach (var food in foodList)
            {
                if (!countMap.ContainsKey(food)) countMap[food] = 0;
                countMap[food]++;
            }
            var sb = new System.Text.StringBuilder("[FoodTraySpawner] Phân bố food:\n");
            foreach (var kvp in countMap)
                sb.AppendLine($"  {kvp.Key.name}: {kvp.Value} items = {kvp.Value / 3} orders");
            Debug.Log(sb.ToString());
        }

        private void Log(string msg)
        {
            if (verboseLog) Debug.Log($"[FoodTraySpawner] {msg}");
        }
    }
}