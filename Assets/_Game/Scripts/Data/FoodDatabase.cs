using System.Collections.Generic;
using UnityEngine;

namespace FoodMatch.Data
{
    /// <summary>
    /// ScriptableObject chứa toàn bộ FoodItemData của game.
    /// Dùng để lookup food theo ID nhanh chóng (Dictionary cache).
    /// </summary>
    [CreateAssetMenu(
        fileName = "FoodDatabase",
        menuName = "FoodMatch/Food Database",
        order = 0)]
    public class FoodDatabase : ScriptableObject
    {
        [Header("─── All Foods ───────────────────────")]
        [Tooltip("Kéo thả toàn bộ FoodItemData SO vào đây.")]
        public List<FoodItemData> allFoods = new List<FoodItemData>();

        // Cache để lookup O(1) thay vì duyệt List O(n)
        private Dictionary<int, FoodItemData> _foodCache;

        // ─── Initialization ───────────────────────────────────────────────────

        /// <summary>
        /// Khởi tạo cache. Gọi 1 lần khi game bắt đầu.
        /// </summary>
        public void Initialize()
        {
            _foodCache = new Dictionary<int, FoodItemData>(allFoods.Count);

            foreach (var food in allFoods)
            {
                if (food == null) continue;

                if (_foodCache.ContainsKey(food.foodID))
                {
                    Debug.LogWarning($"[FoodDatabase] Trùng foodID: {food.foodID} " +
                                     $"({food.foodName}). Bỏ qua item trùng.");
                    continue;
                }

                _foodCache.Add(food.foodID, food);
            }

            Debug.Log($"[FoodDatabase] Đã khởi tạo cache với {_foodCache.Count} loại món ăn.");
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Lấy FoodItemData theo foodID. Trả về null nếu không tìm thấy.
        /// </summary>
        public FoodItemData GetFoodByID(int foodID)
        {
            // Tự khởi tạo cache nếu chưa có (vd: gọi trước Initialize())
            if (_foodCache == null) Initialize();

            if (_foodCache.TryGetValue(foodID, out FoodItemData result))
                return result;

            Debug.LogError($"[FoodDatabase] Không tìm thấy food với ID: {foodID}");
            return null;
        }

        /// <summary>
        /// Trả về danh sách ngẫu nhiên N món từ allFoods (không trùng).
        /// Dùng để pick món cho level.
        /// </summary>
        public List<FoodItemData> GetRandomFoods(int count)
        {
            if (count > allFoods.Count)
            {
                Debug.LogWarning($"[FoodDatabase] Yêu cầu {count} món nhưng chỉ có " +
                                 $"{allFoods.Count}. Trả về tất cả.");
                return new List<FoodItemData>(allFoods);
            }

            // Fisher-Yates shuffle rồi lấy 'count' phần tử đầu
            var shuffled = new List<FoodItemData>(allFoods);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            return shuffled.GetRange(0, count);
        }

        /// <summary>
        /// Kiểm tra foodID có tồn tại trong database không.
        /// </summary>
        public bool ContainsFood(int foodID)
        {
            if (_foodCache == null) Initialize();
            return _foodCache.ContainsKey(foodID);
        }

        public int TotalFoods => allFoods.Count;

#if UNITY_EDITOR
        [ContextMenu("Validate All Food IDs")]
        private void ValidateAllFoodIDs()
        {
            var seen = new HashSet<int>();
            bool hasError = false;

            foreach (var food in allFoods)
            {
                if (food == null)
                {
                    Debug.LogWarning("[FoodDatabase] Có slot NULL trong allFoods!");
                    hasError = true;
                    continue;
                }
                if (!seen.Add(food.foodID))
                {
                    Debug.LogError($"[FoodDatabase] TRÙNG foodID: {food.foodID} ({food.foodName})");
                    hasError = true;
                }
            }

            if (!hasError)
                Debug.Log($"[FoodDatabase] OK! {allFoods.Count} foods, tất cả ID đều hợp lệ.");
        }
#endif
    }
}