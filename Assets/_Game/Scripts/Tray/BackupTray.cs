using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Food;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Khay chứa đồ thừa.
    ///
    /// THAY ĐỔI:
    ///   - ReceiveFood() lưu đúng anchor transform để food snap vị trí chính xác.
    ///   - GetNextSlotWorldPosition() trả về world position của anchor thực tế.
    ///   - Thêm GetAnchorForFood() để FoodFlowController snap food sau animation.
    /// </summary>
    public class BackupTray : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Slot Visual (chế độ Standalone) ──")]
        [SerializeField] private GameObject slotVisualPrefab;

        [Header("─── Warning ──────────────────────────")]
        [SerializeField] private int warningThreshold = 2;

        [Header("─── Expansion (chế độ Standalone) ────")]
        [SerializeField] private int maxSlots = 7;
        [SerializeField] private float shiftDuration = 0.35f;

        [Header("─── Debug ───────────────────────────")]
        [SerializeField] private bool showDebugLog = true;

        // ─── Runtime ──────────────────────────────────────────────────────────
        private readonly List<Transform> _slotAnchors = new List<Transform>();

        // slot index → food item đang chiếm
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
            _isInitialized = true;
            Log($"ResetTray: capacity={capacity}");
        }

        public void ExpandCapacity(int addCount)
        {
            _capacity += addCount;
            Log($"ExpandCapacity(+{addCount}): capacity={_capacity}");
            EventBus.RaiseBackupExpanded(_capacity);
            CheckWarningAndLose();
        }

        // =========================================================================
        // CHẾ ĐỘ A — Standalone
        // =========================================================================

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

        public void ExpandCapacity()
        {
            if (_capacity >= maxSlots) { Log($"Đã đạt maxSlots={maxSlots}."); return; }

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
        // CORE API
        // =========================================================================

        /// <summary>
        /// World position của anchor slot trống tiếp theo.
        /// FoodFlowController dùng vị trí này làm đích bay của food.
        /// </summary>
        public Vector3 GetNextSlotWorldPosition()
        {
            int idx = GetFreeSlotIndex();
            if (idx < 0) return Vector3.zero;
            return _slotAnchors[idx].position;
        }

        /// <summary>
        /// Sau khi food bay xong và gọi ReceiveFood(), trả về anchor transform
        /// để FoodFlowController có thể gắn SlotFollower hoặc snap chính xác.
        /// </summary>
        public Transform GetAnchorForFood(FoodItem food)
        {
            foreach (var kv in _occupants)
            {
                if (kv.Value == food && kv.Key < _slotAnchors.Count)
                    return _slotAnchors[kv.Key];
            }
            return null;
        }

        /// <summary>
        /// Nhận food vào slot trống. Gọi bởi FoodFlowController SAU animation bay.
        /// Food đã được snap về targetPos bởi FoodFlowController trước khi gọi hàm này.
        /// </summary>
        public void ReceiveFood(FoodItem food)
        {
            int idx = GetFreeSlotIndex();
            if (idx < 0)
            {
                Debug.LogError("[BackupTray] Không còn slot trống!");
                return;
            }

            _occupants[idx] = food;

            // Snap food về đúng anchor (phòng trường hợp anchor di chuyển
            // trong khoảng thời gian food đang bay)
            if (idx < _slotAnchors.Count)
                food.transform.position = _slotAnchors[idx].position;

            Log($"ReceiveFood: {food.Data?.foodName} → slot[{idx}] @ {_slotAnchors[idx].position}");
            CheckWarningAndLose();
        }

        public bool TryRemoveFood(FoodItem food)
        {
            foreach (var kv in _occupants)
            {
                if (kv.Value != food) continue;
                _occupants.Remove(kv.Key);
                Log($"TryRemoveFood: {food.Data?.foodName} removed from slot[{kv.Key}]");

                // Reset warning nếu slot vừa giải phóng giúp thoát ngưỡng warning
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
            Log("ClearAllFood hoàn tất.");
        }

        /// <summary>Tất cả food hiện trong backup (dùng cho Magnet Item và auto-match).</summary>
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
                Log($"CẢNH BÁO: còn {free} slot trống!");
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