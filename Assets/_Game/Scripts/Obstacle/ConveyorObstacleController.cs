// ConveyorObstacleController.cs
using System.Collections;
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
    ///
    /// FIX 1 — Food count đúng:
    ///   Reserve đúng conveyorCount × foodPerConveyor items khỏi SharedFoodList.
    ///   FoodTraySpawner nhận phần còn lại → tổng luôn = total food level.
    ///   Ví dụ: 30 food, 3 tray × 3 food/tray → reserve 9, tray nhận 21.
    ///
    /// FIX 2 — Belt chờ food spawn xong mới chạy:
    ///   _isRunning = false khi init.
    ///   ConveyorTray.InitializeWithList() trả về thời gian animation tổng.
    ///   Controller StartCoroutine chờ đúng thời gian đó rồi mới bật _isRunning.
    /// </summary>
    public class ConveyorObstacleController : ObstacleController<ConveyorObstacleData>
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Layout ───────────────────────────")]
        [SerializeField] private RectTransform conveyorArea;
        [SerializeField] private GameObject conveyorTrayPrefab;
        [SerializeField] private float traySpacing = 20f;
        [SerializeField] private float trayPosY = 0f;

        [Header("─── Wrap / Offscreen ─────────────────")]
        [Tooltip("Khoảng đệm THÊM ngoài màn hình bên trái (pixels).\n" +
                 "Tăng khi có nhiều tray để khoảng trống offscreen dài hơn.")]
        [SerializeField] private float extraOffscreenPadding = 300f;

        // ─── Runtime ──────────────────────────────────────────────────────────
        private readonly List<ConveyorTray> _trays = new List<ConveyorTray>();
        private Transform _neutralContainer;

        private float _trayWidth;
        private float _step;
        private float _speed;

        private float _exitX;
        private float _leftEdge;

        // Belt KHÔNG chạy cho đến khi tất cả food spawn animation xong
        private bool _isRunning;

        // ─── ObstacleController ───────────────────────────────────────────────

        protected override void OnInitialize(ConveyorObstacleData data)
        {
            if (conveyorArea == null) { Debug.LogError("[Conveyor] conveyorArea null!"); return; }
            if (conveyorTrayPrefab == null) { Debug.LogError("[Conveyor] prefab null!"); return; }

            _isRunning = false;
            _speed = data.speed;

            if (_neutralContainer == null)
            {
                var go = new GameObject("ConveyorFoodContainer");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                _neutralContainer = go.transform;
            }

            var rt = conveyorTrayPrefab.GetComponent<RectTransform>();
            _trayWidth = (rt != null && rt.rect.width > 0f) ? rt.rect.width : 120f;
            _step = _trayWidth + traySpacing;

            _exitX = conveyorArea.rect.width * 0.5f + _trayWidth * 0.5f;
            _leftEdge = -(conveyorArea.rect.width * 0.5f + _trayWidth * 0.5f);

            // ── FIX 1: Reserve đúng conveyorCount × foodPerConveyor ───────────
            var foodList = ReserveAndBuildFoodList(data);
            if (foodList == null || foodList.Count == 0)
            {
                Debug.LogError("[Conveyor] Không có food để spawn!");
                return;
            }

            ShuffleList(foodList);

            // ── FIX 2: Tính tổng thời gian animation → delay belt ─────────────
            float totalAnimTime = SpawnAllTrays(data, foodList);
            StartCoroutine(StartBeltAfterDelay(totalAnimTime));

            Debug.Log($"[Conveyor] Init — {data.conveyorCount} trays × {data.foodPerConveyor} food " +
                      $"= {data.conveyorCount * data.foodPerConveyor} reserved. " +
                      $"Belt starts after {totalAnimTime:F2}s.");
        }

        protected override void OnReset()
        {
            StopAllCoroutines();
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

        // ─── Coroutine: chờ food spawn xong rồi mới chạy belt ────────────────

        private IEnumerator StartBeltAfterDelay(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            _isRunning = true;
            Debug.Log("[Conveyor] Belt bắt đầu di chuyển.");
        }

        // ─── Update — sushi belt ──────────────────────────────────────────────

        private void Update()
        {
            if (!_isRunning || _trays.Count == 0) return;

            float delta = _speed * Time.deltaTime;

            for (int i = 0; i < _trays.Count; i++)
            {
                var tray = _trays[i];
                if (tray == null) continue;
                var pos = tray.RectTransform.anchoredPosition;
                pos.x += delta;
                tray.RectTransform.anchoredPosition = pos;
            }

            for (int i = 0; i < _trays.Count; i++)
            {
                var tray = _trays[i];
                if (tray == null) continue;
                if (tray.RectTransform.anchoredPosition.x < _exitX) continue;

                float minX = float.MaxValue;
                for (int j = 0; j < _trays.Count; j++)
                {
                    if (j == i || _trays[j] == null) continue;
                    float x = _trays[j].RectTransform.anchoredPosition.x;
                    if (x < minX) minX = x;
                }

                float newX;
                if (minX < float.MaxValue)
                {
                    newX = minX - _step;
                    if (newX > _leftEdge)
                        newX = _leftEdge - extraOffscreenPadding;
                }
                else
                {
                    newX = _leftEdge - extraOffscreenPadding;
                }

                tray.RectTransform.anchoredPosition = new Vector2(newX, trayPosY);
            }
        }

        // ─── Spawn ────────────────────────────────────────────────────────────

        /// <summary>
        /// Spawn tất cả tray. Trả về tổng thời gian animation lâu nhất
        /// (để controller biết chờ bao lâu trước khi bật belt).
        /// </summary>
        private float SpawnAllTrays(ConveyorObstacleData data, List<FoodItemData> foodList)
        {
            int count = data.conveyorCount;
            float totalWidth = count * _trayWidth + (count - 1) * traySpacing;
            float startX = -totalWidth * 0.5f + _trayWidth * 0.5f;

            float maxAnimEnd = 0f;

            for (int i = 0; i < count; i++)
            {
                float posX = startX + i * _step;
                var trayFoods = BuildTrayFoodList(foodList, i, data.foodPerConveyor);
                float trayDelay = i * 0.06f;

                float animEnd = SpawnOneTray(trayFoods, data.foodPerConveyor, posX, trayDelay);
                if (animEnd > maxAnimEnd) maxAnimEnd = animEnd;
            }

            // Thêm buffer nhỏ để animation chắc chắn hoàn tất trước khi belt chạy
            return maxAnimEnd + 0.1f;
        }

        private List<FoodItemData> BuildTrayFoodList(List<FoodItemData> foodList,
                                                     int trayIndex, int foodPerConveyor)
        {
            var result = new List<FoodItemData>(foodPerConveyor);
            int baseIdx = trayIndex * foodPerConveyor;
            for (int j = 0; j < foodPerConveyor; j++)
                result.Add(foodList[(baseIdx + j) % foodList.Count]);
            return result;
        }

        /// <summary>
        /// Spawn 1 tray. Trả về thời điểm animation kết thúc (delay + duration).
        /// </summary>
        private float SpawnOneTray(List<FoodItemData> trayFoods, int foodPerConveyor,
                                   float posX, float trayDelay)
        {
            GameObject go = PoolManager.Instance != null
                ? PoolManager.Instance.GetConveyorTray()
                : Instantiate(conveyorTrayPrefab);

            if (go == null) return trayDelay;

            go.transform.SetParent(conveyorArea, false);
            go.SetActive(true);

            var tray = go.GetComponent<ConveyorTray>();
            if (tray == null) { Destroy(go); return trayDelay; }

            if (tray is IPoolable p) p.OnSpawn();

            // Đặt tray ngay tại posY đúng — food spawn scale 0→1 tại anchor
            tray.RectTransform.anchoredPosition = new Vector2(posX, trayPosY);

            if (_neutralContainer == null)
            {
                Debug.LogError("[Conveyor] _neutralContainer null!");
                Destroy(go);
                return trayDelay;
            }

            float animEnd = tray.InitializeWithList(trayFoods, foodPerConveyor,
                                                    _neutralContainer, trayDelay);
            _trays.Add(tray);
            return animEnd;
        }

        // ─── Food Source ──────────────────────────────────────────────────────

        /// <summary>
        /// Reserve đúng conveyorCount × foodPerConveyor items từ SharedFoodList.
        /// FoodTraySpawner chạy sau → nhận phần còn lại.
        /// Ví dụ: 30 food tổng, 3 tray × 3 food = reserve 9 → tray nhận 21.
        /// </summary>
        private List<FoodItemData> ReserveAndBuildFoodList(ConveyorObstacleData data)
        {
            if (OrderQueue.Instance == null)
            {
                Debug.LogError("[Conveyor] OrderQueue.Instance null!");
                return new List<FoodItemData>();
            }

            // ── FIX: reserve đúng TỔNG số food cần (count × perConveyor) ──────
            int totalToReserve = data.conveyorCount * data.foodPerConveyor;
            var result = OrderQueue.Instance.ConsumeFoodForTubes(totalToReserve);

            if (result == null || result.Count == 0)
                Debug.LogError("[Conveyor] Không reserve được food từ OrderQueue!");
            else
                Debug.Log($"[Conveyor] Reserved {result.Count} food " +
                          $"({data.conveyorCount} trays × {data.foodPerConveyor} food/tray). " +
                          $"Còn lại {OrderQueue.Instance.SharedFoodList.Count} cho FoodTray.");

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