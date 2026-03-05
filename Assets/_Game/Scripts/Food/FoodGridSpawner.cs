using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Data;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Xếp các cellPrefab (tam giác rỗng không đáy) thành hình đa giác
    /// bằng cách chỉ thay đổi VỊ TRÍ và ROTATION Y.
    ///
    /// KHÔNG thay đổi scale, KHÔNG thay đổi rotation X/Z của prefab.
    ///
    /// Nguyên lý:
    ///   - Prefab có pivot ở chóp nhọn (điểm gần tâm).
    ///   - Đặt pivot tại tâm vòng tròn, xoay Y góc i × (360°/C).
    ///   - Sau đó dịch chuyển ra xa tâm theo trục Z local một khoảng R
    ///     để khoảng cách từ tâm đến mép ngoài vừa khít MainTrayArea.
    ///   - C prefab sẽ tự khép kín thành hình đa giác C cạnh.
    ///
    /// Lưu ý: Vì không scale, các cạnh bên của prefab có thể không
    ///         khớp hoàn toàn nếu góc prefab ≠ 360°/C. Để khớp hoàn toàn
    ///         cần model prefab có góc = 360°/C hoặc dùng nhiều prefab
    ///         khác nhau. Đây là giải pháp "đặt đúng chỗ, xoay đúng góc"
    ///         mà không làm biến dị hình dạng.
    ///
    /// Setup:
    ///   1. mainTrayArea  → RectTransform UI làm mốc tính tâm + kích thước
    ///   2. cellPrefab    → GameObject 3D, pivot ở chóp nhọn
    ///   3. prefabDepth   → chiều sâu từ pivot đến mép ngoài (trục Z local)
    ///   4. innerRadius   → khoảng từ tâm ra pivot (0 = pivot chạm tâm)
    /// </summary>
    public class FoodGridSpawner : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("─── References ─────────────────────")]
        [Tooltip("RectTransform MainTrayArea – mốc tính tâm và kích thước vùng spawn.")]
        [SerializeField] private RectTransform mainTrayArea;

        [Tooltip("Prefab 3D tam giác rỗng. Pivot phải ở CHÓP NHỌN (điểm gần tâm).")]
        [SerializeField] private GameObject cellPrefab;

        [Tooltip("(Tùy chọn) Container 3D gom toàn bộ cell. Để trống sẽ tự tạo.")]
        [SerializeField] private Transform cellContainer;

        [Header("─── Kích thước Prefab ─────────────")]
        [Tooltip("Chiều sâu từ pivot (chóp nhọn) đến mép ngoài xa nhất (đơn vị world).")]
        [SerializeField] private float prefabDepth = 1f;

        [Tooltip("Bán kính trong: khoảng từ tâm polygon ra đến pivot của prefab.\n" +
                 "= 0 nếu pivot chóp nhọn chạm thẳng tâm.\n" +
                 "> 0 nếu muốn có lõi trống ở giữa (như hình tham khảo).")]
        [SerializeField] private float innerRadius = 0f;

        [Header("─── Auto Fit ────────────────────────")]
        [Tooltip("Tự động tính khoảng cách ra để polygon vừa khít MainTrayArea.\n" +
                 "Nếu tắt, dùng innerRadius + prefabDepth cố định.")]
        [SerializeField] private bool autoFit = true;

        [Tooltip("Tỉ lệ kích thước so với MainTrayArea (0.9 = 90% để có margin).")]
        [Range(0.5f, 1f)]
        [SerializeField] private float fitRatio = 0.9f;

        [Header("─── Tầng (Rows) ─────────────────────")]
        [Tooltip("Khoảng cách giữa các tầng theo trục Y (world units).")]
        [SerializeField] private float rowHeight = 1f;

        [Tooltip("Tầng đầu tiên (row=0) bắt đầu ở Y bao nhiêu (local của container).")]
        [SerializeField] private float baseY = 0f;

        [Header("─── Spawn Animation ─────────────────")]
        [SerializeField] private float spawnDuration = 0.35f;
        [SerializeField] private Ease spawnEase = Ease.OutBack;
        [SerializeField] private float staggerDelay = 0.04f;

        // ─── Runtime ──────────────────────────────────────────────────────────

        // _grid[rowIndex][colIndex]
        private readonly List<List<Transform>> _grid = new();

        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public float OuterRadius { get; private set; }

        // ─────────────────────────────────────────────────────────────────────

        public void SpawnGrid(LevelConfig config)
        {
            if (!Validate(config)) return;
            ClearGrid();

            Columns = Mathf.Max(3, config.trayColumns);
            Rows = Mathf.Max(1, config.trayRows);

            // Tạo container nếu chưa có
            if (cellContainer == null)
            {
                var go = new GameObject("CellContainer");
                go.transform.SetParent(transform, false);
                cellContainer = go.transform;
            }

            StartCoroutine(SpawnAfterLayout());
        }

        public void ClearGrid()
        {
            StopAllCoroutines();
            foreach (var row in _grid)
                foreach (var cell in row)
                    if (cell != null) Destroy(cell.gameObject);
            _grid.Clear();
        }

        // ─── Core ─────────────────────────────────────────────────────────────

        private IEnumerator SpawnAfterLayout()
        {
            yield return null; // chờ Canvas layout xong

            // ── Tính tâm world từ MainTrayArea ──────────────────────────────
            Vector3[] corners = new Vector3[4];
            mainTrayArea.GetWorldCorners(corners);
            // corners: [0]=BL [1]=TL [2]=TR [3]=BR
            Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;

            // Đặt container vào đúng tâm MainTrayArea, giữ Z của container
            worldCenter.z = cellContainer.position.z;
            cellContainer.position = worldCenter;
            cellContainer.rotation = Quaternion.identity;

            // ── Tính R (khoảng đẩy ra từ tâm đến pivot của prefab) ──────────
            float R = innerRadius;

            if (autoFit)
            {
                float areaW = Vector3.Distance(corners[0], corners[3]);
                float areaH = Vector3.Distance(corners[0], corners[1]);
                float maxOuterRadius = Mathf.Min(areaW, areaH) * 0.5f * fitRatio;

                // outerRadius = R + prefabDepth  →  R = maxOuterRadius - prefabDepth
                R = Mathf.Max(0f, maxOuterRadius - prefabDepth);
            }

            OuterRadius = R + prefabDepth;

            // ── Góc xoay giữa 2 prefab liên tiếp ────────────────────────────
            float angleStep = 360f / Columns;

            // ── Spawn ────────────────────────────────────────────────────────
            int spawnOrder = 0;

            for (int row = 0; row < Rows; row++)
            {
                List<Transform> rowList = new();
                float localY = baseY + row * rowHeight;

                for (int col = 0; col < Columns; col++)
                {
                    // Góc xoay của prefab này quanh trục Y (world up)
                    float rotY = col * angleStep;

                    GameObject cellGO = Instantiate(cellPrefab, cellContainer);
                    cellGO.name = $"Cell_R{row}_C{col}";
                    Transform t = cellGO.transform;

                    // ── Reset về gốc của container ──────────────────────────
                    t.localPosition = Vector3.zero;
                    // Giữ nguyên rotation X/Z gốc của prefab, chỉ đổi Y
                    Vector3 originalEuler = cellPrefab.transform.eulerAngles;
                    t.localEulerAngles = new Vector3(
                        originalEuler.x,
                        rotY,               // chỉ thay rotation Y
                        originalEuler.z);

                    // ── Đẩy ra theo trục Z local (hướng nhìn của prefab) ────
                    // Sau khi xoay Y, trục Z local của prefab sẽ hướng ra ngoài
                    t.localPosition = new Vector3(0f, localY, 0f)
                                    + t.forward * R;
                    // Nếu pivot ở chóp và prefab nhìn vào tâm thì dùng -forward:
                    // t.localPosition = new Vector3(0f, localY, 0f) - t.forward * R;

                    // ── Spawn animation ─────────────────────────────────────
                    // Animate từ tâm ra vị trí đúng
                    Vector3 targetPos = t.localPosition;
                    Vector3 startPos = new Vector3(0f, localY, 0f);

                    t.localPosition = startPos;
                    t.localScale = Vector3.zero;

                    float delay = spawnOrder * staggerDelay;
                    Sequence seq = DOTween.Sequence().SetDelay(delay);
                    seq.Append(t.DOLocalMove(targetPos, spawnDuration).SetEase(spawnEase));
                    seq.Join(t.DOScale(Vector3.one, spawnDuration).SetEase(spawnEase));

                    rowList.Add(t);
                    spawnOrder++;
                }

                _grid.Add(rowList);
            }

            Debug.Log($"[FoodGridSpawner] Spawned {Columns}-gon × {Rows} rows | " +
                      $"R(innerRadius)={R:F3} | outerR={OuterRadius:F3} | angleStep={angleStep:F1}°");
        }

        // ─── Public Getters ───────────────────────────────────────────────────

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
            if (config == null)
            {
                Debug.LogError("[FoodGridSpawner] LevelConfig null!");
                return false;
            }
            if (cellPrefab == null)
            {
                Debug.LogError("[FoodGridSpawner] cellPrefab chưa gán!");
                return false;
            }
            if (mainTrayArea == null)
            {
                Debug.LogError("[FoodGridSpawner] mainTrayArea chưa gán!");
                return false;
            }
            return true;
        }

        // ─── Gizmos ───────────────────────────────────────────────────────────
