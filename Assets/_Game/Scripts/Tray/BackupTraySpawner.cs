using System;
using System.Collections;
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

        [Header("─── Animation ────────────────────────")]
        [Tooltip("Thời gian shift slot cũ khi thêm slot mới.")]
        [SerializeField] private float shiftDuration = 0.25f;

        [Tooltip("Thời gian scale-in slot mới.")]
        [SerializeField] private float newSlotScaleDuration = 0.3f;

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
        /// Block input → shift slot cũ + di chuyển food → scale-in slot mới → unblock input.
        /// onComplete được gọi SAU KHI toàn bộ animation kết thúc.
        /// </summary>
        public void AddExtraSlot(Action onComplete = null)
        {
            StartCoroutine(AddExtraSlotRoutine(onComplete));
        }

        // ─── Core Routine ─────────────────────────────────────────────────────

        private IEnumerator AddExtraSlotRoutine(Action onComplete)
        {
            // ── 1. Block input ngay lập tức ──────────────────────────────────
            InputBlocker.Block("BackupTrayExpand");

            int newTotal = _activeSlotAnchors.Count + 1;

            // Snapshot occupants TRƯỚC khi anchor dịch (cần world pos hiện tại)
            var occupants = _backupTray.GetOccupantsSnapshot();

            // ── 2. Tween slot anchors CŨ + food theo cùng ────────────────────
            var tweens = new List<Tween>();

            for (int i = 0; i < _activeSlotAnchors.Count; i++)
            {
                Vector3 targetLocalPos = CalculateSlotLocalPos(i, newTotal);
                Vector3 targetWorldPos = _slotContainer.TransformPoint(targetLocalPos);

                // Tween anchor
                Tween anchorTween = _activeSlotAnchors[i].transform
                    .DOLocalMove(targetLocalPos, shiftDuration)
                    .SetEase(Ease.OutCubic);
                tweens.Add(anchorTween);

                // Tween food nếu slot này đang có food
                if (occupants.TryGetValue(i, out var food) && food != null)
                {
                    food.transform.DOKill();
                    Tween foodTween = food.transform
                        .DOMove(targetWorldPos, shiftDuration)
                        .SetEase(Ease.OutCubic);
                    tweens.Add(foodTween);
                }
            }

            // ── 3. Chờ tất cả shift xong ─────────────────────────────────────
            if (tweens.Count > 0)
                yield return tweens[0].WaitForCompletion();
            else
                yield return new WaitForSeconds(shiftDuration);

            // ── 4. Hard-snap food về đúng anchor (tránh drift floating point) ─
            foreach (var kv in occupants)
            {
                if (kv.Value == null) continue;
                if (kv.Key >= _activeSlotAnchors.Count) continue;
                kv.Value.transform.position = _activeSlotAnchors[kv.Key].transform.position;
            }

            // ── 5. Spawn slot mới ở cuối, scale-in ───────────────────────────
            var newAnchor = SpawnOneSlot(_activeSlotAnchors.Count, newTotal, animate: true);
            _activeSlotAnchors.Add(newAnchor);

            InjectAnchorsToBackupTray();

            // Báo BackupTray mở rộng thêm 1
            _backupTray.ExpandCapacity(1);

            // ── 6. Chờ scale-in xong rồi mới unblock ─────────────────────────
            yield return new WaitForSeconds(newSlotScaleDuration);

            InputBlocker.Unblock("BackupTrayExpand");

            Debug.Log($"[BackupTraySpawner] Thêm slot. Tổng: {_activeSlotAnchors.Count}");

            // ── 7. Báo caller đã xong ─────────────────────────────────────────
            onComplete?.Invoke();
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
                    .DOScale(Vector3.one, newSlotScaleDuration)
                    .SetEase(Ease.OutBack);
            }

            return anchor;
        }

        private void ClearAllSlots()
        {
            _slotPool.ReturnAll(_activeSlotAnchors);
        }

        // ─── Layout ───────────────────────────────────────────────────────────

        private Vector3 CalculateSlotLocalPos(int index, int totalCount)
        {
            float totalWidth = (totalCount - 1) * slotSpacingX;
            float startX = -totalWidth / 2f;

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