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
        // Thời gian chờ tray ease-out hoàn toàn dừng lại
        // FoodGridSpawner.rotateEaseOutDuration = 0.5f → chờ thêm buffer nhỏ
        private const float WaitForTrayStop = 0.6f;

        public void Initialize(BoosterContext ctx)
        {
            _gridSpawner = ctx.FoodGridSpawner;
            _runner = ctx.CoroutineRunner;
        }

        public bool CanExecute()
        {
            if (_gridSpawner == null) return false;
            // cellContainer chưa assign → game chưa start, không execute
            if (_gridSpawner.GetCellContainer() == null) return false;
            return CountTotalFoods() >= 2;
        }

        public void Execute() => _runner.StartCoroutine(ShuffleRoutine());

        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator ShuffleRoutine()
        {
            // ══════════════════════════════════════════════════════════════════
            // BƯỚC 1: Dừng auto-rotate → chờ tray đứng yên hoàn toàn
            // ══════════════════════════════════════════════════════════════════
            _gridSpawner.NotifyInteraction();
            yield return new WaitForSeconds(WaitForTrayStop);

            // ══════════════════════════════════════════════════════════════════
            // BƯỚC 2: Thu thập TẤT CẢ food data (spawned + pending layer 2)
            // vào một danh sách FoodRecord duy nhất
            // ══════════════════════════════════════════════════════════════════
            var allTrays = _gridSpawner.GetCellContainer()
                .GetComponentsInChildren<FoodTray>(includeInactive: false);

            // Refresh spawned foods sau khi chờ
            var spawnedFoods = _gridSpawner.GetAllActiveFoods();

            // Gom tất cả FoodItemData (spawned + pending)
            var allFoodData = new List<FoodItemData>();

            foreach (var food in spawnedFoods)
                if (food?.Data != null) allFoodData.Add(food.Data);

            foreach (var tray in allTrays)
            {
                // Lấy pending layer 2 từ từng tray (GetPendingFoodsOfType trả về theo foodID)
                // Ta cần lấy ALL pending không lọc theo ID → dùng internal helper
                var pendingAll = tray.GetAllPendingData();
                allFoodData.AddRange(pendingAll);
            }

            if (allFoodData.Count < 2)
            {
                Debug.LogWarning("[Shuffle] Không đủ food để shuffle.");
                yield break;
            }

            // ══════════════════════════════════════════════════════════════════
            // BƯỚC 3: Thu thập TẤT CẢ anchor slots từ tất cả tray
            // (layer0 + layer1) → cache world position TẠI THỜI ĐIỂM NÀY
            // Tray đã đứng yên → positions ổn định
            // ══════════════════════════════════════════════════════════════════
            var allSlots = new List<SlotInfo>();

            foreach (var tray in allTrays)
            {
                var layer0 = tray.GetLayer0Anchors();
                var layer1 = tray.GetLayer1Anchors();

                foreach (var anchor in layer0)
                {
                    if (anchor == null) continue;
                    allSlots.Add(new SlotInfo
                    {
                        Anchor = anchor,
                        WorldPos = anchor.position,   // cache ngay lúc tray đứng yên
                        LayerIndex = 0,
                        OwnerTray = tray,
                    });
                }
                foreach (var anchor in layer1)
                {
                    if (anchor == null) continue;
                    allSlots.Add(new SlotInfo
                    {
                        Anchor = anchor,
                        WorldPos = anchor.position,
                        LayerIndex = 1,
                        OwnerTray = tray,
                    });
                }
            }

            // Chỉ dùng số slot = số food thực tế (phòng trường hợp có slot thừa)
            int count = Mathf.Min(allFoodData.Count, allSlots.Count);
            if (count < 2)
            {
                Debug.LogWarning("[Shuffle] Không đủ slot để shuffle.");
                yield break;
            }

            // ══════════════════════════════════════════════════════════════════
            // BƯỚC 4: Unfollow + Despawn tất cả spawned food
            // ══════════════════════════════════════════════════════════════════
            foreach (var food in spawnedFoods)
                food.GetComponent<SlotFollower>()?.Unfollow();

            yield return null; // chờ LateUpdate xử lý unfollow

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
            // BƯỚC 5: Clear tất cả stacks + pending trong mọi tray
            // (bao gồm cả pending layer 2 vì ta đã gom vào allFoodData rồi)
            // ══════════════════════════════════════════════════════════════════
            foreach (var tray in allTrays)
                tray.ClearStacksAndPending();

            // ══════════════════════════════════════════════════════════════════
            // BƯỚC 6: Fisher-Yates shuffle food data
            // (shuffle DATA, giữ nguyên thứ tự SLOT để layer assignment đúng)
            // ══════════════════════════════════════════════════════════════════
            var shuffledData = new List<FoodItemData>(allFoodData);
            for (int i = shuffledData.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (shuffledData[i], shuffledData[j]) = (shuffledData[j], shuffledData[i]);
            }

            // ══════════════════════════════════════════════════════════════════
            // BƯỚC 7: Respawn với layer đúng + SNAP NGAY vào world position đã cache
            //
            // Cơ chế "snap trước, animate sau":
            //   1. GetFood từ pool
            //   2. go.transform.position = cachedWorldPos  ← NGAY LẬP TỨC (không delay)
            //   3. go.transform.localScale = Vector3.zero
            //   4. DOScale(...).SetDelay(stagger) → chỉ scale animation mới delay
            //   5. OnComplete: Follow(anchor) → bám tray từ đây
            //
            // Kết quả: dù tray đã bắt đầu xoay lại trong lúc delay stagger,
            // food vẫn xuất hiện ĐÚNG VỊ TRÍ vì position đã snap trước animation
            // ══════════════════════════════════════════════════════════════════
            var neutralContainer = _gridSpawner.GetNeutralContainer();
            if (neutralContainer == null)
            {
                Debug.LogError("[Shuffle] neutralContainer null!");
                yield break;
            }

            for (int i = 0; i < count; i++)
            {
                var data = shuffledData[i];
                var slotInfo = allSlots[i];
                float delay = i * StaggerDelay;

                if (data?.prefab == null) continue;

                SpawnWithSnapThenAnimate(
                    data, slotInfo, neutralContainer, delay);
            }

            Debug.Log($"[Shuffle] {count} foods respawned — layer theo slot mới.");
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Spawn food theo đúng quy trình:
        ///   position SNAP ngay → scale animate sau (delay chỉ ảnh hưởng scale)
        ///   → Follow anchor sau khi scale xong → không bao giờ drift
        /// </summary>
        private void SpawnWithSnapThenAnimate(
            FoodItemData data, SlotInfo slotInfo,
            Transform neutralContainer, float scaleDelay)
        {
            Vector3 prefabScale = data.prefab.transform.localScale;

            // ── Lấy food từ pool ──────────────────────────────────────────────
            // Spawn tại cachedWorldPos — nhưng ta sẽ override position ngay sau
            GameObject go = PoolManager.Instance.GetFood(data.foodID, slotInfo.WorldPos);
            if (go == null) return;

            go.transform.SetParent(neutralContainer, worldPositionStays: true);

            // ── SNAP NGAY vào world position đã cache (không dùng DOTween delay) ──
            // Đây là điểm mấu chốt: position được set TRƯỚC khi bất kỳ delay nào
            // → dù tray xoay trong khoảng scaleDelay, food đã ở đúng chỗ
            go.transform.position = slotInfo.WorldPos;
            go.transform.localScale = Vector3.zero; // ẩn đến khi scale-in

            // ── Initialize FoodItem theo layerIndex MỚI ────────────────────────
            // layerIndex mới = layer của SLOT mới → visual/collider đúng
            FoodItem item = go.GetComponent<FoodItem>();
            if (item == null)
            {
                Debug.LogError($"[Shuffle] '{data.foodName}' thiếu FoodItem!");
                return;
            }

            item.Initialize(data, slotInfo.LayerIndex);
            item.OwnerTray = slotInfo.OwnerTray;
            item.SetAnchorRef(slotInfo.Anchor);

            // ── Đăng ký vào stack của tray ────────────────────────────────────
            slotInfo.OwnerTray?.RegisterToStack(item, slotInfo.Anchor, slotInfo.LayerIndex);

            // ── Unfollow trong lúc animate (phòng LateUpdate kéo position) ────
            SlotFollower follower = go.GetComponent<SlotFollower>();
            if (follower == null) follower = go.AddComponent<SlotFollower>();
            follower.Unfollow();

            // ── Scale target theo layer mới ───────────────────────────────────
            // Layer 0 → full size | Layer 1 → 80% + grey (đã set bởi Initialize)
            Vector3 targetScale = slotInfo.LayerIndex == 0
                ? prefabScale
                : prefabScale * 0.8f;

            // ── Scale-in animation (delay stagger KHÔNG ảnh hưởng position) ──
            go.transform
                .DOScale(targetScale, 0.3f)
                .SetDelay(scaleDelay)
                .SetEase(Ease.OutBack)
                .SetUpdate(false)
                .OnComplete(() =>
                {
                    if (go == null) return;

                    // Snap lần 2 sau animation → triệt tiêu drift do tray xoay
                    // trong khoảng scaleDelay
                    go.transform.position = slotInfo.Anchor.position;

                    // Bắt đầu Follow anchor → bám tray từ đây
                    follower.Follow(slotInfo.Anchor);
                });
        }

        // ─────────────────────────────────────────────────────────────────────

        private int CountTotalFoods()
        {
            if (_gridSpawner == null) return 0;

            // cellContainer chưa được tạo (trước khi SpawnGrid chạy) → trả về 0
            // tránh UnassignedReferenceException khi Editor poll CanExecute() lúc start
            var cellContainer = _gridSpawner.GetCellContainer();
            if (cellContainer == null) return 0;

            int count = _gridSpawner.GetAllActiveFoods().Count;

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
            public Vector3 WorldPos;   // cached khi tray đứng yên
            public int LayerIndex;
            public FoodTray OwnerTray;
        }
    }
}