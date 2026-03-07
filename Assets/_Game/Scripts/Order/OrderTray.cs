using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;

namespace FoodMatch.Order
{
    /// <summary>
    /// Khay order trên UI. Nhận OrderData, spawn food 3D icon tại các slot,
    /// và là đích bay của food từ grid.
    /// </summary>
    public class OrderTray : MonoBehaviour, IPoolable
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Slots ───────────────────────────")]
        [SerializeField] private List<OrderSlotUI> slots = new List<OrderSlotUI>(3);

        [Header("─── Tray Background UI ───────────────")]
        [SerializeField] private Image trayBgImage;
        [SerializeField] private Color normalBgColor = Color.white;
        [SerializeField] private Color completedBgColor = new Color(0.6f, 1f, 0.6f, 1f);

        [Header("─── Completion VFX ────────────────────")]
        [SerializeField] private GameObject completionVFXRoot;

        [Header("─── Data ────────────────────────────")]
        [Tooltip("Tham chiếu đến FoodDatabase để lookup FoodItemData theo ID.")]
        [SerializeField] private FoodDatabase foodDatabase;

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
            if (trayBgImage != null) trayBgImage.color = normalBgColor;
        }

        public void OnDespawn()
        {
            _rectTransform.DOKill();
            OrderData = null;
            foreach (var slot in slots) slot.ResetSlot();
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Khởi tạo tray: lookup FoodItemData → spawn icon 3D tại tất cả slots → slide in.
        /// Scale của từng icon được quản lý độc lập bởi mỗi OrderSlotUI (Inspector của slot).
        /// </summary>
        public void Initialize(OrderData orderData, int trayIndex,
                               Vector2 homePos, bool enterFromTop = true)
        {
            OrderData = orderData;
            TrayIndex = trayIndex;
            _homeAnchoredPos = homePos;

            var foodData = foodDatabase != null
                ? foodDatabase.GetFoodByID(orderData.FoodID)
                : null;

            if (foodData == null)
            {
                Debug.LogError($"[OrderTray] Không tìm thấy FoodItemData cho foodID={orderData.FoodID}");
            }
            else
            {
                // Mỗi slot tự quản lý foodIconScale của nó qua Inspector
                foreach (var slot in slots)
                    slot.ShowFoodIcon(foodData);
            }

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

        public void MoveTo(Vector2 newHomePos, float duration = 0.35f)
        {
            _homeAnchoredPos = newHomePos;
            _rectTransform.DOAnchorPos(newHomePos, duration)
                .SetEase(Ease.OutCubic).SetUpdate(true);
        }

        public bool TryMatch(int foodID, out int slotIndex)
        {
            slotIndex = -1;
            if (State != OrderState.Active) return false;
            if (OrderData.FoodID != foodID) return false;
            if (OrderData.IsCompleted) return false;

            slotIndex = OrderData.DeliveredCount;
            return true;
        }

        public Vector3 GetNextSlotWorldPosition()
        {
            int next = OrderData?.DeliveredCount ?? 0;
            return next < slots.Count ? slots[next].WorldPosition : transform.position;
        }

        public Vector3 GetNextSlotFoodScale()
        {
            int next = OrderData?.DeliveredCount ?? 0;
            // Lấy FoodTargetScale từ slot tương ứng — mỗi slot có thể có scale riêng
            return next < slots.Count ? slots[next].FoodTargetScale : Vector3.one;
        }

        public void ConfirmDelivery(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count) return;

            OrderData.Deliver();
            slots[slotIndex].PlayReceiveAnimation();
            slots[slotIndex].MarkDelivered();

            if (OrderData.IsCompleted)
                DOVirtual.DelayedCall(0.35f, () => ChangeState(OrderState.Completed), false);
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
            trayBgImage?.DOColor(completedBgColor, 0.2f).SetUpdate(true);
            transform.DOPunchScale(Vector3.one * 0.2f, 0.45f, 7, 0.5f).SetUpdate(true);
            completionVFXRoot?.SetActive(true);

            OnCompleted?.Invoke(this);
            EventBus.RaiseOrderCompleted(TrayIndex);

            DOVirtual.DelayedCall(0.7f, () => ChangeState(OrderState.Leaving), false);
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