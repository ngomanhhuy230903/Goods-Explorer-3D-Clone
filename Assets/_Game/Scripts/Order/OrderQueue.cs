using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;

namespace FoodMatch.Order
{
    /// <summary>
    /// Quản lý toàn bộ vòng đời của các OrderTray:
    ///   - Sinh order từ danh sách food trong level
    ///   - Spawn/despawn OrderTray qua Pool
    ///   - Auto-match khi player tap food
    ///   - Kiểm tra AllOrdersCompleted → Win
    /// Là Singleton trong Scene, gắn vào GameObject "OrderQueue".
    /// </summary>
    public class OrderQueue : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Layout ──────────────────────────")]
        [Tooltip("Parent RectTransform chứa các OrderTray trên UI.")]
        [SerializeField] private RectTransform orderAreaRect;

        [Tooltip("Khoảng cách ngang giữa các OrderTray.")]
        [SerializeField] private float traySpacing = 220f;

        [Header("─── Debug ───────────────────────────")]
        [SerializeField] private bool showDebugLog = true;

        // ─── Runtime ──────────────────────────────────────────────────────────
        // Các order đang hiển thị trên màn hình
        private readonly List<OrderTray> _activeTrays = new List<OrderTray>();

        // Queue chứa các OrderData chưa được hiển thị (chờ slot trống)
        private readonly Queue<OrderData> _pendingOrders = new Queue<OrderData>();

        private int _maxActiveOrders;     // Từ LevelConfig.maxActiveCustomers
        private int _totalOrderCount;     // Tổng số order trong level
        private int _completedOrderCount; // Đã hoàn thành bao nhiêu
        private bool _isInitialized;

