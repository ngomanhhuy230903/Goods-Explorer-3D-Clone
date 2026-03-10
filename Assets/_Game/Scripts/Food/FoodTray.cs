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

                // Trigger promote nếu layer 0 trống SAU KHI remove
                // (chỉ trigger khi remove từ layer 0)
                if (l == 0) TryPromoteNextLayer();

                return true;
            }

            return false; // không tìm thấy trong bất kỳ layer nào
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
        /// Remove 1 pending food data khỏi layer 2 queue.
        /// Gọi sau khi MagnetBooster đã spawn và gửi food đó lên order.
        /// </summary>
        public bool RemovePendingFood(FoodItemData data)
        {
            return _pendingLayer2.Remove(data);
        }
        /// <summary>
        /// Spawn 1 food vào đúng anchor + layer.
        /// ShuffleBooster dùng sau khi shuffle slot.
        /// ĐẢM BẢO SlotFollower.Follow() được gọi sau khi spawn xong.
        /// </summary>
        public void SpawnSingleFood(FoodItemData data, Transform anchor,
                                    int layerIdx, Transform neutralContainer,
                                    float spawnDelay = 0f)
        {
            if (_neutralContainer == null)
                _neutralContainer = neutralContainer;

            // Dùng SpawnFoodItem có sẵn — nó đã gọi follower.Follow(anchor) bên trong
            SpawnFoodItem(data, anchor, layerIdx, neutralContainer, spawnDelay);
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
            go.transform.position = anchor.position;
            go.transform.localScale = Vector3.zero; // bắt đầu từ 0

            // ── SlotFollower: UNFOLLOW trong lúc scale-in ─────────────────────────
            // Nếu follow ngay thì LateUpdate snap position ổn nhưng scale-in vẫn chạy
            // → thực ra follow ngay cũng được vì LateUpdate chỉ set position, không set scale
            // Vấn đề cũ là sau Shuffle, follower vẫn trỏ về anchor CŨ
            // SpawnSingleFood truyền anchor MỚI → Follow(anchor mới) là đúng
            SlotFollower follower = go.GetComponent<SlotFollower>();
            if (follower == null) follower = go.AddComponent<SlotFollower>();

            // Unfollow trước để tránh LateUpdate can thiệp trong frame đầu
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

            // Scale-in animation → sau khi xong mới Follow anchor mới
            go.transform
                .DOScale(layerIdx == 0 ? prefabScale : prefabScale * 0.8f,
                         0.3f)
                .SetDelay(spawnDelay)
                .SetEase(Ease.OutBack)
                .SetUpdate(false)
                .OnComplete(() =>
                {
                    if (go == null || follower == null) return;

            // Follow anchor mới sau khi animation xong
            // → không còn bị kéo về anchor cũ
            follower.Follow(anchor);
                });
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

            // Lấy world position của anchor TẠI THỜI ĐIỂM GỌI HÀM
            // (không delay — nếu delay thì tray có thể xoay làm lệch vị trí)
            Vector3 spawnWorldPos = anchor.position;

            GameObject go = PoolManager.Instance.GetFood(data.foodID, spawnWorldPos);
            if (go == null) return;

            go.transform.SetParent(neutralContainer, worldPositionStays: true);

            // SNAP ngay vào đúng world position — không để DOTween delay mới set position
            go.transform.position = spawnWorldPos;

            // Scale bắt đầu từ 0
            go.transform.localScale = Vector3.zero;

            FoodItem item = go.GetComponent<FoodItem>();
            if (item == null)
            {
                Debug.LogError($"[FoodTray] '{data.foodName}' thiếu FoodItem!");
                return;
            }

            // Initialize với layerIdx MỚI (theo vị trí mới sau shuffle)
            // → tự động set màu, scale xám, collider đúng theo layer
            item.Initialize(data, layerIdx);
            item.OwnerTray = this;
            item.SetAnchorRef(anchor);

            // Thêm vào stack đúng layer
            int stackIdx = Mathf.Clamp(layerIdx, 0, 1);
            _stacks[stackIdx].Add(item);
            _itemAnchorMap[item] = anchor;

            // Scale target theo layer
            Vector3 targetScale = layerIdx == 0
                ? prefabScale           // layer 0: full size
                : prefabScale * 0.8f;  // layer 1: nhỏ hơn (greyed)

            // Scale-in animation với delay stagger
            // OnComplete: Follow anchor → bám theo tray từ đây
            go.transform
                .DOScale(targetScale, 0.3f)
                .SetDelay(spawnDelay)
                .SetEase(Ease.OutBack)
                .SetUpdate(false)
                .OnComplete(() =>
                {
                    if (go == null) return;

            // Snap lại 1 lần nữa sau animation phòng drift
            go.transform.position = anchor.position;

            // Bắt đầu follow anchor MỚI từ đây
            SlotFollower follower = go.GetComponent<SlotFollower>();
                    if (follower == null) follower = go.AddComponent<SlotFollower>();
                    follower.Follow(anchor);
                });
        }

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