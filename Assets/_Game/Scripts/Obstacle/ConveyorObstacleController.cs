// ConveyorObstacleController.cs
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;
using FoodMatch.Order;

namespace FoodMatch.Obstacle
{
    /// <summary>
    /// Băng chuyền kiểu sushi: N tray nhỏ nối đuôi nhau, di chuyển trái → phải.
    /// Khi tray ra ngoài màn hình phải → wrap về sau tray trái nhất.
    ///
    /// FOOD SOURCE: Lấy từ OrderQueue.SharedFoodList (giống FoodTraySpawner),
    /// trừ food ra khỏi list trước khi FoodTraySpawner đọc — đảm bảo không bị trùng.
    ///
    /// SPEED: anchoredPosition pixels/second — 200 = nhanh rõ rệt.
    ///
    /// SPAWN LAYOUT: dàn đều toàn bộ ConveyorArea, khoảng cách = traySpacing.
    /// </summary>
    public class ConveyorObstacleController : ObstacleController<ConveyorObstacleData>
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Layout ───────────────────────────")]
        [Tooltip("RectTransform của ConveyorArea — tất cả tray là con của object này.")]
        [SerializeField] private RectTransform conveyorArea;

        [Tooltip("Prefab ConveyorTray.")]
        [SerializeField] private GameObject conveyorTrayPrefab;

        [Tooltip("Khoảng cách giữa các tray (pixels).")]
        [SerializeField] private float traySpacing = 20f;

        [Tooltip("Y cố định của tất cả tray trong ConveyorArea.")]
        [SerializeField] private float trayPosY = 0f;

        // ─── Runtime ──────────────────────────────────────────────────────────
        // Dùng List<Entry> thay vì List<ConveyorTray> để giữ thêm posX gốc
        private readonly List<ConveyorTray> _trays = new List<ConveyorTray>();
        private Transform _neutralContainer; // tự tạo khi Init, không cần assign

        private float _trayWidth;   // width thực của 1 tray (từ RectTransform prefab)
        private float _step;        // _trayWidth + traySpacing
        private float _speed;

        // Bounds (anchoredPosition.x trong ConveyorArea local space)
        // Tray vượt _exitX → wrap về trước tray trái nhất
        private float _exitX;

        private bool _isRunning;

        // ─── ObstacleController ───────────────────────────────────────────────

        protected override void OnInitialize(ConveyorObstacleData data)
        {
            if (conveyorArea == null) { Debug.LogError("[Conveyor] conveyorArea null!"); return; }
            if (conveyorTrayPrefab == null) { Debug.LogError("[Conveyor] prefab null!"); return; }

            _speed = data.speed;

            // Tạo container riêng để chứa FoodItem 3D trong world space
            // Đặt dưới ConveyorObstacleController để Hierarchy gọn
            if (_neutralContainer == null)
            {
                var go = new GameObject("ConveyorFoodContainer");
                go.transform.SetParent(transform, false);
                // Reset về world zero để food spawn đúng world position
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                _neutralContainer = go.transform;
            }

            // Tính layout
            var rt = conveyorTrayPrefab.GetComponent<RectTransform>();
            _trayWidth = (rt != null && rt.rect.width > 0f) ? rt.rect.width : 120f;
            _step = _trayWidth + traySpacing;

            // Tray vượt nửa tray ra ngoài phải → wrap
            _exitX = conveyorArea.rect.width * 0.5f + _trayWidth * 0.5f;

            // Lấy food từ SharedFoodList và reserve trước
            var foodList = ReserveAndBuildFoodList(data);
            if (foodList == null || foodList.Count == 0)
            {
                Debug.LogError("[Conveyor] Không có food để spawn!");
                return;
            }

            SpawnAllTrays(data, foodList);

            _isRunning = true;
            Debug.Log($"[Conveyor] Init — {data.conveyorCount} trays × {data.foodPerConveyor} food, speed={data.speed}");
        }

        protected override void OnReset()
        {
            _isRunning = false;

            foreach (var tray in _trays)
            {
                if (tray == null) continue;
                DOTween.Kill(tray.gameObject);
                tray.ResetTray();
                if (PoolManager.Instance != null)
                    PoolManager.Instance.ReturnConveyorTray(tray.gameObject);
                else
                    Destroy(tray.gameObject);
            }
            _trays.Clear();

            if (_neutralContainer != null)
            {
                Destroy(_neutralContainer.gameObject);
                _neutralContainer = null;
            }

            Debug.Log("[Conveyor] Reset");
        }

        // ─── Update — sushi belt ──────────────────────────────────────────────

