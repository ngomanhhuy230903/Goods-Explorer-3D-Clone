// ConveyorTray.cs
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;
using FoodMatch.Food;

namespace FoodMatch.Obstacle
{
    /// <summary>
    /// Một băng chuyền nhỏ — container UI 2D chứa FoodItem 3D.
    ///
    /// FIX — InitializeWithList() trả về float = thời điểm animation kết thúc
    ///   (delay + duration của food cuối cùng). Controller dùng giá trị này
    ///   để biết chờ bao lâu trước khi bật belt.
    ///
    /// Animation: food spawn scale 0→1 TẠI anchor — không di chuyển vị trí.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ConveyorTray : MonoBehaviour, IPoolable
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Layer 0 Anchors (Active) ─────────")]
        [SerializeField] private List<Transform> layer0Anchors = new List<Transform>(3);

        [Header("─── Layer 1 Anchors (Greyed) ─────────")]
        [SerializeField] private List<Transform> layer1Anchors = new List<Transform>(3);

        // ─── Runtime ──────────────────────────────────────────────────────────
        public RectTransform RectTransform { get; private set; }
        public int FoodID { get; private set; } = -1;
        public bool IsCollected { get; private set; }
        public bool HasLayer0Food => _layer0.Count > 0;

        private readonly List<FoodItem> _layer0 = new List<FoodItem>();
        private readonly List<FoodItem> _layer1 = new List<FoodItem>();
        private readonly Dictionary<FoodItem, Transform> _anchorMap = new Dictionary<FoodItem, Transform>();

        private Transform _neutralContainer;

        private const float SpawnDuration = 0.3f;   // giống FoodTray
        private const float FoodStagger = 0.04f;  // giống FoodTray

        // ─────────────────────────────────────────────────────────────────────
        private void Awake() => RectTransform = GetComponent<RectTransform>();

        // ─── IPoolable ────────────────────────────────────────────────────────
        public void OnSpawn()
        {
            IsCollected = false;
            ClearAllFood();
        }

        public void OnDespawn()
        {
            DOTween.Kill(gameObject);
            ClearAllFood();
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// [LEGACY] Giữ tương thích — gọi InitializeWithList() bên trong.
        /// </summary>
        public float Initialize(FoodItemData foodData, int foodPerConveyor,
                                Transform neutralContainer, float baseDelay = 0f)
        {
            var list = new List<FoodItemData>();
            for (int i = 0; i < foodPerConveyor; i++) list.Add(foodData);
            return InitializeWithList(list, foodPerConveyor, neutralContainer, baseDelay);
        }

        /// <summary>
        /// Khởi tạo với danh sách food ngẫu nhiên.
        /// baseDelay: delay cộng thêm cho tray này (stagger theo thứ tự tray).
        ///
        /// RETURN: thời điểm animation kết thúc = delay của food cuối + SpawnDuration.
        ///   Controller dùng giá trị này để chờ trước khi bật belt.
        /// </summary>
        public float InitializeWithList(List<FoodItemData> foodList, int foodPerConveyor,
                                        Transform neutralContainer, float baseDelay = 0f)
        {
            _neutralContainer = neutralContainer;
            FoodID = (foodList != null && foodList.Count > 0 && foodList[0] != null)
                ? foodList[0].foodID : -1;

            if (foodList == null || foodList.Count == 0) return baseDelay;

            int l0 = Mathf.Min(foodPerConveyor, layer0Anchors.Count);
            int l1 = Mathf.Min(foodPerConveyor - l0, layer1Anchors.Count);
            int totalFood = l0 + l1;

            for (int i = 0; i < l0; i++)
            {
                float delay = baseDelay + i * FoodStagger;
                SpawnFoodItem(foodList[i % foodList.Count], layer0Anchors[i],
                              layerIdx: 0, delay: delay);
            }
            for (int i = 0; i < l1; i++)
            {
                float delay = baseDelay + (l0 + i) * FoodStagger;
                SpawnFoodItem(foodList[(l0 + i) % foodList.Count], layer1Anchors[i],
                              layerIdx: 1, delay: delay);
            }

            // Thời điểm kết thúc = delay của food cuối cùng + duration scale
            float lastDelay = baseDelay + (totalFood - 1) * FoodStagger;
            return lastDelay + SpawnDuration;
        }

        /// <summary>
        /// Giống FoodTray.TryPopItem().
        /// </summary>
        public FoodItem TryPopItem(FoodItem item)
        {
            if (item == null) return null;

            if (!_layer0.Contains(item))
            {
                item.PlayLockedBounce();
                return null;
            }

            item.GetComponent<SlotFollower>()?.Unfollow();
            _anchorMap.Remove(item);
            _layer0.Remove(item);
            item.OwnerTray = null;

            if (_layer0.Count == 0 && _layer1.Count > 0)
                PromoteLayer1ToLayer0();

            if (_layer0.Count == 0 && _layer1.Count == 0)
                IsCollected = true;

            transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 4, 0.4f).SetUpdate(true);
            return item;
        }

