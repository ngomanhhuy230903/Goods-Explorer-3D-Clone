// TubeObstacleController.cs
using System.Collections.Generic;
using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Order;

namespace FoodMatch.Obstacle
{
    /// <summary>
    /// Quản lý toàn bộ FoodTube trong một level.
    /// Flow:
    ///   1. OnInitialize() → gọi OrderQueue.ConsumeFoodForTubes() để reserve food
    ///   2. Spawn FoodTube prefab vào Canvas theo tubeAnchors
    ///   3. FoodTraySpawner sau đó đọc SharedFoodList đã bị rút bớt → spawn ít hơn
    ///   4. Player lấy food → TakeHeadFromTube(index)
    /// </summary>
    public class TubeObstacleController : ObstacleController<TubeObstacleData>
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("─── Tube Setup ──────────────────────────")]
        [Tooltip("Prefab FoodTube (RectTransform + Image + FoodTube.cs trên cùng GameObject).")]
        [SerializeField] private FoodTube tubePrefab;

        [Tooltip("Canvas cha để spawn ống UI vào.")]
        [SerializeField] private Canvas parentCanvas;

        [Tooltip("Danh sách RectTransform anchor trong Canvas — xác định vị trí & size từng ống.\n" +
                 "Số lượng phải >= tubeCount trong TubeObstacleData.")]
        [SerializeField] private List<RectTransform> tubeAnchors = new();

        [Tooltip("Container 3D trung lập dùng làm parent cho food 3D spawn ra.\n" +
                 "Kéo cùng GameObject mà FoodGridSpawner dùng làm NeutralContainer vào đây.")]
        [SerializeField] private Transform neutralWorldContainer;

        [Header("─── Debug ───────────────────────────────")]
        [SerializeField] private bool verboseLog = true;

        // ─── Runtime ─────────────────────────────────────────────────────────

        private readonly List<FoodTube> _tubes = new();
        private readonly List<FoodItemData> _reservedItems = new();

        /// <summary>Food đã reserve cho tubes — FoodTraySpawner có thể đọc để debug.</summary>
        public IReadOnlyList<FoodItemData> ReservedFoodItems => _reservedItems;
        public int TubeCount => _tubes.Count;

        // ─── ObstacleController ───────────────────────────────────────────────

        protected override void OnInitialize(TubeObstacleData data)
        {
            if (!data.IsValid()) return;

            _reservedItems.Clear();
            _tubes.Clear();

            var foodForTubes = ReserveFoodFromQueue(data);
            if (foodForTubes == null) return;

            SpawnTubes(data, foodForTubes);
        }

        protected override void OnReset()
        {
            foreach (var tube in _tubes)
            {
                if (tube == null) continue;
                tube.ClearTube();
                Destroy(tube.gameObject);
            }
            _tubes.Clear();
            _reservedItems.Clear();
            Log("Đã reset tất cả tubes.");
        }

        // ─── Public API ──────────────────────────────────────────────────────

        /// <summary>Gọi khi player lấy food đầu ống index.</summary>
        public void TakeHeadFromTube(int tubeIndex)
        {
            if (tubeIndex < 0 || tubeIndex >= _tubes.Count)
            {
                Debug.LogWarning($"[TubeObstacleController] TubeIndex {tubeIndex} out of range.");
                return;
            }
            _tubes[tubeIndex].TakeHead();
        }

        /// <summary>Trả về FoodItemData hiện tại của head ống index (null nếu rỗng).</summary>
        public FoodItemData PeekTubeHead(int tubeIndex)
        {
            if (tubeIndex < 0 || tubeIndex >= _tubes.Count) return null;
            return _tubes[tubeIndex].HeadData;
        }

        // ─── Private ─────────────────────────────────────────────────────────

        private List<List<FoodItemData>> ReserveFoodFromQueue(TubeObstacleData data)
        {
            if (OrderQueue.Instance == null)
            {
                Debug.LogError("[TubeObstacleController] OrderQueue.Instance là null!");
                return null;
            }

            int totalNeeded = data.GetTotalFoodCount();
            var picked = OrderQueue.Instance.ConsumeFoodForTubes(totalNeeded);

            if (picked == null || picked.Count == 0)
            {
                Debug.LogError("[TubeObstacleController] ConsumeFoodForTubes trả về rỗng!");
                return null;
            }

            _reservedItems.AddRange(picked);
            Log($"Reserved {picked.Count}/{totalNeeded} food " +
                $"(còn lại {OrderQueue.Instance.SharedFoodList.Count} cho tray).");

            // Phân phối food vào từng ống
            var result = new List<List<FoodItemData>>();
            int cursor = 0;
            for (int i = 0; i < data.tubeCount; i++)
            {
                int count = Mathf.Min(data.GetFoodCountForTube(i), picked.Count - cursor);
                var tubeFood = new List<FoodItemData>();
                for (int k = 0; k < count; k++)
                    tubeFood.Add(picked[cursor++]);
                result.Add(tubeFood);
                Log($"Tube[{i}] ← {tubeFood.Count} food");
            }

            return result;
        }

        private void SpawnTubes(TubeObstacleData data, List<List<FoodItemData>> foodPerTube)
        {
            if (tubePrefab == null)
            {
                Debug.LogError("[TubeObstacleController] tubePrefab chưa được assign!");
                return;
            }

            int count = Mathf.Min(data.tubeCount, tubeAnchors.Count);
            if (count < data.tubeCount)
                Debug.LogWarning(
                    $"[TubeObstacleController] tubeCount={data.tubeCount} " +
                    $"nhưng chỉ có {tubeAnchors.Count} anchor → spawn {count} ống.");

            for (int i = 0; i < count; i++)
            {
                var anchor = tubeAnchors[i];
                if (anchor == null)
                {
                    Debug.LogWarning($"[TubeObstacleController] Anchor[{i}] null, bỏ qua.");
                    continue;
                }

                var tubeGO = Instantiate(
                    tubePrefab.gameObject,
                    parentCanvas != null ? parentCanvas.transform : transform);

                var tube = tubeGO.GetComponent<FoodTube>();
                if (tube == null)
                {
                    Debug.LogError("[TubeObstacleController] tubePrefab không có FoodTube component!");
                    Destroy(tubeGO);
                    continue;
                }

                tubeGO.name = $"FoodTube_{i}";
                tube.OnTubeEmpty += OnTubeEmpty;
                tube.SetTubeAnchor(anchor);

                var foods = i < foodPerTube.Count ? foodPerTube[i] : new List<FoodItemData>();
                tube.Initialize(i, foods, neutralWorldContainer);

                _tubes.Add(tube);
                Log($"Spawned Tube[{i}] tại '{anchor.name}' — {foods.Count} food.");
            }

            Log($"Tổng cộng {_tubes.Count} ống đã spawn.");
        }

        private void OnTubeEmpty(FoodTube tube)
        {
            Log($"Tube[{tube.TubeIndex}] đã rỗng.");
        }

        private void Log(string msg)
        {
            if (verboseLog) Debug.Log($"[TubeObstacleController] {msg}");
        }
    }
}