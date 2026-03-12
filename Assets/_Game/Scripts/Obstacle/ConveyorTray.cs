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
    /// Vị trí được ConveyorObstacleController update mỗi frame.
    ///
    /// FOOD SPAWN: Giống FoodTray.SpawnFoodItem() — PoolManager.GetFood() → FoodItem,
    ///   đặt vào neutralContainer (giống FoodTray), follow spawnPoint anchor.
    ///   FoodItem tự có FoodInteractionHandler → player tap trực tiếp vào food 3D,
    ///   KHÔNG tap vào ConveyorTray UI.
    ///
    /// TAP FLOW: FoodInteractionHandler.HandleTap() → FoodFlowController.HandleFoodTapped()
    ///   → match order → OrderTray, hoặc → BackupTray.
    ///   ConveyorTray chỉ cần expose TryPopItem() để FoodFlowController gọi giống FoodTray.TryPopItem().
    ///
    /// LAYER: Layer 0 = active (collider ON), Layer 1 = greyed (collider OFF).
    ///   Sau khi layer 0 hết → PromoteLayer1ToLayer0() tự động.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ConveyorTray : MonoBehaviour, IPoolable
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Layer 0 Anchors (Active) ─────────")]
        [Tooltip("Tối đa 3 empty child — anchor cho FoodItem layer 0.")]
        [SerializeField] private List<Transform> layer0Anchors = new List<Transform>(3);

        [Header("─── Layer 1 Anchors (Greyed) ─────────")]
        [Tooltip("Tối đa 3 empty child — anchor cho FoodItem layer 1.")]
        [SerializeField] private List<Transform> layer1Anchors = new List<Transform>(3);

        // ─── Runtime ──────────────────────────────────────────────────────────
        public RectTransform RectTransform { get; private set; }
        public int FoodID { get; private set; } = -1;
        public bool IsCollected { get; private set; }
        public bool HasLayer0Food => _layer0.Count > 0;

        // Giống FoodTray._stacks
        private readonly List<FoodItem> _layer0 = new List<FoodItem>();
        private readonly List<FoodItem> _layer1 = new List<FoodItem>();
        private readonly Dictionary<FoodItem, Transform> _anchorMap = new Dictionary<FoodItem, Transform>();

        private FoodItemData _foodData;
        // neutralContainer — food 3D sống trong world space, giống FoodTray
        private Transform _neutralContainer;

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
        /// Khởi tạo food. neutralContainer = world space container giống FoodTray.
        /// </summary>
        public void Initialize(FoodItemData foodData, int foodPerConveyor,
                               Transform neutralContainer)
        {
            _foodData = foodData;
            _neutralContainer = neutralContainer;
            FoodID = foodData != null ? foodData.foodID : -1;

            int l0 = Mathf.Min(foodPerConveyor, layer0Anchors.Count);
            int l1 = Mathf.Min(foodPerConveyor - l0, layer1Anchors.Count);

            for (int i = 0; i < l0; i++)
                SpawnFoodItem(foodData, layer0Anchors[i], layerIdx: 0, delay: i * 0.05f);
            for (int i = 0; i < l1; i++)
                SpawnFoodItem(foodData, layer1Anchors[i], layerIdx: 1, delay: (l0 + i) * 0.05f);
        }

        /// <summary>
        /// Giống FoodTray.TryPopItem() — gọi bởi FoodFlowController.HandleFoodTapped().
        /// - Item phải ở layer 0 (collider ON).
        /// - Nếu layer 0 rỗng sau khi pop → promote layer 1.
        /// </summary>
        public FoodItem TryPopItem(FoodItem item)
        {
            if (item == null) return null;

            // Layer 1 = locked, không cho pop (giống FoodTray)
            if (!_layer0.Contains(item))
            {
                item.PlayLockedBounce();
                return null;
            }

            // Unfollow anchor (giống FoodTray.TryPopItem)
            item.GetComponent<SlotFollower>()?.Unfollow();
            _anchorMap.Remove(item);
            _layer0.Remove(item);
            item.OwnerTray = null;

            // Promote nếu layer 0 rỗng
            if (_layer0.Count == 0 && _layer1.Count > 0)
                PromoteLayer1ToLayer0();

            if (_layer0.Count == 0 && _layer1.Count == 0)
                IsCollected = true;

            transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 4, 0.4f).SetUpdate(true);

            return item;
        }

        /// <summary>Reset khi trả về pool.</summary>
        public void ResetTray()
        {
            IsCollected = false;
            FoodID = -1;
            _foodData = null;
            _neutralContainer = null;
            DOTween.Kill(gameObject);
            transform.localScale = Vector3.one;
            ClearAllFood();
        }

        // ─── Spawn — Y HỆT FoodTray.SpawnFoodItem() ──────────────────────────

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

            // Lấy từ pool (y hệt FoodTray)
            GameObject go = PoolManager.Instance.GetFood(data.foodID, anchor.position);
            if (go == null) return;

            // Đặt vào neutralContainer, world position = anchor.position
            go.transform.SetParent(_neutralContainer, worldPositionStays: true);
            go.transform.position = anchor.position;
            go.transform.localScale = Vector3.zero;

            // SlotFollower (y hệt FoodTray)
            SlotFollower follower = go.GetComponent<SlotFollower>();
            if (follower == null) follower = go.AddComponent<SlotFollower>();
            follower.Unfollow();

            FoodItem item = go.GetComponent<FoodItem>();
            if (item == null)
            {
                Debug.LogError($"[ConveyorTray] FoodItem missing trên {data.foodName}!");
                PoolManager.Instance.ReturnFood(data.foodID, go);
                return;
            }

            item.Initialize(data, layerIdx);
            // OwnerTray = null vì ConveyorTray không phải FoodTray
            // FoodInteractionHandler sẽ check OwnerTray == null
            // → gọi HandleBackupFoodTapped hoặc cần override
            // → Ta dùng ConveyorFoodOwner component để đánh dấu
            var owner = go.GetComponent<ConveyorFoodOwner>();
            if (owner == null) owner = go.AddComponent<ConveyorFoodOwner>();
            owner.OwnerConveyorTray = this;

            item.SetAnchorRef(anchor);

            _anchorMap[item] = anchor;
            if (layerIdx == 0) _layer0.Add(item); else _layer1.Add(item);

            // Pop-in animation (y hệt FoodTray)
            Vector3 targetScale = layerIdx == 0 ? prefabScale : prefabScale * 0.8f;
            go.transform
                .DOScale(targetScale, 0.3f)
                .SetDelay(delay)
                .SetEase(Ease.OutBack)
                .SetUpdate(false)
                .OnComplete(() =>
                {
                    if (go == null || follower == null) return;
                    follower.Follow(anchor); // follow anchor (anchor là child của ConveyorTray UI)
                });
        }

        // ─── Promote Layer 1 → Layer 0 — Y HỆT FoodTray.PromoteLayer1ToLayer0() ──

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

                SlotFollower follower = item.GetComponent<SlotFollower>();
                follower?.Unfollow();

                FoodItem capturedItem = item;
                Transform capturedAnchor = targetAnchor;
                Vector3 prefabScale = item.Data?.prefab != null
                                           ? item.Data.prefab.transform.localScale
                                           : Vector3.one;

                // Animate về anchor layer 0 (y hệt FoodTray.PromoteLayer1ToLayer0)
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

                item.SetLayerVisual(0); // khôi phục màu, bật collider
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
    /// <summary>
    /// Đánh dấu FoodItem này thuộc ConveyorTray — để FoodInteractionHandler
    /// biết gọi TryPopItem() trên ConveyorTray thay vì OwnerTray.
    /// </summary>
    public class ConveyorFoodOwner : MonoBehaviour
    {
        public ConveyorTray OwnerConveyorTray;
    }
}