#if UNITY_EDITOR
        [Header("─── Editor Preview ──────────────────")]
        [SerializeField] private int previewColumns = 4;
        [SerializeField] private int previewRows = 2;

        private void OnDrawGizmosSelected()
        {
            if (mainTrayArea == null) return;

            Vector3[] corners = new Vector3[4];
            mainTrayArea.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;
            center.z = transform.position.z;

            float areaW = Vector3.Distance(corners[0], corners[3]);
            float areaH = Vector3.Distance(corners[0], corners[1]);

            int cols = Mathf.Max(3, previewColumns);
            int rows = Mathf.Max(1, previewRows);

            float maxOuter = Mathf.Min(areaW, areaH) * 0.5f * fitRatio;
            float rIn = autoFit ? Mathf.Max(0f, maxOuter - prefabDepth) : innerRadius;
            float rOut = rIn + prefabDepth;

            float angleStep = 360f / cols;

            for (int row = 0; row < rows; row++)
            {
                float yPos = baseY + row * rowHeight;
                Vector3 rowCtr = center + Vector3.up * yPos;

                // Vẽ vòng ngoài polygon
                Gizmos.color = Color.cyan;
                DrawPolygon(rowCtr, rOut, cols, angleStep);

                // Vẽ lõi trong
                if (rIn > 0f)
                {
                    Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
                    DrawCircle(rowCtr, rIn, cols * 6);
                }

                // Vẽ vách ngăn (ranh giới giữa các prefab)
                Gizmos.color = Color.yellow;
                for (int col = 0; col < cols; col++)
                {
                    float rad = (col * angleStep - angleStep * 0.5f) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                    Gizmos.DrawLine(rowCtr + dir * rIn, rowCtr + dir * rOut);
                }

                // Vẽ tâm mỗi prefab (hướng nhìn ra ngoài)
                Gizmos.color = Color.green;
                for (int col = 0; col < cols; col++)
                {
                    float rad = col * angleStep * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                    Vector3 pivotPos = rowCtr + dir * rIn;
                    Gizmos.DrawSphere(pivotPos, rOut * 0.04f);
                    Gizmos.DrawLine(pivotPos, pivotPos + dir * prefabDepth);
                }
            }

            // Khung MainTrayArea
            Gizmos.color = Color.green;
            Gizmos.DrawLine(corners[0], corners[1]);
            Gizmos.DrawLine(corners[1], corners[2]);
            Gizmos.DrawLine(corners[2], corners[3]);
            Gizmos.DrawLine(corners[3], corners[0]);
        }

        private void DrawPolygon(Vector3 center, float radius, int sides, float angleStep)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = ((i - 0.5f) * angleStep) * Mathf.Deg2Rad;
                float a1 = ((i + 0.5f) * angleStep) * Mathf.Deg2Rad;
                Vector3 p0 = center + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius;
                Vector3 p1 = center + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius;
                Gizmos.DrawLine(p0, p1);
            }
        }

        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            float step = 360f / segments * Mathf.Deg2Rad;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * step, a1 = (i + 1) * step;
                Vector3 p0 = center + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius;
                Vector3 p1 = center + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius;
                Gizmos.DrawLine(p0, p1);
            }
        }
#endif
    }
}