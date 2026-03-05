using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Mở rộng BackupTray với khả năng sinh/xoá slot anchors động
    /// dựa theo LevelConfig.backupTrayCapacity.
    ///
    /// ► Gắn cùng GameObject với BackupTray.cs
    /// ► SlotAnchor prefab: 1 GameObject rỗng (hoặc có placeholder visual).
    /// ► Gọi SpawnSlots(capacity) từ LevelManager thay cho ResetTray trực tiếp.
    /// </summary>
    [RequireComponent(typeof(BackupTray))]
    public class BackupTraySpawner : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Slot Anchor Prefab ─────────────")]
        [Tooltip("Prefab đại diện cho 1 ô trong khay thừa (3D Transform anchor).")]
        [SerializeField] private GameObject slotAnchorPrefab;

        [Header("─── Layout (World Space) ────────────")]
        [Tooltip("Khoảng cách giữa các slot theo trục X (world units).")]
        [SerializeField] private float slotSpacingX = 150f;

        [Tooltip("Offset Y so với pivot của BackupTray.")]
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
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
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
        /// Các slot cũ DOMove sang trái, slot mới scale từ 0 → 1.
        /// </summary>
        public void AddExtraSlot()
        {
            int newTotal = _activeSlotAnchors.Count + 1;

            // Dịch chuyển slot cũ sang vị trí mới
            for (int i = 0; i < _activeSlotAnchors.Count; i++)
            {
                Vector3 targetPos = CalculateSlotWorldPos(i, newTotal);
                _activeSlotAnchors[i].transform
                    .DOMove(targetPos, 0.25f)
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
            Vector3 worldPos = CalculateSlotWorldPos(index, totalCount);

            // Lấy từ ObjectPool (Get đặt position và SetActive(true))
            var anchor = _slotPool.Get(worldPos);
            anchor.transform.SetParent(_slotContainer);
            anchor.name = $"SlotAnchor_{index}";

            if (animate)
            {
                anchor.transform.localScale = Vector3.zero;
                anchor.transform
                    .DOScale(Vector3.one, 0.3f)
                    .SetEase(Ease.OutBack);
            }
            else
            {
                anchor.transform.localScale = Vector3.one;
            }

            return anchor;
        }

        private void ClearAllSlots()
        {
            // ReturnAll trả về pool và clear list (theo API ObjectPool của bạn)
            _slotPool.ReturnAll(_activeSlotAnchors);
            // _activeSlotAnchors đã được clear bởi ReturnAll
        }

        // ─── Layout ───────────────────────────────────────────────────────────

        /// <summary>
        /// Tính world position căn giữa quanh pivot của BackupTray.
        /// </summary>
        private Vector3 CalculateSlotWorldPos(int index, int totalCount)
        {
            float totalWidth = (totalCount - 1) * slotSpacingX;
            float startX = transform.position.x - totalWidth / 2f;

            return new Vector3(
                startX + index * slotSpacingX,
                transform.position.y + slotOffsetY,
                transform.position.z
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

        // ─── Editor Preview ───────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.7f);

            // Nếu đã runtime thì vẽ slot thực
            if (_activeSlotAnchors != null && _activeSlotAnchors.Count > 0)
            {
                foreach (var a in _activeSlotAnchors)
                    if (a != null)
                        Gizmos.DrawWireCube(a.transform.position, Vector3.one * 0.4f);
                return;
            }

            // Editor time: vẽ preview với capacity mặc định = 5
            for (int i = 0; i < 5; i++)
                Gizmos.DrawWireCube(CalculateSlotWorldPos(i, 5), Vector3.one * 0.4f);
        }
#endif
    }
}