using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Food;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Khay chứa đồ thừa. Slot được reserve TRƯỚC khi food bay đến để tránh collision.
    /// </summary>
    public class BackupTray : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Slot Visual ──────────────────────")]
        [SerializeField] private GameObject slotVisualPrefab;

        [Header("─── Warning ──────────────────────────")]
        [SerializeField] private int warningThreshold = 2;

        [Header("─── Expansion ────────────────────────")]
        [SerializeField] private int maxSlots = 7;
        [SerializeField] private float shiftDuration = 0.35f;

        [Header("─── Debug ───────────────────────────")]
        [SerializeField] private bool showDebugLog = true;

        // ─── Runtime ──────────────────────────────────────────────────────────
        private readonly List<Transform> _slotAnchors = new List<Transform>();

        // slot index → food item đang chiếm (confirmed)
        private readonly Dictionary<int, FoodItem> _occupants = new Dictionary<int, FoodItem>();

        private float _slotSpacing = 1.5f;
        private int _capacity = 0;
        private bool _warningActive = false;

        // ─── Public Properties ────────────────────────────────────────────────
        public int OccupiedCount => _occupants.Count;
        public int Capacity => _capacity;

        // ─── Reservation-aware free slot check ───────────────────────────────

        /// <summary>
        /// Trả về index slot trống VÀ chưa bị reserve.
        /// Dùng cho kiểm tra trước khi tạo command.
        /// </summary>
        public bool HasFreeSlot() => TryGetFreeSlotIndex(out _);

        /// <summary>
        /// Reserve slot tiếp theo cho 1 food cụ thể.
        /// Gọi TRƯỚC khi bắt đầu animation bay.
        /// Trả về slot index, hoặc -1 nếu không còn slot.
        /// </summary>
        public int TryReserveNextSlot(int foodInstanceId)
        {
            if (!TryGetFreeSlotIndex(out int idx)) return -1;

            if (!SlotReservationRegistry.Instance.TryReserveBackupSlot(idx, foodInstanceId))
            {
                // Slot vừa bị snatch — tìm lại
                for (int i = 0; i < _slotAnchors.Count; i++)
                {
                    if ((_occupants.ContainsKey(i) && _occupants[i] != null)) continue;
                    if (SlotReservationRegistry.Instance.IsBackupSlotReserved(i)) continue;
                    if (SlotReservationRegistry.Instance.TryReserveBackupSlot(i, foodInstanceId))
                        return i;
                }
                return -1;
            }

            return idx;
        }

        // ─── External Setup ───────────────────────────────────────────────────

        public void SetSlotAnchors(List<Transform> anchors)
        {
            _slotAnchors.Clear();
            _slotAnchors.AddRange(anchors);
            _capacity = _slotAnchors.Count;
            Log($"SetSlotAnchors: {_capacity} anchors injected.");
        }

        public void ResetTray(int capacity)
        {
            foreach (var kv in _occupants)
                if (kv.Value != null)
                    PoolManager.Instance.ReturnFood(kv.Value.FoodID, kv.Value.gameObject);

            _occupants.Clear();
            _capacity = capacity;
            _warningActive = false;
            Log($"ResetTray: capacity={capacity}");
        }

        public void Initialize(int initialSlotCount, float slotSpacing = 1.5f)
        {
            _slotSpacing = slotSpacing;
            ClearSlotsAndFood();

            float totalWidth = (initialSlotCount - 1) * slotSpacing;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < initialSlotCount; i++)
            {
                var anchor = CreateSlotAnchor(i, new Vector3(startX + i * slotSpacing, 0f, 0f));
                _slotAnchors.Add(anchor);
            }

            _capacity = initialSlotCount;
            Log($"Initialize: {_capacity} slots.");
        }

        // ─── CORE API ─────────────────────────────────────────────────────────

        /// <summary>
        /// World position của slot index cụ thể.
        /// FoodFlowController dùng slotIndex từ reservation để lấy đích bay.
        /// </summary>
        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotAnchors.Count) return Vector3.zero;
            return _slotAnchors[slotIndex].position;
        }

        /// <summary>Legacy — dùng GetSlotWorldPosition(slotIndex) thay thế.</summary>
        public Vector3 GetNextSlotWorldPosition()
        {
            TryGetFreeSlotIndex(out int idx);
            return idx >= 0 ? _slotAnchors[idx].position : Vector3.zero;
        }

        public Transform GetAnchorForFood(FoodItem food)
        {
            foreach (var kv in _occupants)
                if (kv.Value == food && kv.Key < _slotAnchors.Count)
                    return _slotAnchors[kv.Key];
            return null;
        }

        /// <summary>
        /// Confirm food vào đúng slot đã reserved (slotIndex từ command).
        /// Snap hard về anchor để tránh drift.
        /// </summary>
        public void ReceiveFood(FoodItem food, int reservedSlotIndex)
        {
            if (reservedSlotIndex < 0 || reservedSlotIndex >= _slotAnchors.Count)
            {
                Debug.LogError($"[BackupTray] Invalid slot index {reservedSlotIndex}!");
                return;
            }

            _occupants[reservedSlotIndex] = food;

            // Hard snap — anchor có thể đã dịch chuyển trong lúc food bay
            food.transform.position = _slotAnchors[reservedSlotIndex].position;

            Log($"ReceiveFood: {food.Data?.foodName} → slot[{reservedSlotIndex}]");
            CheckWarningAndLose();
        }

        public void ReceiveFood(FoodItem food)
        {
            int idx = -1;
            foreach (var kv in _occupants)
            {
                // Tìm slot có reservation cho food này
            }

            TryGetFreeSlotIndex(out idx);
            if (idx < 0)
            {
                Debug.LogError("[BackupTray] Không còn slot trống (legacy ReceiveFood)!");
                return;
            }
            ReceiveFood(food, idx);
        }

        public bool TryRemoveFood(FoodItem food)
        {
            foreach (var kv in _occupants)
            {
                if (kv.Value != food) continue;
                _occupants.Remove(kv.Key);
                SlotReservationRegistry.Instance.ReleaseBackupSlot(kv.Key);
                Log($"TryRemoveFood: {food.Data?.foodName} from slot[{kv.Key}]");
                CheckWarningAndLose();
                return true;
            }
            return false;
        }

        public void ClearAllFood()
        {
            foreach (var kv in _occupants)
                if (kv.Value != null)
                    PoolManager.Instance.ReturnFood(kv.Value.FoodID, kv.Value.gameObject);
            _occupants.Clear();
            _warningActive = false;
            Log("ClearAllFood.");
        }

        public List<FoodItem> GetAllFoods()
        {
            var result = new List<FoodItem>();
            foreach (var kv in _occupants)
                if (kv.Value != null) result.Add(kv.Value);
            return result;
        }

        /// <summary>
        /// Snapshot của occupants hiện tại: slotIndex → FoodItem.
        /// BackupTraySpawner dùng để reposition food khi anchor dịch chuyển.
        /// </summary>
        public Dictionary<int, FoodItem> GetOccupantsSnapshot()
        {
            return new Dictionary<int, FoodItem>(_occupants);
        }

        // ─── Expansion ────────────────────────────────────────────────────────

        public void ExpandCapacity(int addCount)
        {
            _capacity += addCount;
            Log($"ExpandCapacity(+{addCount}): capacity={_capacity}");
            EventBus.RaiseBackupExpanded(_capacity);
            CheckWarningAndLose();
        }

        public void ExpandCapacity()
        {
            if (_capacity >= maxSlots) { Log("maxSlots reached."); return; }

            int newCount = _capacity + 1;
            float totalWidth = (newCount - 1) * _slotSpacing;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < _slotAnchors.Count; i++)
            {
                Vector3 newWorldPos = transform.TransformPoint(
                    new Vector3(startX + i * _slotSpacing, 0f, 0f));
                _slotAnchors[i].DOMove(newWorldPos, shiftDuration).SetEase(Ease.OutCubic);

                if (_occupants.TryGetValue(i, out var occ) && occ != null)
                    occ.transform.DOMove(newWorldPos, shiftDuration).SetEase(Ease.OutCubic);
            }

            Vector3 finalWorld = transform.TransformPoint(
                new Vector3(startX + _capacity * _slotSpacing, 0f, 0f));
            Vector3 startWorld = finalWorld + new Vector3(_slotSpacing * 2f, 0f, 0f);

            var newAnchor = CreateSlotAnchor(_capacity, Vector3.zero);
            newAnchor.position = startWorld;
            newAnchor.DOMove(finalWorld, shiftDuration).SetEase(Ease.OutBack).SetDelay(0.05f);

            _slotAnchors.Add(newAnchor);
            _capacity++;
            EventBus.RaiseBackupExpanded(_capacity);
            CheckWarningAndLose();
        }

        // ─── Internal Helpers ─────────────────────────────────────────────────

        private bool TryGetFreeSlotIndex(out int idx)
        {
            for (int i = 0; i < _slotAnchors.Count; i++)
            {
                bool occupied = _occupants.ContainsKey(i) && _occupants[i] != null;
                bool reserved = SlotReservationRegistry.Instance.IsBackupSlotReserved(i);
                if (!occupied && !reserved)
                {
                    idx = i;
                    return true;
                }
            }
            idx = -1;
            return false;
        }

        private Transform CreateSlotAnchor(int index, Vector3 localPosition)
        {
            var go = new GameObject($"BackupSlot_{index}");
            go.transform.SetParent(transform);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            if (slotVisualPrefab != null)
                Instantiate(slotVisualPrefab, go.transform);

            return go.transform;
        }

        private void ClearSlotsAndFood()
        {
            foreach (var kv in _occupants)
                if (kv.Value != null)
                    PoolManager.Instance.ReturnFood(kv.Value.FoodID, kv.Value.gameObject);
            _occupants.Clear();

            foreach (var anchor in _slotAnchors)
                if (anchor != null) Destroy(anchor.gameObject);
            _slotAnchors.Clear();

            _capacity = 0;
            _warningActive = false;
        }

        private void CheckWarningAndLose()
        {
            int occupied = _occupants.Count;

            // Đếm slot đang reserved nhưng CHƯA confirmed (food đang bay đến)
            int pendingReserved = 0;
            for (int i = 0; i < _capacity; i++)
            {
                bool isOccupied = _occupants.ContainsKey(i) && _occupants[i] != null;
                bool isReserved = SlotReservationRegistry.Instance.IsBackupSlotReserved(i);
                // Chỉ đếm reserved nếu slot đó CHƯA confirmed (tránh double-count)
                if (!isOccupied && isReserved) pendingReserved++;
            }

            int effectiveUsed = occupied + pendingReserved;
            int free = _capacity - effectiveUsed;

            // Chỉ thua khi TẤT CẢ slot đã CONFIRMED đầy (không còn food đang bay)
            // pendingReserved == 0 đảm bảo không trigger sớm khi food cuối vẫn đang animate
            if (occupied >= _capacity && pendingReserved == 0)
            {
                Log("BACKUP TRAY ĐẦY → THUA!");
                EventBus.RaiseBackupFull();
                return;
            }

            if (free <= warningThreshold && free > 0 && !_warningActive)
            {
                _warningActive = true;
                Log($"CẢNH BÁO: {free} slots còn trống!");
                EventBus.RaiseBackupWarning(_occupants.Count, _capacity);
            }
            else if (free > warningThreshold)
            {
                _warningActive = false;
            }
        }

        private void Log(string msg)
        {
            if (showDebugLog) Debug.Log($"[BackupTray] {msg}");
        }
    }
}