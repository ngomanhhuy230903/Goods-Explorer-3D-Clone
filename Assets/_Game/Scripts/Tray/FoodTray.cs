using System.Collections.Generic;
using UnityEngine;
using FoodMatch.Core;
using FoodMatch.Data;
using FoodMatch.Food;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Khay chính chứa đồ ăn.
    /// Chịu trách nhiệm:
    /// 1. Sinh lưới slot theo LevelConfig
    /// 2. Phân bổ FoodItem ngẫu nhiên vào các slot
    /// 3. Xử lý layer shift khi layer 0 bị lấy hết
    /// </summary>
    public class FoodTray : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── Grid Layout ─────────────────────")]
        [Tooltip("Prefab của TraySlot (1 ô trong lưới)")]
        [SerializeField] private GameObject slotPrefab;

        [Tooltip("Khoảng cách giữa các slot theo chiều ngang")]
        [SerializeField] private float cellWidth = 120f;

        [Tooltip("Khoảng cách giữa các slot theo chiều dọc")]
        [SerializeField] private float cellHeight = 120f;

        [Tooltip("Transform gốc để spawn các slot vào")]
        [SerializeField] private Transform gridRoot;

        // ─── Runtime ──────────────────────────────────────────────────────────
        private LevelConfig _config;

        // Lưu toàn bộ slot theo [row, col]
        private TraySlot[,] _slots;

        // Danh sách tất cả FoodItem đang active (chưa được lấy đi)
        private List<FoodItem> _allActiveItems = new List<FoodItem>();

        // Số item đã giao thành công cho khách
        private int _deliveredCount = 0;

        // Total food cần giao
        private int _totalFoodCount = 0;

        // Sự kiện báo cho GameManager biết tất cả đồ ăn đã được giao
        public System.Action OnAllFoodDelivered;

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Khởi tạo toàn bộ khay theo config của level.
        /// Gọi từ LevelManager sau khi load level.
        /// </summary>
        public void Initialize(LevelConfig config)
        {
            _config = config;
            _totalFoodCount = config.totalFoodCount;
            _deliveredCount = 0;
            _allActiveItems.Clear();

            // Xóa slot cũ nếu có (reset level)
            ClearGrid();

            // Sinh lưới slot
            BuildGrid();

            // Tạo danh sách food ID ngẫu nhiên
            List<int> foodIDList = GenerateFoodIDList();

            // Phân bổ food vào slot
            DistributeFoodToSlots(foodIDList);
        }

        // ─── Step 1: Build Grid ───────────────────────────────────────────────

        private void BuildGrid()
        {
            int rows = _config.trayRows;
            int cols = _config.trayColumns;
            _slots = new TraySlot[rows, cols];

            // Tính offset để căn giữa lưới
            float totalWidth = (cols - 1) * cellWidth;
            float totalHeight = (rows - 1) * cellHeight;
            Vector2 originOffset = new Vector2(-totalWidth / 2f, totalHeight / 2f);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // Tính vị trí local của slot
                    Vector2 localPos = new Vector2(
                        originOffset.x + c * cellWidth,
                        originOffset.y - r * cellHeight
                    );

                    // Spawn slot từ prefab
                    GameObject slotGO = Instantiate(slotPrefab, gridRoot);
                    slotGO.name = $"Slot_{r}_{c}";

                    RectTransform rt = slotGO.GetComponent<RectTransform>();
                    if (rt != null)
                        rt.anchoredPosition = localPos;
                    else
                        slotGO.transform.localPosition = localPos;

                    TraySlot slot = slotGO.GetComponent<TraySlot>();
                    slot.rowIndex = r;
                    slot.columnIndex = c;

                    _slots[r, c] = slot;
                }
            }
        }

        // ─── Step 2: Generate Food ID List ────────────────────────────────────

        /// <summary>
        /// Tạo danh sách food ID đảm bảo:
        /// - Mỗi ID xuất hiện số lần chia hết cho 3
        /// - Tổng = totalFoodCount
        /// - Xáo trộn ngẫu nhiên (Fisher-Yates)
        /// </summary>
        private List<int> GenerateFoodIDList()
        {
            List<int> result = new List<int>();
            List<FoodItemData> foods = _config.availableFoods;

            if (foods == null || foods.Count == 0)
            {
                Debug.LogError("[FoodTray] availableFoods trống!");
                return result;
            }

            int totalNeeded = _totalFoodCount;
            int foodTypeCount = foods.Count;

            // Tính số lần mỗi loại xuất hiện (chia đều, mỗi loại phải là bội 3)
            // Ví dụ: 30 món, 3 loại → mỗi loại 10 lần (10 chia hết cho 3? Không!)
            // → Điều chỉnh: mỗi loại tối thiểu 3 lần, tăng dần
            int basePerType = (totalNeeded / foodTypeCount / 3) * 3;
            if (basePerType < 3) basePerType = 3;

            // Phân bổ
            int assigned = 0;
            for (int i = 0; i < foodTypeCount && assigned < totalNeeded; i++)
            {
                int remaining = totalNeeded - assigned;
                int typesLeft = foodTypeCount - i;

                // Phân bổ cho loại này
                int countForThisType;
                if (i == foodTypeCount - 1)
                {
                    // Loại cuối nhận hết phần còn lại, làm tròn lên bội 3
                    countForThisType = remaining;
                    countForThisType = Mathf.CeilToInt(countForThisType / 3f) * 3;
                }
                else
                {
                    countForThisType = basePerType;
                    // Đảm bảo còn đủ cho các loại sau
                    int maxForThis = remaining - (typesLeft - 1) * 3;
                    countForThisType = Mathf.Min(countForThisType, maxForThis);
                    countForThisType = Mathf.Max(countForThisType, 3);
                    // Làm tròn về bội 3
                    countForThisType = Mathf.CeilToInt(countForThisType / 3f) * 3;
                }

                for (int k = 0; k < countForThisType; k++)
                    result.Add(foods[i].foodID);

                assigned += countForThisType;
            }

            // Nếu assigned > totalNeeded do làm tròn, cắt bớt (nhưng giữ bội 3)
            while (result.Count > totalNeeded)
                result.RemoveAt(result.Count - 1);

            // Fisher-Yates Shuffle
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }

            Debug.Log($"[FoodTray] Generated {result.Count} food IDs " +
                      $"(target: {totalNeeded}).");
            return result;
        }

        // ─── Step 3: Distribute Food To Slots ────────────────────────────────

        /// <summary>
        /// Phân bổ danh sách food ID vào các slot theo từng layer.
        /// Mỗi slot có thể chứa tối đa layerCount FoodItem (1 per layer).
        /// </summary>
        private void DistributeFoodToSlots(List<int> foodIDList)
        {
            int rows = _config.trayRows;
            int cols = _config.trayColumns;
            int layerCount = _config.layerCount;
            int totalSlots = rows * cols;

            // Tạo index list ngẫu nhiên để phân bổ không theo thứ tự
            List<int> slotIndices = new List<int>();
            for (int i = 0; i < totalSlots * layerCount; i++)
                slotIndices.Add(i);

            // Fisher-Yates shuffle slotIndices
            for (int i = slotIndices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (slotIndices[i], slotIndices[j]) = (slotIndices[j], slotIndices[i]);
            }

            // Map: slotIndex → FoodItem per layer
            // Key: row * cols + col, Value: mảng item theo layer
            Dictionary<int, FoodItem[]> slotItemMap
                = new Dictionary<int, FoodItem[]>();

            // Khởi tạo map
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    slotItemMap[r * cols + c] = new FoodItem[layerCount];

            // Phân bổ từng food ID vào slot+layer ngẫu nhiên
            int foodIndex = 0;
            foreach (int flatIndex in slotIndices)
            {
                if (foodIndex >= foodIDList.Count) break;

                int slotFlat = flatIndex / layerCount;
                int layerIdx = flatIndex % layerCount;

                // Slot này layer này chưa có item
                if (slotItemMap[slotFlat][layerIdx] == null)
                {
                    int foodID = foodIDList[foodIndex];
                    FoodItemData data = GetFoodDataByID(foodID);

                    if (data != null)
                    {
                        // Lấy từ pool
                        GameObject foodGO = PoolManager.Instance.GetFood(
                            foodID,
                            _slots[slotFlat / cols, slotFlat % cols].transform.position
                        );

                        if (foodGO != null)
                        {
                            FoodItem item = foodGO.GetComponent<FoodItem>();
                            item.Initialize(data, layerIdx);

                            // Set parent về đúng slot transform
                            Transform slotTf = _slots[slotFlat / cols,
                                                       slotFlat % cols].transform;
                            foodGO.transform.SetParent(slotTf, false);
                            foodGO.transform.localPosition = Vector3.zero;

                            slotItemMap[slotFlat][layerIdx] = item;
                            _allActiveItems.Add(item);
                            foodIndex++;
                        }
                    }
                }
            }

            // Truyền mảng item vào từng TraySlot để khởi tạo
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int key = r * cols + c;
                    _slots[r, c].Initialize(slotItemMap[key]);
                }
            }

            Debug.Log($"[FoodTray] Đã phân bổ {foodIndex} items vào {totalSlots} slots " +
                      $"x {layerCount} layers.");
        }

        // ─── Step 4: Player Interaction ───────────────────────────────────────

        /// <summary>
        /// Gọi khi player chạm vào 1 FoodItem.
        /// Trả về item nếu có thể tương tác, null nếu không.
        /// </summary>
        public FoodItem TrySelectItem(FoodItem item)
        {
            if (item == null || item.OwnerSlot == null) return null;

            TraySlot slot = item.OwnerSlot;

            // Chỉ cho tương tác layer 0
            if (slot.TopItem != item)
            {
                // Layer 1 trở xuống → nảy bounce
                item.PlayLockedBounce();
                return null;
            }

            // Lấy item ra khỏi slot
            FoodItem popped = slot.PopTopItem();
            _allActiveItems.Remove(popped);

            // Kiểm tra win
            _deliveredCount++;
            if (_deliveredCount >= _totalFoodCount)
                OnAllFoodDelivered?.Invoke();

            return popped;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private FoodItemData GetFoodDataByID(int id)
        {
            foreach (var food in _config.availableFoods)
                if (food.foodID == id) return food;
            return null;
        }

        /// <summary>
        /// Xóa toàn bộ slot và item hiện có (dùng khi reset level).
        /// </summary>
        private void ClearGrid()
        {
            if (gridRoot == null) return;
            foreach (Transform child in gridRoot)
                Destroy(child.gameObject);

            _allActiveItems.Clear();
            _slots = null;
        }

        /// <summary>
        /// Lấy danh sách tất cả FoodItem đang ở layer 0 (có thể tương tác).
        /// Dùng cho Magnet Item.
        /// </summary>
        public List<FoodItem> GetAllInteractableItems()
        {
            var result = new List<FoodItem>();
            if (_slots == null) return result;

            int rows = _config.trayRows;
            int cols = _config.trayColumns;

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    var top = _slots[r, c]?.TopItem;
                    if (top != null) result.Add(top);
                }

            return result;
        }

        /// <summary>
        /// Đảo ngẫu nhiên vị trí các item đang ở layer 0.
        /// Dùng cho Shuffle Item.
        /// </summary>
        public void ShuffleTopLayer()
        {
            List<FoodItem> topItems = GetAllInteractableItems();
            if (topItems.Count < 2) return;

            // Lưu positions
            List<Transform> parents = new List<Transform>();
            foreach (var item in topItems)
                parents.Add(item.transform.parent);

            // Fisher-Yates shuffle parents
            for (int i = parents.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (parents[i], parents[j]) = (parents[j], parents[i]);
            }

            // Gán lại parent (item đổi chỗ cho nhau)
            for (int i = 0; i < topItems.Count; i++)
            {
                topItems[i].transform.SetParent(parents[i], false);
                topItems[i].transform.localPosition = Vector3.zero;
            }
        }
    }
}