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

        // ─── Runtime ──────────────────────────────────────────────────────────

        /// <summary>
        /// _stacks[0] = layer 0 (spawned, interactive)
        /// _stacks[1] = layer 1 (spawned, greyed-out)
        /// Layer 2 chỉ lưu DATA, chưa spawn GameObject cho đến khi cần promote.
        /// </summary>
        private readonly List<FoodItem>[] _stacks =
        {
            new List<FoodItem>(),   // layer 0
            new List<FoodItem>(),   // layer 1
        };

        /// <summary>
        /// Data-only cho layer 2. Chưa spawn GameObject.
        /// Sẽ được spawn khi layer 1 promote lên layer 0.
        /// </summary>
        private readonly List<FoodItemData> _pendingLayer2 = new();

        private readonly Dictionary<FoodItem, Transform> _itemAnchorMap = new();

        // Cache neutralContainer để dùng khi spawn layer 2 lazily
        private Transform _neutralContainer;

        public int TrayID { get; private set; }
        public FoodItem TopItem => _stacks[0].Count > 0 ? _stacks[0][0] : null;
        public bool IsEmpty => TopItem == null;

        /// <summary>
        /// Capacity vật lý = layer 0 + layer 1 anchors thôi.
        /// Layer 2 không cần anchor riêng — dùng lại anchor của layer 1 khi promoted.
        /// </summary>
        public int TotalAnchorCapacity => layer0Anchors.Count + layer1Anchors.Count;

        /// <summary>
        /// Tổng số food thực sự tray này đang "giữ" (cả spawned lẫn pending).
        /// Dùng để FoodTraySpawner tính đúng capacity khi distribute.
        /// </summary>
        public int TotalFoodCount =>
            _stacks[0].Count + _stacks[1].Count + _pendingLayer2.Count;

        /// <summary>
        /// Max food tray này có thể chứa = layer0 + layer1 + layer2
        /// (layer2 dùng lại số slot của layer1)
        /// </summary>
        public int MaxFoodCapacity =>
            layer0Anchors.Count + layer1Anchors.Count + layer1Anchors.Count;

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// foods[0..layer0Count-1]          → spawn ngay vào layer 0
        /// foods[layer0Count..layer1End-1]  → spawn ngay vào layer 1 (mờ/nhỏ)
        /// foods[layer1End..]               → lưu vào _pendingLayer2, CHƯA spawn
        /// </summary>
        public void SpawnFoods(List<FoodItemData> foods, int trayID,
                               Transform neutralContainer, float globalDelay = 0f)
        {
            TrayID = trayID;
            _neutralContainer = neutralContainer;
            ClearTray();

            int l0Cap = layer0Anchors.Count;
            int l1Cap = layer1Anchors.Count;

            for (int i = 0; i < foods.Count; i++)
            {
                FoodItemData data = foods[i];
                if (data?.prefab == null) continue;

                if (i < l0Cap)
                {
                    // ── Layer 0: spawn + animate scale-in ─────────────────
                    SpawnFoodItem(data, layer0Anchors[i], layerIdx: 0,
                                  neutralContainer, spawnDelay: globalDelay + i * 0.04f);
                }
                else if (i < l0Cap + l1Cap)
                {
                    // ── Layer 1: spawn ngay, greyed-out, không animate ────
                    int anchorIdx = i - l0Cap;
                    SpawnFoodItem(data, layer1Anchors[anchorIdx], layerIdx: 1,
                                  neutralContainer, spawnDelay: 0f);
                }
                else
                {
                    // ── Layer 2: chỉ lưu data, chưa spawn ────────────────
                    _pendingLayer2.Add(data);
                }
            }
        }

        /// <summary>
        /// Thử lấy item ra khỏi layer 0.
        /// Sau khi remove, nếu layer 0 trống HOÀN TOÀN:
        ///   → promote toàn bộ layer 1 lên layer 0
        ///   → spawn layer 2 pending vào vị trí layer 1
        /// </summary>
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

            TryPromoteNextLayer();

            return item;
        }

        public void ClearTray()
        {
            for (int l = 0; l < 2; l++)
            {
                foreach (var item in _stacks[l])
                {
                    if (item == null) continue;
                    item.GetComponent<SlotFollower>()?.Unfollow();
                    PoolManager.Instance.ReturnFood(item.FoodID, item.gameObject);
                }
                _stacks[l].Clear();
            }
            _pendingLayer2.Clear();
            _itemAnchorMap.Clear();
        }

        // ─── Promote Logic ────────────────────────────────────────────────────

        /// <summary>
        /// Khi layer 0 trống hoàn toàn:
        ///   1. Promote toàn bộ layer 1 lên layer 0 (DOMove animation)
        ///   2. Spawn các food pending của layer 2 vào vị trí layer 1
        /// </summary>
        private void TryPromoteNextLayer()
        {
            if (_stacks[0].Count != 0) return;
            if (_stacks[1].Count == 0 && _pendingLayer2.Count == 0) return;

            // Bước 1: Promote layer 1 → layer 0
            if (_stacks[1].Count > 0)
            {
                PromoteLayer1ToLayer0();
            }

            // Bước 2: Spawn pending layer 2 → vào vị trí layer 1
            // (thực hiện sau promote để anchor layer1 đã "trống")
            if (_pendingLayer2.Count > 0)
            {
                SpawnPendingLayer2();
            }
        }

        /// <summary>
        /// Promote TOÀN BỘ item trong layer 1 lên layer 0.
        /// Mỗi item animate DOMove đến anchor layer0 tương ứng.
        /// </summary>
        private void PromoteLayer1ToLayer0()
        {
            var itemsToPromote = new List<FoodItem>(_stacks[1]);
            _stacks[1].Clear();

            for (int i = 0; i < itemsToPromote.Count; i++)
            {
                FoodItem promoted = itemsToPromote[i];
                if (promoted == null) continue;

                _itemAnchorMap.Remove(promoted);

                Transform targetAnchor = i < layer0Anchors.Count
                    ? layer0Anchors[i]
                    : GetFreeAnchor(layer0Anchors);

                if (targetAnchor == null)
                {
                    Debug.LogWarning($"[FoodTray] Không tìm được anchor layer0 cho {promoted.name}!");
                    continue;
                }

                SlotFollower follower = promoted.GetComponent<SlotFollower>();
                follower?.Unfollow();

                FoodItem capturedItem = promoted;
                Transform capturedAnchor = targetAnchor;
                SlotFollower capturedFollower = follower;
                Vector3 prefabScale = promoted.Data.prefab.transform.localScale;

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

                promoted.SetLayerVisual(0);   // trở thành layer 0: full color, full scale
                _stacks[0].Add(promoted);
                TrayAnimator.PlayLayerShiftIn(promoted);
            }
        }

        /// <summary>
        /// Spawn các FoodItemData đang pending trong _pendingLayer2
        /// vào các anchor của layer 1 (nay đã trống sau khi promote).
        /// </summary>
        private void SpawnPendingLayer2()
        {
            if (_neutralContainer == null)
            {
                Debug.LogError("[FoodTray] _neutralContainer null — không thể spawn layer 2!");
                return;
            }

            // Lấy snapshot rồi clear pending ngay
            var toSpawn = new List<FoodItemData>(_pendingLayer2);
            _pendingLayer2.Clear();

            for (int i = 0; i < toSpawn.Count; i++)
            {
                FoodItemData data = toSpawn[i];
                if (data?.prefab == null) continue;

                Transform anchor = i < layer1Anchors.Count
                    ? layer1Anchors[i]
                    : GetFreeAnchor(layer1Anchors);

                if (anchor == null)
                {
                    Debug.LogWarning($"[FoodTray] Không đủ anchor layer1 để spawn pending item {i}!");
                    continue;
                }

                // Spawn với scale-in animation (delay nhỏ theo thứ tự)
                SpawnFoodItem(data, anchor, layerIdx: 1,
                              _neutralContainer, spawnDelay: i * 0.04f);
            }
        }

        // ─── Spawn Helper ─────────────────────────────────────────────────────

        private void SpawnFoodItem(FoodItemData data, Transform anchor, int layerIdx,
                                   Transform neutralContainer, float spawnDelay)
        {
            Vector3 prefabScale = data.prefab.transform.localScale;

            GameObject go = PoolManager.Instance.GetFood(data.foodID, anchor.position);
            if (go == null) return;

            go.transform.SetParent(neutralContainer, worldPositionStays: true);
            go.transform.localScale = prefabScale;
            go.transform.position = anchor.position;

            SlotFollower follower = go.GetComponent<SlotFollower>();
            if (follower == null) follower = go.AddComponent<SlotFollower>();
            follower.Follow(anchor);

            FoodItem item = go.GetComponent<FoodItem>();
            if (item == null)
            {
                Debug.LogError($"[FoodTray] '{data.foodName}' thiếu FoodItem component!");
                return;
            }

            item.Initialize(data, layerIdx);
            item.OwnerTray = this;
            item.SetAnchorRef(anchor);

            _stacks[layerIdx].Add(item);
            _itemAnchorMap[item] = anchor;

            // Scale-in animation
            go.transform.localScale = Vector3.zero;
            go.transform
                .DOScale(prefabScale, 0.3f)
                .SetDelay(spawnDelay)
                .SetEase(Ease.OutBack)
                .SetUpdate(false);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

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