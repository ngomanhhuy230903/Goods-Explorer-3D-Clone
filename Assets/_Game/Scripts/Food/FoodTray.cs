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

        private readonly List<FoodItem>[] _stacks =
        {
            new List<FoodItem>(),
            new List<FoodItem>(),
        };

        private const int MAX_LAYER_COUNT = 5;
        private readonly List<List<FoodItemData>> _pendingLayers = new();

        private List<FoodItemData> _pendingLayer2 => _pendingLayers.Count > 0
            ? _pendingLayers[0]
            : null;

        private readonly Dictionary<FoodItem, Transform> _itemAnchorMap = new();
        private Transform _neutralContainer;

        // ─── BoxCollider Fix ──────────────────────────────────────────────────
        private BoxCollider[] _childColliders;
        private float[] _originalRotZ;
        private float _originalLossyScaleX;

        // ─── Properties ───────────────────────────────────────────────────────

        public int TrayID { get; private set; }
        public FoodItem TopItem => _stacks[0].Count > 0 ? _stacks[0][0] : null;
        public bool IsEmpty => TopItem == null;

        /// <summary>
        /// Khi true: TryPopItem() từ chối mọi tương tác (tray đang bị LockObstacle khóa).
        /// </summary>
        public bool IsLocked { get; private set; } = false;

        public int TotalAnchorCapacity => layer0Anchors.Count + layer1Anchors.Count;

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

        public int MaxFoodCapacity =>
            layer0Anchors.Count + layer1Anchors.Count * (1 + _pendingLayers.Count);

        public int MaxTheoreticalCapacity =>
            layer0Anchors.Count + layer1Anchors.Count * (MAX_LAYER_COUNT - 1);

        // ─── Awake ────────────────────────────────────────────────────────────

        private void Awake()
        {
            _pendingLayers.Clear();
            for (int i = 0; i < MAX_LAYER_COUNT - 2; i++)
                _pendingLayers.Add(new List<FoodItemData>());

            _originalLossyScaleX = transform.lossyScale.x;

            var all = GetComponentsInChildren<BoxCollider>(includeInactive: true);
            var childList = new List<BoxCollider>();
            foreach (var col in all)
                if (col.transform != transform)
                    childList.Add(col);

            _childColliders = childList.ToArray();
            _originalRotZ = new float[_childColliders.Length];

            for (int i = 0; i < _childColliders.Length; i++)
            {
                float z = _childColliders[i].transform.localEulerAngles.z;
                if (z > 180f) z -= 360f;
                _originalRotZ[i] = z;
            }
        }

        // ─── Lock / Unlock ────────────────────────────────────────────────────

        /// <summary>
        /// Gọi từ LockObstacleController để khóa hoặc mở tray.
        /// </summary>
        public void SetLocked(bool locked)
        {
            IsLocked = locked;
        }

        // ─── Public: Collider Fix ─────────────────────────────────────────────

        public void RefixColliders()
        {
            if (_childColliders == null || _childColliders.Length == 0) return;
            if (Mathf.Approximately(_originalLossyScaleX, 0f)) return;

            float scaleXMultiple = transform.lossyScale.x / _originalLossyScaleX;

            for (int i = 0; i < _childColliders.Length; i++)
            {
                BoxCollider col = _childColliders[i];
                if (col == null) continue;

                Transform t = col.transform;
                Vector3 euler = t.localEulerAngles;

                t.localEulerAngles = new Vector3(
                    euler.x,
                    euler.y,
                    _originalRotZ[i] * scaleXMultiple
                );
            }
        }

        // ─── Public API ───────────────────────────────────────────────────────

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
                    int foodOffset = i - (l0Cap + l1Cap);
                    int pendingLayerIdx = foodOffset / l1Cap;

                    if (pendingLayerIdx < _pendingLayers.Count)
                        _pendingLayers[pendingLayerIdx].Add(data);
                    else
                        Debug.LogWarning($"[FoodTray] Tray {TrayID}: food thứ {i} vượt quá " +
                                         $"MaxTheoreticalCapacity ({MaxTheoreticalCapacity})! Bỏ qua.");
                }
            }
        }

        /// <summary>
        /// Cố gắng lấy food ra khỏi tray.
        /// Nếu tray đang bị khóa → bounce và từ chối.
        /// </summary>
        public FoodItem TryPopItem(FoodItem item)
        {
            if (IsLocked)
            {
                item.PlayLockedBounce();
                return null;
            }

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
            foreach (var pending in _pendingLayers) pending.Clear();
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
            foreach (var pending in _pendingLayers) pending.Clear();
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
            foreach (var pending in _pendingLayers) pending.Clear();
            _itemAnchorMap.Clear();
        }

        public List<FoodItemData> GetPendingFoodsOfType(int foodID)
        {
            var result = new List<FoodItemData>();
            foreach (var pending in _pendingLayers)
                foreach (var data in pending)
                    if (data != null && data.foodID == foodID)
                        result.Add(data);
            return result;
        }

        public IReadOnlyList<FoodItemData> GetAllPendingData()
        {
            var all = new List<FoodItemData>();
            foreach (var pending in _pendingLayers) all.AddRange(pending);
            return all;
        }

        public bool RemovePendingFood(FoodItemData data)
        {
            foreach (var pending in _pendingLayers)
                if (pending.Remove(data)) return true;
            return false;
        }

        public void AddPendingData(FoodItemData data)
        {
            if (data == null) return;
            int l1Cap = layer1Anchors.Count;

            foreach (var pending in _pendingLayers)
            {
                if (pending.Count < l1Cap)
                {
                    pending.Add(data);
                    return;
                }
            }

            if (_pendingLayers.Count > 0)
            {
                _pendingLayers[_pendingLayers.Count - 1].Add(data);
                Debug.LogWarning($"[FoodTray] Tray {TrayID}: AddPendingData overflow!");
            }
        }

        public void SpawnSingleFood(FoodItemData data, Transform anchor,
                                    int layerIdx, Transform neutralContainer,
                                    float spawnDelay = 0f)
        {
            if (_neutralContainer == null) _neutralContainer = neutralContainer;
            SpawnFoodItem(data, anchor, layerIdx, neutralContainer, spawnDelay);
        }

        public void SpawnSingleFoodSnap(FoodItemData data, Transform anchor,
                                        int layerIdx, Transform neutralContainer,
                                        float spawnDelay = 0f)
        {
            if (_neutralContainer == null) _neutralContainer = neutralContainer;

            Vector3 prefabScale = data.prefab.transform.localScale;
            Vector3 spawnWorldPos = anchor.position;

            GameObject go = PoolManager.Instance.GetFood(data.foodID, spawnWorldPos);
            if (go == null) return;

            go.transform.SetParent(neutralContainer, worldPositionStays: true);
            go.transform.position = spawnWorldPos;
            go.transform.localScale = Vector3.zero;

            FoodItem item = go.GetComponent<FoodItem>();
            if (item == null) { Debug.LogError($"[FoodTray] '{data.foodName}' thiếu FoodItem!"); return; }

            item.Initialize(data, layerIdx);
            item.OwnerTray = this;
            item.SetAnchorRef(anchor);

            _stacks[Mathf.Clamp(layerIdx, 0, 1)].Add(item);
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
            if (!_stacks[stackIdx].Contains(item)) _stacks[stackIdx].Add(item);
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

        private void SpawnAndShiftPendingLayers()
        {
            if (_neutralContainer == null)
            {
                Debug.LogError("[FoodTray] _neutralContainer null — không thể spawn pending layer!");
                return;
            }

            var toSpawn = new List<FoodItemData>(_pendingLayers[0]);
            _pendingLayers[0].Clear();

            for (int idx = 0; idx < _pendingLayers.Count - 1; idx++)
            {
                _pendingLayers[idx].AddRange(_pendingLayers[idx + 1]);
                _pendingLayers[idx + 1].Clear();
            }

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

                SpawnFoodItem(data, anchor, layerIdx: 1, _neutralContainer, spawnDelay: i * 0.04f);
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
            if (item == null) { Debug.LogError($"[FoodTray] '{data.foodName}' thiếu FoodItem!"); return; }

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