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
    /// Khi tray ra ngoài màn hình phải → wrap về bên trái ngoài màn hình.
    ///
    /// FIX:
    /// 1. Food ngẫu nhiên — shuffle rồi gán round-robin, không bao giờ cùng loại cả hàng.
    /// 2. Số lượng food = SharedFoodList AFTER conveyor reserve (giống FoodTraySpawner).
    /// 3. Wrap: tray di chuyển về bên trái ngoài màn hình (entry point), không nhảy giữa chừng.
    ///    Khoảng cách offscreen được tính theo tổng chiều rộng dải băng chuyền + extraOffscreenPadding.
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

        [Header("─── Wrap / Offscreen ─────────────────")]
        [Tooltip("Khoảng cách THÊM ngoài màn hình (pixels) — tính thêm vào tổng chiều rộng belt.\n" +
                 "Càng lớn → khoảng trống ngoài màn hình càng dài trước khi tray hiện vào.\n" +
                 "Nên đặt = 1-2 × (trayWidth + traySpacing) để mượt.")]
        [SerializeField] private float extraOffscreenPadding = 200f;

        // ─── Runtime ──────────────────────────────────────────────────────────
        private readonly List<ConveyorTray> _trays = new List<ConveyorTray>();
        private Transform _neutralContainer;

        private float _trayWidth;
        private float _step;          // _trayWidth + traySpacing
        private float _speed;

        // Bounds trong ConveyorArea local space (anchoredPosition.x)
        private float _exitX;        // tray vượt qua → wrap (phía PHẢI)
        private float _entryX;       // tray được đặt tại đây khi wrap (phía TRÁI, ngoài màn hình)

        // Tổng chiều rộng của toàn bộ belt (dùng để tính _entryX đúng)
        private float _beltTotalWidth;

        private bool _isRunning;

        // ─── ObstacleController ───────────────────────────────────────────────

        protected override void OnInitialize(ConveyorObstacleData data)
        {
            if (conveyorArea == null) { Debug.LogError("[Conveyor] conveyorArea null!"); return; }
            if (conveyorTrayPrefab == null) { Debug.LogError("[Conveyor] prefab null!"); return; }

            _speed = data.speed;

            // NeutralContainer cho FoodItem 3D
            if (_neutralContainer == null)
            {
                var go = new GameObject("ConveyorFoodContainer");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                _neutralContainer = go.transform;
            }

            // Tính layout
            var rt = conveyorTrayPrefab.GetComponent<RectTransform>();
            _trayWidth = (rt != null && rt.rect.width > 0f) ? rt.rect.width : 120f;
            _step = _trayWidth + traySpacing;

            int count = data.conveyorCount;
            _beltTotalWidth = count * _trayWidth + (count - 1) * traySpacing;

            // Exit: nửa tray vượt qua cạnh phải của ConveyorArea
            _exitX = conveyorArea.rect.width * 0.5f + _trayWidth * 0.5f;

            // Entry: điểm bên trái ngoài màn hình — cách cạnh trái đủ xa để tray chưa thấy được
            // = -(half area width) - (half tray) - extraOffscreenPadding
            // Công thức: mỗi tray sau khi wrap cần xếp đằng sau tray trái nhất 1 _step.
            // Ta giữ logic tìm minX rồi trừ _step — nhưng bổ sung _entryX là fallback cap
            // để không bao giờ wrap vào giữa màn hình.
            _entryX = -(conveyorArea.rect.width * 0.5f + _trayWidth * 0.5f + extraOffscreenPadding);

            // Lấy food từ SharedFoodList (sau khi conveyor đã reserve)
            var foodList = ReserveAndBuildFoodList(data);
            if (foodList == null || foodList.Count == 0)
            {
                Debug.LogError("[Conveyor] Không có food để spawn!");
                return;
            }

            // Shuffle để food ngẫu nhiên
            ShuffleList(foodList);

            SpawnAllTrays(data, foodList);

            _isRunning = true;
            Debug.Log($"[Conveyor] Init — {data.conveyorCount} trays × {data.foodPerConveyor} food, " +
                      $"speed={data.speed}, exitX={_exitX:F1}, entryX={_entryX:F1}");
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

                // Tìm X nhỏ nhất trong các tray còn lại (tray trái nhất)
                float minX = float.MaxValue;
                for (int j = 0; j < _trays.Count; j++)
                {
                    if (j == i || _trays[j] == null) continue;
                    float x = _trays[j].RectTransform.anchoredPosition.x;
                    if (x < minX) minX = x;
                }

                // Đặt tray sau tray trái nhất, ngoài màn hình bên trái
                // Giữ cộng delta để không lag 1 frame
                float newX;
                if (minX < float.MaxValue)
                {
                    // Nối đuôi phía trái
                    newX = minX - _step;

                    // Đảm bảo newX không bao giờ nằm trong màn hình (tránh nhảy vào giữa)
                    float leftEdge = -(conveyorArea.rect.width * 0.5f + _trayWidth * 0.5f);
                    if (newX > leftEdge)
                        newX = leftEdge - extraOffscreenPadding;
                }
                else
                {
                    // Fallback: chỉ 1 tray duy nhất → đặt về _entryX
                    newX = _entryX;
                }

                tray.RectTransform.anchoredPosition = new Vector2(newX, trayPosY);
            }
        }

        // ─── Spawn ────────────────────────────────────────────────────────────

        private void SpawnAllTrays(ConveyorObstacleData data, List<FoodItemData> foodList)
        {
            int count = data.conveyorCount;

            // Dàn đều trong ConveyorArea
            float totalWidth = count * _trayWidth + (count - 1) * traySpacing;
            float startX = -totalWidth * 0.5f + _trayWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float posX = startX + i * _step;

                // ─── FIX: round-robin qua foodList đã shuffle → food ngẫu nhiên ───
                // Mỗi tray lấy food theo index mod Count, không lặp cùng loại cả hàng
                var trayFoodList = BuildTrayFoodList(foodList, i, data.foodPerConveyor);

                float delay = i * 0.06f;
                SpawnOneTray(trayFoodList, data.foodPerConveyor, posX, delay);
            }
        }

        /// <summary>
        /// Lấy danh sách food cho 1 tray bằng cách round-robin qua foodList đã shuffle.
        /// Tray i lấy food ở index (i * foodPerConveyor + j) % foodList.Count.
        /// </summary>
        private List<FoodItemData> BuildTrayFoodList(List<FoodItemData> foodList,
                                                     int trayIndex, int foodPerConveyor)
        {
            var result = new List<FoodItemData>(foodPerConveyor);
            int baseIdx = trayIndex * foodPerConveyor;
            for (int j = 0; j < foodPerConveyor; j++)
            {
                int idx = (baseIdx + j) % foodList.Count;
                result.Add(foodList[idx]);
            }
            return result;
        }

        private void SpawnOneTray(List<FoodItemData> trayFoods, int foodPerConveyor,
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

            // Bắt đầu từ trên (slide xuống giống OrderTray)
            tray.RectTransform.anchoredPosition = new Vector2(posX, trayPosY + 280f);

            if (_neutralContainer == null)
            {
                Debug.LogError("[Conveyor] _neutralContainer null!");
                Destroy(go);
                return;
            }

            // ─── FIX: truyền danh sách food ngẫu nhiên thay vì 1 FoodItemData duy nhất ───
            tray.InitializeWithList(trayFoods, foodPerConveyor, _neutralContainer);

            // Slide xuống đúng Y
            tray.RectTransform
                .DOAnchorPosY(trayPosY, 0.4f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetDelay(slideDelay);

            _trays.Add(tray);
        }

        // ─── Food Source ──────────────────────────────────────────────────────

        /// <summary>
        /// Reserve food từ OrderQueue (giống TubeObstacleController).
        /// XÓA food khỏi SharedFoodList để FoodTraySpawner không spawn trùng.
        /// Số lượng = data.conveyorCount (mỗi tray 1 loại food, nhân foodPerConveyor khi hiển thị).
        /// </summary>
        private List<FoodItemData> ReserveAndBuildFoodList(ConveyorObstacleData data)
        {
            if (OrderQueue.Instance == null)
            {
                Debug.LogError("[Conveyor] OrderQueue.Instance null!");
                return new List<FoodItemData>();
            }

            // Reserve đúng conveyorCount items — mỗi tray sẽ tự round-robin
            var result = OrderQueue.Instance.ConsumeFoodForTubes(data.conveyorCount);

            if (result == null || result.Count == 0)
                Debug.LogError("[Conveyor] Không reserve được food từ OrderQueue!");
            else
                Debug.Log($"[Conveyor] Reserved {result.Count} food " +
                          $"(còn lại {OrderQueue.Instance.SharedFoodList.Count} cho tray).");

            return result ?? new List<FoodItemData>();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public void NotifyFoodTaken(ConveyorTray tray)
        {
            if (tray == null) return;
            if (tray.IsCollected)
                _trays.Remove(tray);
        }
    }
}