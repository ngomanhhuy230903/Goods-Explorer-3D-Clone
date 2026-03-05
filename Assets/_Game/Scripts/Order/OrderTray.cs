using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using FoodMatch.Core;

namespace FoodMatch.Order
{
    /// <summary>
    /// Loại 1 — Khay order trên UI (Top của màn hình).
    /// Chỉ chứa 3 slot làm điểm neo để food 3D bay đến.
    /// </summary>
    public class OrderTray : MonoBehaviour, IPoolable
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Slots ───────────────────────────")]
        [Tooltip("3 slot theo thứ tự 0→1→2.")]
        [SerializeField] private List<OrderSlotUI> slots = new List<OrderSlotUI>(3);

        [Header("─── Tray Background UI ───────────────")]
        [Tooltip("Image nền của khay — đổi màu khi completed.")]
        [SerializeField] private Image trayBgImage;
        [SerializeField] private Color normalBgColor = Color.white;
        [SerializeField] private Color completedBgColor = new Color(0.6f, 1f, 0.6f, 1f);

        [Header("─── Completion VFX ────────────────────")]
        [SerializeField] private GameObject completionVFXRoot;

        // ─── Runtime ──────────────────────────────────────────────────────────
        public OrderState State { get; private set; } = OrderState.Idle;
        public OrderData OrderData { get; private set; }
        public int TrayIndex { get; private set; }

        public event Action<OrderTray> OnCompleted;
        public event Action<OrderTray> OnLeft;

        private RectTransform _rectTransform;
        private Vector2 _homeAnchoredPos;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        // ─── IPoolable ────────────────────────────────────────────────────────
        public void OnSpawn()
        {
            State = OrderState.Idle;
            completionVFXRoot?.SetActive(false);

            if (trayBgImage != null)
                trayBgImage.color = normalBgColor;
        }

        public void OnDespawn()
        {
            _rectTransform.DOKill();
            OrderData = null;

            foreach (var slot in slots)
                slot.ResetSlot();
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Khởi tạo khay với dữ liệu order.
        /// Gọi bởi OrderQueue.
        /// </summary>
        public void Initialize(OrderData orderData, int trayIndex,
                               Vector2 homePos, bool enterFromTop = true)
        {
            OrderData = orderData;
            TrayIndex = trayIndex;
            _homeAnchoredPos = homePos;

            if (enterFromTop)
            {
                _rectTransform.anchoredPosition = homePos + new Vector2(0f, 250f);
                _rectTransform
                    .DOAnchorPos(homePos, 0.45f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .OnComplete(() => ChangeState(OrderState.Active));
            }
            else
            {
                _rectTransform.anchoredPosition = homePos;
                ChangeState(OrderState.Active);
            }
        }

        /// <summary>
        /// Dời khay sang vị trí mới (khi order bên cạnh rời đi).
        /// </summary>
        public void MoveTo(Vector2 newHomePos, float duration = 0.35f)
        {
            _homeAnchoredPos = newHomePos;
            _rectTransform
                .DOAnchorPos(newHomePos, duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        /// <summary>
        /// Kiểm tra order này có nhận foodID không.
        /// Trả về index slot tiếp theo, hoặc -1 nếu không match.
        /// </summary>
        public bool TryMatch(int foodID, out int slotIndex)
        {
            slotIndex = -1;
            if (State != OrderState.Active) return false;
            if (OrderData.FoodID != foodID) return false;
            if (OrderData.IsCompleted) return false;

            slotIndex = OrderData.DeliveredCount;
            return true;
        }

        /// <summary>
        /// Lấy WorldPosition của slot tiếp theo — đích bay cho FoodItem.
        /// </summary>
        public Vector3 GetNextSlotWorldPosition()
        {
            int next = OrderData?.DeliveredCount ?? 0;
            return next < slots.Count
                ? slots[next].WorldPosition
                : transform.position;
        }

        /// <summary>
        /// Lấy scale đích mà food phải DOScale về khi đến slot.
        /// </summary>
        public Vector3 GetNextSlotFoodScale()
        {
            int next = OrderData?.DeliveredCount ?? 0;
            return next < slots.Count
                ? slots[next].FoodTargetScale
                : Vector3.one * 0.4f;
        }

        /// <summary>
        /// Xác nhận giao món thành công.
        /// GỌI TRONG OnComplete callback của DOTween food animation.
        /// </summary>
        public void ConfirmDelivery(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count) return;

            OrderData.Deliver();
            slots[slotIndex].PlayReceiveAnimation();
            slots[slotIndex].MarkDelivered();

            if (OrderData.IsCompleted)
            {
                DOVirtual.DelayedCall(0.35f,
                    () => ChangeState(OrderState.Completed), false);
            }
        }

        // ─── State Machine ────────────────────────────────────────────────────

        private void ChangeState(OrderState newState)
        {
            State = newState;
            switch (newState)
            {
                case OrderState.Active: OnEnterActive(); break;
                case OrderState.Completed: OnEnterCompleted(); break;
                case OrderState.Leaving: OnEnterLeaving(); break;
            }
        }

        private void OnEnterActive()
        {
            // Idle bounce nhẹ lên xuống
            _rectTransform.DOKill();
            _rectTransform
                .DOAnchorPosY(_homeAnchoredPos.y + 5f, 0.9f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private void OnEnterCompleted()
        {
            _rectTransform.DOKill();

            // Đổi màu nền sang xanh lá
            trayBgImage?.DOColor(completedBgColor, 0.2f).SetUpdate(true);

            // Bounce ăn mừng
            transform
                .DOPunchScale(Vector3.one * 0.2f, 0.45f, 7, 0.5f)
                .SetUpdate(true);

            completionVFXRoot?.SetActive(true);

            OnCompleted?.Invoke(this);
            EventBus.RaiseOrderCompleted(TrayIndex);

            DOVirtual.DelayedCall(0.7f,
                () => ChangeState(OrderState.Leaving), false);
        }

        private void OnEnterLeaving()
        {
            _rectTransform
                .DOAnchorPosY(_homeAnchoredPos.y + 350f, 0.4f)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    OnLeft?.Invoke(this);
                    EventBus.RaiseOrderLeft(TrayIndex);
                    PoolManager.Instance.ReturnOrder(gameObject);
                });
        }
    }
}