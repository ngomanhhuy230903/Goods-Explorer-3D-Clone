using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Food;
using FoodMatch.Tray;
using FoodMatch.Data;
using FoodMatch.Core;

namespace FoodMatch.Items
{
    [Booster]
    public class ShuffleBooster : IBooster
    {
        public string BoosterName => "Shuffle";

        private FoodGridSpawner _gridSpawner;
        private MonoBehaviour _runner;

        private const float DespawnDuration = 0.18f;
        private const float StaggerDelay = 0.04f;

        public void Initialize(BoosterContext ctx)
        {
            _gridSpawner = ctx.FoodGridSpawner;
            _runner = ctx.CoroutineRunner;
        }

        public bool CanExecute()
        {
            if (_gridSpawner == null) return false;
            return _gridSpawner.GetAllActiveFoods().Count >= 2;
        }

        public void Execute() => _runner.StartCoroutine(ShuffleRoutine());

        private IEnumerator ShuffleRoutine()
        {
            var allFoods = _gridSpawner.GetAllActiveFoods();
            if (allFoods.Count < 2) yield break;

            // ── Bước 1: DỪNG AUTO-ROTATE trước khi làm bất cứ thứ gì ─────────
            // Nếu foodtray đang xoay → slot position thay đổi mỗi frame
            // → food spawn xong sẽ bị "trượt" về vị trí đúng thay vì snap ngay
            _gridSpawner.NotifyInteraction(); // reset idle timer + dừng auto-rotate

            // Chờ 1 frame để StopAutoRotate ease-out xử lý
            // (StopAutoRotate dùng DOTween ease-out 0.5s)
            // Ta không cần chờ đủ 0.5s — chỉ cần dừng xoay trước khi spawn
            yield return null;

            // ── Bước 2: Snapshot slot WORLD POSITION tại frame này ────────────
            // Cache world position ngay lúc này vì sau đó tray có thể tiếp tục xoay
            // Quan trọng: dùng slot.position (world) không phải localPosition
            var snapshots = new List<SlotSnapshot>(allFoods.Count);
            foreach (var food in allFoods)
            {
                var follower = food.GetComponent<SlotFollower>();
                var slot = follower?.targetSlot ?? food.AnchorRef;
                snapshots.Add(new SlotSnapshot
                {
                    Slot = slot,
                    // Cache world position TẠI THỜI ĐIỂM NÀY
                    SlotWorldPos = slot != null ? slot.position : food.transform.position,
                    Data = food.Data,
                    LayerIndex = food.LayerIndex,
                    OwnerTray = food.OwnerTray,
                });
            }

            // ── Bước 3: Unfollow TẤT CẢ cùng lúc ────────────────────────────
            foreach (var food in allFoods)
                food.GetComponent<SlotFollower>()?.Unfollow();

            yield return null; // chờ LateUpdate xử lý

            // ── Bước 4: Despawn TẤT CẢ cùng lúc ─────────────────────────────
            foreach (var food in allFoods)
            {
                var capturedFood = food;
                capturedFood.transform
                    .DOScale(Vector3.zero, DespawnDuration)
                    .SetEase(Ease.InBack)
                    .OnComplete(() =>
                    {
                        if (capturedFood != null)
                            PoolManager.Instance.ReturnFood(
                                capturedFood.FoodID, capturedFood.gameObject);
                    });
            }

            yield return new WaitForSeconds(DespawnDuration + 0.05f);

            // ── Bước 5: Clear stacks ──────────────────────────────────────────
            var allTrays = _gridSpawner.GetCellContainer()
                .GetComponentsInChildren<FoodTray>(includeInactive: false);
            foreach (var tray in allTrays)
                tray.ClearStacksOnly();

            // ── Bước 6: Fisher-Yates shuffle SLOTS ───────────────────────────
            var shuffledSlots = new List<Transform>(snapshots.Count);
            foreach (var s in snapshots) shuffledSlots.Add(s.Slot);

            for (int i = shuffledSlots.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (shuffledSlots[i], shuffledSlots[j])
                    = (shuffledSlots[j], shuffledSlots[i]);
            }

            // ── Bước 7: Respawn stagger — snap ngay vào slot mới ─────────────
            var neutralContainer = _gridSpawner.GetNeutralContainer();
            if (neutralContainer == null)
            {
                Debug.LogError("[Shuffle] neutralContainer null!");
                yield break;
            }

            for (int i = 0; i < snapshots.Count; i++)
            {
                var snap = snapshots[i];
                var newSlot = shuffledSlots[i];
                float delay = i * StaggerDelay;

                if (newSlot == null || snap.Data == null) continue;

                // Xác định layerIndex mới dựa theo slot mới thuộc layer nào
                int newLayerIndex = GetLayerIndexOfSlot(newSlot, snap.OwnerTray);

                snap.OwnerTray?.SpawnSingleFoodSnap(
                    data: snap.Data,
                    anchor: newSlot,
                    layerIdx: newLayerIndex,  // layer theo VỊ TRÍ MỚI
                    neutralContainer: neutralContainer,
                    spawnDelay: delay);
            }

            Debug.Log($"[Shuffle] {snapshots.Count} foods respawned.");
        }

        /// <summary>
        /// Xác định slot này thuộc layer 0 hay layer 1 của FoodTray.
        /// So sánh với danh sách layer0Anchors và layer1Anchors.
        /// </summary>
        private int GetLayerIndexOfSlot(Transform slot, FoodTray tray)
        {
            if (tray == null) return 0;
            return tray.GetLayerIndexOfAnchor(slot);
        }

        private struct SlotSnapshot
        {
            public Transform Slot;
            public Vector3 SlotWorldPos;  // cached world pos lúc snapshot
            public FoodItemData Data;
            public int LayerIndex;
            public FoodTray OwnerTray;
        }
    }
}