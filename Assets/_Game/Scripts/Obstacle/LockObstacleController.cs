// LockObstacleController.cs
using System.Collections.Generic;
using UnityEngine;
using FoodMatch.Data;
using FoodMatch.Tray;
using FoodMatch.Order;
using FoodMatch.Core;

namespace FoodMatch.Obstacle
{
    public class LockObstacleController : ObstacleController<LockObstacleData>
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Lock View ────────────────────────")]
        [SerializeField] private LockTrayView lockViewPrefab;

        [Tooltip("Offset world so với vị trí FoodTray (dùng để đặt icon phía trên tray).")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.3f, -0.1f);

        // ─── Runtime ──────────────────────────────────────────────────────────

        private readonly Dictionary<FoodTray, LockTrayView> _lockedTrays = new();
        private readonly List<FoodTray> _stillLocked = new();

        private LockObstacleData _pendingData;
        private FoodGridSpawner _gridSpawner;
        private bool _isListening = false;

        // ─── ObstacleController ───────────────────────────────────────────────

        protected override void OnInitialize(LockObstacleData data)
        {
            _lockedTrays.Clear();
            _stillLocked.Clear();

            _gridSpawner = Object.FindObjectOfType<FoodGridSpawner>();
            if (_gridSpawner == null)
            {
                Debug.LogError("[LockObstacle] Không tìm thấy FoodGridSpawner!");
                return;
            }

            _pendingData = data;
            _gridSpawner.OnSpawnComplete += OnGridAndFoodReady;
            Debug.Log("[LockObstacle] Đang chờ FoodGridSpawner.OnSpawnComplete...");
        }

        protected override void OnReset()
        {
            if (_gridSpawner != null)
                _gridSpawner.OnSpawnComplete -= OnGridAndFoodReady;
            _gridSpawner = null;
            _pendingData = null;

            SubscribeEvents(false);

            foreach (var kv in _lockedTrays)
            {
                kv.Key?.SetLocked(false);
                if (kv.Value != null)
                {
                    kv.Value.StopFollowing();
                    Object.Destroy(kv.Value.gameObject);
                }
            }

            _lockedTrays.Clear();
            _stillLocked.Clear();
            Debug.Log("[LockObstacle] Reset — tất cả khóa đã gỡ.");
        }

        // ─── Deferred Init ────────────────────────────────────────────────────

        private void OnGridAndFoodReady()
        {
            if (_gridSpawner != null)
                _gridSpawner.OnSpawnComplete -= OnGridAndFoodReady;

            if (_pendingData == null) return;

            // Container ngang hàng với food — không bị ảnh hưởng bởi FoodTray scale
            Transform container = _gridSpawner.GetNeutralContainer();
            if (container == null)
            {
                Debug.LogWarning("[LockObstacle] NeutralContainer null, dùng gridSpawner transform.");
                container = _gridSpawner.transform;
            }

            var allTrays = GetAllTrays();
            if (allTrays.Count == 0)
            {
                Debug.LogWarning("[LockObstacle] Không tìm thấy FoodTray nào!");
                return;
            }

            int countToLock = Mathf.Min(_pendingData.lockedTrayCount, allTrays.Count);
            ShuffleList(allTrays);

            for (int i = 0; i < countToLock; i++)
            {
                FoodTray tray = allTrays[i];
                int hp = _pendingData.GetHpForTray(i);

                tray.SetLocked(true);

                // Lấy anchor Slot 1-2 (layer0, index 1) làm điểm spawn
                // Fallback: index 0 nếu không có index 1, fallback tiếp: tray.transform
                var layer0 = tray.GetLayer0Anchors();
                Transform anchorTarget = layer0.Count > 1 ? layer0[1]
                                       : layer0.Count > 0 ? layer0[0]
                                       : tray.transform;

                LockTrayView view = SpawnLockView(anchorTarget, container);
                view.Setup(hp);

                _lockedTrays[tray] = view;
                _stillLocked.Add(tray);

                Debug.Log($"[LockObstacle] Tray[{tray.TrayID}] bị khóa — HP={hp} | anchor={anchorTarget.name}");
            }

            _pendingData = null;
            SubscribeEvents(true);
            Debug.Log($"[LockObstacle] Init xong — {countToLock}/{allTrays.Count} trays bị khóa.");
        }

        // ─── Order Event ──────────────────────────────────────────────────────

        private void HandleOrderCompleted(int _)
        {
            if (_stillLocked.Count == 0) return;

            int idx = Random.Range(0, _stillLocked.Count);
            FoodTray target = _stillLocked[idx];

            if (!_lockedTrays.TryGetValue(target, out LockTrayView view)) return;

            bool justUnlocked = view.TakeHit();
            Debug.Log($"[LockObstacle] Tray[{target.TrayID}] -1 HP " +
                      $"→ còn {view.CurrentHp} HP{(justUnlocked ? " → UNLOCK!" : "")}");

            if (justUnlocked)
                UnlockTray(target);
        }

        // ─── Private Helpers ──────────────────────────────────────────────────

        private void UnlockTray(FoodTray tray)
        {
            tray.SetLocked(false);
            _stillLocked.Remove(tray);
            Debug.Log($"[LockObstacle] Tray[{tray.TrayID}] đã mở khóa! " +
                      $"Còn {_stillLocked.Count} tray bị khóa.");
            if (_stillLocked.Count == 0)
                Debug.Log("[LockObstacle] Tất cả trays đã được mở khóa!");
        }

        /// <summary>
        /// Spawn LockView vào neutralContainer (KHÔNG phải child của FoodTray).
        /// Scale giữ nguyên 100% như prefab gốc — không bị ảnh hưởng bởi FoodTray hierarchy.
        /// Follow theo anchor (Slot 1-2 của layer0) để bám đúng vị trí khi tray xoay.
        /// </summary>
        private LockTrayView SpawnLockView(Transform anchor, Transform container)
        {
            LockTrayView view;

            Vector3 spawnPos = anchor.position + worldOffset;

            if (lockViewPrefab != null)
            {
                view = Object.Instantiate(lockViewPrefab, spawnPos, Quaternion.identity, container);
            }
            else
            {
                var go = new GameObject("LockView");
                go.transform.SetParent(container, false);
                go.transform.position = spawnPos;
                view = go.AddComponent<LockTrayView>();
                Debug.LogWarning("[LockObstacle] lockViewPrefab chưa gán — dùng fallback.");
            }

            // Follow anchor (Slot 1-2) — bám theo đúng slot khi grid xoay
            view.Follow(anchor, worldOffset);
            return view;
        }

        private void SubscribeEvents(bool subscribe)
        {
            if (subscribe && !_isListening)
            {
                EventBus.OnOrderCompleted += HandleOrderCompleted;
                _isListening = true;
            }
            else if (!subscribe && _isListening)
            {
                EventBus.OnOrderCompleted -= HandleOrderCompleted;
                _isListening = false;
            }
        }

        private static List<FoodTray> GetAllTrays() =>
            new List<FoodTray>(Object.FindObjectsOfType<FoodTray>(includeInactive: false));

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}