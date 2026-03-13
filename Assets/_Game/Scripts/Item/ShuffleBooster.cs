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

        private bool _isShuffling;

        private const float DespawnDuration = 0.12f;
        private const float StaggerDelay = 0.025f;
        private const float SpawnScaleDur = 0.2f;

        public void Initialize(BoosterContext ctx)
        {
            _gridSpawner = ctx.FoodGridSpawner;
            _runner = ctx.CoroutineRunner;
        }

        public bool CanExecute()
        {
            if (_isShuffling) return false;
            if (_gridSpawner == null) return false;
            if (_gridSpawner.GetCellContainer() == null) return false;
            return CountTotalFoods() >= 2;
        }

        public void Execute() => _runner.StartCoroutine(ShuffleRoutine());

        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator ShuffleRoutine()
        {
            _isShuffling = true;

            try
            {
                // BƯỚC 1: Khoá xoay
                _gridSpawner.LockRotation();
                yield return null;

                var allTrays = _gridSpawner.GetCellContainer()
                    .GetComponentsInChildren<FoodTray>(includeInactive: false);

                // BƯỚC 2: Thu thập tất cả food data (spawned + pending)
                var spawnedFoods = _gridSpawner.GetAllActiveFoods()
                    .FindAll(f => f != null && f.OwnerTray != null);

                var allFoodData = new List<FoodItemData>();
                foreach (var food in spawnedFoods)
                    if (food?.Data != null) allFoodData.Add(food.Data);
                foreach (var tray in allTrays)
                    allFoodData.AddRange(tray.GetAllPendingData());

                if (allFoodData.Count < 2)
                {
                    _gridSpawner.UnlockRotation();
                    Debug.LogWarning("[Shuffle] Không đủ food để shuffle.");
                    BoosterManager.Instance?.NotifyBoosterCompleted(BoosterName, consumed: false);
                    yield break;
                }

                // BƯỚC 3: Shuffle food data
                FisherYatesShuffle(allFoodData);

                // BƯỚC 4: Tách layer0 và layer1, shuffle từng layer riêng
                // → random vị trí trong từng layer nhưng layer0 LUÔN được fill trước layer1
                // → đảm bảo không bao giờ có food ở layer1 mà layer0 trống
                var layer0Slots = new List<SlotInfo>();
                var layer1Slots = new List<SlotInfo>();
                foreach (var tray in allTrays)
                {
                    foreach (var anchor in tray.GetLayer0Anchors())
                    {
                        if (anchor == null) continue;
                        layer0Slots.Add(new SlotInfo { Anchor = anchor, LayerIndex = 0, OwnerTray = tray });
                    }
                    foreach (var anchor in tray.GetLayer1Anchors())
                    {
                        if (anchor == null) continue;
                        layer1Slots.Add(new SlotInfo { Anchor = anchor, LayerIndex = 1, OwnerTray = tray });
                    }
                }
                FisherYatesShuffle(layer0Slots);
                FisherYatesShuffle(layer1Slots);
                var allSlots = new List<SlotInfo>(layer0Slots.Count + layer1Slots.Count);
                allSlots.AddRange(layer0Slots);
                allSlots.AddRange(layer1Slots);


                int totalPhysicalSlots = allSlots.Count;
                int spawnCount = Mathf.Min(allFoodData.Count, totalPhysicalSlots);

                // Food fit vừa slot → spawn trực tiếp
                var spawnableData = allFoodData.GetRange(0, spawnCount);

                // Food dư (nhiều food hơn slot vật lý) → vào pending
                var overflowData = allFoodData.Count > spawnCount
                    ? allFoodData.GetRange(spawnCount, allFoodData.Count - spawnCount)
                    : new List<FoodItemData>();

                // BƯỚC 5: Despawn food cũ
                foreach (var food in spawnedFoods)
                    food.GetComponent<SlotFollower>()?.Unfollow();

                yield return null;

                foreach (var food in spawnedFoods)
                {
                    var captured = food;
                    captured.transform
                        .DOScale(Vector3.zero, DespawnDuration)
                        .SetEase(Ease.InBack)
                        .OnComplete(() =>
                        {
                            if (captured != null)
                                PoolManager.Instance.ReturnFood(captured.FoodID, captured.gameObject);
                        });
                }

                yield return new WaitForSeconds(DespawnDuration + 0.05f);

                // BƯỚC 6: Clear stacks + pending
                foreach (var tray in allTrays)
                    tray.ClearStacksAndPending();

                // BƯỚC 7: Redistribute overflow vào pending
                // Overflow cũng được phân bổ ngẫu nhiên qua các tray (không fill tray0 trước)
                if (overflowData.Count > 0)
                {
                    // Shuffle thứ tự tray để overflow không dồn vào tray đầu
                    var trayOrder = new List<FoodTray>(allTrays);
                    FisherYatesShuffle(trayOrder);

                    int overflowIdx = 0;
                    // Pass 1: mỗi tray nhận tối đa GetLayer1Anchors().Count pending
                    for (int t = 0; t < trayOrder.Count && overflowIdx < overflowData.Count; t++)
                    {
                        int maxPending = trayOrder[t].GetLayer1Anchors().Count;
                        for (int k = 0; k < maxPending && overflowIdx < overflowData.Count; k++)
                            trayOrder[t].AddPendingData(overflowData[overflowIdx++]);
                    }

                    // Pass 2: nếu vẫn còn dư thì round-robin tiếp
                    int trayIndex = 0;
                    while (overflowIdx < overflowData.Count)
                    {
                        trayOrder[trayIndex % trayOrder.Count].AddPendingData(overflowData[overflowIdx++]);
                        trayIndex++;
                    }

                    Debug.Log($"[Shuffle] {overflowData.Count} food redistributed vào pending (random tray order).");
                }

                // BƯỚC 8: Spawn food vào slot đã shuffle
                var neutralContainer = _gridSpawner.GetNeutralContainer();
                if (neutralContainer == null)
                {
                    _gridSpawner.UnlockRotation();
                    Debug.LogError("[Shuffle] neutralContainer null!");
                    BoosterManager.Instance?.NotifyBoosterCompleted(BoosterName, consumed: true);
                    yield break;
                }

                for (int i = 0; i < spawnCount; i++)
                {
                    var data = spawnableData[i];
                    var slotInfo = allSlots[i]; // slot đã được shuffle → random
                    if (data?.prefab == null) continue;
                    SpawnWithSnapThenAnimate(data, slotInfo, neutralContainer, i * StaggerDelay);
                }

                // BƯỚC 9: Chờ animation xong → mở khóa
                float lastDelay = (spawnCount - 1) * StaggerDelay;
                yield return new WaitForSeconds(lastDelay + SpawnScaleDur + 0.05f);

                _gridSpawner.UnlockRotation();

                Debug.Log($"[Shuffle] Done. Spawned={spawnCount} | Pending={overflowData.Count} | Total={allFoodData.Count}");
            }
            finally
            {
                _isShuffling = false;
                BoosterManager.Instance?.NotifyBoosterCompleted(BoosterName, consumed: true);
            }
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fisher-Yates shuffle — O(n), unbiased.
        /// </summary>
        private static void FisherYatesShuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ─────────────────────────────────────────────────────────────────────

        private void SpawnWithSnapThenAnimate(
            FoodItemData data, SlotInfo slotInfo,
            Transform neutralContainer, float scaleDelay)
        {
            Vector3 prefabScale = data.prefab.transform.localScale;

            GameObject go = PoolManager.Instance.GetFood(data.foodID, slotInfo.Anchor.position);
            if (go == null) return;

            go.transform.SetParent(neutralContainer, worldPositionStays: true);
            go.transform.position = slotInfo.Anchor.position;
            go.transform.localScale = Vector3.zero;

            FoodItem item = go.GetComponent<FoodItem>();
            if (item == null)
            {
                Debug.LogError($"[Shuffle] '{data.foodName}' thiếu FoodItem!");
                return;
            }

            item.Initialize(data, slotInfo.LayerIndex);
            item.OwnerTray = slotInfo.OwnerTray;
            item.SetAnchorRef(slotInfo.Anchor);

            slotInfo.OwnerTray?.RegisterToStack(item, slotInfo.Anchor, slotInfo.LayerIndex);

            SlotFollower follower = go.GetComponent<SlotFollower>();
            if (follower == null) follower = go.AddComponent<SlotFollower>();
            follower.Follow(slotInfo.Anchor);

            Vector3 targetScale = slotInfo.LayerIndex == 0
                ? prefabScale
                : prefabScale * 0.8f;

            go.transform
                .DOScale(targetScale, SpawnScaleDur)
                .SetDelay(scaleDelay)
                .SetEase(Ease.OutBack)
                .SetUpdate(false);
        }

        // ─────────────────────────────────────────────────────────────────────

        private int CountTotalFoods()
        {
            if (_gridSpawner == null) return 0;
            var cellContainer = _gridSpawner.GetCellContainer();
            if (cellContainer == null) return 0;

            int count = _gridSpawner.GetAllActiveFoods()
                .FindAll(f => f != null && f.OwnerTray != null).Count;

            var allTrays = cellContainer
                .GetComponentsInChildren<FoodTray>(includeInactive: false);
            foreach (var tray in allTrays)
                count += tray.GetAllPendingData().Count;

            return count;
        }

        // ─────────────────────────────────────────────────────────────────────

        private struct SlotInfo
        {
            public Transform Anchor;
            public int LayerIndex;
            public FoodTray OwnerTray;
        }
    }
}