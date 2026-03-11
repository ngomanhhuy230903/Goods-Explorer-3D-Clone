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

        // ── Timing (tổng ~2s) ─────────────────────────────────────────────────
        // Despawn: 0.12s × N food đồng thời + 0.05s buffer ≈ 0.17s
        // Spawn stagger: N × 0.025s + 0.2s scale ≈ tuỳ N food
        // Không cần WaitForTrayStop vì ta LockRotation ngay lập tức
        private const float DespawnDuration = 0.12f;   // giảm từ 0.18 → 0.12
        private const float StaggerDelay = 0.025f;  // giảm từ 0.04 → 0.025
        private const float SpawnScaleDur = 0.2f;    // giảm từ 0.3 → 0.2

        public void Initialize(BoosterContext ctx)
        {
            _gridSpawner = ctx.FoodGridSpawner;
            _runner = ctx.CoroutineRunner;
        }

        public bool CanExecute()
        {
            if (_isShuffling) return false;
            if (_gridSpawner == null) return false;
            // cellContainer chưa assign → game chưa start, không execute
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
                // ══════════════════════════════════════════════════════════════════
                // BƯỚC 1: KHÓA xoay ngay lập tức
                // ══════════════════════════════════════════════════════════════════
                _gridSpawner.LockRotation();
                yield return null;

                var allTrays = _gridSpawner.GetCellContainer()
                    .GetComponentsInChildren<FoodTray>(includeInactive: false);

                // ══════════════════════════════════════════════════════════════════
                // BƯỚC 2: Tách data thành 2 nhóm rõ ràng
                //   • spawnableData  = food sẽ được shuffle vào slot vật lý (layer0+1)
                //   • overflowData   = food vượt quá slot vật lý → giữ làm pending
                //
                // Lý do tách: allSlots chỉ có anchor layer0+layer1 (slot vật lý).
                // Pending layer2+ không có anchor riêng — chúng dùng lại anchor layer1
                // khi được promote. Nếu nhét tất cả vào allSlots sẽ thiếu anchor.
                // ══════════════════════════════════════════════════════════════════
                var spawnedFoods = _gridSpawner.GetAllActiveFoods()
                    .FindAll(f => f != null && f.OwnerTray != null);

                // Đếm tổng slot vật lý để biết bao nhiêu food có thể spawn ngay
                int totalPhysicalSlots = 0;
                foreach (var tray in allTrays)
                    totalPhysicalSlots += tray.GetLayer0Anchors().Count
                                        + tray.GetLayer1Anchors().Count;

                // Gom TẤT CẢ data (spawned + pending mọi layer)
                var allFoodData = new List<FoodItemData>();
                foreach (var food in spawnedFoods)
                    if (food?.Data != null) allFoodData.Add(food.Data);
                foreach (var tray in allTrays)
                    allFoodData.AddRange(tray.GetAllPendingData());

                if (allFoodData.Count < 2)
                {
                    _gridSpawner.UnlockRotation();
                    Debug.LogWarning("[Shuffle] Không đủ food để shuffle.");
                    yield break;
                }

                // Shuffle TOÀN BỘ data trước khi tách
                for (int i = allFoodData.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (allFoodData[i], allFoodData[j]) = (allFoodData[j], allFoodData[i]);
                }

                // Tách: phần đầu → spawn vào slot vật lý | phần sau → pending
                int spawnCount = Mathf.Min(allFoodData.Count, totalPhysicalSlots);
                var spawnableData = allFoodData.GetRange(0, spawnCount);
                var overflowData = allFoodData.Count > spawnCount
                    ? allFoodData.GetRange(spawnCount, allFoodData.Count - spawnCount)
                    : new List<FoodItemData>();

                // ══════════════════════════════════════════════════════════════════
                // BƯỚC 3: Thu thập slot vật lý (layer0 + layer1 của mọi tray)
                // ══════════════════════════════════════════════════════════════════
                var allSlots = new List<SlotInfo>();
                foreach (var tray in allTrays)
                {
                    foreach (var anchor in tray.GetLayer0Anchors())
                    {
                        if (anchor == null) continue;
                        allSlots.Add(new SlotInfo { Anchor = anchor, LayerIndex = 0, OwnerTray = tray });
                    }
                    foreach (var anchor in tray.GetLayer1Anchors())
                    {
                        if (anchor == null) continue;
                        allSlots.Add(new SlotInfo { Anchor = anchor, LayerIndex = 1, OwnerTray = tray });
                    }
                }

                // ══════════════════════════════════════════════════════════════════
                // BƯỚC 4: Unfollow + Despawn tất cả spawned food
                // ══════════════════════════════════════════════════════════════════
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

                // ══════════════════════════════════════════════════════════════════
                // BƯỚC 5: Clear tất cả stacks + pending
                // ══════════════════════════════════════════════════════════════════
                foreach (var tray in allTrays)
                    tray.ClearStacksAndPending();

                // ══════════════════════════════════════════════════════════════════
                // BƯỚC 6: Redistribute overflow vào pending của các tray
                // Phân đều overflow vào layer1 capacity của từng tray (giống FoodTraySpawner)
                // ══════════════════════════════════════════════════════════════════
                if (overflowData.Count > 0)
                {
                    int overflowIdx = 0;
                    // Vòng tròn qua các tray, mỗi tray nhận tối đa layer1Count pending
                    // (layer2 dùng lại slot layer1 khi promote — đúng với thiết kế FoodTray)
                    for (int t = 0; t < allTrays.Length && overflowIdx < overflowData.Count; t++)
                    {
                        int maxPending = allTrays[t].GetLayer1Anchors().Count;
                        for (int k = 0; k < maxPending && overflowIdx < overflowData.Count; k++)
                        {
                            allTrays[t].AddPendingData(overflowData[overflowIdx++]);
                        }
                    }

                    // Nếu vẫn còn thừa (rất hiếm), tiếp tục round-robin
                    int trayIndex = 0;
                    while (overflowIdx < overflowData.Count)
                    {
                        allTrays[trayIndex % allTrays.Length].AddPendingData(overflowData[overflowIdx++]);
                        trayIndex++;
                    }

                    Debug.Log($"[Shuffle] {overflowData.Count} food redistributed vào pending layer2.");
                }

                // ══════════════════════════════════════════════════════════════════
                // BƯỚC 7: Spawn spawnableData vào slot vật lý
                // ══════════════════════════════════════════════════════════════════
                var neutralContainer = _gridSpawner.GetNeutralContainer();
                if (neutralContainer == null)
                {
                    _gridSpawner.UnlockRotation();
                    Debug.LogError("[Shuffle] neutralContainer null!");
                    yield break;
                }

                for (int i = 0; i < spawnCount; i++)
                {
                    var data = spawnableData[i];
                    var slotInfo = allSlots[i];

                    if (data?.prefab == null) continue;

                    SpawnWithSnapThenAnimate(data, slotInfo, neutralContainer, i * StaggerDelay);
                }

                // ══════════════════════════════════════════════════════════════════
                // BƯỚC 8: Mở khóa xoay sau khi animation hoàn tất
                // ══════════════════════════════════════════════════════════════════
                float lastDelay = (spawnCount - 1) * StaggerDelay;
                yield return new WaitForSeconds(lastDelay + SpawnScaleDur + 0.05f);

                _gridSpawner.UnlockRotation();

                Debug.Log($"[Shuffle] Spawned={spawnCount} | Pending={overflowData.Count} | Total={allFoodData.Count}");
            }
            finally
            {
                _isShuffling = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Spawn food theo đúng quy trình:
        ///   1. Snap position ngay = anchor.position hiện tại
        ///   2. Follow(anchor) NGAY LẬP TỨC — SlotFollower chỉ set position,
        ///      không ảnh hưởng scale → food bám tray trong suốt animation
        ///   3. DOScale từ 0 → targetScale (delay stagger chỉ ảnh hưởng scale)
        ///
        /// Kết quả: dù player hay auto-rotate làm tray xoay trong lúc stagger delay,
        /// food luôn ở đúng vị trí anchor vì SlotFollower cập nhật mỗi LateUpdate.
        /// </summary>
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

            // ── Follow NGAY — SlotFollower chỉ set position mỗi LateUpdate ──
            // Scale = 0 nên food vô hình, nhưng position luôn bám anchor
            // → khi scale-in bắt đầu, food xuất hiện đúng vị trí dù tray đã xoay
            SlotFollower follower = go.GetComponent<SlotFollower>();
            if (follower == null) follower = go.AddComponent<SlotFollower>();
            follower.Follow(slotInfo.Anchor);

            Vector3 targetScale = slotInfo.LayerIndex == 0
                ? prefabScale
                : prefabScale * 0.8f;

            // Chỉ scale mới delay — position luôn đúng nhờ SlotFollower
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

            // Chỉ đếm food đang thuộc FoodTray (OwnerTray != null)
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