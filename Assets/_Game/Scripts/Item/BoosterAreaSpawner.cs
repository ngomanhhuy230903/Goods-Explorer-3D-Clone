using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Managers;
using FoodMatch.Data;

namespace FoodMatch.Items
{
    /// <summary>
    /// Gán vào BottomArea.
    /// Khi GameState chuyển sang Play → spawn BoosterSlot prefabs vào area.
    /// Layout căn giữa theo trục X, spacing đều — giống BackupTraySpawner.
    ///
    /// Chỉ spawn booster đã unlock theo SaveManager.CurrentLevel.
    /// Booster chưa unlock vẫn spawn nhưng hiện LockOverlay.
    /// </summary>
    public class BoosterAreaSpawner : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("─── Data ────────────────────────────")]
        [SerializeField] private BoosterDatabase database;

        [Header("─── Prefab ──────────────────────────")]
        [SerializeField] private BoosterSlotView slotPrefab;

        [Header("─── Layout ──────────────────────────")]
        [Tooltip("RectTransform chứa các slot (chính là BottomArea hoặc con của nó).")]
        [SerializeField] private RectTransform slotContainer;

        [Tooltip("Khoảng cách giữa các slot theo trục X (px).")]
        [SerializeField] private float slotSpacingX = 160f;

        [Tooltip("Offset Y so với pivot của slotContainer.")]
        [SerializeField] private float slotOffsetY = 0f;

        [Header("─── Spawn Mode ──────────────────────")]
        [Tooltip("True = chỉ spawn booster đã unlock. False = spawn tất cả (locked hiện overlay).")]
        [SerializeField] private bool spawnUnlockedOnly = false;
        private bool _hasSpawnedThisLevel = false;
        [Header("─── Animation ────────────────────────")]
        [SerializeField] private float staggerDelay = 0.06f;
        [SerializeField] private float scaleInDuration = 0.25f;

        // ── Runtime ───────────────────────────────────────────────────────────
        private readonly List<BoosterSlotView> _activeSlots = new();

        // ── Unity ─────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            GameManager.OnGameStateChanged += HandleGameStateChanged;
            EventBus.OnBoosterUnlocked += HandleBoosterUnlocked;
            EventBus.OnBoosterActivated += HandleBoosterActivated;
        }

        private void OnDisable()
        {
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
            EventBus.OnBoosterUnlocked -= HandleBoosterUnlocked;
            EventBus.OnBoosterActivated -= HandleBoosterActivated;
        }

        // ── State Handler ─────────────────────────────────────────────────────

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.LoadLevel:
                    // Level mới hoàn toàn → reset flag + clear UI cũ
                    _hasSpawnedThisLevel = false;
                    ClearAll();
                    break;

                case GameState.Play:
                    // Chỉ spawn nếu chưa spawn trong level này
                    // Revive: Play lại nhưng _hasSpawnedThisLevel vẫn true → skip
                    if (!_hasSpawnedThisLevel)
                    {
                        SpawnAll();
                        _hasSpawnedThisLevel = true;
                    }
                    break;

                case GameState.Win:
                    // Win → level kết thúc, dọn dẹp + reset flag
                    ClearAll();
                    _hasSpawnedThisLevel = false;
                    break;

                case GameState.Lose:
                    // KHÔNG reset flag, KHÔNG ClearAll
                    // Booster vẫn hiển thị, Revive sẽ Play tiếp mà không spawn lại
                    break;
            }
        }

        // ── Spawn ─────────────────────────────────────────────────────────────

        private void SpawnAll()
        {
            ClearAll();

            if (database == null || slotPrefab == null || slotContainer == null)
            {
                Debug.LogError("[BoosterAreaSpawner] Thiếu database/prefab/container!");
                return;
            }

            var toSpawn = new List<BoosterData>();
            int currentLevel = SaveManager.CurrentLevel;

            foreach (var data in database.Boosters)
            {
                if (spawnUnlockedOnly && !data.IsUnlocked(currentLevel)) continue;
                toSpawn.Add(data);
            }

            if (toSpawn.Count == 0) return;

            for (int i = 0; i < toSpawn.Count; i++)
            {
                var slot = Instantiate(slotPrefab, slotContainer);
                slot.name = $"BoosterSlot_{toSpawn[i].boosterName}";

                var rt = slot.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = CalculateSlotPos(i, toSpawn.Count);

                slot.Bind(toSpawn[i]);
                _activeSlots.Add(slot);

                slot.transform.localScale = Vector3.zero;
                int captured = i;
                DOVirtual.DelayedCall(captured * staggerDelay, () =>
                {
                    if (slot != null)
                        slot.transform
                            .DOScale(Vector3.one, scaleInDuration)
                            .SetEase(Ease.OutBack);
                });
            }

            Debug.Log($"[BoosterAreaSpawner] Spawned {_activeSlots.Count} booster slots.");
        }

        private void ClearAll()
        {
            foreach (var slot in _activeSlots)
                if (slot != null) Destroy(slot.gameObject);
            _activeSlots.Clear();
        }

        // ── Layout ────────────────────────────────────────────────────────────

        private Vector2 CalculateSlotPos(int index, int totalCount)
        {
            float totalWidth = (totalCount - 1) * slotSpacingX;
            float startX = -totalWidth / 2f;
            return new Vector2(startX + index * slotSpacingX, slotOffsetY);
        }

        // ── Event Handler ─────────────────────────────────────────────────────

        /// <summary>
        /// Fire từ BoosterManager.NotifyBoosterCompleted() — tức là SAU KHI
        /// booster đã thực hiện xong. Chỉ refresh quantity + button state của slot đó.
        /// </summary>
        private void HandleBoosterActivated(string boosterName)
        {
            if (database?.GetByName(boosterName) == null) return;

            foreach (var slot in _activeSlots)
            {
                if (slot == null) continue;
                if (slot.name == $"BoosterSlot_{boosterName}")
                {
                    slot.RefreshQuantity(); // ← gọi RefreshQuantity thay vì OnBoosterCompleted
                    break;
                }
            }
        }

        /// <summary>
        /// Khi lên level unlock booster mới → Bind lại slot tương ứng.
        /// </summary>
        private void HandleBoosterUnlocked(string boosterName)
        {
            var data = database?.GetByName(boosterName);
            if (data == null) return;

            foreach (var slot in _activeSlots)
            {
                if (slot == null) continue;
                if (slot.name == $"BoosterSlot_{boosterName}")
                {
                    slot.Bind(data);

                    slot.transform.DOKill();
                    slot.transform
                        .DOScale(Vector3.one * 1.2f, 0.2f).SetEase(Ease.OutBack)
                        .OnComplete(() => slot.transform.DOScale(Vector3.one, 0.15f));
                    break;
                }
            }
        }
    }
}