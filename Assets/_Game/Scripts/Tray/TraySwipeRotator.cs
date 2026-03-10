using UnityEngine;
using UnityEngine.EventSystems;
using FoodMatch.Level;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Xoay CellContainer bằng cách đọc Input thô (mouse/touch) trực tiếp.
    /// KHÔNG dùng Raycast, KHÔNG dùng UI EventSystem → không bị chặn bởi 3D collider.
    /// Logic: giữ ngón tay/chuột + kéo ngang bất kỳ đâu trên màn hình → xoay.
    /// Nếu muốn giới hạn vùng kéo, chỉ cần set dragZoneRect trong Inspector.
    /// </summary>
    public class TraySwipeRotator : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("References")]
        [SerializeField] private FoodGridSpawner spawner;

        [Header("Drag Zone (tuỳ chọn)")]
        [Tooltip("Giới hạn vùng kéo theo RectTransform của MainTrayArea. " +
                 "Để trống = chấp nhận kéo từ bất kỳ đâu trên màn hình.")]
        [SerializeField] private RectTransform dragZoneRect;

        [Tooltip("Canvas chứa dragZoneRect (cần để convert tọa độ).")]
        [SerializeField] private Canvas dragZoneCanvas;

        [Header("Swipe Settings")]
        [SerializeField] private float degreesPerPixel = 0.4f;
        [SerializeField] private float swipeThreshold = 5f;
        [SerializeField] private float inertiaDecay = 8f;
        [SerializeField] private float maxInertiaSpeed = 360f;

        // ─── Runtime ──────────────────────────────────────────────────────────
        private bool _isDragging;
        private float _lastDragX;
        private float _inertiaSpeed;
        private float _totalDragDelta;

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
            ResetSwipeState();
        }

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
#if UNITY_EDITOR || UNITY_STANDALONE
            HandleMouse();
#else
            HandleTouch();
#endif
            ApplyInertia();
        }

        // ─── Mouse (Editor / Standalone) ─────────────────────────────────────

        private void HandleMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (!IsInsideDragZone(Input.mousePosition)) return;
                BeginDrag(Input.mousePosition.x);
            }
            else if (Input.GetMouseButton(0) && _isDragging)
            {
                ProcessDrag(Input.mousePosition.x);
            }
            else if (Input.GetMouseButtonUp(0) && _isDragging)
            {
                EndDrag();
            }
        }

        // ─── Touch (Mobile) ───────────────────────────────────────────────────

        private void HandleTouch()
        {
            if (Input.touchCount == 0)
            {
                if (_isDragging) EndDrag();
                return;
            }

            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (!IsInsideDragZone(touch.position)) return;
                    BeginDrag(touch.position.x);
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (_isDragging) ProcessDrag(touch.position.x);
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (_isDragging) EndDrag();
                    break;
            }
        }

        // ─── Core Drag Logic ──────────────────────────────────────────────────

        private void BeginDrag(float screenX)
        {
            _isDragging = true;
            _lastDragX = screenX;
            _totalDragDelta = 0f;
            _inertiaSpeed = 0f;
            spawner?.NotifyInteraction();
        }

        private void ProcessDrag(float screenX)
        {
            float deltaX = screenX - _lastDragX;
            _totalDragDelta += Mathf.Abs(deltaX);
            _lastDragX = screenX;

            if (_totalDragDelta < swipeThreshold) return;

            var container = CellContainer;
            if (container == null) return;

            float degrees = -deltaX * degreesPerPixel;
            container.Rotate(Vector3.up, degrees, Space.World);

            float instantSpeed = Time.deltaTime > 0f ? degrees / Time.deltaTime : 0f;
            _inertiaSpeed = Mathf.Clamp(instantSpeed, -maxInertiaSpeed, maxInertiaSpeed);
        }

        private void EndDrag()
        {
            _isDragging = false;
            if (_totalDragDelta < swipeThreshold)
                _inertiaSpeed = 0f;
        }

        // ─── Inertia ──────────────────────────────────────────────────────────

        private void ApplyInertia()
        {
            if (_isDragging || Mathf.Approximately(_inertiaSpeed, 0f)) return;

            var container = CellContainer;
            if (container != null)
                container.Rotate(Vector3.up, _inertiaSpeed * Time.deltaTime, Space.World);

            _inertiaSpeed = Mathf.MoveTowards(
                _inertiaSpeed, 0f, inertiaDecay * Mathf.Abs(_inertiaSpeed) * Time.deltaTime);

            if (Mathf.Abs(_inertiaSpeed) < 0.5f)
                _inertiaSpeed = 0f;
        }

        // ─── Drag Zone Check ──────────────────────────────────────────────────

        /// <summary>
        /// Kiểm tra điểm chạm có nằm trong dragZoneRect không.
        /// Nếu không set dragZoneRect → luôn trả về true (kéo được toàn màn hình).
        /// </summary>
        private bool IsInsideDragZone(Vector2 screenPos)
        {
            if (dragZoneRect == null) return true;

            Camera cam = (dragZoneCanvas != null &&
                          dragZoneCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                         ? dragZoneCanvas.worldCamera
                         : null;

            return RectTransformUtility.RectangleContainsScreenPoint(
                dragZoneRect, screenPos, cam);
        }

        // ─── Public ───────────────────────────────────────────────────────────

        public void SetSpawner(FoodGridSpawner s) => spawner = s;
    }
}