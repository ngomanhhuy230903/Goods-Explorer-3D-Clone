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
        [SerializeField] private Vector3 lockViewLocalOffset = new Vector3(0f, 1.5f, -0.1f);

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
                if (kv.Value == null) continue;
                kv.Value.HideImmediate();
                Destroy(kv.Value.gameObject);
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

            var allTrays = GetAllTrays();
            if (allTrays.Count == 0)
            {
                Debug.LogWarning("[LockObstacle] OnGridAndFoodReady: không tìm thấy FoodTray!");
                return;
            }

            int countToLock = Mathf.Min(_pendingData.lockedTrayCount, allTrays.Count);
            ShuffleList(allTrays);

            for (int i = 0; i < countToLock; i++)
            {
                FoodTray tray = allTrays[i];
                int hp = _pendingData.GetHpForTray(i);

                LockTrayView view = SpawnLockView(tray);
                view.Setup(hp);

                _lockedTrays[tray] = view;
                _stillLocked.Add(tray);

                Debug.Log($"[LockObstacle] Tray[{tray.TrayID}] bị khóa — HP={hp}");
            }

            _pendingData = null;
            SubscribeEvents(true);
            Debug.Log($"[LockObstacle] Init xong — {countToLock}/{allTrays.Count} trays bị khóa.");
        }

        // ─── Event Handler ────────────────────────────────────────────────────

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
            _stillLocked.Remove(tray);
            Debug.Log($"[LockObstacle] Tray[{tray.TrayID}] đã mở khóa! " +
                      $"Còn {_stillLocked.Count} tray bị khóa.");
            if (_stillLocked.Count == 0)
                Debug.Log("[LockObstacle] Tất cả trays đã được mở khóa!");
        }

        private LockTrayView SpawnLockView(FoodTray tray)
        {
            LockTrayView view;

            if (lockViewPrefab != null)
            {
                // Spawn KHÔNG worldPositionStays để sau đó ta tự tính scale
                view = Instantiate(lockViewPrefab, tray.transform);
            }
            else
            {
                var go = new GameObject("LockView");
                go.transform.SetParent(tray.transform, worldPositionStays: false);
                view = go.AddComponent<LockTrayView>();
                Debug.LogWarning("[LockObstacle] lockViewPrefab chưa gán — dùng fallback.");
            }

            view.transform.localPosition = lockViewLocalOffset;

            // ── Giữ nguyên world scale của prefab ─────────────────────────────
            // Sau Instantiate(prefab, parent) localScale bị nhân với parent lossyScale.
            // Ta tính ngược: localScale = prefabWorldScale / parent.lossyScale
            if (lockViewPrefab != null)
            {
                Vector3 prefabWorldScale = lockViewPrefab.transform.lossyScale;
                Vector3 parentLossy = tray.transform.lossyScale;

                view.transform.localScale = new Vector3(
                    Mathf.Approximately(parentLossy.x, 0f) ? 1f : prefabWorldScale.x / parentLossy.x,
                    Mathf.Approximately(parentLossy.y, 0f) ? 1f : prefabWorldScale.y / parentLossy.y,
                    Mathf.Approximately(parentLossy.z, 0f) ? 1f : prefabWorldScale.z / parentLossy.z
                );
            }

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