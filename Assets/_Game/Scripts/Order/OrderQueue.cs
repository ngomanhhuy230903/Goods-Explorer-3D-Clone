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
        /// </summary>
        public MatchResult TryMatchFood(int foodID)
        {
            if (!_isInitialized)
                return MatchResult.NoMatch();

            foreach (var tray in _activeTrays)
            {
                if (tray.TryMatch(foodID, out int slotIndex))
                {
                    return MatchResult.Matched(tray, slotIndex);
                }
            }

            return MatchResult.NoMatch();
        }

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

        private List<OrderData> GenerateOrders(LevelConfig config)
        {
            var result = new List<OrderData>();
            int orderCount = config.totalFoodCount / 3;

            var foodPool = BuildBalancedFoodPool(config, orderCount);
            ShuffleList(foodPool);

            foreach (var foodData in foodPool)
                result.Add(new OrderData(foodData, totalRequired: 3));

            return result;
        }

        private List<FoodItemData> BuildBalancedFoodPool(LevelConfig config, int orderCount)
        {
            var pool = new List<FoodItemData>();
            int typeCount = config.availableFoods.Count;

            if (typeCount == 0)
            {
                Debug.LogError("[OrderQueue] availableFoods trống!");
                return pool;
            }

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
        /// Lấp đầy các slot active còn trống và tính toán lại tâm để dàn đều Order.
        /// </summary>
        private void FillActiveSlots()
        {
            int initialCount = _activeTrays.Count;
            int slotsToFill = Mathf.Min(_maxActiveOrders - initialCount, _pendingOrders.Count);

            // Nếu không có order nào cần lấp thì thoát luôn
            if (slotsToFill == 0) return;

            // Số lượng tray sẽ có trên màn hình sau khi lấp đầy
            int finalCount = initialCount + slotsToFill;

            // 1. Cập nhật lại vị trí các tray CŨ đang hiển thị để nhường chỗ cho tray mới
            for (int i = 0; i < initialCount; i++)
            {
                Vector2 newPos = CalculateTrayPosition(i, finalCount);
                _activeTrays[i].MoveTo(newPos);
            }

            // 2. Spawn và thiết lập vị trí cho các tray MỚI
            for (int i = 0; i < slotsToFill; i++)
            {
                var orderData = _pendingOrders.Dequeue();
                int newIndex = _activeTrays.Count; // Lấy index hiện tại để setup mảng
                Vector2 targetPos = CalculateTrayPosition(newIndex, finalCount);
                SpawnOrderTray(orderData, newIndex, targetPos);
            }
        }

        private void SpawnOrderTray(OrderData orderData, int slotIndex, Vector2 targetPos)
        {
            var go = PoolManager.Instance.GetOrder(Vector3.zero);
            if (go == null)
            {
                Debug.LogError("[OrderQueue] Không lấy được OrderTray từ pool!");
                return;
            }

            go.transform.SetParent(orderAreaRect, false);

            var tray = go.GetComponent<OrderTray>();
            if (tray == null)
            {
                Debug.LogError("[OrderQueue] OrderTray prefab thiếu component OrderTray!");
                return;
            }

            tray.OnCompleted += OnTrayCompleted;
            tray.OnLeft += OnTrayLeft;

            _activeTrays.Add(tray); // Add vào list trước

            // Initialize tray ngay tại targetPos chính xác của nó
            tray.Initialize(orderData, slotIndex, targetPos, enterFromTop: true);

            Log($"Spawn OrderTray [{slotIndex}] foodID={orderData.FoodID}");
        }

        /// <summary>
        /// Công thức chuẩn: Tự động chia đều để luôn lấy điểm giữa của tổng chiều rộng làm tâm (0,0).
        /// </summary>
        private Vector2 CalculateTrayPosition(int index, int totalCount)
        {
            if (totalCount <= 0) return Vector2.zero;

            // Tổng chiều rộng = (số lượng - 1) * khoảng cách
            float totalWidth = (totalCount - 1) * traySpacing;

            // Điểm bắt đầu (trái nhất)
            float startX = -totalWidth / 2f;

            return new Vector2(startX + index * traySpacing, 0f);
        }

        /// <summary>
        /// Sắp xếp lại các tray khi chỉ có tray bị xóa đi (không có order mới bù vào).
        /// </summary>
        private void RearrangeActiveTrays()
        {
            int count = _activeTrays.Count;
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                var newPos = CalculateTrayPosition(i, count);
                _activeTrays[i].MoveTo(newPos);
            }
        }

        // ─── Event Handlers ───────────────────────────────────────────────────

        private void OnTrayCompleted(OrderTray tray)
        {
            _completedOrderCount++;
            Log($"Order hoàn thành! [{_completedOrderCount}/{_totalOrderCount}]");

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
            tray.OnCompleted -= OnTrayCompleted;
            tray.OnLeft -= OnTrayLeft;

            _activeTrays.Remove(tray);

            // Thử lấp đầy slot mới. Nếu có order pending, FillActiveSlots sẽ tự xử lý việc dịch chuyển.
            int slotsToFill = Mathf.Min(_maxActiveOrders - _activeTrays.Count, _pendingOrders.Count);
            if (slotsToFill > 0)
            {
                FillActiveSlots();
            }
            else
            {
                // Nếu đã hết hàng chờ, thì chỉ cần dồn các tray còn lại vào giữa
                RearrangeActiveTrays();
            }
        }

        private void HandleOrderCompleted(int trayIndex)
        {
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