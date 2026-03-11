using System.Collections.Generic;
using UnityEngine;

namespace FoodMatch.Data
{
    /// <summary>
    /// Cấu hình đầy đủ cho 1 level.
    /// Tạo asset: Click phải > Create > FoodMatch > Level Config
    /// </summary>
    [CreateAssetMenu(
        fileName = "Level_",
        menuName = "FoodMatch/Level Config",
        order = 2)]
    public class LevelConfig : ScriptableObject
    {
        [Header("─── Level Info ──────────────────────")]
        [Tooltip("Số thứ tự level (1-based). Dùng để hiển thị UI và load đúng level.")]
        public int levelIndex = 1;

        [Tooltip("Tên level tùy chỉnh (VD: 'Breakfast Rush'). Để trống = dùng số.")]
        public string levelDisplayName;

        [Header("─── Food Setup ──────────────────────")]
        [Tooltip("Danh sách loại món ăn SẼ XUẤT HIỆN trong level này.")]
        public List<FoodItemData> availableFoods = new List<FoodItemData>();

        [Tooltip("Tổng số món trên khay. PHẢI chia hết cho 3. VD: 9, 12, 15, 18...")]
        [Min(3)]
        public int totalFoodCount = 9;

        [Header("─── Layer Setup (Tầng đồ ăn) ────────")]
        [Tooltip("Số tầng (layer) chồng lên nhau. Min=2, Max=4.")]
        [Range(2, 5)]
        public int layerCount = 2;

        [Tooltip("Số cột trên mỗi hàng của khay đồ ăn.")]
        [Range(3, 6)]
        public int trayColumns = 3;

        [Tooltip("Số hàng của khay đồ ăn.")]
        [Range(1, 5)]
        public int trayRows = 3;

        [Header("─── Order Setup ──────────────────")]
        [Tooltip("Số khách hàng tối đa cùng lúc trên màn hình. Max=4 theo thiết kế.")]
        [Range(1, 4)]
        public int maxActiveOrders = 2;

        [Header("─── Backup Tray ─────────────────────")]
        [Tooltip("Số ô tối đa của khay thừa ban đầu.")]
        [Range(3, 7)]
        public int backupTrayCapacity = 5;

        [Header("─── Timing ──────────────────────────")]
        [Tooltip("Thời gian giới hạn (giây). Set 0 = không giới hạn thời gian.")]
        [Min(0)]
        public float timeLimitSeconds = 0f;
        [Header("─── Obstacles ───────────────────────")]
        [HideInInspector] // ẩn default drawer, CustomEditor sẽ vẽ lại
        [SerializeReference]
        public List<ObstacleData> obstacles = new List<ObstacleData>();

        [HideInInspector] // chỉ dùng trong Editor
        public ObstaclePreset obstaclePreset;
        // ─── Validation ───────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Tự động làm tròn totalFoodCount lên bội số 3 gần nhất
            if (totalFoodCount % 3 != 0)
            {
                totalFoodCount = Mathf.CeilToInt(totalFoodCount / 3f) * 3;
                Debug.LogWarning($"[LevelConfig] Level {levelIndex}: " +
                                 $"totalFoodCount đã được tự động chỉnh thành {totalFoodCount} (bội số 3).");
            }
        }
#endif

        /// <summary>
        /// Trả về tên hiển thị level: dùng levelDisplayName nếu có, không thì dùng số.
        /// </summary>
        public string GetDisplayName()
        {
            return string.IsNullOrEmpty(levelDisplayName)
                ? $"Level {levelIndex}"
                : levelDisplayName;
        }

        /// <summary>
        /// Kiểm tra level config có hợp lệ không trước khi load.
        /// </summary>
        public bool IsValid()
        {
            if (availableFoods == null || availableFoods.Count == 0)
            {
                Debug.LogError($"[LevelConfig] Level {levelIndex}: availableFoods trống!");
                return false;
            }
            if (totalFoodCount % 3 != 0)
            {
                Debug.LogError($"[LevelConfig] Level {levelIndex}: totalFoodCount không chia hết cho 3!");
                return false;
            }

            // Validate từng obstacle
            if (obstacles != null)
            {
                foreach (var o in obstacles)
                {
                    if (o == null) continue;
                    if (!o.IsValid())
                    {
                        Debug.LogError($"[LevelConfig] Level {levelIndex}: " +
                                       $"Obstacle '{o.ObstacleName}' không hợp lệ!");
                        return false;
                    }
                }
            }
            return true;
        }
        // ─── Obstacle Helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Lấy obstacle theo kiểu. Trả về null nếu không tồn tại hoặc disabled.
        /// Dùng: config.GetObstacle<LockObstacleData>()
        /// </summary>
        public T GetObstacle<T>() where T : ObstacleData
        {
            if (obstacles == null) return null;
            foreach (var o in obstacles)
                if (o is T typed && typed.isEnabled) return typed;
            return null;
        }
        /// <summary>Kiểm tra obstacle có tồn tại và được bật không.</summary>
        public bool HasObstacle<T>() where T : ObstacleData => GetObstacle<T>() != null;

    }
}