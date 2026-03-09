using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using FoodMatch.Tray;
using FoodMatch.Level;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Cho phép player vuốt ngang để xoay CellContainer.
    /// Tự động thông báo FoodGridSpawner dừng auto-rotate khi có tương tác.
    /// </summary>
    public class TraySwipeRotator : MonoBehaviour,
                                    IPointerDownHandler,
                                    IDragHandler,
                                    IPointerUpHandler
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("References")]
        [Tooltip("FoodGridSpawner quản lý CellContainer cần xoay.")]
        [SerializeField] private FoodGridSpawner spawner;

        [Header("Swipe Settings")]
        [Tooltip("Số độ xoay / pixel vuốt ngang.")]
        [SerializeField] private float degreesPerPixel = 0.4f;

        [Tooltip("Khoảng cách pixel tối thiểu để coi là 'đang vuốt' (không phải tap).")]
        [SerializeField] private float swipeThreshold = 5f;

        [Tooltip("Inertia: tốc độ xoay giảm dần sau khi nhả tay (0 = tắt inertia).")]
        [SerializeField] private float inertiaDecay = 8f;

        [Tooltip("Tốc độ xoay tối đa (độ/giây) do inertia.")]
        [SerializeField] private float maxInertiaSpeed = 360f;

        // ─── Runtime ──────────────────────────────────────────────────────────
        private bool _isDragging = false;
        private float _lastDragX = 0f;
        private float _inertiaSpeed = 0f;
        private float _totalDragDelta = 0f;

        /// <summary>
        /// Luôn lấy fresh CellContainer từ spawner mỗi lần dùng.
        /// Tránh stale reference sau ClearGrid() / SpawnGrid().
        /// </summary>
        private Transform CellContainer => spawner?.GetCellContainer();

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (spawner == null)
                spawner = FindObjectOfType<FoodGridSpawner>();
        }

        private void OnEnable()
        {
            GameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        /// <summary>
        /// Khi LoadLevel (bao gồm Try Again): reset toàn bộ swipe state.
        /// Đảm bảo không còn _isDragging hay _inertiaSpeed stale từ session trước.
        /// </summary>
        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.LoadLevel)
                ResetSwipeState();
        }

        private void ResetSwipeState()
        {
            _isDragging = false;
            _lastDragX = 0f;
            _inertiaSpeed = 0f;
            _totalDragDelta = 0f;
        }

        // ─────────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_isDragging) return;

            // Inertia sau khi nhả tay
            if (!Mathf.Approximately(_inertiaSpeed, 0f))
            {
                var container = CellContainer;
                if (container != null)
                    container.Rotate(Vector3.up, _inertiaSpeed * Time.deltaTime, Space.World);

                _inertiaSpeed = Mathf.MoveTowards(
                    _inertiaSpeed, 0f, inertiaDecay * Mathf.Abs(_inertiaSpeed) * Time.deltaTime);

                if (Mathf.Abs(_inertiaSpeed) < 0.5f)
                    _inertiaSpeed = 0f;
            }
        }

        // ─── Pointer Events ───────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            _isDragging = true;
            _lastDragX = eventData.position.x;
            _totalDragDelta = 0f;
            _inertiaSpeed = 0f;
            spawner?.NotifyInteraction();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            float deltaX = eventData.position.x - _lastDragX;
            _totalDragDelta += Mathf.Abs(deltaX);
            _lastDragX = eventData.position.x;

            if (_totalDragDelta < swipeThreshold) return;

            var container = CellContainer;
            if (container == null) return;

            float degrees = -deltaX * degreesPerPixel;
            container.Rotate(Vector3.up, degrees, Space.World);

            float instantSpeed = (Time.deltaTime > 0f) ? (degrees / Time.deltaTime) : 0f;
            _inertiaSpeed = Mathf.Clamp(instantSpeed, -maxInertiaSpeed, maxInertiaSpeed);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isDragging = false;

            if (_totalDragDelta < swipeThreshold)
                _inertiaSpeed = 0f;
        }

        // ─── Public ───────────────────────────────────────────────────────────

        /// <summary>Gán spawner từ code nếu không dùng Inspector.</summary>
        public void SetSpawner(FoodGridSpawner s) => spawner = s;
    }
}