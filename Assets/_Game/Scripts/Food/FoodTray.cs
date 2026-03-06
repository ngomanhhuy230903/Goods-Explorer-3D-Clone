using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;
using FoodMatch.Food;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Gắn vào FoodTray prefab (cellPrefab của FoodGridSpawner).
    ///
    /// Food là CON của anchor (empty object trong FoodTray).
    /// Vị trí = localPosition(0,0,0) trong anchor → đúng vị trí anchor.
    /// Scale và rotation được override về giá trị prefab gốc sau SetParent
    /// → không bị ảnh hưởng bởi scale/rotation của FoodTray hay CellContainer.
    ///
    /// Callback OnSpawnComplete của FoodGridSpawner được invoke SAU KHI
    /// animation scale cell xong → anchor.position đã đúng khi SpawnFoods() chạy.
    /// </summary>
    public class FoodTray : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Layer Anchors ────────────────────")]
        [SerializeField] private List<Transform> layer0Anchors = new();
        [SerializeField] private List<Transform> layer1Anchors = new();
        [SerializeField] private List<Transform> layer2Anchors = new();

        // ─── Runtime ──────────────────────────────────────────────────────────
        private readonly List<FoodItem>[] _stacks = {
            new List<FoodItem>(),
            new List<FoodItem>(),
            new List<FoodItem>()
        };

        public int TrayID { get; private set; }
        public FoodItem TopItem => _stacks[0].Count > 0 ? _stacks[0][0] : null;
        public bool IsEmpty => TopItem == null;
        public int TotalAnchorCapacity =>
            layer0Anchors.Count + layer1Anchors.Count + layer2Anchors.Count;

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Spawn food vào các anchor. Gọi sau khi cell animation xong.
        /// </summary>
        public void SpawnFoods(List<FoodItemData> foods, int trayID,
                               Transform cellContainer, float globalDelay = 0f)
        {
            TrayID = trayID;
            ClearTray();

            var allAnchors = new List<Transform>();
            allAnchors.AddRange(layer0Anchors);
            allAnchors.AddRange(layer1Anchors);
            allAnchors.AddRange(layer2Anchors);

            int count = Mathf.Min(foods.Count, allAnchors.Count);

            for (int i = 0; i < count; i++)
            {
                FoodItemData data = foods[i];
                if (data?.prefab == null) continue;

                Transform anchor = allAnchors[i];
                int layerIdx = GetLayerIndex(anchor);

                GameObject go = PoolManager.Instance.GetFood(data.foodID, Vector3.zero);
                if (go == null) continue;

                // ── Bước 1: Tạm thời không có parent (root) ───────────────────
                go.transform.SetParent(null, false);

                // ── Bước 2: Gán đúng world position + rotation của anchor ──────
                // anchor.position lúc này đúng vì cell đã scale xong
                go.transform.position = anchor.position;
                go.transform.rotation = anchor.rotation;

                // ── Bước 3: Gán world scale = normalScale của prefab ──────────
                // Khi ở root (no parent), localScale = world scale
                go.transform.localScale = data.normalScale;

                // ── Bước 4: SetParent CellContainer, GIỮ NGUYÊN world transform
                // Unity tự tính localPosition/localScale/localRotation tương đương
                go.transform.SetParent(cellContainer, worldPositionStays: true);

                FoodItem item = go.GetComponent<FoodItem>();
                if (item == null)
                {
                    Debug.LogError($"[FoodTray] '{data.foodName}' thiếu FoodItem component!");
                    continue;
                }

                item.Initialize(data, layerIdx);
                item.OwnerTray = this;
                _stacks[layerIdx].Add(item);

                // Pop-in animation chỉ layer 0
                if (layerIdx == 0)
                {
                    Vector3 targetLocal = go.transform.localScale;
                    go.transform.localScale = Vector3.zero;
                    go.transform
                        .DOScale(targetLocal, 0.3f)
                        .SetDelay(globalDelay + i * 0.04f)
                        .SetEase(Ease.OutBack)
                        .SetUpdate(false);
                }
            }
        }

        /// <summary>Player tap vào item.</summary>
        public FoodItem TryPopItem(FoodItem item)
        {
            if (!_stacks[0].Contains(item))
            {
                item.PlayLockedBounce();
                return null;
            }

            _stacks[0].Remove(item);
            item.OwnerTray = null;

            PromoteLayer(fromLayer: 1, toLayer: 0, toAnchors: layer0Anchors);
            PromoteLayer(fromLayer: 2, toLayer: 1, toAnchors: layer1Anchors);

            return item;
        }

        public void ClearTray()
        {
            for (int l = 0; l < 3; l++)
            {
                foreach (var item in _stacks[l])
                    if (item != null)
                        PoolManager.Instance.ReturnFood(item.FoodID, item.gameObject);
                _stacks[l].Clear();
            }
        }

        // ─── Promote ──────────────────────────────────────────────────────────

        private void PromoteLayer(int fromLayer, int toLayer, List<Transform> toAnchors)
        {
            if (_stacks[fromLayer].Count == 0) return;

            FoodItem promoted = _stacks[fromLayer][0];
            _stacks[fromLayer].RemoveAt(0);

            Transform targetAnchor = GetFreeAnchor(toAnchors);
            if (targetAnchor != null)
            {
                promoted.transform
                    .DOMove(targetAnchor.position, 0.3f)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(false)
                    .OnComplete(() =>
                    {
                        promoted.transform.SetParent(targetAnchor, worldPositionStays: false);
                        promoted.transform.localPosition = Vector3.zero;
                        promoted.transform.localRotation = Quaternion.identity;
                        promoted.transform.localScale = promoted.Data.normalScale;
                    });
            }

            promoted.SetLayerVisual(toLayer);
            _stacks[toLayer].Add(promoted);

            if (toLayer == 0)
                TrayAnimator.PlayLayerShiftIn(promoted);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private int GetLayerIndex(Transform anchor)
        {
            if (layer0Anchors.Contains(anchor)) return 0;
            if (layer1Anchors.Contains(anchor)) return 1;
            return 2;
        }

        private Transform GetFreeAnchor(List<Transform> anchors)
        {
            foreach (var a in anchors)
                if (a.childCount == 0) return a;
            return anchors.Count > 0 ? anchors[0] : null;
        }
    }
}