        public void ResetTray()
        {
            IsCollected = false;
            FoodID = -1;
            _neutralContainer = null;
            DOTween.Kill(gameObject);
            transform.localScale = Vector3.one;
            ClearAllFood();
        }

        // ─── Spawn — giống hệt FoodTray.SpawnFoodItem() ──────────────────────
        // Food spawn TẠI anchor.position, scale 0 → prefabScale.
        // Không có chuyển động vị trí → không bị "bay từ trên xuống".

        private void SpawnFoodItem(FoodItemData data, Transform anchor,
                                   int layerIdx, float delay)
        {
            if (data?.prefab == null) return;
            if (_neutralContainer == null)
            {
                Debug.LogError("[ConveyorTray] neutralContainer null!");
                return;
            }

            Vector3 prefabScale = data.prefab.transform.localScale;

            GameObject go = PoolManager.Instance.GetFood(data.foodID, anchor.position);
            if (go == null) return;

            go.transform.SetParent(_neutralContainer, worldPositionStays: true);
            go.transform.position = anchor.position;
            go.transform.localScale = Vector3.zero;

            SlotFollower follower = go.GetComponent<SlotFollower>();
            if (follower == null) follower = go.AddComponent<SlotFollower>();
            follower.Unfollow();

            FoodItem item = go.GetComponent<FoodItem>();
            if (item == null)
            {
                Debug.LogError($"[ConveyorTray] FoodItem missing: {data.foodName}");
                PoolManager.Instance.ReturnFood(data.foodID, go);
                return;
            }

            item.Initialize(data, layerIdx);
            item.SetAnchorRef(anchor);

            var owner = go.GetComponent<ConveyorFoodOwner>();
            if (owner == null) owner = go.AddComponent<ConveyorFoodOwner>();
            owner.OwnerConveyorTray = this;

            _anchorMap[item] = anchor;
            if (layerIdx == 0) _layer0.Add(item); else _layer1.Add(item);

            Vector3 targetScale = layerIdx == 0 ? prefabScale : prefabScale * 0.8f;
            go.transform
                .DOScale(targetScale, SpawnDuration)
                .SetDelay(delay)
                .SetEase(Ease.OutBack)
                .SetUpdate(false)
                .OnComplete(() =>
                {
                    if (go == null || follower == null) return;
                    // Follow anchor BẮT ĐẦU sau khi scale xong
                    follower.Follow(anchor);
                });
        }

        // ─── Promote Layer 1 → Layer 0 ───────────────────────────────────────

        private void PromoteLayer1ToLayer0()
        {
            var toPromote = new List<FoodItem>(_layer1);
            _layer1.Clear();

            for (int i = 0; i < toPromote.Count; i++)
            {
                FoodItem item = toPromote[i];
                if (item == null) continue;

                _anchorMap.Remove(item);

                Transform targetAnchor = i < layer0Anchors.Count
                    ? layer0Anchors[i]
                    : layer0Anchors[layer0Anchors.Count - 1];

                item.GetComponent<SlotFollower>()?.Unfollow();

                FoodItem capturedItem = item;
                Transform capturedAnchor = targetAnchor;
                Vector3 prefabScale = item.Data?.prefab != null
                    ? item.Data.prefab.transform.localScale : Vector3.one;

                item.transform
                    .DOMove(targetAnchor.position, 0.3f)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(false)
                    .OnComplete(() =>
                    {
                        if (capturedItem == null) return;
                        capturedItem.transform.localScale = prefabScale;
                        _anchorMap[capturedItem] = capturedAnchor;
                        var f = capturedItem.GetComponent<SlotFollower>();
                        if (f == null) f = capturedItem.gameObject.AddComponent<SlotFollower>();
                        f.Follow(capturedAnchor);
                    });

                item.SetLayerVisual(0);
                _layer0.Add(item);
                _anchorMap[item] = targetAnchor;
            }
        }

        // ─── Cleanup ──────────────────────────────────────────────────────────

        private void ClearAllFood()
        {
            ReturnList(_layer0);
            ReturnList(_layer1);
            _anchorMap.Clear();
        }

        private void ReturnList(List<FoodItem> list)
        {
            foreach (var item in list)
            {
                if (item == null) continue;
                item.GetComponent<SlotFollower>()?.Unfollow();
                PoolManager.Instance.ReturnFood(item.FoodID, item.gameObject);
            }
            list.Clear();
        }
    }

    // ─── Marker component ─────────────────────────────────────────────────────
    public class ConveyorFoodOwner : MonoBehaviour
    {
        public ConveyorTray OwnerConveyorTray;
    }
}