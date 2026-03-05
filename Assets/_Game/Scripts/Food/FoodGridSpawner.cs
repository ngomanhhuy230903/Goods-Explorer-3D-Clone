using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Data;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Sinh các hình đa giác (polygon) bên trong MainTrayArea.
    /// - Số cạnh polygon  = trayColumns  (4 cột → tứ giác, 6 cột → lục giác, v.v.)
    /// - Số polygon chồng = trayRows     (2 hàng → 2 polygon xếp đè lên nhau)
    /// - Mỗi polygon là 1 vòng slot-prefab (tam giác rỗng) xếp đều quanh tâm.
    ///
    /// Gắn script này vào GameObject "FoodGridSpawner" trong Scene Game.
    /// Gọi SpawnGrid(config) từ LevelManager.
    /// </summary>
    public class FoodGridSpawner : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("RectTransform của MainTrayArea – nơi polygon được spawn vào.")]
        [SerializeField] private RectTransform mainTrayArea;

        [Tooltip("Prefab 1 slot (hình tam giác rỗng đáy).")]
        [SerializeField] private RectTransform slotPrefab;

        [Header("Layout")]
        [Tooltip("% khoảng trống giữa các slot trên vòng polygon (0 = sát nhau).")]
        [Range(0f, 0.4f)]
        [SerializeField] private float slotGapRatio = 0.08f;

        [Tooltip("Khoảng dịch Y giữa các hàng polygon chồng lên nhau (px). " +
                 "Giá trị dương → hàng sau cao hơn; âm → thấp hơn.")]
        [SerializeField] private float rowOffsetY = 20f;

        [Tooltip("Tỉ lệ thu nhỏ polygon theo từng hàng (hàng sau nhỏ hơn hàng trước).")]
        [Range(0.7f, 1f)]
        [SerializeField] private float rowScaleStep = 0.92f;

        [Header("─── Spawn Animation ─────────────────")]
        [SerializeField] private float spawnDuration = 0.35f;
        [SerializeField] private Ease spawnEase = Ease.OutBack;

        // ─── Runtime ──────────────────────────────────────────────────────────

        /// <summary>Tất cả slot đã spawn, theo thứ tự [rowIndex][slotIndex].</summary>
        private readonly List<List<RectTransform>> _spawnedRows = new();

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sinh polygon dựa theo LevelConfig.
        /// Gọi từ LevelManager.InitFoodGrid().
        /// </summary>
        public void SpawnGrid(LevelConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[FoodGridSpawner] LevelConfig null!");
                return;
            }

            ClearGrid();

            int sides = Mathf.Max(3, config.trayColumns); // số cạnh = số cột
            int rows = Mathf.Max(1, config.trayRows);    // số polygon chồng = số hàng

            // ── Tính bán kính vừa MainTrayArea ─────────────────────────────
            Rect area = mainTrayArea.rect;
            float maxByWidth = area.width * 0.45f;
            float maxByHeight = area.height * 0.45f / (1f + (rows - 1) * 0.15f);
            float baseRadius = Mathf.Min(maxByWidth, maxByHeight);

            // Chiều dài cạnh 1 slot dựa vào khoảng cách giữa 2 đỉnh liên tiếp
            float chordLength = 2f * baseRadius * Mathf.Sin(Mathf.PI / sides);
            float slotSize = chordLength * (1f - slotGapRatio);

            // Tổng chiều cao dải polygon để căn giữa theo Y
            float totalHeight = (rows - 1) * rowOffsetY;
            float startY = totalHeight / 2f; // bắt đầu từ trên, đi xuống

            for (int row = 0; row < rows; row++)
            {
                float rowRadius = baseRadius * Mathf.Pow(rowScaleStep, row);
                float rowSlot = slotSize * Mathf.Pow(rowScaleStep, row);
                float centerY = startY - row * rowOffsetY;

                List<RectTransform> rowSlots = new();

                for (int s = 0; s < sides; s++)
                {
                    // Góc đỉnh thứ s; -PI/2 để đỉnh đầu nằm chính trên
                    float angle = (2f * Mathf.PI * s / sides) - Mathf.PI / 2f;

                    float x = rowRadius * Mathf.Cos(angle);
                    float y = centerY + rowRadius * Mathf.Sin(angle);

                    RectTransform slot = Instantiate(slotPrefab, mainTrayArea);
                    slot.anchoredPosition = new Vector2(x, y);
                    slot.sizeDelta = new Vector2(rowSlot, rowSlot);

                    // Xoay mặt tam giác hướng ra ngoài tâm
                    float angleDeg = angle * Mathf.Rad2Deg;
                    slot.localRotation = Quaternion.Euler(0f, 0f, angleDeg + 90f);

                    // ── Spawn animation ──────────────────────────────────────
                    slot.localScale = Vector3.zero;
                    float delay = row * 0.06f + s * 0.02f;
                    slot.DOScale(Vector3.one, spawnDuration)
                        .SetEase(spawnEase)
                        .SetDelay(delay);

                    rowSlots.Add(slot);
                }

                _spawnedRows.Add(rowSlots);
            }

            Debug.Log($"[FoodGridSpawner] Spawned {sides}-sided polygon × {rows} rows " +
                      $"(radius={baseRadius:F1}px, slotSize={slotSize:F1}px)");
        }

        /// <summary>Xoá toàn bộ polygon đã spawn.</summary>
        public void ClearGrid()
        {
            foreach (var row in _spawnedRows)
                foreach (var slot in row)
                    if (slot != null) Destroy(slot.gameObject);

            _spawnedRows.Clear();
        }

        // ─── Public Getters (dùng khi tích hợp FoodItem sau) ─────────────────

        /// <summary>Lấy anchoredPosition của slot [rowIndex][slotIndex].</summary>
        public Vector2 GetSlotPosition(int rowIndex, int slotIndex)
        {
            if (rowIndex < 0 || rowIndex >= _spawnedRows.Count) return Vector2.zero;
            var row = _spawnedRows[rowIndex];
            if (slotIndex < 0 || slotIndex >= row.Count) return Vector2.zero;
            return row[slotIndex].anchoredPosition;
        }

        /// <summary>Lấy sizeDelta của slot [rowIndex][slotIndex].</summary>
        public Vector2 GetSlotSize(int rowIndex, int slotIndex)
        {
            if (rowIndex < 0 || rowIndex >= _spawnedRows.Count) return Vector2.zero;
            var row = _spawnedRows[rowIndex];
            if (slotIndex < 0 || slotIndex >= row.Count) return Vector2.zero;
            return row[slotIndex].sizeDelta;
        }

        /// <summary>Số hàng polygon đã spawn.</summary>
        public int RowCount => _spawnedRows.Count;

        /// <summary>Số slot trong hàng rowIndex.</summary>
        public int SlotCountInRow(int rowIndex) =>
            (rowIndex >= 0 && rowIndex < _spawnedRows.Count) ? _spawnedRows[rowIndex].Count : 0;

        // ─── Gizmos (Scene View preview khi chưa Play) ───────────────────────
