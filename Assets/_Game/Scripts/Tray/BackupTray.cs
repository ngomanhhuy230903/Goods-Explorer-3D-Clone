using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;
using FoodMatch.Food;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Khay chứa đồ thừa.
    ///
    /// Có 2 chế độ hoạt động:
    ///   A) Standalone: gọi Initialize(count, spacing) — tự tạo anchor GameObjects.
    ///   B) Kết hợp BackupTraySpawner: Spawner tạo anchor từ pool rồi gọi
    ///      SetSlotAnchors() + ResetTray() để inject vào đây.
    ///
    /// BackupTraySpawner dùng chế độ B nên cần 3 method:
    ///   - SetSlotAnchors(List&lt;Transform&gt;)   ← inject anchors từ ngoài vào
    ///   - ResetTray(int capacity)             ← reset occupants, không tạo anchor
    ///   - ExpandCapacity(int addCount)        ← overload nhận số slot tăng thêm
    /// </summary>
    public class BackupTray : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Slot Visual (chế độ Standalone) ──")]
        [Tooltip("Chỉ dùng khi KHÔNG có BackupTraySpawner. Prefab visual placeholder cho slot.")]
        [SerializeField] private GameObject slotVisualPrefab;

        [Header("─── Warning ──────────────────────────")]
        [Tooltip("Cảnh báo khi số slot trống <= giá trị này.")]
        [SerializeField] private int warningThreshold = 2;

        [Header("─── Expansion (chế độ Standalone) ────")]
        [Tooltip("Số slot tối đa. Chỉ dùng khi KHÔNG có BackupTraySpawner.")]
        [SerializeField] private int maxSlots = 7;
        [SerializeField] private float shiftDuration = 0.35f;

        [Header("─── Debug ───────────────────────────")]
        [SerializeField] private bool showDebugLog = true;

        // ─── Runtime ──────────────────────────────────────────────────────────
        private readonly List<Transform> _slotAnchors = new List<Transform>();
        private readonly Dictionary<int, FoodItem> _occupants = new Dictionary<int, FoodItem>();

        private float _slotSpacing = 1.5f;
        private int _capacity = 0;
        private bool _isInitialized = false;
        private bool _warningActive = false;

        // ─── Public Properties ────────────────────────────────────────────────
        public int OccupiedCount => _occupants.Count;
        public int Capacity => _capacity;
        public bool HasFreeSlot() => GetFreeSlotIndex() >= 0;

        // =========================================================================
        // CHẾ ĐỘ B — BackupTraySpawner inject từ ngoài vào
        // =========================================================================

        /// <summary>
        /// BackupTraySpawner gọi sau khi spawn xong anchor list.
        /// BackupTray dùng list này làm nguồn sự thật cho slot positions.
        /// </summary>
        public void SetSlotAnchors(List<Transform> anchors)
        {
            _slotAnchors.Clear();
            _slotAnchors.AddRange(anchors);
            _capacity = _slotAnchors.Count;
            Log($"SetSlotAnchors: {_capacity} anchors injected.");
        }

        /// <summary>
        /// Dọn occupants, set capacity mới — KHÔNG tạo anchor (Spawner đã lo).
        /// Gọi bởi BackupTraySpawner sau SetSlotAnchors().
        /// </summary>
        public void ResetTray(int capacity)
        {
            foreach (var kv in _occupants)
                if (kv.Value != null)
                    PoolManager.Instance.ReturnFood(kv.Value.FoodID, kv.Value.gameObject);

            _occupants.Clear();
            _capacity = capacity;
            _warningActive = false;
            _isInitialized = true;
            Log($"ResetTray: capacity={capacity}");
        }

        /// <summary>
        /// Overload dùng bởi BackupTraySpawner.AddExtraSlot().
        /// Spawner đã lo animation + inject anchor mới qua SetSlotAnchors().
        /// BackupTray chỉ cập nhật capacity và raise event.
        /// </summary>
        public void ExpandCapacity(int addCount)
        {
            _capacity += addCount;
            Log($"ExpandCapacity(+{addCount}): capacity={_capacity}");
            EventBus.RaiseBackupExpanded(_capacity);
            CheckWarningAndLose();
        }

        // =========================================================================
        // CHẾ ĐỘ A — Standalone, tự tạo anchor (dùng khi không có Spawner)
        // =========================================================================

        /// <summary>
        /// Tự tạo slot anchors runtime. Dùng khi không có BackupTraySpawner.
        /// Nếu có BackupTraySpawner thì dùng SetSlotAnchors() + ResetTray() thay thế.
        /// </summary>
        public void Initialize(int initialSlotCount, float slotSpacing = 1.5f)
        {
            _slotSpacing = slotSpacing;
            _isInitialized = false;

            ClearSlotsAndFood();

            float totalWidth = (initialSlotCount - 1) * slotSpacing;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < initialSlotCount; i++)
            {
                var anchor = CreateSlotAnchor(i, new Vector3(startX + i * slotSpacing, 0f, 0f));
                _slotAnchors.Add(anchor);
            }

            _capacity = initialSlotCount;
            _isInitialized = true;
            Log($"Initialize: {_capacity} slots, spacing={slotSpacing}");
        }

        /// <summary>
        /// No-arg overload — mở rộng thêm 1 slot (standalone mode).
        /// Tự tạo anchor mới, tự DOMove các slot cũ.
        /// </summary>
        public void ExpandCapacity()
        {
            if (_capacity >= maxSlots)
            {
                Log($"Đã đạt maxSlots={maxSlots}.");
                return;
            }

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

            Log($"ExpandCapacity: {_capacity} slots.");
            EventBus.RaiseBackupExpanded(_capacity);
            CheckWarningAndLose();
        }

        // =========================================================================
        // CORE API (dùng chung cả 2 chế độ)
        // =========================================================================

        /// <summary>World position của slot trống tiếp theo. Vector3.zero nếu đầy.</summary>
        public Vector3 GetNextSlotWorldPosition()
        {
            int idx = GetFreeSlotIndex();
            return idx >= 0 ? _slotAnchors[idx].position : Vector3.zero;
        }

        /// <summary>Nhận food vào slot trống. Gọi bởi FoodFlowController sau animation bay.</summary>
        public void ReceiveFood(FoodItem food)
        {
            int idx = GetFreeSlotIndex();
            if (idx < 0)
            {
                Debug.LogError("[BackupTray] Không còn slot trống!");
                return;
            }

            _occupants[idx] = food;
            Log($"ReceiveFood: {food.Data?.foodName} → slot[{idx}]");
            CheckWarningAndLose();
        }

        /// <summary>Lấy food ra (Magnet Item hoặc re-match).</summary>
        public bool TryRemoveFood(FoodItem food)
        {
            foreach (var kv in _occupants)
            {
                if (kv.Value != food) continue;
                _occupants.Remove(kv.Key);
                Log($"TryRemoveFood: {food.Data?.foodName} removed from slot[{kv.Key}]");
                return true;
            }
            return false;
        }

        /// <summary>Xóa toàn bộ food (Item Clear Tray).</summary>
        public void ClearAllFood()
        {
            foreach (var kv in _occupants)
                if (kv.Value != null)
                    PoolManager.Instance.ReturnFood(kv.Value.FoodID, kv.Value.gameObject);

            _occupants.Clear();
            _warningActive = false;
            Log("ClearAllFood hoàn tất.");
        }

        /// <summary>Tất cả food hiện trong backup (Magnet Item).</summary>
        public List<FoodItem> GetAllFoods()
        {
            var result = new List<FoodItem>();
            foreach (var kv in _occupants)
                if (kv.Value != null) result.Add(kv.Value);
            return result;
        }

        // ─── Internal ─────────────────────────────────────────────────────────

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

        private int GetFreeSlotIndex()
        {
            for (int i = 0; i < _slotAnchors.Count; i++)
            {
                if (!_occupants.ContainsKey(i) || _occupants[i] == null)
                    return i;
            }
            return -1;
        }

        private void CheckWarningAndLose()
        {
            int free = _capacity - _occupants.Count;

            if (free <= 0)
            {
                Log("BACKUP TRAY ĐẦY → THUA!");
                EventBus.RaiseBackupFull();
                return;
            }

            if (free <= warningThreshold && !_warningActive)
            {
                _warningActive = true;
                Log($"CANH BAO: con {free} slot trong!");
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