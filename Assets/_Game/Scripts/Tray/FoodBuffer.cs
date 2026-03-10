using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Food;

namespace FoodMatch.Tray
{
    public class FoodBuffer : MonoBehaviour
    {
        public static FoodBuffer Instance { get; private set; }

        [Header("─── Layout ──────────────────────────")]
        [SerializeField] private RectTransform bufferContainer;
        [SerializeField] private float spacingX = 90f;
        [SerializeField] private float bufferFoodScale = 0.6f;

        [Header("─── Debug ───────────────────────────")]
        [SerializeField] private bool showDebugLog = true;

        private readonly Dictionary<int, Queue<FoodItem>> _bufferByType
            = new Dictionary<int, Queue<FoodItem>>();
        private readonly List<FoodItem> _allFoods = new List<FoodItem>();

        public bool HasFood => _allFoods.Count > 0;
        public int TotalCount => _allFoods.Count;

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable() => EventBus.OnNewOrderActive += HandleNewOrderActive;
        private void OnDisable() => EventBus.OnNewOrderActive -= HandleNewOrderActive;

        // ─── Public API ───────────────────────────────────────────────────────

        public void AddFood(FoodItem food)
        {
            if (food == null) return;

            int id = food.FoodID;
            if (!_bufferByType.ContainsKey(id))
                _bufferByType[id] = new Queue<FoodItem>();

            _bufferByType[id].Enqueue(food);
            _allFoods.Add(food);

            food.transform.SetParent(bufferContainer, worldPositionStays: true);

            // Scale nhỏ lại cho phù hợp buffer
            food.transform
                .DOScale(food.Data.prefab.transform.localScale * bufferFoodScale, 0.2f)
                .SetEase(Ease.OutBack);

            // Tắt collider — không cho tap trực tiếp
            var col = food.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            RecalculateLayout();
            Log($"AddFood: foodID={id} | total={_allFoods.Count}");
        }

        public FoodItem TakeFood(int foodID)
        {
            if (!_bufferByType.TryGetValue(foodID, out var queue) || queue.Count == 0)
                return null;

            var food = queue.Dequeue();
            _allFoods.Remove(food);

            if (queue.Count == 0)
                _bufferByType.Remove(foodID);

            RecalculateLayout();
            Log($"TakeFood: foodID={foodID} | còn={_allFoods.Count}");
            return food;
        }

        public bool HasFoodOfType(int foodID)
            => _bufferByType.TryGetValue(foodID, out var q) && q.Count > 0;

        public void ClearAll()
        {
            foreach (var food in _allFoods)
                if (food != null)
                    PoolManager.Instance.ReturnFood(food.FoodID, food.gameObject);
            _allFoods.Clear();
            _bufferByType.Clear();
            Log("ClearAll.");
        }

        // ─── Observer ─────────────────────────────────────────────────────────
        private void HandleNewOrderActive(int foodID)
        {
            if (!HasFoodOfType(foodID)) return;

            Log($"HandleNewOrderActive: foodID={foodID} → raise BufferFoodReady");

            // Raise 1 lần — FoodFlowController.SendBufferFoodCoroutine
            // sẽ tự loop lấy hết tất cả food phù hợp
            EventBus.RaiseBufferFoodReady(foodID);
        }

        // ─── Layout ───────────────────────────────────────────────────────────

        private void RecalculateLayout()
        {
            if (_allFoods.Count == 0) return;

            float totalWidth = (_allFoods.Count - 1) * spacingX;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < _allFoods.Count; i++)
            {
                if (_allFoods[i] == null) continue;
                _allFoods[i].transform
                    .DOLocalMove(new Vector3(startX + i * spacingX, 0f, 0f), 0.25f)
                    .SetEase(Ease.OutCubic);
            }
        }

        private void Log(string msg)
        {
            if (showDebugLog) Debug.Log($"[FoodBuffer] {msg}");
        }
    }
}