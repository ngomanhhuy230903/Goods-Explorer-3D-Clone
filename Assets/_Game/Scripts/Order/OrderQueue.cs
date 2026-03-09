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
        private readonly List<OrderTray> _activeTrays = new List<OrderTray>();
        private readonly Queue<OrderData> _pendingOrders = new Queue<OrderData>();

        private int _maxActiveOrders;
        private int _totalOrderCount;
        private int _completedOrderCount;
        private bool _isInitialized;

        /// <summary>
        /// Danh sách food CANONICAL được sinh ra lúc Initialize().
        /// FoodTraySpawner sẽ dùng đúng list này để đảm bảo số lượng
        /// từng loại food khớp hoàn toàn với OrderQueue.
        /// Key: FoodItemData, Value: số lượng cần spawn vào FoodTray.
        /// </summary>
        public IReadOnlyList<FoodItemData> SharedFoodList { get; private set; }

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
        /// Sau khi gọi xong, SharedFoodList đã sẵn sàng để truyền sang FoodTraySpawner.
        /// </summary>
        public void Initialize(LevelConfig config)
        {
            _activeTrays.Clear();
            _pendingOrders.Clear();
            _completedOrderCount = 0;
            _maxActiveOrders = config.maxActiveOrders;

            // ── Sinh danh sách food canonical (1 nguồn duy nhất) ────────────
            var canonicalFoodList = BuildCanonicalFoodList(config);
            SharedFoodList = canonicalFoodList.AsReadOnly();

            // ── Sinh OrderData từ canonical list ─────────────────────────────
            var orders = GenerateOrdersFromList(canonicalFoodList);
            _totalOrderCount = orders.Count;

            foreach (var o in orders)
                _pendingOrders.Enqueue(o);

            _isInitialized = true;

            FillActiveSlots();

            Log($"Khởi tạo xong: {_totalOrderCount} orders, " +
                $"SharedFoodList={canonicalFoodList.Count} items, " +
                $"hiển thị tối đa {_maxActiveOrders} cùng lúc.");
        }

        public MatchResult TryMatchFood(int foodID)
        {
            if (!_isInitialized)
                return MatchResult.NoMatch();

            foreach (var tray in _activeTrays)
            {
                if (tray.TryMatch(foodID, out int slotIndex))
                    return MatchResult.Matched(tray, slotIndex);
            }

            return MatchResult.NoMatch();
        }

        public void Reset()
        {
            foreach (var tray in _activeTrays)
            {
                tray.OnCompleted -= OnTrayCompleted;
                tray.OnLeft -= OnTrayLeft;     
                PoolManager.Instance.ReturnOrder(tray.gameObject);
            }

            _activeTrays.Clear();
            _pendingOrders.Clear();
            _completedOrderCount = 0;
            _isInitialized = false;
            SharedFoodList = null;
        }

        // ─── Food List Generation ─────────────────────────────────────────────

        /// <summary>
        /// Tạo danh sách food canonical: chia đều số lượng theo type,
        /// tổng = config.totalFoodCount, mỗi item xuất hiện bội số 3 lần.
        /// Đây là NGUỒN SỰ THẬT DUY NHẤT cho cả OrderQueue lẫn FoodTraySpawner.
        /// </summary>
        private List<FoodItemData> BuildCanonicalFoodList(LevelConfig config)
        {
            var result = new List<FoodItemData>();
            int orderCount = config.totalFoodCount / 3; // Mỗi order cần 3 món cùng loại
            int typeCount = config.availableFoods.Count;

            if (typeCount == 0)
            {
                Debug.LogError("[OrderQueue] availableFoods trống!");
                return result;
            }

            // Chia đều orderCount cho các type
            int basePerType = orderCount / typeCount;
            int remainder = orderCount % typeCount;

            for (int i = 0; i < typeCount; i++)
            {
                // Số order của type này = basePerType hoặc basePerType+1
                int ordersForType = basePerType + (i < remainder ? 1 : 0);

                // Mỗi order cần 3 food → nhân 3
                for (int j = 0; j < ordersForType * 3; j++)
                    result.Add(config.availableFoods[i]);
            }

            // Shuffle để FoodTray spawn ngẫu nhiên
            ShuffleList(result);

            Log($"BuildCanonicalFoodList: {result.Count} foods từ {typeCount} types. " +
                $"basePerType={basePerType}, remainder={remainder}");

            return result;
        }

        /// <summary>
        /// Sinh OrderData từ canonical food list đã có.
        /// Đếm số lượng từng type rồi tạo order tương ứng.
        /// </summary>
        private List<OrderData> GenerateOrdersFromList(List<FoodItemData> canonicalList)
        {
            // Đếm số lần xuất hiện của từng FoodItemData
            var countMap = new Dictionary<FoodItemData, int>();
            foreach (var food in canonicalList)
            {
                if (!countMap.ContainsKey(food)) countMap[food] = 0;
                countMap[food]++;
            }

            var orders = new List<OrderData>();
            foreach (var kvp in countMap)
            {
                // Số order của type này = count / 3
                int orderCountForType = kvp.Value / 3;
                for (int i = 0; i < orderCountForType; i++)
                    orders.Add(new OrderData(kvp.Key, totalRequired: 3));
            }

            // Shuffle order list để đa dạng thứ tự hiển thị
            ShuffleList(orders);

            return orders;
        }

        // ─── Slot Management ──────────────────────────────────────────────────

        private void FillActiveSlots()
        {
            int initialCount = _activeTrays.Count;
            int slotsToFill = Mathf.Min(_maxActiveOrders - initialCount, _pendingOrders.Count);

            if (slotsToFill == 0) return;

            int finalCount = initialCount + slotsToFill;

            for (int i = 0; i < initialCount; i++)
            {
                Vector2 newPos = CalculateTrayPosition(i, finalCount);
                _activeTrays[i].MoveTo(newPos);
            }

            for (int i = 0; i < slotsToFill; i++)
            {
                var orderData = _pendingOrders.Dequeue();
                int newIndex = _activeTrays.Count;
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

            _activeTrays.Add(tray);

            tray.Initialize(orderData, slotIndex, targetPos, enterFromTop: true);

            Log($"Spawn OrderTray [{slotIndex}] foodID={orderData.FoodID}");
        }

        private Vector2 CalculateTrayPosition(int index, int totalCount)
        {
            if (totalCount <= 0) return Vector2.zero;
            float totalWidth = (totalCount - 1) * traySpacing;
            float startX = -totalWidth / 2f;
            return new Vector2(startX + index * traySpacing, 0f);
        }

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

            int slotsToFill = Mathf.Min(_maxActiveOrders - _activeTrays.Count, _pendingOrders.Count);
            if (slotsToFill > 0)
                FillActiveSlots();
            else
                RearrangeActiveTrays();
        }

        private void HandleOrderCompleted(int trayIndex) =>
            Log($"[EventBus] OrderCompleted trayIndex={trayIndex}");

        private void HandleOrderLeft(int trayIndex) =>
            Log($"[EventBus] OrderLeft trayIndex={trayIndex}");

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