        private void Update()
        {
            if (!_isRunning || _trays.Count == 0) return;

            float delta = _speed * Time.deltaTime;

            // 1. Di chuyển tất cả tray sang phải
            for (int i = 0; i < _trays.Count; i++)
            {
                var tray = _trays[i];
                if (tray == null) continue;
                var pos = tray.RectTransform.anchoredPosition;
                pos.x += delta;
                tray.RectTransform.anchoredPosition = pos;
            }

            // 2. Wrap tray nào đã ra ngoài phải
            for (int i = 0; i < _trays.Count; i++)
            {
                var tray = _trays[i];
                if (tray == null) continue;
                if (tray.RectTransform.anchoredPosition.x < _exitX) continue;

                // Tìm X nhỏ nhất trong các tray còn lại
                float minX = float.MaxValue;
                for (int j = 0; j < _trays.Count; j++)
                {
                    if (j == i || _trays[j] == null) continue;
                    float x = _trays[j].RectTransform.anchoredPosition.x;
                    if (x < minX) minX = x;
                }

                // Nối đuôi phía trái — cộng thêm delta để không lag 1 frame
                float newX = (minX < float.MaxValue)
                    ? minX - _step
                    : -_exitX; // fallback nếu chỉ có 1 tray

                tray.RectTransform.anchoredPosition = new Vector2(newX, trayPosY);
            }
        }

        // ─── Spawn ────────────────────────────────────────────────────────────

        private void SpawnAllTrays(ConveyorObstacleData data, List<FoodItemData> foodList)
        {
            int count = data.conveyorCount;

            // Dàn đều trong ConveyorArea — giống OrderArea
            float totalWidth = count * _trayWidth + (count - 1) * traySpacing;
            float startX = -totalWidth * 0.5f + _trayWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float posX = startX + i * _step;
                var food = foodList[i % foodList.Count];
                float delay = i * 0.06f;
                SpawnOneTray(food, data.foodPerConveyor, posX, delay);
            }
        }

        private void SpawnOneTray(FoodItemData food, int foodPerConveyor,
                                  float posX, float slideDelay)
        {
            GameObject go = PoolManager.Instance != null
                ? PoolManager.Instance.GetConveyorTray()
                : Instantiate(conveyorTrayPrefab);

            if (go == null) return;

            go.transform.SetParent(conveyorArea, false);
            go.SetActive(true);

            var tray = go.GetComponent<ConveyorTray>();
            if (tray == null) { Destroy(go); return; }

            if (tray is IPoolable p) p.OnSpawn();

            // Đặt vào đúng posX ngay — slide vào từ trên (giống OrderTray Enter)
            tray.RectTransform.anchoredPosition = new Vector2(posX, trayPosY + 280f);

            // neutralContainer cần thiết để FoodItem sống đúng world space
            if (_neutralContainer == null)
            {
                Debug.LogError("[Conveyor] _neutralContainer null!");
                Destroy(go);
                _trays.Remove(tray);
                return;
            }
            tray.Initialize(food, foodPerConveyor, _neutralContainer);

            // Slide xuống đúng Y, giữ nguyên X (không DOAnchorPos vì controller sẽ update X)
            tray.RectTransform
                .DOAnchorPosY(trayPosY, 0.4f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetDelay(slideDelay);

            _trays.Add(tray);
        }

        // ─── Food Source — reserve từ SharedFoodList ──────────────────────────

        /// <summary>
        /// Reserve food từ OrderQueue bằng ConsumeFoodForTubes() —
        /// giống TubeObstacleController, XÓA food khỏi _canonicalFoodList
        /// để FoodTraySpawner không spawn trùng lên grid.
        /// Gọi TRƯỚC khi FoodTraySpawner.SpawnFood().
        /// </summary>
        private List<FoodItemData> ReserveAndBuildFoodList(ConveyorObstacleData data)
        {
            if (OrderQueue.Instance == null)
            {
                Debug.LogError("[Conveyor] OrderQueue.Instance null!");
                return new List<FoodItemData>();
            }

            // ConsumeFoodForTubes() đã handle shuffle + RemoveAt nội bộ trên List<>
            var result = OrderQueue.Instance.ConsumeFoodForTubes(data.conveyorCount);

            if (result == null || result.Count == 0)
                Debug.LogError("[Conveyor] Không reserve được food từ OrderQueue!");
            else
                Debug.Log($"[Conveyor] Reserved {result.Count} food " +
                          $"(còn lại {OrderQueue.Instance.SharedFoodList.Count} cho tray).");

            return result ?? new List<FoodItemData>();
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Gọi sau khi FoodFlowController xử lý xong 1 food từ conveyor.
        /// Nếu tray hết food hoàn toàn → remove khỏi update list.
        /// </summary>
        public void NotifyFoodTaken(ConveyorTray tray)
        {
            if (tray == null) return;
            if (tray.IsCollected)
                _trays.Remove(tray);
        }
    }
}