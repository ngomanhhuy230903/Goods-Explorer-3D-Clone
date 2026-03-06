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
        public System.Action OnSpawnComplete;
        public int Columns { get; private set; }
        public int Rows { get; private set; }

        // Auto-rotate state
        private float _idleTimer = 0f;
        private bool _isAutoRotating = false;
        private float _currentRotSpeed = 0f;
        private Tweener _rotateTweener = null;

        // ─────────────────────────────────────────────────────────────────────

        public void SpawnGrid(LevelConfig config)
        {
            if (!Validate(config)) return;
            ClearGrid();

            Columns = Mathf.Max(3, config.trayColumns);
            Rows = Mathf.Max(1, config.trayRows);

            if (cellContainer == null)
            {
                var go = new GameObject("CellContainer");
                go.transform.SetParent(transform, false);
                cellContainer = go.transform;
            }

            ResetIdleTimer();
            StartCoroutine(SpawnAfterLayout());
        }

        public void ClearGrid()
        {
            StopAllCoroutines();
            StopAutoRotate(instant: true);
            foreach (var row in _grid)
                foreach (var cell in row)
                    if (cell != null) Destroy(cell.gameObject);
            _grid.Clear();
        }

        // ─── Update ───────────────────────────────────────────────────────────

        private void Update()
        {
            if (cellContainer == null || _grid.Count == 0) return;

            _idleTimer += Time.deltaTime;

            if (!_isAutoRotating && _idleTimer >= idleTimeBeforeRotate)
                StartAutoRotate();

            if (_isAutoRotating && !Mathf.Approximately(_currentRotSpeed, 0f))
                cellContainer.Rotate(Vector3.up, _currentRotSpeed * Time.deltaTime, Space.World);
        }

        // ─── Public: Gọi khi player tương tác ────────────────────────────────

        /// <summary>
        /// Gọi bất cứ khi nào player tương tác (click, drag, chọn food...)
        /// để reset bộ đếm idle và dừng xoay tự động.
        /// </summary>
        public void NotifyInteraction()
        {
            ResetIdleTimer();
            if (_isAutoRotating)
                StopAutoRotate(instant: false);
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

            //if (verboseLog)
            //    Debug.Log("[FoodGridSpawner] Idle → bắt đầu xoay tự động.");
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
            //   - XY  → tâm MainTrayArea
            //   - Z   → containerPositionZ (chỉnh ngoài editor)
            //   - Scale → LUÔN = 1, không chạm vào
            // ════════════════════════════════════════════════════════════════
            Vector3[] corners = new Vector3[4];
            mainTrayArea.GetWorldCorners(corners);

            Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
            worldCenter.z = containerPositionZ;

            cellContainer.position = worldCenter;
            cellContainer.rotation = Quaternion.identity;
            cellContainer.localScale = Vector3.one; // ← cứng, không thay đổi

            // ════════════════════════════════════════════════════════════════
            // BƯỚC 2: Tính hệ số nhân scaleX
            //   - Chỉ trục X thay đổi để mép đáy khít nhau
            //   - Y và Z giữ nguyên scale gốc của prefab hoàn toàn
            // ════════════════════════════════════════════════════════════════
            float angleStep = 360f / Columns;
            float halfAngleRad = (angleStep * 0.5f) * Mathf.Deg2Rad;
            float idealBaseWidth = 2f * prefabDepth * Mathf.Tan(halfAngleRad);
            float scaleXRatio = (prefabBaseWidth > 0f) ? idealBaseWidth / prefabBaseWidth : 1f;
            float finalScaleXMul = scaleXRatio * debugScaleXMultiplier;

            Vector3 prefabOriginalScale = cellPrefab.transform.localScale;

            // Scale target: chỉ X thay đổi, Y và Z = gốc
            Vector3 targetCellScale = new Vector3(
                prefabOriginalScale.x * finalScaleXMul, // ← multiply X
                prefabOriginalScale.y,                   // ← giữ nguyên
                prefabOriginalScale.z);                  // ← giữ nguyên

            // ════════════════════════════════════════════════════════════════
            // BƯỚC 3: Tính rowHeight
            // ════════════════════════════════════════════════════════════════
            float measuredH = MeasurePrefabHeightY();
            float rowHeight = (debugRowHeightOverride > 0f)
                              ? debugRowHeightOverride
                              : measuredH + rowGap;

            // ════════════════════════════════════════════════════════════════
            // BƯỚC 4: Spawn
            // ════════════════════════════════════════════════════════════════
            int spawnOrder = 0;

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

                    // Chỉ multiply X, Y và Z không đổi
                    t.localScale = targetCellScale;

                    // Animate scale từ 0 → target
                    t.localScale = Vector3.zero;
                    float delay = spawnOrder * staggerDelay;
                    DOTween.Sequence()
                        .SetDelay(delay)
                        .Append(t.DOScale(targetCellScale, spawnDuration).SetEase(spawnEase));

                    rowList.Add(t);
                    spawnOrder++;
                }

                _grid.Add(rowList);
                OnSpawnComplete?.Invoke();
            }

            // ════════════════════════════════════════════════════════════════
            // DEBUG LOG
            // ════════════════════════════════════════════════════════════════
            //if (verboseLog)
            //{
            //    Debug.Log($"[FoodGridSpawner] ══════════════════════════════════════════");
            //    Debug.Log($"[FoodGridSpawner] {Columns} cột × {Rows} hàng");
            //    Debug.Log($"[FoodGridSpawner] ── Container ────────────────────────────");
            //    Debug.Log($"[FoodGridSpawner]   position (world)     = {cellContainer.position}");
            //    Debug.Log($"[FoodGridSpawner]   containerPositionZ   = {containerPositionZ:F3}");
            //    Debug.Log($"[FoodGridSpawner]   scale                = {cellContainer.localScale}  ✓ = (1,1,1)");
            //    Debug.Log($"[FoodGridSpawner] ── Scale X (NHÂN) ──────────────────────");
            //    Debug.Log($"[FoodGridSpawner]   prefabOriginalScale  = {prefabOriginalScale}");
            //    Debug.Log($"[FoodGridSpawner]   idealBaseWidth       = {idealBaseWidth:F4}");
            //    Debug.Log($"[FoodGridSpawner]   scaleXRatio          = {scaleXRatio:F4}");
            //    Debug.Log($"[FoodGridSpawner]   debugScaleXMultiplier= {debugScaleXMultiplier:F4}" +
            //               (Mathf.Approximately(debugScaleXMultiplier, 1f) ? "" : "  ⚠️ Override!"));
            //    Debug.Log($"[FoodGridSpawner]   finalScaleXMul       = {finalScaleXMul:F4}");
            //    Debug.Log($"[FoodGridSpawner]   targetCellScale      = {targetCellScale}");
            //    Debug.Log($"[FoodGridSpawner] ── Row Height ───────────────────────────");
            //    Debug.Log($"[FoodGridSpawner]   measuredHeightY      = {measuredH:F4}");
            //    Debug.Log($"[FoodGridSpawner]   rowGap               = {rowGap:F4}");
            //    Debug.Log($"[FoodGridSpawner]   rowHeight            = {rowHeight:F4}");
            //    Debug.Log($"[FoodGridSpawner]   totalHeight          = {rowHeight * Rows:F4}");

            //    for (int r = 0; r < Rows; r++)
            //        Debug.Log($"[FoodGridSpawner]   row {r}  localY = {(baseY + r * rowHeight):F4}");

            //    Debug.Log($"[FoodGridSpawner] ── Idle Rotate ──────────────────────────");
            //    Debug.Log($"[FoodGridSpawner]   idleDelay            = {idleTimeBeforeRotate:F1}s");
            //    Debug.Log($"[FoodGridSpawner]   autoRotateSpeed      = {autoRotateSpeed:F1}°/s");
            //    Debug.Log($"[FoodGridSpawner]   easeIn               = {rotateEaseInDuration:F2}s");
            //    Debug.Log($"[FoodGridSpawner]   easeOut              = {rotateEaseOutDuration:F2}s");
            //    Debug.Log($"[FoodGridSpawner] ══════════════════════════════════════════");
            //}
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

        // ─── Validate ─────────────────────────────────────────────────────────

        private bool Validate(LevelConfig config)
        {
            if (config == null) { Debug.LogError("[FoodGridSpawner] LevelConfig null!"); return false; }
            if (cellPrefab == null) { Debug.LogError("[FoodGridSpawner] cellPrefab chưa gán!"); return false; }
            if (mainTrayArea == null) { Debug.LogError("[FoodGridSpawner] mainTrayArea chưa gán!"); return false; }
            return true;
        }

        // ─── Gizmos ───────────────────────────────────────────────────────────
