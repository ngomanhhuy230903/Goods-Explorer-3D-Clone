using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;
using FoodMatch.Food;

namespace FoodMatch.Tray
{
    public class FoodTray : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Layer Anchors ────────────────────")]
        [SerializeField] private List<Transform> layer0Anchors = new();
        [SerializeField] private List<Transform> layer1Anchors = new();
        [SerializeField] private List<Transform> layer2Anchors = new();

        // ─── Runtime ──────────────────────────────────────────────────────────
        private readonly List<FoodItem>[] _stacks =
        {
            new List<FoodItem>(),
            new List<FoodItem>(),
            new List<FoodItem>()
        };

        private readonly Dictionary<FoodItem, Transform> _itemAnchorMap = new();

        public int TrayID { get; private set; }
        public FoodItem TopItem => _stacks[0].Count > 0 ? _stacks[0][0] : null;
        public bool IsEmpty => TopItem == null;
        public int TotalAnchorCapacity =>
            layer0Anchors.Count + layer1Anchors.Count + layer2Anchors.Count;

        // ─── Public API ───────────────────────────────────────────────────────

        public void SpawnFoods(List<FoodItemData> foods, int trayID,
                               Transform neutralContainer, float globalDelay = 0f)
        {
            TrayID = trayID;
            ClearTray();

            var allAnchors = CollectAllAnchors();
            int count = Mathf.Min(foods.Count, allAnchors.Count);

            for (int i = 0; i < count; i++)
            {
                FoodItemData data = foods[i];
                if (data?.prefab == null) continue;

                Transform anchor = allAnchors[i];
                int layerIdx = GetLayerIndex(anchor);

                // ── 1. Lấy scale gốc từ prefab TRƯỚC KHI làm bất cứ điều gì ──
                // Đây là nguồn sự thật duy nhất, không phụ thuộc parent
                Vector3 prefabScale = data.prefab.transform.localScale;

                // ── 2. Lấy food từ pool (position tạm, sẽ set lại sau) ────────
                GameObject go = PoolManager.Instance.GetFood(data.foodID, anchor.position);
                if (go == null) continue;

                // ── 3. SetParent neutralContainer, GIỮ world transform ─────────
                // worldPositionStays: true → Unity giữ world position/rotation
                // nhưng localScale sẽ bị tính lại → ta override ngay bên dưới
                go.transform.SetParent(neutralContainer, worldPositionStays: true);

                // ── 4. Override localScale về đúng prefab scale ────────────────
                // neutralContainer luôn scale (1,1,1) nên localScale = world scale
                go.transform.localScale = prefabScale;

                // ── 5. Ép đúng world position của anchor ──────────────────────
                go.transform.position = anchor.position;

                // ── 6. Gắn SlotFollower bám anchor mỗi frame ─────────────────
                SlotFollower follower = go.GetComponent<SlotFollower>();
                if (follower == null)
                    follower = go.AddComponent<SlotFollower>();
                follower.Follow(anchor);

                // ── 7. Khởi tạo FoodItem (Initialize sẽ gọi SetLayerVisual) ───
                FoodItem item = go.GetComponent<FoodItem>();
                if (item == null)
                {
                    Debug.LogError($"[FoodTray] '{data.foodName}' thiếu FoodItem component!");
                    continue;
                }

                item.Initialize(data, layerIdx);
                item.OwnerTray = this;
                item.SetAnchorRef(anchor);

                _stacks[layerIdx].Add(item);
                _itemAnchorMap[item] = anchor;

                // ── 8. Pop-in animation chỉ layer 0 ──────────────────────────
                // targetScale lấy từ prefab (đã biết chắc đúng)
                // set về zero SAU khi đã lưu targetScale
                if (layerIdx == 0)
                {
                    go.transform.localScale = Vector3.zero;
                    go.transform
                        .DOScale(prefabScale, 0.3f)
                        .SetDelay(globalDelay + i * 0.04f)
                        .SetEase(Ease.OutBack)
                        .SetUpdate(false);
                }
            }
        }

        public FoodItem TryPopItem(FoodItem item)
        {
            if (!_stacks[0].Contains(item))
            {
                item.PlayLockedBounce();
                return null;
            }

            item.GetComponent<SlotFollower>()?.Unfollow();
            _itemAnchorMap.Remove(item);

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
                {
                    if (item == null) continue;
                    item.GetComponent<SlotFollower>()?.Unfollow();
                    PoolManager.Instance.ReturnFood(item.FoodID, item.gameObject);
                }
                _stacks[l].Clear();
            }
            _itemAnchorMap.Clear();
        }

        // ─── Promote ──────────────────────────────────────────────────────────

        private void PromoteLayer(int fromLayer, int toLayer, List<Transform> toAnchors)
        {
            if (_stacks[fromLayer].Count == 0) return;

            FoodItem promoted = _stacks[fromLayer][0];
            _stacks[fromLayer].RemoveAt(0);
            _itemAnchorMap.Remove(promoted);

            Transform targetAnchor = GetFreeAnchor(toAnchors);
            if (targetAnchor == null) return;

            SlotFollower follower = promoted.GetComponent<SlotFollower>();
            follower?.Unfollow();

            // Scale đích lấy từ prefab gốc
            Vector3 prefabScale = promoted.Data.prefab.transform.localScale;

            promoted.transform
                .DOMove(targetAnchor.position, 0.3f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(false)
                .OnComplete(() =>
                {
                    promoted.transform.localScale = prefabScale;
                    follower?.Follow(targetAnchor);
                    _itemAnchorMap[promoted] = targetAnchor;
                });

            promoted.SetLayerVisual(toLayer);
            _stacks[toLayer].Add(promoted);

            if (toLayer == 0)
                TrayAnimator.PlayLayerShiftIn(promoted);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private List<Transform> CollectAllAnchors()
        {
            var all = new List<Transform>();
            all.AddRange(layer0Anchors);
            all.AddRange(layer1Anchors);
            all.AddRange(layer2Anchors);
            return all;
        }

        private int GetLayerIndex(Transform anchor)
        {
            if (layer0Anchors.Contains(anchor)) return 0;
            if (layer1Anchors.Contains(anchor)) return 1;
            return 2;
        }

        private Transform GetFreeAnchor(List<Transform> anchors)
        {
            foreach (var a in anchors)
            {
                bool occupied = false;
                foreach (var kv in _itemAnchorMap)
                    if (kv.Value == a) { occupied = true; break; }
                if (!occupied) return a;
            }
            return anchors.Count > 0 ? anchors[0] : null;
        }
    }
}