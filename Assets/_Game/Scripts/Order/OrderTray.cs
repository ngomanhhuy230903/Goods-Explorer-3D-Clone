using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;

namespace FoodMatch.Order
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  STATE PATTERN – Mỗi state là class riêng, dễ thêm state mới
    // ═══════════════════════════════════════════════════════════════════════════
    public abstract class OrderTrayState
    {
        protected OrderTray Owner { get; }
        protected OrderTrayState(OrderTray owner) => Owner = owner;

        public virtual void Enter() { }
        public virtual void Exit() { }
        // Template Method: subclass override từng bước
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
            // Float animation – DOTween loop
            rt.DOAnchorPosY(homePos.y + 6f, 1.0f)
              .SetEase(Ease.InOutSine)
              .SetLoops(-1, LoopType.Yoyo)
              .SetUpdate(true);

            if (Owner.OrderData != null)
                DOVirtual.DelayedCall(0.1f,
                    () => EventBus.RaiseNewOrderActive(Owner.OrderData.FoodID), false)
                    .SetTarget(Owner.gameObject);
        }

        public override void Exit()
        {
            Owner.RectTransform.DOKill(); // Dừng float trước khi transition
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
            // DOTween: flash bg + punch scale
            _bgImage?.DOColor(_completedColor, 0.2f).SetUpdate(true);
            Owner.transform
                 .DOPunchScale(Vector3.one * 0.22f, 0.45f, 7, 0.5f)
                 .SetUpdate(true);

            _vfxRoot?.SetActive(true);

            // Notify observers – gọi qua internal helper để tránh CS0070
            Owner.RaiseCompleted();
            EventBus.RaiseOrderCompleted(Owner.TrayIndex);

            // KEY FIX: Delay ngắn rồi chuyển sang Leaving (bay lên, KHÔNG rearrange)
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
            var homePos = Owner.HomeAnchoredPos;

            // KEY FIX: Bay THẲNG LÊN, không liên quan đến tray khác
            Owner.RectTransform
                 .DOAnchorPosY(homePos.y + 400f, 0.38f)
                 .SetEase(Ease.InBack)
                 .SetUpdate(true)
                 .OnComplete(() =>
                 {
                     Owner.RaiseLeft(); // internal helper tránh CS0070
                     EventBus.RaiseOrderLeft(Owner.TrayIndex);
                     PoolManager.Instance.ReturnOrder(Owner.gameObject);
                 });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  STATE ID ENUM
    // ═══════════════════════════════════════════════════════════════════════════
    public enum OrderTrayStateId { Idle, Enter, Active, Completed, Leaving }

    // ═══════════════════════════════════════════════════════════════════════════
    //  OBSERVER PATTERN – Typed events thay vì UnityEvent để GC-friendly
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Khay order trên UI.
    ///
    /// Áp dụng:
    ///   State Pattern   – mỗi state class riêng, Template Method enter/exit
    ///   Observer        – event OnCompleted, OnLeft
    ///   Object Pool     – IPoolable
    ///   DOTween         – mọi animation qua DOTween (không dùng Coroutine)
    ///   Decorator       – DOTween extension method (xem bên dưới)
    ///   Null Object     – FoodDatabase lookup trả null-safe
    ///
    /// KEY FIX: OnEnterLeaving chỉ bay LÊN, không notify rearrange.
    /// OrderQueue giữ slot position cố định qua SlotRegistry.
    /// </summary>
    public class OrderTray : MonoBehaviour, IPoolable
    {
        // ── Inspector ────────────────────────────────────────────────────────
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

        // ── Observer events ───────────────────────────────────────────────────
        public event Action<OrderTray> OnCompleted;
        public event Action<OrderTray> OnLeft;

        // ── Internal raise helpers (State classes bên ngoài gọi qua đây, tránh CS0070) ─
        internal void RaiseCompleted() => OnCompleted?.Invoke(this);
        internal void RaiseLeft() => OnLeft?.Invoke(this);

        // ── State Machine ─────────────────────────────────────────────────────
        private OrderTrayState _currentState;
        private Dictionary<OrderTrayStateId, OrderTrayState> _states;

        /// <summary>State hiện tại dạng enum — check từ bên ngoài thay cho OrderState cũ.</summary>
        public OrderTrayStateId CurrentStateId { get; private set; } = OrderTrayStateId.Idle;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            RectTransform = GetComponent<RectTransform>();
            BuildStateMachine();
        }

        // ── State Machine Construction (Factory) ──────────────────────────────
        private void BuildStateMachine()
        {
            _states = new Dictionary<OrderTrayStateId, OrderTrayState>
            {
                [OrderTrayStateId.Idle] = new IdleState(this),
                // EnterState & CompletedState rebuilt per-init karena perlu homePos
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
        }

        public void OnDespawn()
        {
            DOTween.Kill(gameObject);
            RectTransform.DOKill();
            OrderData = null;
            foreach (var slot in slots) slot.ResetSlot();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Khởi tạo tray: rebuild states cần homePos → spawn icon → slide in.
        /// </summary>
        public void Initialize(OrderData orderData, int trayIndex,
                               Vector2 homePos, bool enterFromTop = true)
        {
            OrderData = orderData;
            TrayIndex = trayIndex;
            HomeAnchoredPos = homePos;

            // Rebuild states phụ thuộc homePos / data
            _states[OrderTrayStateId.Enter] = new EnterState(this, homePos);
            _states[OrderTrayStateId.Completed] = new CompletedState(
                this, trayBgImage, completedBgColor, completionVFXRoot);

            // Lookup food (Null Object: nếu null log error, không crash)
            var foodData = foodDatabase != null
                ? foodDatabase.GetFoodByID(orderData.FoodID)
                : null;

            if (foodData == null)
                Debug.LogError($"[OrderTray] Không tìm thấy FoodItemData id={orderData.FoodID}");
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

        /// <summary>Di chuyển home position (chỉ dùng nếu layout thay đổi, không phải khi tray complete).</summary>
        public void MoveTo(Vector2 newHomePos, float duration = 0.35f)
        {
            HomeAnchoredPos = newHomePos;
            RectTransform.DOAnchorPos(newHomePos, duration)
                         .SetEase(Ease.OutCubic)
                         .SetUpdate(true);
        }

        public bool TryMatch(int foodID, out int slotIndex)
        {
            slotIndex = -1;
            if (_currentState is not ActiveState) return false;
            if (OrderData == null || OrderData.FoodID != foodID) return false;
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
            return next < slots.Count ? slots[next].FoodTargetScale : Vector3.one;
        }

        public void ConfirmDelivery(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count) return;

            OrderData.Deliver();
            slots[slotIndex].PlayReceiveAnimation();
            slots[slotIndex].MarkDelivered();

            if (OrderData.IsCompleted)
                TransitionTo(OrderTrayStateId.Completed);
        }

        // ── State Transition (internal, called by State classes) ──────────────
        /// <summary>
        /// Chuyển state: gọi Exit() trên state cũ → Enter() trên state mới.
        /// Internal vì chỉ State hoặc OrderQueue trigger.
        /// </summary>
        internal void TransitionTo(OrderTrayStateId stateId)
        {
            if (!_states.TryGetValue(stateId, out var nextState))
            {
                Debug.LogError($"[OrderTray] State {stateId} chưa được đăng ký!");
                return;
            }

            _currentState?.Exit();
            CurrentStateId = stateId;   // ← cập nhật enum để bên ngoài check
            _currentState = nextState;
            _currentState.Enter();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DECORATOR PATTERN – DOTween extension methods để tái sử dụng animation
    // ═══════════════════════════════════════════════════════════════════════════
    public static class OrderTrayDOTweenExtensions
    {
        /// <summary>Float animation chuẩn dùng cho Active state.</summary>
        public static Tweener DoFloatLoop(this RectTransform rt, float baseY, float amplitude = 6f, float duration = 1f)
        {
            return rt.DOAnchorPosY(baseY + amplitude, duration)
                     .SetEase(Ease.InOutSine)
                     .SetLoops(-1, LoopType.Yoyo)
                     .SetUpdate(true);
        }

        /// <summary>Slide in từ trên.</summary>
        public static Tweener DoSlideInFromTop(this RectTransform rt, Vector2 targetPos, float duration = 0.45f)
        {
            return rt.DOAnchorPos(targetPos, duration)
                     .SetEase(Ease.OutBack)
                     .SetUpdate(true);
        }

        /// <summary>Bay lên ra khỏi màn hình.</summary>
        public static Tweener DoFlyOut(this RectTransform rt, float targetY, float duration = 0.38f)
        {
            return rt.DOAnchorPosY(targetY, duration)
                     .SetEase(Ease.InBack)
                     .SetUpdate(true);
        }
    }
}