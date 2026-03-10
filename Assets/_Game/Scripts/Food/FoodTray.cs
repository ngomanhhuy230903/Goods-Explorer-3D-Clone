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
                    SpawnFoodItem(data, layer0Anchors[i], layerIdx: 0,
                                  neutralContainer, spawnDelay: globalDelay + i * 0.04f);
                }
                else if (i < l0Cap + l1Cap)
                {
                    int anchorIdx = i - l0Cap;
                    SpawnFoodItem(data, layer1Anchors[anchorIdx], layerIdx: 1,
                                  neutralContainer, spawnDelay: 0f);
                }
                else
                {
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

        /// <summary>
        /// MagnetBooster dùng: remove food khỏi bất kỳ layer nào (0, 1).
        /// KHÔNG trigger promote — Magnet tự lo animation và flow.
        /// Trả về true nếu tìm và remove thành công.
        /// </summary>
        public bool ForceRemoveFromAnyLayer(FoodItem item)
        {
            for (int l = 0; l < 2; l++)
            {
                if (!_stacks[l].Contains(item)) continue;

                item.GetComponent<SlotFollower>()?.Unfollow();
                _itemAnchorMap.Remove(item);
                _stacks[l].Remove(item);
                item.OwnerTray = null;

                if (l == 0) TryPromoteNextLayer();

                return true;
            }

            return false;
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

        /// <summary>
        /// Chỉ clear internal stacks/maps mà KHÔNG return food về pool.
        /// Dùng bởi ShuffleBooster sau khi đã tự return pool ở bước despawn.
        /// </summary>
        public void ClearStacksOnly()
        {
            for (int l = 0; l < 2; l++)
            {
                foreach (var item in _stacks[l])
                {
                    if (item == null) continue;
                    item.GetComponent<SlotFollower>()?.Unfollow();
                    item.OwnerTray = null;
                }
                _stacks[l].Clear();
            }
            _pendingLayer2.Clear();
            _itemAnchorMap.Clear();
        }

        /// <summary>
        /// Clear stacks (không return pool) + pending layer 2.
        /// ShuffleBooster gọi sau khi đã gom toàn bộ data vào pool shuffle.
        /// </summary>
        public void ClearStacksAndPending()
        {
            for (int l = 0; l < 2; l++)
            {
                foreach (var item in _stacks[l])
                {
                    if (item == null) continue;
                    item.GetComponent<SlotFollower>()?.Unfollow();
                    item.OwnerTray = null;
                }
                _stacks[l].Clear();
            }
            _pendingLayer2.Clear();
            _itemAnchorMap.Clear();
        }

        /// <summary>
        /// Trả về pending data layer 2+ theo foodID.
        /// MagnetBooster dùng để lấy food chưa spawn vào scene.
        /// </summary>
        public List<FoodItemData> GetPendingFoodsOfType(int foodID)
        {
            var result = new List<FoodItemData>();
            foreach (var data in _pendingLayer2)
                if (data != null && data.foodID == foodID)
                    result.Add(data);
            return result;
        }

        /// <summary>
        /// Trả về TOÀN BỘ pending data layer 2 (không lọc theo foodID).
        /// ShuffleBooster gom vào pool chung trước khi shuffle.
        /// </summary>
        public IReadOnlyList<FoodItemData> GetAllPendingData() => _pendingLayer2;

        /// <summary>
        /// Remove 1 pending food data khỏi layer 2 queue.
        /// Gọi sau khi MagnetBooster đã spawn và gửi food đó lên order.
        /// </summary>
        public bool RemovePendingFood(FoodItemData data)
        {
            return _pendingLayer2.Remove(data);
        }

        /// <summary>
        /// Thêm 1 FoodItemData vào cuối queue pending layer 2.
        /// ShuffleBooster gọi để redistribute overflow food (layer 3, 4...)
        /// sau khi shuffle — đảm bảo tổng food không thay đổi.
        /// </summary>
        public void AddPendingData(FoodItemData data)
        {
            if (data != null)
                _pendingLayer2.Add(data);
        }

        /// <summary>
        /// Spawn 1 food vào đúng anchor + layer.
        /// ShuffleBooster dùng sau khi shuffle slot.
        /// </summary>
        public void SpawnSingleFood(FoodItemData data, Transform anchor,
                                    int layerIdx, Transform neutralContainer,
                                    float spawnDelay = 0f)
        {
            if (_neutralContainer == null)
                _neutralContainer = neutralContainer;

            SpawnFoodItem(data, anchor, layerIdx, neutralContainer, spawnDelay);
        }

        /// <summary>
        /// Spawn food + snap NGAY vào world position của anchor.
        /// Dùng cho Shuffle: đảm bảo food xuất hiện đúng chỗ dù tray đang xoay.
        /// LayerIndex theo anchor mới → visual đúng (màu, scale, collider).
        /// </summary>
        public void SpawnSingleFoodSnap(FoodItemData data, Transform anchor,
                                        int layerIdx, Transform neutralContainer,
                                        float spawnDelay = 0f)
        {
            if (_neutralContainer == null)
                _neutralContainer = neutralContainer;

            Vector3 prefabScale = data.prefab.transform.localScale;
            Vector3 spawnWorldPos = anchor.position;   // cache ngay lúc tray đứng yên

            GameObject go = PoolManager.Instance.GetFood(data.foodID, spawnWorldPos);
            if (go == null) return;

            go.transform.SetParent(neutralContainer, worldPositionStays: true);

            // ── SNAP position NGAY, không delay ──────────────────────────────
            // Đây là điểm mấu chốt: position set trước bất kỳ delay nào
            // → dù tray xoay trong khoảng spawnDelay, food đã ở đúng chỗ
            go.transform.position = spawnWorldPos;
            go.transform.localScale = Vector3.zero;

            FoodItem item = go.GetComponent<FoodItem>();
            if (item == null)
            {
                Debug.LogError($"[FoodTray] '{data.foodName}' thiếu FoodItem!");
                return;
            }

            // Initialize với layerIdx MỚI → visual/collider đúng theo layer
            item.Initialize(data, layerIdx);
            item.OwnerTray = this;
            item.SetAnchorRef(anchor);

            int stackIdx = Mathf.Clamp(layerIdx, 0, 1);
            _stacks[stackIdx].Add(item);
            _itemAnchorMap[item] = anchor;

            SlotFollower follower = go.GetComponent<SlotFollower>();
            if (follower == null) follower = go.AddComponent<SlotFollower>();
            follower.Unfollow(); // không follow trong lúc animate

            Vector3 targetScale = layerIdx == 0 ? prefabScale : prefabScale * 0.8f;

            // Chỉ scale mới delay — position đã snap sẵn
            go.transform
                .DOScale(targetScale, 0.3f)
                .SetDelay(spawnDelay)
                .SetEase(Ease.OutBack)
                .SetUpdate(false)
                .OnComplete(() =>
                {
                    if (go == null) return;

                    // Snap lần 2 sau animation → triệt tiêu drift tray xoay trong delay
                    go.transform.position = anchor.position;
                    follower.Follow(anchor);
                });
        }

        /// <summary>
        /// Đăng ký FoodItem vào stack và anchor map của tray.
        /// ShuffleBooster gọi sau khi tự spawn food vào đúng slot mới.
        /// </summary>
        public void RegisterToStack(FoodItem item, Transform anchor, int layerIdx)
        {
            int stackIdx = Mathf.Clamp(layerIdx, 0, 1);
            if (!_stacks[stackIdx].Contains(item))
                _stacks[stackIdx].Add(item);
            _itemAnchorMap[item] = anchor;
        }

        /// <summary>
        /// Trả về danh sách anchor của layer 0.
        /// ShuffleBooster dùng để thu thập tất cả available slots.
        /// </summary>
        public IReadOnlyList<Transform> GetLayer0Anchors() => layer0Anchors;

        /// <summary>
        /// Trả về danh sách anchor của layer 1.
        /// ShuffleBooster dùng để thu thập tất cả available slots.
        /// </summary>
        public IReadOnlyList<Transform> GetLayer1Anchors() => layer1Anchors;

        /// <summary>
        /// Trả về 0 nếu anchor thuộc layer0Anchors, 1 nếu thuộc layer1Anchors.
        /// ShuffleBooster dùng để xác định visual của food sau khi đổi slot.
        /// </summary>
        public int GetLayerIndexOfAnchor(Transform anchor)
        {
            if (layer0Anchors.Contains(anchor)) return 0;
            if (layer1Anchors.Contains(anchor)) return 1;
            return 0; // fallback
        }

        // ─── Promote Logic ────────────────────────────────────────────────────

        private void TryPromoteNextLayer()
        {
            if (_stacks[0].Count != 0) return;
            if (_stacks[1].Count == 0 && _pendingLayer2.Count == 0) return;

            if (_stacks[1].Count > 0)
                PromoteLayer1ToLayer0();

            if (_pendingLayer2.Count > 0)
                SpawnPendingLayer2();
        }

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
                Vector3 prefabScale = promoted.Data?.prefab != null
                                                ? promoted.Data.prefab.transform.localScale
                                                : Vector3.one;

                promoted.transform
                    .DOMove(targetAnchor.position, 0.3f)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(false)
                    .OnComplete(() =>
                    {
                        if (capturedItem == null) return;

                        capturedItem.transform.localScale = prefabScale;
                        _itemAnchorMap[capturedItem] = capturedAnchor;

                        var f = capturedItem.GetComponent<SlotFollower>();
                        if (f == null) f = capturedItem.gameObject.AddComponent<SlotFollower>();
                        f.Follow(capturedAnchor);
                    });

                promoted.SetLayerVisual(0);
                _stacks[0].Add(promoted);
                TrayAnimator.PlayLayerShiftIn(promoted);
            }
        }

        private void SpawnPendingLayer2()
        {
            if (_neutralContainer == null)
            {
                Debug.LogError("[FoodTray] _neutralContainer null — không thể spawn layer 2!");
                return;
            }

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
            go.transform.position = anchor.position;
            go.transform.localScale = Vector3.zero;

            SlotFollower follower = go.GetComponent<SlotFollower>();
            if (follower == null) follower = go.AddComponent<SlotFollower>();
            follower.Unfollow();

            FoodItem item = go.GetComponent<FoodItem>();
            if (item == null)
            {
                Debug.LogError($"[FoodTray] '{data.foodName}' thiếu FoodItem!");
                return;
            }

            item.Initialize(data, layerIdx);
            item.OwnerTray = this;
            item.SetAnchorRef(anchor);

            _stacks[Mathf.Clamp(layerIdx, 0, 1)].Add(item);
            _itemAnchorMap[item] = anchor;

            go.transform
                .DOScale(layerIdx == 0 ? prefabScale : prefabScale * 0.8f, 0.3f)
                .SetDelay(spawnDelay)
                .SetEase(Ease.OutBack)
                .SetUpdate(false)
                .OnComplete(() =>
                {
                    if (go == null || follower == null) return;
                    follower.Follow(anchor);
                });
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