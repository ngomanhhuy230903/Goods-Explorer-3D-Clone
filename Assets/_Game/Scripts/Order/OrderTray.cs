using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;

namespace FoodMatch.Order
{
    public abstract class OrderTrayState
    {
        protected OrderTray Owner { get; }
        protected OrderTrayState(OrderTray owner) => Owner = owner;
        public virtual void Enter() { }
        public virtual void Exit() { }
    }

    public sealed class IdleState : OrderTrayState
    {
        public IdleState(OrderTray owner) : base(owner) { }
    }

    public sealed class EnterState : OrderTrayState
    {
        private readonly Vector2 _homePos;
        public EnterState(OrderTray owner, Vector2 homePos) : base(owner) => _homePos = homePos;

        public override void Enter()
        {
            var rt = Owner.RectTransform;
            rt.anchoredPosition = _homePos + new Vector2(0f, 280f);
            rt.DOAnchorPos(_homePos, 0.45f)
              .SetEase(Ease.OutBack)
              .SetUpdate(true)
              .OnComplete(() => Owner.TransitionTo(OrderTrayStateId.Active));
        }
    }

    public sealed class ActiveState : OrderTrayState
    {
        public ActiveState(OrderTray owner) : base(owner) { }

        public override void Enter()
        {
            var rt = Owner.RectTransform;
            var homePos = Owner.HomeAnchoredPos;

            rt.DOKill();
            rt.DoFloatLoop(homePos.y);

            if (Owner.OrderData != null)
                DOVirtual.DelayedCall(0.1f,
                    () => EventBus.RaiseNewOrderActive(Owner.OrderData.FoodID), false)
                    .SetTarget(Owner.gameObject);
        }

        public override void Exit()
        {
            Owner.RectTransform.DOKill();
        }
    }

    public sealed class CompletedState : OrderTrayState
    {
        private readonly Image _bgImage;
        private readonly Color _completedColor;
        private readonly GameObject _vfxRoot;

        public CompletedState(OrderTray owner, Image bgImage, Color completedColor, GameObject vfxRoot)
            : base(owner)
        {
            _bgImage = bgImage;
            _completedColor = completedColor;
            _vfxRoot = vfxRoot;
        }

        public override void Enter()
        {
            _bgImage?.DOColor(_completedColor, 0.2f).SetUpdate(true);
            Owner.transform.DOPunchScale(Vector3.one * 0.22f, 0.45f, 7, 0.5f).SetUpdate(true);
            _vfxRoot?.SetActive(true);

            SlotReservationRegistry.Instance.ClearOrderTray(Owner.TrayIndex);

            Owner.RaiseCompleted();
            EventBus.RaiseOrderCompleted(Owner.TrayIndex);

            DOVirtual.DelayedCall(0.7f,
                () => Owner.TransitionTo(OrderTrayStateId.Leaving), false)
                .SetTarget(Owner.gameObject);
        }
    }

    public sealed class LeavingState : OrderTrayState
    {
        public LeavingState(OrderTray owner) : base(owner) { }

