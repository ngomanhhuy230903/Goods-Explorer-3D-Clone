using System.Collections.Generic;
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Gắn cùng GameObject với FoodGridSpawner.
    /// Đăng ký OnSpawnComplete — được invoke SAU KHI animation cell xong
    /// → lúc đó anchor.position đã đúng → SpawnFoods chạy bình thường.
    /// </summary>
    public class FoodTraySpawner : MonoBehaviour
    {
        [Header("─── Spawn Animation ─────────────────")]
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

            // Lúc này cell đã scale xong → anchor.position đúng
            _trays.Clear();
            _trays.AddRange(
                _gridSpawner.GetCellContainer().GetComponentsInChildren<FoodTray>());

            if (_trays.Count == 0)
            {
                Debug.LogError("[FoodTraySpawner] Không tìm thấy FoodTray!");
                return;
            }

            Log($"Tìm thấy {_trays.Count} FoodTray.");

            Transform cellContainer = _gridSpawner.GetCellContainer();
            List<FoodItemData> foodList = GenerateFoodList(_pendingConfig);
            var distribution = DistributeToTrays(foodList);

            for (int i = 0; i < _trays.Count; i++)
            {
                _trays[i].SpawnFoods(
                    distribution[i],
                    trayID: i,
                    cellContainer: cellContainer,
                    globalDelay: i * trayStaggerDelay);

                Log($"Tray[{i}] → {distribution[i].Count} foods");
            }

            Log($"Spawn xong: {foodList.Count} food → {_trays.Count} trays.");
            _pendingConfig = null;
        }

        // ─────────────────────────────────────────────────────────────────────

        private List<FoodItemData> GenerateFoodList(LevelConfig config)
        {
            var result = new List<FoodItemData>();
            var foods = config.availableFoods;
            if (foods == null || foods.Count == 0) return result;

            for (int i = 0; i < config.totalFoodCount; i++)
                result.Add(foods[Random.Range(0, foods.Count)]);

            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
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

            // Bước A: tối thiểu 2 food/tray
            for (int i = 0; i < trayCount && remaining > 0; i++)
            {
                int give = Mathf.Min(2, remaining);
                for (int k = 0; k < give; k++)
                    result[i].Add(foodList[foodIdx++]);
                remaining -= give;
            }

            // Bước B: phần còn lại ngẫu nhiên
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

        private void Log(string msg)
        {
            if (verboseLog) Debug.Log($"[FoodTraySpawner] {msg}");
        }
    }
}