        // ─────────────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            EventBus.OnOrderCompleted += HandleOrderCompleted;
            EventBus.OnOrderLeft += HandleOrderLeft;
        }

        private void OnDisable()
        {
            EventBus.OnOrderCompleted -= HandleOrderCompleted;
            EventBus.OnOrderLeft -= HandleOrderLeft;
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Khởi tạo hệ thống order từ LevelConfig.
        /// Gọi bởi LevelManager khi bắt đầu level.
        /// </summary>
        public void Initialize(LevelConfig config)
        {
            _activeTrays.Clear();
            _pendingOrders.Clear();
            _completedOrderCount = 0;
            _maxActiveOrders = config.maxActiveOrders;

            // ── Sinh danh sách OrderData từ totalFoodCount ──────────────────
            // Mỗi order = 1 loại food x 3 lần
            // Tổng food = totalFoodCount → số order = totalFoodCount / 3
            var orders = GenerateOrders(config);
            _totalOrderCount = orders.Count;

            // Đẩy vào queue
            foreach (var o in orders)
                _pendingOrders.Enqueue(o);

            _isInitialized = true;

            // Hiển thị ngay các order đầu tiên (số lượng = maxActiveOrders)
            FillActiveSlots();

            Log($"Khởi tạo xong: {_totalOrderCount} orders, " +
                $"hiển thị tối đa {_maxActiveOrders} cùng lúc.");
        }

        /// <summary>
        /// Xử lý khi player tap vào 1 food.
        /// Quét toàn bộ active tray để tìm match.
        /// Trả về kết quả qua MatchResult.
        /// </summary>
        public MatchResult TryMatchFood(int foodID)
        {
            if (!_isInitialized)
                return MatchResult.NoMatch();

            // Quét từng active tray
            foreach (var tray in _activeTrays)
            {
                if (tray.TryMatch(foodID, out int slotIndex))
                {
                    return MatchResult.Matched(tray, slotIndex);
                }
            }

            // Không match → về backup tray
            return MatchResult.NoMatch();
        }

        /// <summary>
        /// Reset toàn bộ khi load lại level.
        /// </summary>
        public void Reset()
        {
            foreach (var tray in _activeTrays)
                PoolManager.Instance.ReturnOrder(tray.gameObject);

            _activeTrays.Clear();
            _pendingOrders.Clear();
            _completedOrderCount = 0;
            _isInitialized = false;
        }

        // ─── Order Generation ─────────────────────────────────────────────────

        /// <summary>
        /// Sinh danh sách OrderData từ LevelConfig.
        /// Logic: totalFoodCount / 3 = số order.
        /// Mỗi order lấy ngẫu nhiên 1 loại food từ availableFoods.
        /// Đảm bảo tổng số món đặt ra = totalFoodCount (chia hết cho 3).
        /// </summary>
        private List<OrderData> GenerateOrders(LevelConfig config)
        {
            var result = new List<OrderData>();
            int orderCount = config.totalFoodCount / 3;

            // Tạo pool food ID cân bằng: mỗi food xuất hiện đều nhau
            var foodPool = BuildBalancedFoodPool(config, orderCount);

            // Xáo trộn để ngẫu nhiên thứ tự order
            ShuffleList(foodPool);

            foreach (var foodData in foodPool)
                result.Add(new OrderData(foodData, totalRequired: 3));

            return result;
        }

        /// <summary>
        /// Tạo pool food cân bằng:
        /// Chia đều orderCount cho các loại food có sẵn.
        /// Ví dụ: 10 orders, 2 loại food → mỗi loại 5 order.
        /// </summary>
        private List<FoodItemData> BuildBalancedFoodPool(LevelConfig config, int orderCount)
        {
            var pool = new List<FoodItemData>();
            int typeCount = config.availableFoods.Count;

            if (typeCount == 0)
            {
                Debug.LogError("[OrderQueue] availableFoods trống!");
                return pool;
            }

            // Phân bổ đều
            int basePerType = orderCount / typeCount;
            int remainder = orderCount % typeCount;

            for (int i = 0; i < typeCount; i++)
            {
                int count = basePerType + (i < remainder ? 1 : 0);
                for (int j = 0; j < count; j++)
                    pool.Add(config.availableFoods[i]);
            }

            return pool;
        }

        // ─── Slot Management ──────────────────────────────────────────────────

        /// <summary>
        /// Lấp đầy các slot active còn trống từ pendingOrders.
        /// Gọi mỗi khi 1 order rời khỏi màn hình.
        /// </summary>
        private void FillActiveSlots()
        {
            while (_activeTrays.Count < _maxActiveOrders
                   && _pendingOrders.Count > 0)
            {
                var orderData = _pendingOrders.Dequeue();
                SpawnOrderTray(orderData);
            }
        }

        private void SpawnOrderTray(OrderData orderData)
        {
            // Lấy từ pool
            var go = PoolManager.Instance.GetOrder(Vector3.zero);
            if (go == null)
            {
                Debug.LogError("[OrderQueue] Không lấy được OrderTray từ pool!");
                return;
            }

            // Gắn vào orderArea (UI)
            go.transform.SetParent(orderAreaRect, false);

            var tray = go.GetComponent<OrderTray>();
            if (tray == null)
            {
                Debug.LogError("[OrderQueue] OrderTray prefab thiếu component OrderTray!");
                return;
            }

            // Tính vị trí anchoredPosition
            int slotIndex = _activeTrays.Count;
            Vector2 targetPos = CalculateTrayPosition(slotIndex);

            // Subscribe event (dùng lambda + index để tránh nhầm)
            tray.OnCompleted += OnTrayCompleted;
            tray.OnLeft += OnTrayLeft;

            _activeTrays.Add(tray);
            tray.Initialize(orderData, slotIndex, targetPos, enterFromTop: true);

            Log($"Spawn OrderTray [{slotIndex}] foodID={orderData.FoodID}");
        }

        /// <summary>
        /// Tính anchoredPosition cho tray dựa theo index và số lượng active.
        /// Căn giữa theo chiều ngang.
        /// </summary>
        private Vector2 CalculateTrayPosition(int index)
        {
            // Tổng chiều rộng của tất cả tray
            int count = Mathf.Max(_activeTrays.Count, 1);
            float totalWidth = count * traySpacing;
            float startX = -(totalWidth - traySpacing) / 2f;

            return new Vector2(startX + index * traySpacing, 0f);
        }

        /// <summary>
        /// Tính lại và cập nhật vị trí tất cả active tray.
        /// Gọi sau khi 1 tray rời đi để các tray còn lại dồn lại giữa.
        /// </summary>
        private void RearrangeActiveTrays()
        {
            int count = _activeTrays.Count;
            if (count == 0) return;

            float totalWidth = count * traySpacing;
            float startX = -(totalWidth - traySpacing) / 2f;

            for (int i = 0; i < count; i++)
            {
                var newPos = new Vector2(startX + i * traySpacing, 0f);
                _activeTrays[i].MoveTo(newPos);
            }
        }

        // ─── Event Handlers ───────────────────────────────────────────────────

        private void OnTrayCompleted(OrderTray tray)
        {
            _completedOrderCount++;
            Log($"Order hoàn thành! [{_completedOrderCount}/{_totalOrderCount}]");

            // Kiểm tra win ngay khi order cuối completed
            // (không đợi Leave vì có thể còn pending orders)
            bool allDone = _completedOrderCount >= _totalOrderCount
                           && _pendingOrders.Count == 0;

            if (allDone)
            {
                Log("TẤT CẢ ORDER HOÀN THÀNH → WIN!");
                EventBus.RaiseAllOrdersCompleted();
            }
        }

        private void OnTrayLeft(OrderTray tray)
        {
            // Unsubscribe để tránh memory leak
            tray.OnCompleted -= OnTrayCompleted;
            tray.OnLeft -= OnTrayLeft;

            // Xóa khỏi danh sách active
            _activeTrays.Remove(tray);

            // Dồn lại vị trí
            RearrangeActiveTrays();

            // Lấp slot mới từ queue
            FillActiveSlots();
        }

        private void HandleOrderCompleted(int trayIndex)
        {
            // EventBus handler — hiện tại chỉ log, có thể dùng cho UI/Audio sau
            Log($"[EventBus] OrderCompleted trayIndex={trayIndex}");
        }

        private void HandleOrderLeft(int trayIndex)
        {
            Log($"[EventBus] OrderLeft trayIndex={trayIndex}");
        }

        // ─── Utility ──────────────────────────────────────────────────────────

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
            if (showDebugLog)
                Debug.Log($"[OrderQueue] {msg}");
        }
    }
}