        public override void Enter()
        {
            SlotReservationRegistry.Instance.ClearOrderTray(Owner.TrayIndex);

            Owner.RectTransform
                 .DOAnchorPosY(Owner.HomeAnchoredPos.y + 400f, 0.38f)
                 .SetEase(Ease.InBack)
                 .SetUpdate(true)
                 .OnComplete(() =>
                 {
                     Owner.RaiseLeft();
                     EventBus.RaiseOrderLeft(Owner.TrayIndex);
                     PoolManager.Instance.ReturnOrder(Owner.gameObject);
                 });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ENUM
    // ═══════════════════════════════════════════════════════════════════════════
    public enum OrderTrayStateId { Idle, Enter, Active, Completed, Leaving }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ORDER TRAY — Reservation-aware slot management
    // ═══════════════════════════════════════════════════════════════════════════
    public class OrderTray : MonoBehaviour, IPoolable
    {
        [Header("─── Slots ───────────────────────────")]
        [SerializeField] private List<OrderSlotUI> slots = new List<OrderSlotUI>(3);

        [Header("─── Tray Background ───────────────────")]
        [SerializeField] private Image trayBgImage;
        [SerializeField] private Color normalBgColor = Color.white;
        [SerializeField] private Color completedBgColor = new Color(0.6f, 1f, 0.6f, 1f);

        [Header("─── VFX ─────────────────────────────")]
        [SerializeField] private GameObject completionVFXRoot;

        [Header("─── Database ────────────────────────")]
        [SerializeField] private FoodDatabase foodDatabase;

        // ── Exposed for States ────────────────────────────────────────────────
        public RectTransform RectTransform { get; private set; }
        public Vector2 HomeAnchoredPos { get; private set; }
        public OrderData OrderData { get; private set; }
        public int TrayIndex { get; private set; }

        // ── Events (Observer Pattern) ─────────────────────────────────────────
        public event Action<OrderTray> OnCompleted;
        public event Action<OrderTray> OnLeft;

        internal void RaiseCompleted() => OnCompleted?.Invoke(this);
        internal void RaiseLeft() => OnLeft?.Invoke(this);

        // ── State Machine ─────────────────────────────────────────────────────
        private OrderTrayState _currentState;
        private Dictionary<OrderTrayStateId, OrderTrayState> _states;
        public OrderTrayStateId CurrentStateId { get; private set; } = OrderTrayStateId.Idle;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            RectTransform = GetComponent<RectTransform>();
            BuildStateMachine();
        }

        private void BuildStateMachine()
        {
            _states = new Dictionary<OrderTrayStateId, OrderTrayState>
            {
                [OrderTrayStateId.Idle] = new IdleState(this),
                [OrderTrayStateId.Active] = new ActiveState(this),
                [OrderTrayStateId.Leaving] = new LeavingState(this),
            };
            _currentState = _states[OrderTrayStateId.Idle];
        }

        // ── IPoolable ─────────────────────────────────────────────────────────
        public void OnSpawn()
        {
            completionVFXRoot?.SetActive(false);
            if (trayBgImage != null) trayBgImage.color = normalBgColor;
            _currentState = _states[OrderTrayStateId.Idle];
            CurrentStateId = OrderTrayStateId.Idle;
        }

        public void OnDespawn()
        {
            DOTween.Kill(gameObject);
            RectTransform.DOKill();
            SlotReservationRegistry.Instance.ClearOrderTray(TrayIndex);
            OrderData = null;
            foreach (var slot in slots) slot.ResetSlot();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Initialize(OrderData orderData, int trayIndex,
                               Vector2 homePos, bool enterFromTop = true)
        {
            OrderData = orderData;
            TrayIndex = trayIndex;
            HomeAnchoredPos = homePos;

            _states[OrderTrayStateId.Enter] = new EnterState(this, homePos);
            _states[OrderTrayStateId.Completed] = new CompletedState(
                this, trayBgImage, completedBgColor, completionVFXRoot);

            var foodData = foodDatabase != null
                ? foodDatabase.GetFoodByID(orderData.FoodID)
                : null;

            if (foodData == null)
                Debug.LogError($"[OrderTray] FoodItemData id={orderData.FoodID} not found");
            else
                foreach (var slot in slots)
                    slot.ShowFoodIcon(foodData);

            if (enterFromTop)
                TransitionTo(OrderTrayStateId.Enter);
            else
            {
                RectTransform.anchoredPosition = homePos;
                TransitionTo(OrderTrayStateId.Active);
            }
        }

        public void MoveTo(Vector2 newHomePos, float duration = 0.35f)
        {
            HomeAnchoredPos = newHomePos;
            RectTransform.DOAnchorPos(newHomePos, duration)
                         .SetEase(Ease.OutCubic)
                         .SetUpdate(true);
        }

        /// <summary>
        /// Thử match food VÀ reserve slot ngay lập tức.
        /// Nếu slot tiếp theo đã bị reserve, tìm slot kế tiếp còn trống.
        /// Trả về -1 nếu không còn slot nào.
        /// </summary>
        public bool TryMatchAndReserve(int foodID, int foodItemInstanceId, out int reservedSlotIndex)
        {
            reservedSlotIndex = -1;

            if (CurrentStateId != OrderTrayStateId.Active) return false;
            if (OrderData == null || OrderData.FoodID != foodID) return false;
            if (OrderData.IsCompleted) return false;

            // Tìm slot: bắt đầu từ DeliveredCount, nhảy qua slot đã reserved
            int startSlot = OrderData.DeliveredCount;
            int totalSlots = slots.Count;

            for (int s = startSlot; s < totalSlots; s++)
            {
                // Slot này đã delivered (confirmed) rồi → skip
                if (s < OrderData.DeliveredCount) continue;

                // Thử reserve — nếu đã có người khác reserve thì next slot
                if (SlotReservationRegistry.Instance.TryReserveOrderSlot(
                        TrayIndex, s, foodItemInstanceId))
                {
                    reservedSlotIndex = s;
                    return true;
                }
            }

            // Tất cả slot đã đầy hoặc đang reserved
            Debug.Log($"[OrderTray#{TrayIndex}] Không còn slot cho food#{foodItemInstanceId}");
            return false;
        }

        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count)
                return transform.position;
            return slots[slotIndex].WorldPosition;
        }

