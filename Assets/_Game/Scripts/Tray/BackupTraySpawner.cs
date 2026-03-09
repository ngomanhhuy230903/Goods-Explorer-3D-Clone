using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Mở rộng BackupTray với khả năng sinh/xoá slot anchors động
    /// dựa theo LevelConfig.backupTrayCapacity.
    /// </summary>
    [RequireComponent(typeof(BackupTray))]
    public class BackupTraySpawner : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Slot Anchor Prefab ─────────────")]
        [Tooltip("Prefab đại diện cho 1 ô trong khay thừa (3D Transform anchor).")]
        [SerializeField] private GameObject slotAnchorPrefab;

        [Header("─── Layout (Local Space) ────────────")]
        [Tooltip("Khoảng cách giữa các slot theo trục X (local units).")]
        [SerializeField] private float slotSpacingX = 150f;

        [Tooltip("Offset Y so với pivot của SlotAnchors_Container.")]
        [SerializeField] private float slotOffsetY = 0f;

        [Header("─── Pool Config ──────────────────────")]
        [Tooltip("Số slot pre-warm trong pool (nên >= max capacity của bất kỳ level nào = 7).")]
        [SerializeField] private int poolPreloadCount = 7;

        // ─── Runtime ──────────────────────────────────────────────────────────
        private BackupTray _backupTray;
        private ObjectPool _slotPool;
        private Transform _slotContainer;

        // List các slot anchor đang active (theo đúng thứ tự)
        private readonly List<GameObject> _activeSlotAnchors = new List<GameObject>();

        // ─── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            _backupTray = GetComponent<BackupTray>();

            // Container con giữ hierarchy gọn
            var go = new GameObject("SlotAnchors_Container");

            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            _slotContainer = go.transform;

            // Dùng ObjectPool của dự án
            _slotPool = new ObjectPool(slotAnchorPrefab, _slotContainer, poolPreloadCount);
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Gọi từ LevelManager khi load level.
        /// Xoá slot cũ → sinh slot mới → inject vào BackupTray → reset tray.
        /// </summary>
        public void SpawnSlots(int capacity)
        {
            ClearAllSlots();
            capacity = Mathf.Clamp(capacity, 1, 10);

            for (int i = 0; i < capacity; i++)
            {
                var anchor = SpawnOneSlot(i, totalCount: capacity, animate: false);
                _activeSlotAnchors.Add(anchor);
            }

            // Inject Transform list vào BackupTray
            InjectAnchorsToBackupTray();

            // Reset tray với capacity mới
            _backupTray.ResetTray(capacity);

            Debug.Log($"[BackupTraySpawner] Đã sinh {capacity} slot anchors.");
        }

        /// <summary>
        /// Thêm 1 slot mới (Booster +1 Khay).
        /// Các slot cũ DOLocalMove sang trái, slot mới scale từ 0 → 1.
        /// </summary>
        public void AddExtraSlot()
        {
            int newTotal = _activeSlotAnchors.Count + 1;

            for (int i = 0; i < _activeSlotAnchors.Count; i++)
            {
                Vector3 targetLocalPos = CalculateSlotLocalPos(i, newTotal);
                _activeSlotAnchors[i].transform
                    .DOLocalMove(targetLocalPos, 0.25f)
                    .SetEase(Ease.OutCubic);
            }

            // Spawn slot mới ở cuối
            var newAnchor = SpawnOneSlot(_activeSlotAnchors.Count, newTotal, animate: true);
            _activeSlotAnchors.Add(newAnchor);

            InjectAnchorsToBackupTray();

            // Báo BackupTray mở rộng thêm 1
            _backupTray.ExpandCapacity(1);

            Debug.Log($"[BackupTraySpawner] Thêm slot. Tổng: {_activeSlotAnchors.Count}");
        }

        // ─── Spawn / Pool Logic ───────────────────────────────────────────────
        private GameObject SpawnOneSlot(int index, int totalCount, bool animate)
        {

            var anchor = _slotPool.Get(Vector3.zero);
            anchor.transform.SetParent(_slotContainer, false);
            anchor.transform.localScale = Vector3.one;
            anchor.transform.localRotation = Quaternion.identity;
            anchor.name = $"SlotAnchor_{index}";
            anchor.transform.localPosition = CalculateSlotLocalPos(index, totalCount);

            if (animate)
            {
                anchor.transform.localScale = Vector3.zero;
                anchor.transform
                    .DOScale(Vector3.one, 0.3f)
                    .SetEase(Ease.OutBack);
            }

            return anchor;
        }

        private void ClearAllSlots()
        {
            // ReturnAll trả về pool và clear list (theo API ObjectPool của bạn)
            _slotPool.ReturnAll(_activeSlotAnchors);
        }

        // ─── Layout ───────────────────────────────────────────────────────────

        /// <summary>
        /// Tính LOCAL position căn giữa quanh pivot của SlotAnchors_Container.
        /// Dùng local space để tránh bị ảnh hưởng bởi Canvas world scale.
        /// </summary>
        private Vector3 CalculateSlotLocalPos(int index, int totalCount)
        {
            float totalWidth = (totalCount - 1) * slotSpacingX;
            float startX = -totalWidth / 2f; // Căn giữa quanh tâm container

            return new Vector3(
                startX + index * slotSpacingX,
                slotOffsetY,
                0f
            );
        }

        // ─── Inject to BackupTray ─────────────────────────────────────────────
        private void InjectAnchorsToBackupTray()
        {
            var transforms = new List<Transform>();
            foreach (var go in _activeSlotAnchors)
                transforms.Add(go.transform);
            _backupTray.SetSlotAnchors(transforms);
        }
    }
}