//#if UNITY_EDITOR
//        [Header("─── Editor Preview ──────────────────")]
//        [SerializeField] private int previewColumns = 4;
//        [SerializeField] private int previewRows = 2;

//        private void OnDrawGizmosSelected()
//        {
//            if (mainTrayArea == null) return;

//            Vector3[] corners = new Vector3[4];
//            mainTrayArea.GetWorldCorners(corners);
//            Vector3 center = (corners[0] + corners[2]) * 0.5f;
//            center.z = containerPositionZ;

//            int cols = Mathf.Max(3, previewColumns);
//            int rows = Mathf.Max(1, previewRows);

//            float worldD = prefabDepth; // container scale = 1, không nhân gì thêm
//            float angleStep = 360f / cols;

//            float halfAngleRad = (180f / cols) * Mathf.Deg2Rad;
//            float idealBaseWorld = 2f * worldD * Mathf.Tan(halfAngleRad);

//            float localH = (prefabHeightY > 0f ? prefabHeightY : prefabDepth);
//            float localRowH = (debugRowHeightOverride > 0f)
//                              ? debugRowHeightOverride
//                              : localH + rowGap;

//            for (int row = 0; row < rows; row++)
//            {
//                float yPos = baseY + row * localRowH;
//                Vector3 rowCtr = center + Vector3.up * yPos;

