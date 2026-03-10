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

            // ── Bước 1: DỪNG AUTO-ROTATE ─────────────────────────────────────
            _gridSpawner.NotifyInteraction();

            // ── Bước 2: Chờ tray DỪNG HẲN trước khi snapshot ────────────────
            // StopAutoRotate dùng ease-out 0.5s — chờ đủ để tray không còn xoay
            // Điều này đảm bảo world position của anchor là ổn định khi ta cache
            yield return new WaitForSeconds(0.55f);

            // ── Bước 3: Snapshot slot + world position TẠI THỜI ĐIỂM NÀY ────
            // Tray đã đứng yên → anchor.position chính xác
            var snapshots = new List<SlotSnapshot>(allFoods.Count);

            // Refresh danh sách sau khi chờ (phòng có food bị remove trong lúc chờ)
            allFoods = _gridSpawner.GetAllActiveFoods();
            if (allFoods.Count < 2) yield break;

            foreach (var food in allFoods)
            {
                var follower = food.GetComponent<SlotFollower>();
                var slot = follower?.targetSlot ?? food.AnchorRef;
                snapshots.Add(new SlotSnapshot
                {
                    Slot = slot,
                    SlotWorldPos = slot != null ? slot.position : food.transform.position,
                    Data = food.Data,
                    LayerIndex = food.LayerIndex,
                    OwnerTray = food.OwnerTray,
                });
            }

            // ── Bước 4: Unfollow TẤT CẢ ─────────────────────────────────────
            foreach (var food in allFoods)
                food.GetComponent<SlotFollower>()?.Unfollow();

            yield return null;

            // ── Bước 5: Despawn TẤT CẢ cùng lúc ─────────────────────────────
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

            // ── Bước 6: Clear stacks ──────────────────────────────────────────
            var allTrays = _gridSpawner.GetCellContainer()
                .GetComponentsInChildren<FoodTray>(includeInactive: false);
            foreach (var tray in allTrays)
                tray.ClearStacksOnly();

            // ── Bước 7: Fisher-Yates shuffle SLOTS ───────────────────────────
            var shuffledSlots = new List<Transform>(snapshots.Count);
            foreach (var s in snapshots) shuffledSlots.Add(s.Slot);

            for (int i = shuffledSlots.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (shuffledSlots[i], shuffledSlots[j])
                    = (shuffledSlots[j], shuffledSlots[i]);
            }

            // ── Bước 8: Respawn – SNAP ngay vào world position đã cache ──────
            // SpawnSingleFoodSnap đã snap position ngay trước khi DOTween chạy
            // → food xuất hiện ĐÚNG VỊ TRÍ, không bị trượt về slot sau
            // layerIndex MỚI theo slot mới → tự động áp visual đúng (màu, scale, collider)
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

                // Layer mới = layer của SLOT mới (không phải layer cũ của food)
                int newLayerIndex = GetLayerIndexOfSlot(newSlot, snap.OwnerTray);

                // SpawnSingleFoodSnap:
                //   1. GetFood từ pool → snap position = anchor.position (world, tức thì)
                //   2. DOScale từ 0 → targetScale (theo layer mới)
                //   3. OnComplete: snap lại 1 lần nữa + Follow(anchor mới)
                // → không có khoảng thời gian food "lơ lửng" rồi trượt về vị trí
                snap.OwnerTray?.SpawnSingleFoodSnap(
                    data: snap.Data,
                    anchor: newSlot,
                    layerIdx: newLayerIndex,
                    neutralContainer: neutralContainer,
                    spawnDelay: delay);
            }

            Debug.Log($"[Shuffle] {snapshots.Count} foods respawned với layer đúng.");
        }

        private int GetLayerIndexOfSlot(Transform slot, FoodTray tray)
        {
            if (tray == null) return 0;
            return tray.GetLayerIndexOfAnchor(slot);
        }

        private struct SlotSnapshot
        {
            public Transform Slot;
            public Vector3 SlotWorldPos;
            public FoodItemData Data;
            public int LayerIndex;
            public FoodTray OwnerTray;
        }
    }
}