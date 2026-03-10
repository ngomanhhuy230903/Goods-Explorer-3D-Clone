using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Data;

namespace FoodMatch.Tray
{
    public class FoodGridSpawner : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("─── References ─────────────────────")]
        [SerializeField] private RectTransform mainTrayArea;
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private Transform cellContainer;

        [Header("─── Kích thước Prefab ─────────────")]
        [Tooltip("Chiều dài từ pivot (đỉnh nhọn) đến mép đáy (trục Z local của prefab).")]
        [SerializeField] private float prefabDepth = 1f;

        [Tooltip("Chiều rộng GỐC của đáy tam giác ở scale (1,1,1). Dùng để tính hệ số nhân scaleX.")]
        [SerializeField] private float prefabBaseWidth = 1f;

        [Tooltip("Chiều cao THỰC của prefab theo trục Y world (đo trong scene ở scale 1,1,1).\n" +
                 "Nếu = 0 sẽ tự đo qua Renderer.bounds.")]
        [SerializeField] private float prefabHeightY = 1f;

        [Header("─── Spawn Position ──────────────────")]
        [Tooltip("Vị trí Z của CellContainer trong world space.")]
        [SerializeField] private float containerPositionZ = 0f;

        [Tooltip("Y bắt đầu của hàng 0 (local container).")]
        [SerializeField] private float baseY = 0f;

        [Tooltip("Khoảng cách thêm giữa các hàng (local).\n" +
                 "= 0 khít nhau | > 0 có khe | < 0 chồng lên nhau.")]
        [SerializeField] private float rowGap = 0f;

        [Header("─── Idle Auto-Rotate ─────────────────")]
        [Tooltip("Thời gian không tương tác (giây) trước khi bắt đầu xoay tự động.")]
        [SerializeField] private float idleTimeBeforeRotate = 5f;

        [Tooltip("Tốc độ xoay Y tự động (độ/giây). Âm = ngược chiều.")]
        [SerializeField] private float autoRotateSpeed = 20f;

        [Tooltip("Thời gian ease-in khi bắt đầu xoay (giây).")]
        [SerializeField] private float rotateEaseInDuration = 1f;

        [Tooltip("Thời gian ease-out khi dừng xoay (giây).")]
        [SerializeField] private float rotateEaseOutDuration = 0.5f;

        [Header("─── Debug / Manual Override ─────────")]
        [Tooltip("Nhân thêm hệ số vào scaleX đã tính. = 1 để dùng công thức.\n" +
                 "Chỉ ảnh hưởng trục X, Y và Z của cell giữ nguyên scale gốc hoàn toàn.")]
        [SerializeField] private float debugScaleXMultiplier = 1f;

        [Tooltip("Override toàn bộ rowHeight (local). = 0 để tự tính.")]
        [SerializeField] private float debugRowHeightOverride = 0f;

        [SerializeField] private bool verboseLog = true;

        [Header("─── Spawn Animation ─────────────────")]
        [SerializeField] private float spawnDuration = 0.35f;
        [SerializeField] private Ease spawnEase = Ease.OutBack;
        [SerializeField] private float staggerDelay = 0.04f;

        // ─── Runtime ──────────────────────────────────────────────────────────

        private readonly List<List<Transform>> _grid = new();

        /// <summary>
        /// Invoke SAU KHI animation của cell CUỐI CÙNG hoàn tất.
        /// Lúc này tất cả anchor đã đúng vị trí world → an toàn để spawn food.
        /// </summary>
        public System.Action OnSpawnComplete;

        public int Columns { get; private set; }
        public int Rows { get; private set; }

        // neutralContainer: chứa Food, scale luôn (1,1,1), tạo runtime
        private Transform _neutralContainer;

        // Auto-rotate state
        private float _idleTimer = 0f;
        private bool _isAutoRotating = false;
        private float _currentRotSpeed = 0f;
        private Tweener _rotateTweener = null;

        // Khóa xoay cứng — ShuffleBooster set true trong lúc shuffle
        // Khi locked: Update() bỏ qua idle timer và xoay hoàn toàn
        private bool _isRotationLocked = false;

        // ─────────────────────────────────────────────────────────────────────

        public void SpawnGrid(LevelConfig config)
        {
            if (!Validate(config)) return;
            ClearGrid();

            Columns = Mathf.Max(3, config.trayColumns);
            Rows = Mathf.Max(1, config.trayRows);

            // ── CellContainer ──────────────────────────────────────────────
            if (cellContainer == null)
            {
                var go = new GameObject("CellContainer");
                go.transform.SetParent(transform, false);
                cellContainer = go.transform;
            }

            // ── NeutralContainer (sibling của CellContainer) ───────────────
            if (_neutralContainer != null)
                Destroy(_neutralContainer.gameObject);

            var ncGO = new GameObject("FoodContainer");
            ncGO.transform.SetParent(transform, false);
            ncGO.transform.localPosition = Vector3.zero;
            ncGO.transform.localRotation = Quaternion.identity;
            ncGO.transform.localScale = Vector3.one;
            _neutralContainer = ncGO.transform;

            _isRotationLocked = false; // reset lock khi spawn level mới
            ResetIdleTimer();
            StartCoroutine(SpawnAfterLayout());
        }

        public void ClearGrid()
        {
            StopAllCoroutines();
            StopAutoRotate(instant: true);
            _isRotationLocked = false;

            foreach (var row in _grid)
                foreach (var cell in row)
                    if (cell != null) Destroy(cell.gameObject);
            _grid.Clear();

            if (_neutralContainer != null)
            {
                foreach (Transform child in _neutralContainer)
                    Destroy(child.gameObject);
            }
        }

        // ─── Update ───────────────────────────────────────────────────────────

        private void Update()
        {
            if (cellContainer == null || _grid.Count == 0) return;

            // Đang bị khóa (shuffle chạy) → không đếm idle, không xoay
            if (_isRotationLocked) return;

            _idleTimer += Time.deltaTime;

            if (!_isAutoRotating && _idleTimer >= idleTimeBeforeRotate)
                StartAutoRotate();

            if (_isAutoRotating && !Mathf.Approximately(_currentRotSpeed, 0f))
                cellContainer.Rotate(Vector3.up, _currentRotSpeed * Time.deltaTime, Space.World);
        }

        // ─── Public: Rotation Control ─────────────────────────────────────────

        /// <summary>Gọi khi player tương tác — reset idle, dừng auto-rotate (ease-out).</summary>
        public void NotifyInteraction()
        {
            ResetIdleTimer();
            if (_isAutoRotating)
                StopAutoRotate(instant: false);
        }

        /// <summary>
        /// Dừng xoay CỨNG ngay lập tức + đánh dấu locked.
        /// ShuffleBooster gọi khi bắt đầu shuffle để anchor positions không drift.
        /// Khác NotifyInteraction: không ease-out, hard-stop trong 1 frame.
        /// </summary>
        public void LockRotation()
        {
            _isRotationLocked = true;
            _rotateTweener?.Kill();
            _isAutoRotating = false;
            _currentRotSpeed = 0f;
            ResetIdleTimer();
        }

        /// <summary>
        /// Mở khóa xoay sau khi shuffle hoàn tất.
        /// Reset idle timer → auto-rotate tự khởi động lại sau idleTimeBeforeRotate giây.
        /// </summary>
        public void UnlockRotation()
        {
            _isRotationLocked = false;
            ResetIdleTimer();
        }

        // ─── Auto Rotate ──────────────────────────────────────────────────────

        private void StartAutoRotate()
        {
            _isAutoRotating = true;
            _currentRotSpeed = 0f;

            _rotateTweener?.Kill();
            _rotateTweener = DOTween
                .To(() => _currentRotSpeed, v => _currentRotSpeed = v,
                    autoRotateSpeed, rotateEaseInDuration)
                .SetEase(Ease.InSine);
        }

        private void StopAutoRotate(bool instant)
        {
            _rotateTweener?.Kill();

            if (instant || Mathf.Approximately(_currentRotSpeed, 0f))
            {
                _isAutoRotating = false;
                _currentRotSpeed = 0f;
            }
            else
            {
                _rotateTweener = DOTween
                    .To(() => _currentRotSpeed, v => _currentRotSpeed = v,
                        0f, rotateEaseOutDuration)
                    .SetEase(Ease.OutSine)
                    .OnComplete(() =>
                    {
                        _isAutoRotating = false;
                        _currentRotSpeed = 0f;
                    });
            }

            if (verboseLog)
                Debug.Log("[FoodGridSpawner] Tương tác → dừng xoay tự động.");
        }

        private void ResetIdleTimer() => _idleTimer = 0f;

        // ─── Core Spawn ───────────────────────────────────────────────────────

        private IEnumerator SpawnAfterLayout()
        {
            yield return null;

            // ════════════════════════════════════════════════════════════════
            // BƯỚC 1: Đặt container
            // ════════════════════════════════════════════════════════════════
            Vector3[] corners = new Vector3[4];
            mainTrayArea.GetWorldCorners(corners);

            Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
            worldCenter.z = containerPositionZ;

            cellContainer.position = worldCenter;
            cellContainer.rotation = Quaternion.identity;
            cellContainer.localScale = Vector3.one;

            // ════════════════════════════════════════════════════════════════
            // BƯỚC 2: Tính scaleX
            // ════════════════════════════════════════════════════════════════
            float angleStep = 360f / Columns;
            float halfAngleRad = (angleStep * 0.5f) * Mathf.Deg2Rad;
            float idealBaseWidth = 2f * prefabDepth * Mathf.Tan(halfAngleRad);
            float scaleXRatio = (prefabBaseWidth > 0f) ? idealBaseWidth / prefabBaseWidth : 1f;
            float finalScaleXMul = scaleXRatio * debugScaleXMultiplier;

            Vector3 prefabOriginalScale = cellPrefab.transform.localScale;
            Vector3 targetCellScale = new Vector3(
                prefabOriginalScale.x * finalScaleXMul,
                prefabOriginalScale.y,
                prefabOriginalScale.z);

            // ════════════════════════════════════════════════════════════════
            // BƯỚC 3: Tính rowHeight
            // ════════════════════════════════════════════════════════════════
            float measuredH = MeasurePrefabHeightY();
            float rowHeight = (debugRowHeightOverride > 0f)
                              ? debugRowHeightOverride
                              : measuredH + rowGap;

            // ════════════════════════════════════════════════════════════════
            // BƯỚC 4: Spawn + tính tổng thời gian animation
            // ════════════════════════════════════════════════════════════════
            int totalCells = Rows * Columns;
            int spawnOrder = 0;
            float lastDelay = (totalCells - 1) * staggerDelay;
            float totalAnimTime = lastDelay + spawnDuration;

            for (int row = 0; row < Rows; row++)
            {
                List<Transform> rowList = new();
                float localY = baseY + row * rowHeight;

                for (int col = 0; col < Columns; col++)
                {
                    float rotY = col * angleStep;

                    GameObject cellGO = Instantiate(cellPrefab, cellContainer);
                    cellGO.name = $"Cell_R{row}_C{col}";
                    Transform t = cellGO.transform;

                    t.localPosition = new Vector3(0f, localY, 0f);
                    Vector3 origEuler = cellPrefab.transform.eulerAngles;
                    t.localEulerAngles = new Vector3(origEuler.x, rotY, origEuler.z);
                    t.localScale = Vector3.zero;

                    float delay = spawnOrder * staggerDelay;
                    DOTween.Sequence()
                        .SetDelay(delay)
                        .Append(t.DOScale(targetCellScale, spawnDuration).SetEase(spawnEase));

                    rowList.Add(t);
                    spawnOrder++;
                }

                _grid.Add(rowList);
            }

            // ════════════════════════════════════════════════════════════════
            // BƯỚC 5: Chờ animation CUỐI CÙNG xong → invoke OnSpawnComplete
            // ════════════════════════════════════════════════════════════════
            yield return new WaitForSeconds(totalAnimTime);

            if (verboseLog)
                Debug.Log($"[FoodGridSpawner] Tất cả {totalCells} cell đã scale xong → invoke OnSpawnComplete.");

            OnSpawnComplete?.Invoke();
        }

        // ─── Đo chiều cao Y thực ─────────────────────────────────────────────

        private float MeasurePrefabHeightY()
        {
            if (prefabHeightY > 0f) return prefabHeightY;

            var temp = Instantiate(cellPrefab);
            temp.SetActive(false);

            float height = 0f;
            var renderers = temp.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                height = b.size.y;
            }

            Destroy(temp);
            return (height > 0f) ? height : prefabDepth;
        }

        // ─── Public Getters ───────────────────────────────────────────────────

        public Transform GetCellContainer() => cellContainer;
        public Transform GetNeutralContainer() => _neutralContainer;

        public Vector3 GetCellWorldPosition(int row, int col)
        {
            if (row < 0 || row >= _grid.Count) return Vector3.zero;
            var r = _grid[row];
            if (col < 0 || col >= r.Count) return Vector3.zero;
            return r[col].position;
        }

        public Transform GetCell(int row, int col)
        {
            if (row < 0 || row >= _grid.Count) return null;
            var r = _grid[row];
            if (col < 0 || col >= r.Count) return null;
            return r[col];
        }

        public List<FoodMatch.Food.FoodItem> GetAllActiveFoods()
        {
            var result = new List<FoodMatch.Food.FoodItem>();
            if (_neutralContainer == null) return result;

            var foods = _neutralContainer
                .GetComponentsInChildren<FoodMatch.Food.FoodItem>(includeInactive: false);
            result.AddRange(foods);
            return result;
        }

        /// <summary>
        /// Quét tất cả FoodTray con, gom pending data layer 2+ theo foodID.
        /// MagnetBooster dùng để lấy food chưa spawn.
        /// </summary>
        public List<FoodMatch.Data.FoodItemData> GetPendingFoodsOfType(int foodID)
        {
            var result = new List<FoodMatch.Data.FoodItemData>();
            if (cellContainer == null) return result;

            var trays = cellContainer
                .GetComponentsInChildren<FoodTray>(includeInactive: false);

            foreach (var tray in trays)
                result.AddRange(tray.GetPendingFoodsOfType(foodID));

            return result;
        }

        public Vector3 GetCenterWorldPosition()
            => cellContainer != null ? cellContainer.position : transform.position;

        // ─── Validate ─────────────────────────────────────────────────────────

        private bool Validate(LevelConfig config)
        {
            if (config == null) { Debug.LogError("[FoodGridSpawner] LevelConfig null!"); return false; }
            if (cellPrefab == null) { Debug.LogError("[FoodGridSpawner] cellPrefab chưa gán!"); return false; }
            if (mainTrayArea == null) { Debug.LogError("[FoodGridSpawner] mainTrayArea chưa gán!"); return false; }
            return true;
        }
    }
}