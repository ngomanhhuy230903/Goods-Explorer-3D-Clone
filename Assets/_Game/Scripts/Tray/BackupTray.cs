using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using FoodMatch.Core;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Loại 2 — Khay chứa food 3D thừa (không match order).
    /// Chịu trách nhiệm: warning đỏ khi gần đầy, trigger lose khi đầy.
    /// Food vẫn là 3D model thực sự đứng trong các slot của khay này.
    /// </summary>
    public class BackupTray : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Slot Anchors ─────────────────────")]
        [Tooltip("Danh sách các Transform điểm neo — food 3D sẽ đứng tại đây.")]
        [SerializeField] private List<Transform> slotAnchors = new List<Transform>();

        [Header("─── Food Scale In Slot ──────────────")]
        [Tooltip("Scale mà food DOScale về khi đứng trong backup tray.")]
        [SerializeField] private Vector3 foodScaleInBackup = new Vector3(0.45f, 0.45f, 0.45f);

        [Header("─── Warning Visual ───────────────────")]
        [Tooltip("Image viền/nền của khay — nhấp nháy đỏ khi gần đầy.")]
        [SerializeField] private Image trayBorderImage;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = new Color(1f, 0.25f, 0.25f, 1f);

        [Header("─── Capacity ────────────────────────")]
        [Tooltip("Số ô ban đầu — tăng lên khi dùng booster +1 khay.")]
        [SerializeField] private int initialCapacity = 5;

        // ─── Runtime ──────────────────────────────────────────────────────────
        private readonly List<GameObject> _foodsInSlots = new List<GameObject>();
        private int _currentCapacity;

        public int CurrentCount => _foodsInSlots.Count;
        public int CurrentCapacity => _currentCapacity;
        public bool IsFull => _foodsInSlots.Count >= _currentCapacity;
        public bool IsAlmostFull => _foodsInSlots.Count >= _currentCapacity - 1;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _currentCapacity = initialCapacity;
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Nhận 1 food 3D vào khay thừa.
        /// Trả về vị trí slot (WorldPosition) để FoodAnimator bay đến.
        /// Trả về false nếu khay đã đầy.
        /// </summary>
        public bool TryAddFood(GameObject foodObject, out Vector3 targetPos,
                               out Vector3 targetScale)
        {
            targetPos = Vector3.zero;
            targetScale = foodScaleInBackup;

            if (IsFull)
            {
                TriggerFull();
                return false;
            }

            int slotIndex = _foodsInSlots.Count;
            _foodsInSlots.Add(foodObject);

            // Lấy vị trí slot tương ứng
            targetPos = slotIndex < slotAnchors.Count
                ? slotAnchors[slotIndex].position
                : transform.position + Vector3.right * slotIndex * 0.6f;

            // Kiểm tra warning sau khi thêm
            CheckWarning();

            return true;
        }

        /// <summary>
        /// Lấy food khỏi khay thừa (dùng cho Magnet booster).
        /// Trả về null nếu không có food nào khớp foodID.
        /// </summary>
        public GameObject TakeFood(int foodID)
        {
            // Tìm từ đầu danh sách (cũ nhất trước)
            for (int i = 0; i < _foodsInSlots.Count; i++)
            {
                var foodItem = _foodsInSlots[i]
                    .GetComponent<Food.FoodItem>();

                if (foodItem != null && foodItem.FoodID == foodID)
                {
                    var obj = _foodsInSlots[i];
                    _foodsInSlots.RemoveAt(i);
                    ReArrangeSlots();
                    return obj;
                }
            }
            return null;
        }

        /// <summary>
        /// Xoá sạch toàn bộ food trong khay thừa (Clear booster).
        /// Trả về list food để caller trả về pool.
        /// </summary>
        public List<GameObject> ClearAll()
        {
            var cleared = new List<GameObject>(_foodsInSlots);
            _foodsInSlots.Clear();
            ResetWarningVisual();
            return cleared;
        }

        /// <summary>
        /// Mở rộng thêm 1 slot (booster +1 khay).
        /// </summary>
        public void ExpandCapacity(int addCount = 1)
        {
            _currentCapacity += addCount;
            EventBus.RaiseBackupExpanded(_currentCapacity);
            ResetWarningVisual();
        }

        /// <summary>Reset về trạng thái ban đầu khi load level mới.</summary>
        public void ResetTray(int capacity = -1)
        {
            _foodsInSlots.Clear();

            // Nếu truyền capacity mới thì dùng, không thì giữ initialCapacity
            if (capacity > 0)
                _currentCapacity = capacity;
            else
                _currentCapacity = initialCapacity;

            ResetWarningVisual();
        }

        // ─── Private ──────────────────────────────────────────────────────────

        private void CheckWarning()
        {
            if (IsFull)
            {
                TriggerFull();
            }
            else if (IsAlmostFull)
            {
                PlayWarningPulse();
                EventBus.RaiseBackupWarning(CurrentCount, CurrentCapacity);
            }
        }

        private void TriggerFull()
        {
            PlayWarningPulse(loop: true);
            EventBus.RaiseBackupFull();
        }

        /// <summary>Dồn lại vị trí các food sau khi có 1 food bị lấy ra.</summary>
        private void ReArrangeSlots()
        {
            for (int i = 0; i < _foodsInSlots.Count; i++)
            {
                if (_foodsInSlots[i] == null) continue;

                Vector3 targetPos = i < slotAnchors.Count
                    ? slotAnchors[i].position
                    : transform.position + Vector3.right * i * 0.6f;

                _foodsInSlots[i].transform
                    .DOMove(targetPos, 0.25f)
                    .SetEase(Ease.OutCubic);
            }
        }

        private void PlayWarningPulse(bool loop = false)
        {
            if (trayBorderImage == null) return;

            trayBorderImage.DOKill();

            int loopCount = loop ? -1 : 6;
            trayBorderImage
                .DOColor(warningColor, 0.15f)
                .SetLoops(loopCount, LoopType.Yoyo)
                .SetUpdate(true)
                .OnComplete(ResetWarningVisual);
        }

        private void ResetWarningVisual()
        {
            if (trayBorderImage == null) return;
            trayBorderImage.DOKill();
            trayBorderImage.color = normalColor;
        }
    }
}