using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using FoodMatch.Tray;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Gắn vào một invisible hit-area (UI Panel hoặc Collider) phủ lên khu vực tray.
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
        private Transform _cellContainer;
        private bool _isDragging = false;
        private float _lastDragX = 0f;
        private float _inertiaSpeed = 0f;   // độ/giây, dương = xoay phải
        private float _totalDragDelta = 0f;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (spawner == null)
                spawner = FindObjectOfType<FoodGridSpawner>();
        }

        private void Update()
        {
            if (_isDragging) return;

            // Inertia sau khi nhả tay
            if (!Mathf.Approximately(_inertiaSpeed, 0f))
            {
                _cellContainer = spawner?.GetCellContainer();
                if (_cellContainer != null)
                    _cellContainer.Rotate(Vector3.up, _inertiaSpeed * Time.deltaTime, Space.World);

                _inertiaSpeed = Mathf.MoveTowards(
                    _inertiaSpeed, 0f, inertiaDecay * Mathf.Abs(_inertiaSpeed) * Time.deltaTime);

                // Dưới ngưỡng nhỏ → snap về 0
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

            // Thông báo spawner: có tương tác → dừng auto-rotate
            spawner?.NotifyInteraction();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            float deltaX = eventData.position.x - _lastDragX;
            _totalDragDelta += Mathf.Abs(deltaX);
            _lastDragX = eventData.position.x;

            // Bỏ qua nếu chưa vượt ngưỡng swipe
            if (_totalDragDelta < swipeThreshold) return;

            _cellContainer = spawner?.GetCellContainer();
            if (_cellContainer == null) return;

            float degrees = deltaX * degreesPerPixel;
            _cellContainer.Rotate(Vector3.up, degrees, Space.World);

            // Tính inertia tức thời (làm mượt qua Time.deltaTime)
            float instantSpeed = (Time.deltaTime > 0f)
                ? (degrees / Time.deltaTime)
                : 0f;

            _inertiaSpeed = Mathf.Clamp(instantSpeed, -maxInertiaSpeed, maxInertiaSpeed);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isDragging = false;

            // Nếu chỉ tap (không vuốt) → không có inertia, reset
            if (_totalDragDelta < swipeThreshold)
                _inertiaSpeed = 0f;

            // Tiếp tục inertia tự nhiên (xử lý trong Update)
        }

        // ─── Public ───────────────────────────────────────────────────────────

        /// <summary>Gán spawner từ code nếu không dùng Inspector.</summary>
        public void SetSpawner(FoodGridSpawner s) => spawner = s;
    }
}