#if UNITY_EDITOR
        [Header("─── Editor Preview (không ảnh hưởng runtime) ──")]
        [SerializeField] private int previewSides = 4;
        [SerializeField] private int previewRows = 2;

        private void OnDrawGizmosSelected()
        {
            if (mainTrayArea == null) return;

            Rect area = mainTrayArea.rect;
            float maxByWidth = area.width * 0.45f;
            float maxByHeight = area.height * 0.45f / (1f + (previewRows - 1) * 0.15f);
            float baseRadius = Mathf.Min(maxByWidth, maxByHeight);
            float totalHeight = (previewRows - 1) * rowOffsetY;
            float startY = totalHeight / 2f;

            Vector3 origin = mainTrayArea.position;

            for (int row = 0; row < previewRows; row++)
            {
                float rowRadius = baseRadius * Mathf.Pow(rowScaleStep, row);
                float centerY = startY - row * rowOffsetY;

                Gizmos.color = row == 0
                    ? Color.cyan
                    : new Color(0.4f, 0.8f, 1f, 0.5f);

                Vector3[] verts = new Vector3[previewSides];
                for (int s = 0; s < previewSides; s++)
                {
                    float angle = (2f * Mathf.PI * s / previewSides) - Mathf.PI / 2f;
                    verts[s] = origin + new Vector3(
                        rowRadius * Mathf.Cos(angle),
                        centerY + rowRadius * Mathf.Sin(angle),
                        0f);
                }

                for (int s = 0; s < previewSides; s++)
                    Gizmos.DrawLine(verts[s], verts[(s + 1) % previewSides]);

                foreach (var v in verts)
                    Gizmos.DrawSphere(v, 4f);
            }
        }
#endif
    }
}