//                Gizmos.color = Color.cyan;
//                DrawPolygon(rowCtr, worldD, cols, angleStep);

//                Gizmos.color = Color.green;
//                for (int col = 0; col < cols; col++)
//                {
//                    float rad = col * angleStep * Mathf.Deg2Rad;
//                    Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
//                    Gizmos.DrawSphere(rowCtr, worldD * 0.025f);
//                    Gizmos.DrawLine(rowCtr, rowCtr + dir * worldD);
//                }

//                Gizmos.color = Color.yellow;
//                for (int col = 0; col < cols; col++)
//                {
//                    float rad = (col * angleStep + angleStep * 0.5f) * Mathf.Deg2Rad;
//                    Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
//                    Gizmos.DrawLine(rowCtr, rowCtr + dir * worldD);
//                }

//                UnityEditor.Handles.color = Color.white;
//                UnityEditor.Handles.Label(
//                    rowCtr + Vector3.right * worldD * 1.15f,
//                    $"Row {row} | Y={yPos:F2}\nrowH={localRowH:F2} | BaseW={idealBaseWorld:F2}");
//            }

//            // Z marker
//            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
//            Gizmos.DrawLine(center - Vector3.forward * worldD * 0.3f,
//                            center + Vector3.forward * worldD * 0.3f);

//            // Khung MainTrayArea
//            Gizmos.color = Color.green;
//            Gizmos.DrawLine(corners[0], corners[1]);
//            Gizmos.DrawLine(corners[1], corners[2]);
//            Gizmos.DrawLine(corners[2], corners[3]);
//            Gizmos.DrawLine(corners[3], corners[0]);

//            float scaleXR = (prefabBaseWidth > 0f) ? idealBaseWorld / prefabBaseWidth : 1f;
//            UnityEditor.Handles.color = Color.cyan;
//            UnityEditor.Handles.Label(
//                center + Vector3.up * (localRowH * rows + worldD * 0.5f),
//                $"container scale = (1,1,1) ✓\n" +
//                $"scaleXRatio={scaleXR:F3} × mul={debugScaleXMultiplier:F2}\n" +
//                $"Z={containerPositionZ:F2} | rowH={localRowH:F3} gap={rowGap:F3}\n" +
//                $"idle={idleTimeBeforeRotate:F1}s → {autoRotateSpeed:F1}°/s");
//        }

//        private void DrawPolygon(Vector3 center, float radius, int sides, float step)
//        {
//            for (int i = 0; i < sides; i++)
//            {
//                float a0 = ((i - 0.5f) * step) * Mathf.Deg2Rad;
//                float a1 = ((i + 0.5f) * step) * Mathf.Deg2Rad;
//                Vector3 p0 = center + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius;
//                Vector3 p1 = center + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius;
//                Gizmos.DrawLine(p0, p1);
//            }
//        }
//#endif
    }
}