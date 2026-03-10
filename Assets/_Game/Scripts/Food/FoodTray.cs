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
        /// Layer 2+ lưu DATA trong _pendingLayers[], chưa spawn GameObject cho đến khi promote.
        /// </summary>
        private readonly List<FoodItem>[] _stacks =
        {
            new List<FoodItem>(),   // layer 0
            new List<FoodItem>(),   // layer 1
        };

        /// <summary>
        /// FIX: Thay _pendingLayer2 đơn lẻ bằng queue của các pending layer.
        /// _pendingLayers[0] = data của layer 2 (sẽ spawn vào layer 1 khi promote)
        /// _pendingLayers[1] = data của layer 3 (sẽ trở thành layer 2 sau khi promote)
        /// _pendingLayers[2] = data của layer 4 (sẽ trở thành layer 3 sau khi promote)
        /// ... tối đa MAX_LAYER_COUNT - 2 phần tử pending (vì layer 0 và 1 đã spawn)
        /// </summary>
        private const int MAX_LAYER_COUNT = 5;
        private readonly List<List<FoodItemData>> _pendingLayers = new();

        // Giữ _pendingLayer2 làm alias trỏ vào _pendingLayers[0] để tương thích
        // với các API cũ (MagnetBooster, ShuffleBooster).
        private List<FoodItemData> _pendingLayer2 => _pendingLayers.Count > 0
            ? _pendingLayers[0]
            : null;

        private readonly Dictionary<FoodItem, Transform> _itemAnchorMap = new();

        // Cache neutralContainer để dùng khi spawn lazy
        private Transform _neutralContainer;

        public int TrayID { get; private set; }
        public FoodItem TopItem => _stacks[0].Count > 0 ? _stacks[0][0] : null;
        public bool IsEmpty => TopItem == null;

        /// <summary>
        /// Capacity vật lý = layer 0 + layer 1 anchors.
        /// Layer 2+ không cần anchor riêng — dùng lại anchor của layer 1 khi promoted.
        /// </summary>
        public int TotalAnchorCapacity => layer0Anchors.Count + layer1Anchors.Count;

        /// <summary>
        /// Tổng số food thực sự tray này đang "giữ" (cả spawned lẫn pending).
        /// </summary>
        public int TotalFoodCount
        {
            get
            {
                int count = _stacks[0].Count + _stacks[1].Count;
                foreach (var pending in _pendingLayers)
                    count += pending.Count;
                return count;
            }
        }

        /// <summary>
        /// FIX: MaxFoodCapacity tính đúng dựa trên số pending layer thực tế.
        /// = layer0 + layer1 + (layer1 * số pending layer)
        /// Ví dụ với 5 layer: layer0 + layer1 + layer1*3 = layer0 + layer1*4
        /// </summary>
        public int MaxFoodCapacity =>
            layer0Anchors.Count + layer1Anchors.Count * (1 + _pendingLayers.Count);

        /// <summary>
        /// Capacity tối đa lý thuyết khi dùng tất cả MAX_LAYER_COUNT layer.
        /// FoodTraySpawner dùng để pre-check trước khi distribute.
        /// </summary>
        public int MaxTheoreticalCapacity =>
            layer0Anchors.Count + layer1Anchors.Count * (MAX_LAYER_COUNT - 1);

        // ─── Awake ────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Khởi tạo pending layer queue — MAX_LAYER_COUNT - 2 pending list
            // (trừ layer 0 và 1 vì chúng dùng _stacks)
            _pendingLayers.Clear();
            for (int i = 0; i < MAX_LAYER_COUNT - 2; i++)
                _pendingLayers.Add(new List<FoodItemData>());
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// foods[0..l0Cap-1]            → spawn ngay vào layer 0
        /// foods[l0Cap..l0Cap+l1Cap-1]  → spawn ngay vào layer 1 (mờ/nhỏ)
        /// foods[l0Cap+l1Cap..]         → lưu vào _pendingLayers theo thứ tự layer,
        ///                                CHƯA spawn cho đến khi promote.
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
                    // Layer 0: spawn ngay, interactive
                    SpawnFoodItem(data, layer0Anchors[i], layerIdx: 0,
                                  neutralContainer, spawnDelay: globalDelay + i * 0.04f);
                }
                else if (i < l0Cap + l1Cap)
                {
                    // Layer 1: spawn ngay, greyed-out
                    int anchorIdx = i - l0Cap;
                    SpawnFoodItem(data, layer1Anchors[anchorIdx], layerIdx: 1,
                                  neutralContainer, spawnDelay: 0f);
                }
                else
                {
                    // FIX: Layer 2+ — phân phối vào đúng pending slot theo thứ tự
                    // foodOffset = vị trí trong vùng pending (0-based)
                    int foodOffset = i - (l0Cap + l1Cap);
                    // pendingLayerIdx = index trong _pendingLayers (0 = layer2, 1 = layer3, ...)
                    // Mỗi pending layer chứa tối đa l1Cap item (dùng chung anchor layer1)
                    int pendingLayerIdx = foodOffset / l1Cap;

                    if (pendingLayerIdx < _pendingLayers.Count)
                    {
                        _pendingLayers[pendingLayerIdx].Add(data);
                    }
                    else
                    {
                        Debug.LogWarning($"[FoodTray] Tray {TrayID}: food thứ {i} vượt quá " +
                                         $"MaxTheoreticalCapacity ({MaxTheoreticalCapacity})! Bỏ qua.");
                    }
                }
            }
        }

        /// <summary>
        /// Thử lấy item ra khỏi layer 0.
        /// Sau khi remove, nếu layer 0 trống HOÀN TOÀN → promote chain.
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
        /// MagnetBooster: remove food khỏi bất kỳ layer nào (0, 1).
        /// KHÔNG trigger promote — Magnet tự lo animation và flow.
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

            // FIX: Clear toàn bộ pending layers
            foreach (var pending in _pendingLayers)
                pending.Clear();

            _itemAnchorMap.Clear();
        }

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

            // FIX: Clear toàn bộ pending layers
            foreach (var pending in _pendingLayers)
                pending.Clear();

            _itemAnchorMap.Clear();
        }

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

            // FIX: Clear toàn bộ pending layers
            foreach (var pending in _pendingLayers)
                pending.Clear();

            _itemAnchorMap.Clear();
        }

        /// <summary>
        /// Trả về pending data layer 2 theo foodID.
        /// Tương thích ngược với MagnetBooster (chỉ cần layer đầu tiên pending).
        /// </summary>
        public List<FoodItemData> GetPendingFoodsOfType(int foodID)
        {
            var result = new List<FoodItemData>();
            // FIX: Tìm trong toàn bộ pending layers, không chỉ layer2
            foreach (var pending in _pendingLayers)
                foreach (var data in pending)
                    if (data != null && data.foodID == foodID)
                        result.Add(data);
            return result;
        }

        /// <summary>
        /// Trả về TOÀN BỘ pending data (layer 2+).
        /// ShuffleBooster gom vào pool chung trước khi shuffle.
        /// </summary>
        public IReadOnlyList<FoodItemData> GetAllPendingData()
        {
            // FIX: Gom tất cả pending layers vào 1 list
            var all = new List<FoodItemData>();
            foreach (var pending in _pendingLayers)
                all.AddRange(pending);
            return all;
        }

        /// <summary>
        /// Remove 1 pending food data khỏi bất kỳ pending layer nào.
        /// </summary>
        public bool RemovePendingFood(FoodItemData data)
        {
            // FIX: Tìm và remove trong toàn bộ pending layers
            foreach (var pending in _pendingLayers)
                if (pending.Remove(data)) return true;
            return false;
        }

        /// <summary>
        /// Thêm 1 FoodItemData vào cuối pending layer đầu tiên còn chỗ.
        /// ShuffleBooster gọi để redistribute overflow food.
        /// </summary>
        public void AddPendingData(FoodItemData data)
        {
            if (data == null) return;

            int l1Cap = layer1Anchors.Count;

            // FIX: Tìm pending layer còn chỗ để thêm vào
            foreach (var pending in _pendingLayers)
            {
                if (pending.Count < l1Cap)
                {
                    pending.Add(data);
                    return;
                }
            }

            // Fallback: thêm vào layer pending cuối nếu tất cả đều đầy
            if (_pendingLayers.Count > 0)
            {
                _pendingLayers[_pendingLayers.Count - 1].Add(data);
                Debug.LogWarning($"[FoodTray] Tray {TrayID}: AddPendingData overflow! " +
                                 $"Đã thêm vào pending layer cuối.");
            }
        }

        public void SpawnSingleFood(FoodItemData data, Transform anchor,
                                    int layerIdx, Transform neutralContainer,
                                    float spawnDelay = 0f)
        {
            if (_neutralContainer == null)
                _neutralContainer = neutralContainer;

            SpawnFoodItem(data, anchor, layerIdx, neutralContainer, spawnDelay);
        }

        public void SpawnSingleFoodSnap(FoodItemData data, Transform anchor,
                                        int layerIdx, Transform neutralContainer,
                                        float spawnDelay = 0f)
        {
            if (_neutralContainer == null)
                _neutralContainer = neutralContainer;

            Vector3 prefabScale = data.prefab.transform.localScale;
            Vector3 spawnWorldPos = anchor.position;

            GameObject go = PoolManager.Instance.GetFood(data.foodID, spawnWorldPos);
            if (go == null) return;

            go.transform.SetParent(neutralContainer, worldPositionStays: true);
            go.transform.position = spawnWorldPos;
            go.transform.localScale = Vector3.zero;

            FoodItem item = go.GetComponent<FoodItem>();
            if (item == null)
            {
                Debug.LogError($"[FoodTray] '{data.foodName}' thiếu FoodItem!");
                return;
            }

            item.Initialize(data, layerIdx);
            item.OwnerTray = this;
            item.SetAnchorRef(anchor);

            int stackIdx = Mathf.Clamp(layerIdx, 0, 1);
            _stacks[stackIdx].Add(item);
            _itemAnchorMap[item] = anchor;

            SlotFollower follower = go.GetComponent<SlotFollower>();
            if (follower == null) follower = go.AddComponent<SlotFollower>();
            follower.Unfollow();

            Vector3 targetScale = layerIdx == 0 ? prefabScale : prefabScale * 0.8f;

            go.transform
                .DOScale(targetScale, 0.3f)
                .SetDelay(spawnDelay)
                .SetEase(Ease.OutBack)
                .SetUpdate(false)
                .OnComplete(() =>
                {
                    if (go == null) return;
                    go.transform.position = anchor.position;
                    follower.Follow(anchor);
                });
        }

        public void RegisterToStack(FoodItem item, Transform anchor, int layerIdx)
        {
            int stackIdx = Mathf.Clamp(layerIdx, 0, 1);
            if (!_stacks[stackIdx].Contains(item))
                _stacks[stackIdx].Add(item);
            _itemAnchorMap[item] = anchor;
        }

        public IReadOnlyList<Transform> GetLayer0Anchors() => layer0Anchors;
        public IReadOnlyList<Transform> GetLayer1Anchors() => layer1Anchors;

        public int GetLayerIndexOfAnchor(Transform anchor)
        {
            if (layer0Anchors.Contains(anchor)) return 0;
            if (layer1Anchors.Contains(anchor)) return 1;
            return 0;
        }

        // ─── Promote Logic ────────────────────────────────────────────────────

        private void TryPromoteNextLayer()
        {
            if (_stacks[0].Count != 0) return;

            bool hasPending = false;
            foreach (var p in _pendingLayers)
                if (p.Count > 0) { hasPending = true; break; }

            if (_stacks[1].Count == 0 && !hasPending) return;

            if (_stacks[1].Count > 0)
                PromoteLayer1ToLayer0();

            // FIX: Promote chain — layer 2 → layer 1, layer 3 → layer 2, ...
            // Chỉ spawn layer pending đầu tiên vào layer 1 vật lý,
            // rồi shift toàn bộ queue pending lên 1 bậc.
            if (_pendingLayers.Count > 0 && _pendingLayers[0].Count > 0)
                SpawnAndShiftPendingLayers();
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

        /// <summary>
        /// FIX: Spawn pending layer đầu tiên vào slot layer 1 vật lý,
        /// sau đó shift toàn bộ queue pending lên 1 bậc.
        /// Layer 2 → vật lý layer 1
        /// Layer 3 → pending[0] (tức là layer 2 mới)
        /// Layer 4 → pending[1] (tức là layer 3 mới)
        /// </summary>
        private void SpawnAndShiftPendingLayers()
        {
            if (_neutralContainer == null)
            {
                Debug.LogError("[FoodTray] _neutralContainer null — không thể spawn pending layer!");
                return;
            }

            // Lấy data của layer 2 (pending[0]) để spawn vào vật lý layer 1
            var toSpawn = new List<FoodItemData>(_pendingLayers[0]);
            _pendingLayers[0].Clear();

            // FIX: Shift tất cả pending layer lên 1 bậc
            // pending[1] (layer 3) → pending[0] (layer 2 mới)
            // pending[2] (layer 4) → pending[1] (layer 3 mới)
            for (int layerIdx = 0; layerIdx < _pendingLayers.Count - 1; layerIdx++)
            {
                _pendingLayers[layerIdx].AddRange(_pendingLayers[layerIdx + 1]);
                _pendingLayers[layerIdx + 1].Clear();
            }

            // Spawn data lên vật lý layer 1
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