        public Vector3 GetNextSlotWorldPosition()
        {
            int next = OrderData?.DeliveredCount ?? 0;
            return next < slots.Count ? slots[next].WorldPosition : transform.position;
        }

        public Vector3 GetNextSlotFoodScale()
        {
            int next = OrderData?.DeliveredCount ?? 0;
            return next < slots.Count ? slots[next].FoodTargetScale : Vector3.one;
        }

        public int FoodID => OrderData?.FoodID ?? -1;
        public int RemainingCount => OrderData?.RemainingCount ?? 0;

        // ── Delivery Confirm (2-phase) ────────────────────────────────────────

        /// <summary>
        /// [PHASE 1] Đánh dấu delivered về mặt LOGIC ngay lập tức, TRƯỚC khi animation bay xong.
        /// Gọi ngay sau khi food bắt đầu bay → OrderData.DeliveredCount tăng ngay →
        /// food thứ 2 cùng type sẽ thấy slot tiếp theo, không bị đánh trùng.
        /// </summary>
        public void PreConfirmDelivery(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count) return;
            OrderData.Deliver();
            // Không transition Completed ở đây — đợi FinalizeDeliveryVisual
            // để animation đáp xuống xong rồi mới show hiệu ứng hoàn thành.
        }

        /// <summary>
        /// [PHASE 2] Chạy visual sau khi food đáp xuống slot (OnComplete của fly animation).
        /// - Nếu order IsCompleted → TransitionTo Completed.
        /// </summary>
        public void FinalizeDeliveryVisual(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count) return;

            slots[slotIndex].PlayReceiveAnimation();
            slots[slotIndex].MarkDelivered();

            if (OrderData.IsCompleted)
                TransitionTo(OrderTrayStateId.Completed);
        }

        /// <summary>
        /// Legacy single-call confirm. Dùng khi chỉ có 1 food bay (không cần tách phase).
        /// </summary>
        public void ConfirmDelivery(int slotIndex)
        {
            PreConfirmDelivery(slotIndex);
            FinalizeDeliveryVisual(slotIndex);
        }

        /// <summary>Hủy reservation nếu food bay bị cancel/fail.</summary>
        public void ReleaseSlotReservation(int slotIndex)
        {
            SlotReservationRegistry.Instance.ReleaseOrderSlot(TrayIndex, slotIndex);
        }

        // ── State Transition ──────────────────────────────────────────────────
        internal void TransitionTo(OrderTrayStateId stateId)
        {
            if (!_states.TryGetValue(stateId, out var nextState))
            {
                Debug.LogError($"[OrderTray] State {stateId} not registered!");
                return;
            }

            _currentState?.Exit();
            CurrentStateId = stateId;
            _currentState = nextState;
            _currentState.Enter();
        }

        /// <summary>
        /// Trả về FoodTargetScale của slot tại <paramref name="slotIndex"/>.
        /// MagnetBooster cache giá trị này TRƯỚC khi bất kỳ ConfirmDelivery nào được gọi
        /// để đảm bảo từng food lấy đúng scale của slot mình sẽ đáp xuống.
        /// </summary>
        public Vector3 GetSlotFoodScale(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count)
                return Vector3.one;
            return slots[slotIndex].FoodTargetScale;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DECORATOR PATTERN — DOTween Extensions
    // ═══════════════════════════════════════════════════════════════════════════
    public static class OrderTrayDOTweenExtensions
    {
        public static Tweener DoFloatLoop(this RectTransform rt, float baseY,
                                          float amplitude = 6f, float duration = 1f)
            => rt.DOAnchorPosY(baseY + amplitude, duration)
                 .SetEase(Ease.InOutSine)
                 .SetLoops(-1, LoopType.Yoyo)
                 .SetUpdate(true);

        public static Tweener DoSlideInFromTop(this RectTransform rt,
                                                Vector2 targetPos, float duration = 0.45f)
            => rt.DOAnchorPos(targetPos, duration)
                 .SetEase(Ease.OutBack)
                 .SetUpdate(true);

        public static Tweener DoFlyOut(this RectTransform rt,
                                        float targetY, float duration = 0.38f)
            => rt.DOAnchorPosY(targetY, duration)
                 .SetEase(Ease.InBack)
                 .SetUpdate(true);
    }
}