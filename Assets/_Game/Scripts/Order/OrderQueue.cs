using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;

namespace FoodMatch.Order
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  STRATEGY PATTERN – Layout algorithm có thể swap lúc runtime
    // ═══════════════════════════════════════════════════════════════════════════
    public interface ILayoutStrategy
    {
        Vector2 CalculatePosition(int index, int totalCount);
    }

    /// <summary>Trải đều các tray ngang màn hình.</summary>
    public sealed class HorizontalCenterLayout : ILayoutStrategy
    {
        private readonly float _spacing;
        public HorizontalCenterLayout(float spacing) => _spacing = spacing;

        public Vector2 CalculatePosition(int index, int totalCount)
        {
            if (totalCount <= 0) return Vector2.zero;
            float totalWidth = (totalCount - 1) * _spacing;
            float startX = -totalWidth / 2f;
            return new Vector2(startX + index * _spacing, 0f);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  BUILDER PATTERN – Xây dựng OrderData an toàn
    // ═══════════════════════════════════════════════════════════════════════════
    public sealed class OrderDataBuilder
    {
        private FoodItemData _food;
        private int _required = 3;

        public OrderDataBuilder WithFood(FoodItemData food) { _food = food; return this; }
        public OrderDataBuilder WithRequired(int count) { _required = count; return this; }
        public OrderData Build()
        {
            if (_food == null) throw new InvalidOperationException("OrderDataBuilder: food chưa được set.");
            return new OrderData(_food, _required);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  REPOSITORY PATTERN – Slot registry tách khỏi layout logic
    // ═══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Lưu trữ vị trí cố định của từng slot. Slot không thay đổi vị trí
    /// khi tray hoàn thành → food bay không bị lệch.
    /// </summary>
    public sealed class SlotRegistry
    {
        // slotIndex → anchoredPosition BẤT BIẾN (set 1 lần khi Initialize)
        private readonly Dictionary<int, Vector2> _slotPositions = new Dictionary<int, Vector2>();
        // slotIndex → tray đang chiếm slot (null = free)
        private readonly Dictionary<int, OrderTray> _slotOccupants = new Dictionary<int, OrderTray>();
        private int _totalSlots = 0;

        // ── Pre-allocate tất cả slot khi Initialize ──────────────────────────
        /// <summary>
        /// Tạo trước đủ số slot với vị trí cố định.
        /// Gọi 1 lần duy nhất trong Initialize, KHÔNG gọi lại.
        /// </summary>
        public void PreAllocateSlots(int count, System.Func<int, Vector2> positionFactory)
        {
            Clear();
            _totalSlots = count;
            for (int i = 0; i < count; i++)
                _slotPositions[i] = positionFactory(i);
            // Tất cả slot bắt đầu free (không có occupant)
        }

        /// <summary>Đặt tray vào slot đã tồn tại. Gọi sau PreAllocateSlots.</summary>
        public void OccupySlot(int slotIdx, OrderTray tray)
        {
            if (!_slotPositions.ContainsKey(slotIdx))
            {
                UnityEngine.Debug.LogError($"[SlotRegistry] Slot {slotIdx} chưa được pre-allocate!");
                return;
            }
            _slotOccupants[slotIdx] = tray;
        }

        /// <summary>Giải phóng slot, giữ nguyên vị trí để tray mới điền vào.</summary>
        public void FreeSlot(int slotIdx) => _slotOccupants.Remove(slotIdx);

        public bool TryGetPosition(int slotIdx, out Vector2 pos) =>
            _slotPositions.TryGetValue(slotIdx, out pos);

        public bool IsSlotFree(int slotIdx) => !_slotOccupants.ContainsKey(slotIdx);

        /// <summary>Trả về các slot index còn trống, theo thứ tự tăng dần.</summary>
        public List<int> GetFreeSlotIndicesSorted()
        {
            var free = new List<int>();
            for (int i = 0; i < _totalSlots; i++)
                if (!_slotOccupants.ContainsKey(i))
                    free.Add(i);
            return free; // đã theo thứ tự 0,1,2,... → slot 0 luôn điền trước
        }

        public int TotalSlots => _totalSlots;

        public void Clear()
        {
            _slotPositions.Clear();
            _slotOccupants.Clear();
            _totalSlots = 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SINGLETON + FACADE – OrderQueue điều phối toàn bộ hệ thống
    // ═══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Quản lý toàn bộ vòng đời OrderTray.
    ///
    /// KEY FIX: OrderTray hoàn thành CHỈ bay lên và ra khỏi màn hình,
    /// KHÔNG rearrange (đẩy) các tray đang active → food đang bay không bị lệch.
    /// Pending order sẽ điền vào đúng SLOT CŨ của tray vừa rời đi.
    ///
    /// Patterns: Singleton, Facade, Observer (EventBus), Strategy (Layout),
    ///           Command (MatchResult), Builder (OrderDataBuilder),
    ///           Repository (SlotRegistry), Object Pool.
    /// </summary>
    public class OrderQueue : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        public static OrderQueue Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Inspector ────────────────────────────────────────────────────────
        [Header("─── Layout ──────────────────────────")]
        [SerializeField] private RectTransform orderAreaRect;
        [SerializeField] private float traySpacing = 220f;

        [Header("─── Debug ───────────────────────────")]
        [SerializeField] private bool showDebugLog = true;

        // ── Dependencies (injected via Strategy) ─────────────────────────────
        private ILayoutStrategy _layout;
        private SlotRegistry _slotRegistry;

        // ── Runtime ──────────────────────────────────────────────────────────
        private readonly List<OrderTray> _activeTrays = new List<OrderTray>();
        private readonly Queue<OrderData> _pendingOrders = new Queue<OrderData>();

        private int _maxActiveOrders;
        private int _totalOrderCount;
        private int _completedOrderCount;
        private bool _isInitialized;

        /// <summary>
        /// Danh sách food CANONICAL – NGUỒN SỰ THẬT DUY NHẤT.
        /// FoodTraySpawner dùng list này để đảm bảo số lượng khớp với OrderQueue.
        /// </summary>
        public IReadOnlyList<FoodItemData> SharedFoodList { get; private set; }

        // ── Observer (EventBus subscriptions) ────────────────────────────────
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

        // ═════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Khởi tạo hệ thống order từ LevelConfig.
        /// Inject Strategy + khởi tạo Registry trước khi dùng.
        /// </summary>
        public void Initialize(LevelConfig config)
        {
            // Inject Strategy
            _layout = new HorizontalCenterLayout(traySpacing);
            _slotRegistry = new SlotRegistry();

            _activeTrays.Clear();
            _pendingOrders.Clear();
            _completedOrderCount = 0;
            _maxActiveOrders = config.maxActiveOrders;

            // Builder pattern qua helper nội bộ
            var canonicalList = BuildCanonicalFoodList(config);
            SharedFoodList = canonicalList.AsReadOnly();

            var orders = GenerateOrdersFromList(canonicalList);
            _totalOrderCount = orders.Count;
            foreach (var o in orders) _pendingOrders.Enqueue(o);

            _isInitialized = true;

            // Pre-allocate TẤT CẢ slot với vị trí cố định ngay từ đầu
            // Sau này FillActiveSlots chỉ cần OccupySlot vào đúng vị trí đã có
            _slotRegistry.PreAllocateSlots(_maxActiveOrders,
                idx => _layout.CalculatePosition(idx, _maxActiveOrders));

            FillActiveSlots();

            Log($"Init: {_totalOrderCount} orders | {canonicalList.Count} foods | max={_maxActiveOrders}");
        }

        /// <summary>
        /// COMMAND PATTERN: trả về MatchResult, caller tự Execute() khi muốn.
        /// </summary>
        public MatchResult TryMatchFood(int foodID)
        {
            if (!_isInitialized) return MatchResult.NoMatch();

            foreach (var tray in _activeTrays)
                if (tray.TryMatch(foodID, out int slotIndex))
                    return MatchResult.Matched(tray, slotIndex);

            return MatchResult.NoMatch();
        }

        public void Reset()
        {
            foreach (var tray in _activeTrays)
            {
                UnsubscribeTray(tray);
                PoolManager.Instance.ReturnOrder(tray.gameObject);
            }
            _activeTrays.Clear();
            _pendingOrders.Clear();
            _slotRegistry?.Clear();
            _completedOrderCount = 0;
            _isInitialized = false;
            SharedFoodList = null;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  FOOD LIST GENERATION
        // ═════════════════════════════════════════════════════════════════════

        private List<FoodItemData> BuildCanonicalFoodList(LevelConfig config)
        {
            var result = new List<FoodItemData>();
            int typeCount = config.availableFoods.Count;

            if (typeCount == 0) { Debug.LogError("[OrderQueue] availableFoods trống!"); return result; }

            int orderCount = config.totalFoodCount / 3;
            int basePerType = orderCount / typeCount;
            int remainder = orderCount % typeCount;

            for (int i = 0; i < typeCount; i++)
            {
                int ordersForType = basePerType + (i < remainder ? 1 : 0);
                for (int j = 0; j < ordersForType * 3; j++)
                    result.Add(config.availableFoods[i]);
            }

            ShuffleList(result);
            Log($"BuildCanonical: {result.Count} foods / {typeCount} types | base={basePerType} rem={remainder}");
            return result;
        }

        private List<OrderData> GenerateOrdersFromList(List<FoodItemData> canonicalList)
        {
            var countMap = new Dictionary<FoodItemData, int>();
            foreach (var food in canonicalList)
            {
                if (!countMap.ContainsKey(food)) countMap[food] = 0;
                countMap[food]++;
            }

            var orders = new List<OrderData>();
            foreach (var kvp in countMap)
            {
                int orderCountForType = kvp.Value / 3;
                for (int i = 0; i < orderCountForType; i++)
                {
                    // BUILDER PATTERN
                    orders.Add(new OrderDataBuilder()
                        .WithFood(kvp.Key)
                        .WithRequired(3)
                        .Build());
                }
            }

            ShuffleList(orders);
            return orders;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SLOT MANAGEMENT  ← KEY FIX: slot position không thay đổi
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Điền pending orders vào các slot còn free trong registry.
        /// Registry là SINGLE SOURCE OF TRUTH – vị trí slot không bao giờ thay đổi.
        /// </summary>
        private void FillActiveSlots()
        {
            // Lấy danh sách slot free theo thứ tự (0,1,2,...) → điền tuần tự
            var freeSlots = _slotRegistry.GetFreeSlotIndicesSorted();

            int toFill = Mathf.Min(freeSlots.Count, _pendingOrders.Count);
            if (toFill == 0) return;

            for (int i = 0; i < toFill; i++)
            {
                int slotIdx = freeSlots[i];
                _slotRegistry.TryGetPosition(slotIdx, out Vector2 targetPos);
                var orderData = _pendingOrders.Dequeue();
                SpawnOrderTray(orderData, slotIdx, targetPos);
            }
        }

        private void SpawnOrderTray(OrderData orderData, int slotIdx, Vector2 targetPos)
        {
            var go = PoolManager.Instance.GetOrder(Vector3.zero);
            if (go == null) { Debug.LogError("[OrderQueue] Pool không trả được OrderTray!"); return; }

            go.transform.SetParent(orderAreaRect, false);

            var tray = go.GetComponent<OrderTray>();
            if (tray == null) { Debug.LogError("[OrderQueue] Thiếu component OrderTray!"); return; }

            // Đánh dấu slot đang bị chiếm – Registry biết slot này không còn free
            _slotRegistry.OccupySlot(slotIdx, tray);

            SubscribeTray(tray);
            _activeTrays.Add(tray);

            tray.Initialize(orderData, slotIdx, targetPos, enterFromTop: true);
            Log($"Spawn tray slot={slotIdx} pos={targetPos} foodID={orderData.FoodID}");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// KEY FIX: Tray hoàn thành → chỉ tăng counter + kiểm tra win.
        /// KHÔNG rearrange, KHÔNG dịch chuyển bất kỳ tray nào.
        /// </summary>
        private void OnTrayCompleted(OrderTray tray)
        {
            _completedOrderCount++;
            Log($"Order done [{_completedOrderCount}/{_totalOrderCount}]");

            bool allDone = _completedOrderCount >= _totalOrderCount
                        && _pendingOrders.Count == 0;

            if (allDone)
            {
                Log("ALL ORDERS COMPLETED → WIN!");
                EventBus.RaiseAllOrdersCompleted();
            }
            // Không gọi RearrangeActiveTrays ở đây → fix lệch vị trí food
        }

        /// <summary>
        /// Tray đã rời màn hình (sau animation bay lên).
        /// Giải phóng slot → FillActiveSlots vào ĐÚNG VỊ TRÍ CŨ.
        /// </summary>
        private void OnTrayLeft(OrderTray tray)
        {
            UnsubscribeTray(tray);
            _activeTrays.Remove(tray);

            // Giải phóng slot trong registry (giữ nguyên vị trí)
            _slotRegistry.FreeSlot(tray.TrayIndex);

            // Điền pending vào slot cũ nếu còn
            if (_pendingOrders.Count > 0)
                FillActiveSlots();

            // KHÔNG RearrangeActiveTrays → các tray đang hiển thị không nhảy
        }

        private void HandleOrderCompleted(int trayIndex) =>
            Log($"[EventBus] OrderCompleted trayIndex={trayIndex}");

        private void HandleOrderLeft(int trayIndex) =>
            Log($"[EventBus] OrderLeft trayIndex={trayIndex}");

        // ═════════════════════════════════════════════════════════════════════
        //  UTILITY
        // ═════════════════════════════════════════════════════════════════════

        private void SubscribeTray(OrderTray tray)
        {
            tray.OnCompleted += OnTrayCompleted;
            tray.OnLeft += OnTrayLeft;
        }

        private void UnsubscribeTray(OrderTray tray)
        {
            tray.OnCompleted -= OnTrayCompleted;
            tray.OnLeft -= OnTrayLeft;
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void Log(string msg)
        {
            if (showDebugLog) Debug.Log($"[OrderQueue] {msg}");
        }
    }
}