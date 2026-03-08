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

                Vector3 prefabScale = data.prefab.transform.localScale;

                GameObject go = PoolManager.Instance.GetFood(data.foodID, anchor.position);
                if (go == null) continue;

                go.transform.SetParent(neutralContainer, worldPositionStays: true);
                go.transform.localScale = prefabScale;
                go.transform.position = anchor.position;

                SlotFollower follower = go.GetComponent<SlotFollower>();
                if (follower == null)
                    follower = go.AddComponent<SlotFollower>();
                follower.Follow(anchor);

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

        /// <summary>
        /// Thử lấy item ra khỏi layer 0.
        /// Sau khi remove, kiểm tra layer 0 có trống HOÀN TOÀN không:
        ///   - Nếu trống → promote TOÀN BỘ layer 1 lên layer 0 cùng lúc,
        ///     sau đó kiểm tra tiếp layer 1 mới có trống không → promote layer 2.
        ///   - Nếu chưa trống → không làm gì thêm.
        /// </summary>
        public FoodItem TryPopItem(FoodItem item)
        {
            if (!_stacks[0].Contains(item))
            {
                item.PlayLockedBounce();
                return null;
            }

            // ── Tách item ra khỏi tray ────────────────────────────────────
            item.GetComponent<SlotFollower>()?.Unfollow();
            _itemAnchorMap.Remove(item);
            _stacks[0].Remove(item);
            item.OwnerTray = null;

            // ── Kiểm tra và promote theo tầng ─────────────────────────────
            TryPromoteNextLayer();

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

        // ─── Promote Logic ────────────────────────────────────────────────────

        /// <summary>
        /// Kiểm tra từng cặp tầng theo thứ tự:
        ///   Layer 0 trống → promote TOÀN BỘ layer 1 lên, rồi kiểm tra tiếp.
        ///   Layer 1 trống → promote TOÀN BỘ layer 2 lên.
        /// Mỗi lần promote là promote cùng lúc tất cả item trong tầng đó.
        /// </summary>
        private void TryPromoteNextLayer()
        {
            // Bước 1: Layer 0 vừa trống → promote toàn bộ layer 1 lên layer 0
            if (_stacks[0].Count == 0 && _stacks[1].Count > 0)
            {
                PromoteEntireLayer(fromLayer: 1, toLayer: 0, toAnchors: layer0Anchors);

                // Bước 2: Sau khi layer 1 đã lên hết, layer 1 giờ trống
                // → promote toàn bộ layer 2 lên layer 1
                if (_stacks[1].Count == 0 && _stacks[2].Count > 0)
                {
                    PromoteEntireLayer(fromLayer: 2, toLayer: 1, toAnchors: layer1Anchors);
                }
            }
            // Trường hợp layer 0 vẫn còn item nhưng layer 1 đã trống từ trước
            // (không xảy ra trong flow bình thường nhưng guard thêm cho chắc)
            else if (_stacks[1].Count == 0 && _stacks[2].Count > 0)
            {
                PromoteEntireLayer(fromLayer: 2, toLayer: 1, toAnchors: layer1Anchors);
            }
        }

        /// <summary>
        /// Promote TOÀN BỘ item trong fromLayer lên toLayer cùng một lúc.
        /// Mỗi item animate DOMove đến anchor tương ứng trong toAnchors.
        /// Anchor được gán theo chỉ số: item[0] → anchor[0], item[1] → anchor[1], …
        /// </summary>
        private void PromoteEntireLayer(int fromLayer, int toLayer, List<Transform> toAnchors)
        {
            // Snapshot danh sách để tránh modify-during-iteration
            var itemsToPromote = new List<FoodItem>(_stacks[fromLayer]);

            // Xóa sạch fromLayer ngay lập tức
            _stacks[fromLayer].Clear();

            for (int i = 0; i < itemsToPromote.Count; i++)
            {
                FoodItem promoted = itemsToPromote[i];
                if (promoted == null) continue;

                // Gỡ anchor cũ
                _itemAnchorMap.Remove(promoted);

                // Tìm anchor đích trong toLayer
                Transform targetAnchor = i < toAnchors.Count
                    ? toAnchors[i]
                    : GetFreeAnchor(toAnchors); // fallback nếu số anchor ít hơn item

                if (targetAnchor == null)
                {
                    Debug.LogWarning($"[FoodTray] Không tìm được anchor đích cho item {promoted.name}!");
                    continue;
                }

                // Gỡ SlotFollower trước khi animate
                SlotFollower follower = promoted.GetComponent<SlotFollower>();
                follower?.Unfollow();

                // Lưu tham chiếu cục bộ cho closure
                FoodItem capturedItem = promoted;
                Transform capturedAnchor = targetAnchor;
                SlotFollower capturedFollower = follower;
                Vector3 prefabScale = promoted.Data.prefab.transform.localScale;

                // Animate đến vị trí layer mới
                promoted.transform
                    .DOMove(targetAnchor.position, 0.3f)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(false)
                    .OnComplete(() =>
                    {
                        capturedItem.transform.localScale = prefabScale;
                        capturedFollower?.Follow(capturedAnchor);
                        _itemAnchorMap[capturedItem] = capturedAnchor;
                    });

                // Cập nhật visual và thêm vào stack mới ngay (không chờ animation)
                promoted.SetLayerVisual(toLayer);
                _stacks[toLayer].Add(promoted);

                // Hiệu ứng nổi bật khi lên layer 0
                if (toLayer == 0)
                    TrayAnimator.PlayLayerShiftIn(promoted);
            }
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