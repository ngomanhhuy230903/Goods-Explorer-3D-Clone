using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;

namespace FoodMatch.Order
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  STRATEGY PATTERN
    // ═══════════════════════════════════════════════════════════════════════════
    public interface ILayoutStrategy
    {
        Vector2 CalculatePosition(int index, int totalCount);
    }

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
    //  BUILDER PATTERN
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

    public sealed class SlotRegistry
    {
        private readonly Dictionary<int, Vector2> _slotPositions = new();
        private readonly Dictionary<int, OrderTray> _slotOccupants = new();
        private int _totalSlots = 0;

        public void PreAllocateSlots(int count, Func<int, Vector2> positionFactory)
        {
            Clear();
            _totalSlots = count;
            for (int i = 0; i < count; i++)
                _slotPositions[i] = positionFactory(i);
        }

        public void OccupySlot(int slotIdx, OrderTray tray)
        {
            if (!_slotPositions.ContainsKey(slotIdx))
            {
                Debug.LogError($"[SlotRegistry] Slot {slotIdx} chưa được pre-allocate!");
                return;
            }
            _slotOccupants[slotIdx] = tray;
        }

        public void FreeSlot(int slotIdx) => _slotOccupants.Remove(slotIdx);

        public bool TryGetPosition(int slotIdx, out Vector2 pos) =>
            _slotPositions.TryGetValue(slotIdx, out pos);

        public bool IsSlotFree(int slotIdx) => !_slotOccupants.ContainsKey(slotIdx);

        public List<int> GetFreeSlotIndicesSorted()
        {
            var free = new List<int>();
            for (int i = 0; i < _totalSlots; i++)
                if (!_slotOccupants.ContainsKey(i))
                    free.Add(i);
            return free;
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
    //  INTERFACE
    // ═══════════════════════════════════════════════════════════════════════════
    public interface IOrderTrayProvider
    {
        IReadOnlyList<OrderTray> GetActiveTrays();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ORDER QUEUE
    // ═══════════════════════════════════════════════════════════════════════════
    public class OrderQueue : MonoBehaviour, IOrderTrayProvider
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

        // ── Dependencies ─────────────────────────────────────────────────────
        private ILayoutStrategy _layout;
        private SlotRegistry _slotRegistry;

        // ── Runtime ──────────────────────────────────────────────────────────
        private readonly List<OrderTray> _activeTrays = new();
        private readonly Queue<OrderData> _pendingOrders = new();

        private int _maxActiveOrders;
        private int _totalOrderCount;
        private int _completedOrderCount;
        private bool _isInitialized;

        /// <summary>
        /// SOURCE OF TRUTH: danh sách food chuẩn được dùng bởi cả OrderQueue và FoodTray.
        /// FoodTraySpawner lấy copy của list này qua OrderQueue.Instance.SharedFoodList.
        /// </summary>
        public IReadOnlyList<FoodItemData> SharedFoodList { get; private set; }

        // ── IOrderTrayProvider ────────────────────────────────────────────────
        public IReadOnlyList<OrderTray> GetActiveTrays() => _activeTrays;

        // ── EventBus ─────────────────────────────────────────────────────────
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

        public void Initialize(LevelConfig config)
        {
            _layout = new HorizontalCenterLayout(traySpacing);
            _slotRegistry = new SlotRegistry();

            _activeTrays.Clear();
            _pendingOrders.Clear();
            _completedOrderCount = 0;
            _maxActiveOrders = config.maxActiveOrders;

            // ── Build canonical food list ─────────────────────────────────────
            // Đây là SOURCE OF TRUTH duy nhất.
            // FoodTraySpawner sẽ lấy ĐÚNG list này qua SharedFoodList.
            var canonicalList = BuildCanonicalFoodList(config);
            SharedFoodList = canonicalList.AsReadOnly();

            var orders = GenerateOrdersFromList(canonicalList);
            _totalOrderCount = orders.Count;
            foreach (var o in orders) _pendingOrders.Enqueue(o);

            _isInitialized = true;

            _slotRegistry.PreAllocateSlots(_maxActiveOrders,
                idx => _layout.CalculatePosition(idx, _maxActiveOrders));

            SlotReservationRegistry.Instance.ClearAll();
            FillActiveSlots();

            Log($"Init: {_totalOrderCount} orders | {canonicalList.Count} foods | max={_maxActiveOrders}");
        }

        public MatchResult TryMatchFood(int foodID)
        {
            if (!_isInitialized) return MatchResult.NoMatch();
            foreach (var tray in _activeTrays)
                if (tray.TryMatchAndReserve(foodID, 0, out int slotIndex))
                    return MatchResult.Matched(tray, slotIndex);
            return MatchResult.NoMatch();
        }

        public MatchResult TryMatchFoodWithReservation(int foodID, int foodInstanceId)
        {
            if (!_isInitialized) return MatchResult.NoMatch();
            foreach (var tray in _activeTrays)
            {
                if (tray == null) continue;
                if (tray.CurrentStateId != OrderTrayStateId.Active) continue;
                if (tray.TryMatchAndReserve(foodID, foodInstanceId, out int slotIndex))
                    return MatchResult.Matched(tray, slotIndex);
            }
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
            SlotReservationRegistry.Instance.ClearAll();
            _completedOrderCount = 0;
            _isInitialized = false;
            SharedFoodList = null;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  FOOD LIST GENERATION
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Xây dựng canonical food list từ config.
        ///
        /// QUAN TRỌNG: config.totalFoodCount đã được LevelGeneratorEditor tính
        /// sao cho totalFood = (gridCapacity / divisor) * divisor
        /// với divisor = foodTypes * 3 → chia đều hoàn toàn, không bao giờ thừa capacity.
        ///
        /// Ở đây chỉ cần tin vào totalFood và chia đều theo typeCount.
        /// Nếu vì lý do nào totalFood không chia hết cho typeCount*3
        /// (ví dụ config cũ chưa gen lại), tự điều chỉnh xuống để an toàn.
        /// </summary>
        private List<FoodItemData> BuildCanonicalFoodList(LevelConfig config)
        {
            var result = new List<FoodItemData>();
            int typeCount = config.availableFoods.Count;

            if (typeCount == 0)
            {
                Debug.LogError("[OrderQueue] availableFoods trống!");
                return result;
            }

            int totalFood = config.totalFoodCount;
            int divisor = typeCount * 3;

            // Làm tròn XUỐNG để đảm bảo totalFood ≤ capacity
            // (nếu config đã đúng thì totalFood % divisor == 0, không thay đổi gì)
            if (totalFood % divisor != 0)
            {
                int corrected = (totalFood / divisor) * divisor;
                Debug.LogWarning($"[OrderQueue] totalFood={totalFood} không chia hết cho " +
                                 $"typeCount*3={divisor}. Làm tròn XUỐNG thành {corrected}. " +
                                 $"Hãy gen lại LevelConfig bằng LevelGeneratorEditor!");
                totalFood = Mathf.Max(divisor, corrected);
            }

            // Mỗi loại có đúng foodPerType items — chia đều tuyệt đối
            int foodPerType = totalFood / typeCount;

            for (int i = 0; i < typeCount; i++)
                for (int j = 0; j < foodPerType; j++)
                    result.Add(config.availableFoods[i]);

            ShuffleList(result);
            Log($"BuildCanonical: {result.Count} foods / {typeCount} types / " +
                $"{foodPerType} mỗi loại / {foodPerType / 3} orders mỗi loại");
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
                int orderCount = kvp.Value / 3;
                for (int i = 0; i < orderCount; i++)
                    orders.Add(new OrderDataBuilder()
                        .WithFood(kvp.Key)
                        .WithRequired(3)
                        .Build());
            }

            ShuffleList(orders);
            return orders;
        }

        private void FillActiveSlots()
        {
            var freeSlots = _slotRegistry.GetFreeSlotIndicesSorted();
            int toFill = Mathf.Min(freeSlots.Count, _pendingOrders.Count);
            if (toFill == 0) return;

            for (int i = 0; i < toFill; i++)
            {
                int slotIdx = freeSlots[i];
                _slotRegistry.TryGetPosition(slotIdx, out Vector2 targetPos);
                SpawnOrderTray(_pendingOrders.Dequeue(), slotIdx, targetPos);
            }
        }

        private void SpawnOrderTray(OrderData orderData, int slotIdx, Vector2 targetPos)
        {
            var go = PoolManager.Instance.GetOrder(Vector3.zero);
            if (go == null) { Debug.LogError("[OrderQueue] Pool không trả được OrderTray!"); return; }

            go.transform.SetParent(orderAreaRect, false);

            var tray = go.GetComponent<OrderTray>();
            if (tray == null) { Debug.LogError("[OrderQueue] Thiếu component OrderTray!"); return; }

            _slotRegistry.OccupySlot(slotIdx, tray);
            SubscribeTray(tray);
            _activeTrays.Add(tray);

            tray.Initialize(orderData, slotIdx, targetPos, enterFromTop: true);
            Log($"Spawn tray slot={slotIdx} pos={targetPos} foodID={orderData.FoodID}");

            // ── FIX BUG 2: Raise event để FoodFlowController scan BackupTray ──
            // Khi order mới xuất hiện, food trong BackupTray có thể đã match được
            // nhưng chưa tự bay lên vì chưa có event trigger. Raise ở đây để
            // HandleNewOrderActive trong FoodFlowController chạy auto-match ngay.
            EventBus.RaiseNewOrderActive(orderData.FoodID);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ═════════════════════════════════════════════════════════════════════

        private void OnTrayCompleted(OrderTray tray)
        {
            _completedOrderCount++;
            Log($"Order done [{_completedOrderCount}/{_totalOrderCount}]");

            if (_completedOrderCount >= _totalOrderCount && _pendingOrders.Count == 0)
            {
                Log("ALL ORDERS COMPLETED → WIN!");
                EventBus.RaiseAllOrdersCompleted();
            }
        }

        private void OnTrayLeft(OrderTray tray)
        {
            UnsubscribeTray(tray);
            _activeTrays.Remove(tray);
            SlotReservationRegistry.Instance.ClearOrderTray(tray.TrayIndex);
            _slotRegistry.FreeSlot(tray.TrayIndex);
            if (_pendingOrders.Count > 0) FillActiveSlots();
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