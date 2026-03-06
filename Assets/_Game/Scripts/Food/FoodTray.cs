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
    /// ── Chiến lược spawn ──────────────────────────────────────────────────────
    /// Food KHÔNG làm con của anchor hay cellContainer.
    /// Thay vào đó, food được đặt trong neutralContainer (scale = 1,1,1)
    /// và dùng <see cref="SlotFollower"/> để bám theo world position của anchor
    /// mỗi frame qua LateUpdate.
    ///
    /// Lợi ích:
    ///   • Scale của FoodTray / CellContainer không bao giờ ảnh hưởng food.
    ///   • Khi food cần bay đi order → SlotFollower.Unfollow() → DoTween tự do.
    ///   • Promote layer: chỉ đổi targetSlot, không cần reparent.
    ///
    /// Yêu cầu:
    ///   • neutralContainer phải có localScale (1,1,1) và KHÔNG bị scale runtime.
    ///   • FoodItem prefab phải có component <see cref="SlotFollower"/>.
    /// </summary>
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

        // Map: FoodItem → anchor đang giữ nó (để biết anchor nào đang dùng)
        private readonly Dictionary<FoodItem, Transform> _itemAnchorMap = new();

        public int TrayID { get; private set; }
        public FoodItem TopItem => _stacks[0].Count > 0 ? _stacks[0][0] : null;
        public bool IsEmpty => TopItem == null;
        public int TotalAnchorCapacity =>
            layer0Anchors.Count + layer1Anchors.Count + layer2Anchors.Count;

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Spawn food vào các anchor. Gọi SAU KHI cell animation (scale) xong.
        /// </summary>
        /// <param name="foods">Danh sách FoodItemData cần spawn.</param>
        /// <param name="trayID">ID của tray này.</param>
        /// <param name="neutralContainer">
        ///     Container chứa food. Phải có localScale (1,1,1).
        ///     Thường là một GameObject anh em với CellContainer.
        /// </param>
        /// <param name="globalDelay">Delay trước khi chạy pop-in animation.</param>
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

                // ── 1. Lấy food từ pool ─────────────────────────────────────
                GameObject go = PoolManager.Instance.GetFood(data.foodID, anchor.position);
                if (go == null) continue;

                // ── 2. Đặt vào neutralContainer — KHÔNG làm con anchor/tray ─
                go.transform.SetParent(neutralContainer, worldPositionStays: false);

                // ── 3. Áp world scale từ data (neutralContainer scale=1 nên
                //        localScale = world scale)
                //go.transform.localScale = data.normalScale;

                // ── 4. Vị trí ban đầu = world position của anchor ───────────
                go.transform.position = anchor.position;

                // ── 5. Gắn SlotFollower để bám anchor mỗi frame ─────────────
                SlotFollower follower = go.GetComponent<SlotFollower>();
                if (follower == null)
                    follower = go.AddComponent<SlotFollower>();
                follower.Follow(anchor);

                // ── 6. Khởi tạo FoodItem ─────────────────────────────────────
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

                // ── 7. Pop-in animation chỉ layer 0 ──────────────────────────
                if (layerIdx == 0)
                {
                    Vector3 targetScale = data.normalScale;
                    go.transform.localScale = Vector3.zero;
                    go.transform
                        .DOScale(targetScale, 0.3f)
                        .SetDelay(globalDelay + i * 0.04f)
                        .SetEase(Ease.OutBack)
                        .SetUpdate(false);
                }
            }
        }

        /// <summary>Player tap vào item. Trả về item nếu hợp lệ, null nếu bị khoá.</summary>
        public FoodItem TryPopItem(FoodItem item)
        {
            if (!_stacks[0].Contains(item))
            {
                item.PlayLockedBounce();
                return null;
            }

            // Ngừng bám anchor → DoTween / Animation bên ngoài tự do điều khiển
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

            // Tạm dừng bám để DoTween bay mượt
            follower?.Unfollow();

            promoted.transform
                .DOMove(targetAnchor.position, 0.3f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(false)
                .OnComplete(() =>
                {
                    // Sau khi bay tới nơi, bám anchor mới
                    follower?.Follow(targetAnchor);
                    promoted.transform.localScale = promoted.Data.prefab.transform.localScale;

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

        /// <summary>
        /// Trả về anchor chưa có food nào bám theo (dựa vào _itemAnchorMap).
        /// </summary>
        private Transform GetFreeAnchor(List<Transform> anchors)
        {
            foreach (var a in anchors)
            {
                bool occupied = false;
                foreach (var kv in _itemAnchorMap)
                    if (kv.Value == a) { occupied = true; break; }

                if (!occupied) return a;
            }
            // Fallback: slot đầu tiên
            return anchors.Count > 0 ? anchors[0] : null;
        }
    }
}