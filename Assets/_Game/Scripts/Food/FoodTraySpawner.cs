using System.Collections.Generic;
using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Order;
using FoodMatch.Obstacle;

namespace FoodMatch.Tray
{
    public class FoodTraySpawner : MonoBehaviour
    {
        [Header("─── Spawn Animation ─────────────────────")]
        [SerializeField] private float trayStaggerDelay = 0.06f;

        [Header("─── Obstacle Integration ─────────────────")]
        [Tooltip("Tham chiếu tới TubeObstacleController để reserve food trước khi spawn tray.\n" +
                 "Nếu null sẽ tự tìm qua ObstacleManager.")]
        [SerializeField] private TubeObstacleController tubeController;

        [Header("─── Debug ───────────────────────────────")]
        [SerializeField] private bool verboseLog = true;

        private readonly List<FoodTray> _trays = new();
        private LevelConfig _pendingConfig;
        private FoodGridSpawner _gridSpawner;

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _gridSpawner = GetComponent<FoodGridSpawner>();
            if (_gridSpawner == null)
                Debug.LogError("[FoodTraySpawner] Không tìm thấy FoodGridSpawner!");

            // Auto-find nếu chưa assign
            if (tubeController == null && ObstacleManager.Instance != null)
                tubeController = ObstacleManager.Instance.GetComponentInChildren<TubeObstacleController>();
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>[DEPRECATED] Giữ lại để tương thích ngược.</summary>
        public void SetFoodList(IReadOnlyList<FoodItemData> canonicalFoodList)
        {
            Log("SetFoodList() được gọi — không cần thiết nữa.");
        }

        /// <summary>
        /// Bước 1: Tubes reserve food khỏi SharedFoodList.
        /// Bước 2: Đăng ký callback OnGridSpawnComplete để spawn tray sau.
        ///
        /// ⚠️ Gọi hàm này SAU khi ObstacleManager.InitializeObstacles() đã chạy
        ///    (tức là TubeObstacleController đã Initialize và đã reserve food).
        ///    Nếu dùng luồng cũ (gọi SpawnFood trực tiếp), hàm sẽ tự reserve nếu cần.
        /// </summary>
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

            // SharedFoodList lúc này đã bị tube reserve trước rồi → chỉ còn food dành cho tray
            List<FoodItemData> foodList = GetCanonicalFoodListCopy();
            if (foodList == null) return;

            LogTubeReservationSummary();

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

                Log($"Tray[{i}] → {distribution[i].Count} foods " +
                    $"(max capacity: {_trays[i].MaxFoodCapacity})");
            }

            Log($"Spawn xong: {foodList.Count} food → {_trays.Count} trays.");
            _pendingConfig = null;
        }

        // ─────────────────────────────────────────────────────────────────────

        private List<FoodItemData> GetCanonicalFoodListCopy()
        {
            if (OrderQueue.Instance == null)
            {
                Debug.LogError("[FoodTraySpawner] OrderQueue.Instance là null!");
                return null;
            }

            var shared = OrderQueue.Instance.SharedFoodList;
            if (shared == null || shared.Count == 0)
            {
                Debug.LogError("[FoodTraySpawner] OrderQueue.SharedFoodList trống " +
                               "(hoặc toàn bộ đã bị tube reserve)!");
                return null;
            }

            var copy = new List<FoodItemData>(shared);
            Log($"Lấy food list từ OrderQueue: {copy.Count} items " +
                $"(sau khi tube đã reserve).");
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

            // Phân phối phần còn lại theo MaxFoodCapacity
            while (remaining > 0)
            {
                var available = new List<int>();
                for (int i = 0; i < trayCount; i++)
                    if (result[i].Count < _trays[i].MaxFoodCapacity)
                        available.Add(i);

                if (available.Count == 0)
                {
                    Debug.LogWarning(
                        $"[FoodTraySpawner] Hết capacity! {remaining} food không được spawn. " +
                        $"Tổng MaxFoodCapacity = {GetTotalMaxCapacity()} | Food cần spawn = {foodList.Count}");
                    break;
                }

                result[available[Random.Range(0, available.Count)]].Add(foodList[foodIdx++]);
                remaining--;
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────

        private int GetTotalMaxCapacity()
        {
            int total = 0;
            foreach (var t in _trays) total += t.MaxFoodCapacity;
            return total;
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void LogTubeReservationSummary()
        {
            if (!verboseLog || tubeController == null) return;
            var reserved = tubeController.ReservedFoodItems;
            if (reserved == null || reserved.Count == 0) return;

            var countMap = new Dictionary<string, int>();
            foreach (var f in reserved)
            {
                if (f == null) continue;
                if (!countMap.ContainsKey(f.name)) countMap[f.name] = 0;
                countMap[f.name]++;
            }
            var sb = new System.Text.StringBuilder(
                $"[FoodTraySpawner] Tubes đã reserve {reserved.Count} food:\n");
            foreach (var kvp in countMap)
                sb.AppendLine($"  {kvp.Key}: {kvp.Value} items");
            Debug.Log(sb.ToString());
        }

        private void LogFoodDistribution(List<FoodItemData> foodList)
        {
            if (!verboseLog) return;
            var countMap = new Dictionary<FoodItemData, int>();
            foreach (var food in foodList)
            {
                if (!countMap.ContainsKey(food)) countMap[food] = 0;
                countMap[food]++;
            }
            var sb = new System.Text.StringBuilder("[FoodTraySpawner] Phân bố food cho tray:\n");
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