#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using FoodMatch.Data;

namespace FoodMatch.Editor
{
    public class LevelGeneratorEditor : EditorWindow
    {
        // ─── Config ───────────────────────────────────────────────────────────
        private string _savePath = "Assets/_Game/ScriptableObjects/Levels";
        private LevelDatabase _database = null;
        private FoodDatabase _foodDb = null;
        private int _levelCount = 20;
        private bool _clearExisting = false;
        private int _randomSeed = 12345;
        private bool _useRandomSeed = true;

        // ── Phải khớp với FoodTray.anchorsPerLayer trong scene ──
        // Mỗi row chứa được 3 food, mỗi layer nhân thêm 3 food/row
        private const int ANCHORS_PER_LAYER = 3;

        // ─── Giới hạn tham số thiết kế ────────────────────────────────────────
        // Grid size: từ nhỏ nhất đến lớn nhất
        private const int MIN_COLS = 4; private const int MAX_COLS = 6;
        private const int MIN_ROWS = 2; private const int MAX_ROWS = 5;
        private const int MIN_LAYERS = 2; private const int MAX_LAYERS = 5;
        private const int MIN_FOOD_TYPES = 3;
        // MAX_FOOD_TYPES lấy từ _foodDb.allFoods.Count (tối đa 9)

        private Vector2 _scroll;

        [MenuItem("FoodMatch/Level Generator")]
        public static void OpenWindow()
        {
            var window = GetWindow<LevelGeneratorEditor>("Level Generator");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(10);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("🍔 FoodMatch Level Generator", headerStyle);
            EditorGUILayout.Space(5);
            DrawHorizontalLine();
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            _database = (LevelDatabase)EditorGUILayout.ObjectField("Level Database", _database, typeof(LevelDatabase), false);
            _foodDb = (FoodDatabase)EditorGUILayout.ObjectField("Food Database", _foodDb, typeof(FoodDatabase), false);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            _savePath = EditorGUILayout.TextField("Save Path", _savePath);
            _levelCount = EditorGUILayout.IntSlider("Số levels cần tạo", _levelCount, 1, 50);
            _clearExisting = EditorGUILayout.Toggle(
                new GUIContent("Xóa levels cũ trước", "Xóa file cũ trước khi tạo mới"),
                _clearExisting);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            _useRandomSeed = EditorGUILayout.Toggle(
                new GUIContent("Random food theo seed", "Mỗi level chọn ngẫu nhiên tập food từ DB thay vì luôn lấy từ đầu"),
                _useRandomSeed, GUILayout.Width(280));
            GUI.enabled = _useRandomSeed;
            _randomSeed = EditorGUILayout.IntField(_randomSeed);
            if (GUILayout.Button("🎲", GUILayout.Width(30)))
                _randomSeed = Random.Range(1, 99999);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                $"ANCHORS_PER_LAYER = {ANCHORS_PER_LAYER}  " +
                $"(mỗi row chứa {ANCHORS_PER_LAYER} food, nhân theo layers)\n" +
                $"Capacity = cols × rows × layers × {ANCHORS_PER_LAYER}\n" +
                $"totalFood chia hết cho (foodTypes × 3)\n" +
                $"🎲 Random food: mỗi level chọn ngẫu nhiên {MIN_FOOD_TYPES}–9 loại từ DB, " +
                $"level dùng hết 9 loại vẫn random thứ tự để đa dạng",
                MessageType.Info);

            EditorGUILayout.Space(10);
            DrawHorizontalLine();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Preview thông số (tự động theo số level):", EditorStyles.boldLabel);
            DrawDynamicPreviews();

            EditorGUILayout.Space(10);
            DrawHorizontalLine();
            EditorGUILayout.Space(10);

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("✅  TẠO " + _levelCount + " LEVELS", GUILayout.Height(40)))
                GenerateLevels();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            GUI.backgroundColor = new Color(0.9f, 0.5f, 0.3f);
            if (GUILayout.Button("🔄  Tự động tìm Database assets", GUILayout.Height(30)))
                AutoFindDatabases();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);
            EditorGUILayout.EndScrollView();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  THUẬT TOÁN NỘI SUY — tất cả tham số tăng đều theo t = level/total
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Nội suy tuyến tính từ min→max theo vị trí level trong tổng số level được gen.
        /// t = 0 (level đầu) → giá trị min
        /// t = 1 (level cuối) → giá trị max
        ///
        /// Ví dụ: gen 50 levels → tham số tăng rất chậm (step nhỏ)
        ///        gen 10 levels → tham số tăng nhanh (step lớn)
        /// </summary>
        private int LerpInt(int minVal, int maxVal, int levelIndex, int totalLevels)
        {
            if (totalLevels <= 1) return maxVal;
            float t = (float)(levelIndex - 1) / (totalLevels - 1);
            return Mathf.RoundToInt(Mathf.Lerp(minVal, maxVal, t));
        }

        /// <summary>
        /// Tính toán tất cả tham số cho một level dựa trên vị trí tương đối của nó.
        ///
        /// Quy trình:
        ///   1. Nội suy cols, rows, layers, foodTypes theo t
        ///   2. Tính gridCapacity = cols × rows × layers × ANCHORS_PER_LAYER
        ///   3. totalFood = floor(capacity / divisor) × divisor
        ///      trong đó divisor = foodTypes × 3
        ///      → đảm bảo totalFood chia hết cho (foodTypes × 3) VÀ ≤ capacity
        /// </summary>
        private void GetLevelStats(int levelIndex, int totalLevels, int maxDbFoods,
            out int totalFood, out int layers, out int cols, out int rows,
            out int customers, out int foodTypes)
        {
            int maxFoodTypes = Mathf.Min(maxDbFoods, 9);

            // ── 1. Nội suy tham số grid theo vị trí level ─────────────────────
            cols = LerpInt(MIN_COLS, MAX_COLS, levelIndex, totalLevels);
            rows = LerpInt(MIN_ROWS, MAX_ROWS, levelIndex, totalLevels);
            layers = LerpInt(MIN_LAYERS, MAX_LAYERS, levelIndex, totalLevels);
            foodTypes = LerpInt(MIN_FOOD_TYPES, maxFoodTypes, levelIndex, totalLevels);

            // ── 2. Customers dựa theo tỉ lệ level ────────────────────────────
            float t = totalLevels <= 1 ? 1f : (float)(levelIndex - 1) / (totalLevels - 1);
            customers = t < 0.4f ? 1 : 2;

            // ── 3. Grid capacity thực tế ──────────────────────────────────────
            int gridCapacity = cols * rows * layers * ANCHORS_PER_LAYER;

            // ── 4. totalFood = làm tròn XUỐNG capacity về bội số (foodTypes × 3) ──
            // Đảm bảo: totalFood ≤ gridCapacity và totalFood % (foodTypes × 3) == 0
            int divisor = foodTypes * 3;
            totalFood = (gridCapacity / divisor) * divisor;

            // Đảm bảo tối thiểu 1 bộ đầy đủ
            if (totalFood < divisor)
                totalFood = divisor;

            // ── 5. Nếu totalFood vượt capacity (edge case) → fallback ─────────
            // Điều này không nên xảy ra với logic floor ở trên, nhưng phòng ngừa
            while (totalFood > gridCapacity && totalFood >= divisor)
                totalFood -= divisor;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  PREVIEW
        // ═════════════════════════════════════════════════════════════════════

        private void DrawDynamicPreviews()
        {
            int maxFoods = _foodDb != null ? _foodDb.allFoods.Count : 9;
            int n = _levelCount;
            if (n < 1) n = 1;

            // Chia 3 tier dựa trên _levelCount
            int tier1End = Mathf.Max(1, n / 3);
            int tier2Start = tier1End + 1;
            int tier2End = Mathf.Max(tier1End, 2 * n / 3);
            int tier3Start = tier2End + 1;

            DrawTierPreview("🟢 Dễ", 1, tier1End, maxFoods, n);
            if (tier2Start <= tier2End)
                DrawTierPreview("🟡 T.Bình", tier2Start, tier2End, maxFoods, n);
            if (tier3Start <= n)
                DrawTierPreview("🔴 Khó", tier3Start, n, maxFoods, n);
        }

        private void DrawTierPreview(string label, int lMin, int lMax, int maxFoods, int totalLevels)
        {
            GetLevelStats(lMin, totalLevels, maxFoods,
                out int fMin, out int layMin, out int cMin, out int rMin, out int cuMin, out int tMin);
            GetLevelStats(lMax, totalLevels, maxFoods,
                out int fMax, out int layMax, out int cMax, out int rMax, out int cuMax, out int tMax);

            int capMin = cMin * rMin * layMin * ANCHORS_PER_LAYER;
            int capMax = cMax * rMax * layMax * ANCHORS_PER_LAYER;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{label}  (Lv {lMin}–{lMax})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• Grid: {cMin}×{rMin} → {cMax}×{rMax}  |  Layers: {layMin} → {layMax}");
            EditorGUILayout.LabelField($"• Capacity: {capMin} → {capMax}  |  Loại food: {tMin} → {tMax}");
            EditorGUILayout.LabelField($"• Total food: {fMin} → {fMax}  (food/type: {fMin / tMin} → {fMax / tMax})");
            EditorGUILayout.LabelField($"• Khách cùng lúc: {cuMin} → {cuMax}");
            EditorGUILayout.EndVertical();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  GENERATE
        // ═════════════════════════════════════════════════════════════════════

        private void GenerateLevels()
        {
            if (_database == null || _foodDb == null || _foodDb.allFoods.Count == 0)
            {
                EditorUtility.DisplayDialog("Lỗi", "Kiểm tra lại Database của bạn!", "OK");
                return;
            }

            if (!Directory.Exists(_savePath))
            {
                Directory.CreateDirectory(_savePath);
                AssetDatabase.Refresh();
            }

            if (_clearExisting) ClearOldLevels();

            int created = 0;
            var newLevels = new List<LevelConfig>();

            for (int i = 1; i <= _levelCount; i++)
            {
                LevelConfig config = CreateLevelConfig(i, _levelCount);
                if (config == null) continue;

                string filePath = $"{_savePath}/Level_{i:D2}.asset";
                LevelConfig existing = AssetDatabase.LoadAssetAtPath<LevelConfig>(filePath);

                if (existing != null && !_clearExisting)
                {
                    EditorUtility.CopySerialized(config, existing);
                    EditorUtility.SetDirty(existing);
                    newLevels.Add(existing);
                    DestroyImmediate(config);
                }
                else
                {
                    AssetDatabase.CreateAsset(config, filePath);
                    newLevels.Add(config);
                }

                created++;
                EditorUtility.DisplayProgressBar("Đang tạo levels...", $"Level {i}/{_levelCount}", (float)i / _levelCount);
            }

            EditorUtility.ClearProgressBar();
            _database.levels = newLevels;
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Hoàn thành!",
                $"Đã tạo {created} levels!\n\n" +
                $"Công thức: capacity = cols × rows × layers × {ANCHORS_PER_LAYER}\n" +
                $"totalFood = floor(capacity / (foodTypes×3)) × (foodTypes×3)",
                "OK");
        }

        private LevelConfig CreateLevelConfig(int index, int totalLevels)
        {
            var config = CreateInstance<LevelConfig>();
            config.levelIndex = index;
            int maxFoodTypes = _foodDb != null ? _foodDb.allFoods.Count : 9;

            GetLevelStats(index, totalLevels, maxFoodTypes,
                out int totalFood, out int layers, out int cols, out int rows,
                out int orders, out int types);

            config.totalFoodCount = totalFood;
            config.layerCount = layers;
            config.trayColumns = cols;
            config.trayRows = rows;
            config.maxActiveOrders = orders;
            config.backupTrayCapacity = 5;
            config.timeLimitSeconds = 0f;
            config.levelDisplayName = GetLevelName(index, totalLevels);

            if (_foodDb != null && _foodDb.allFoods.Count > 0)
                config.availableFoods = PickFoodsForLevel(_foodDb.allFoods, types, index);

            int capacity = cols * rows * layers * ANCHORS_PER_LAYER;
            string foodNames = config.availableFoods != null
                ? string.Join(", ", config.availableFoods.ConvertAll(f => f.foodName))
                : "none";
            Debug.Log($"[LevelGen] Level {index:D2}/{totalLevels}: " +
                      $"grid={cols}×{rows} layers={layers} " +
                      $"capacity={capacity} totalFood={totalFood} types={types} " +
                      $"food/type={totalFood / types} orders/type={totalFood / types / 3}\n" +
                      $"  Foods: [{foodNames}]");

            return config;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  RANDOM FOOD SELECTION
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Chọn ngẫu nhiên `count` loại food từ allFoods cho level này.
        ///
        /// Chiến lược:
        ///   • Nếu count == allFoods.Count → shuffle toàn bộ danh sách (dùng hết nhưng thứ tự khác nhau)
        ///   • Nếu count < allFoods.Count  → Fisher-Yates partial shuffle, lấy `count` phần tử đầu
        ///
        /// Seed = _randomSeed XOR levelIndex để:
        ///   - Mỗi level có tập food khác nhau
        ///   - Tái tạo được khi gen lại với cùng seed
        ///   - Không bị "trùng pattern" giữa các level liền kề
        ///
        /// Nếu _useRandomSeed = false → luôn lấy từ đầu danh sách (behavior cũ).
        /// </summary>
        private List<FoodItemData> PickFoodsForLevel(List<FoodItemData> allFoods, int count, int levelIndex)
        {
            count = Mathf.Clamp(count, 1, allFoods.Count);

            if (!_useRandomSeed)
                return new List<FoodItemData>(allFoods.GetRange(0, count));

            // Tạo bản copy để không ảnh hưởng danh sách gốc
            var pool = new List<FoodItemData>(allFoods);

            // Seed riêng cho từng level — unchecked để cho phép wrap-around thay vì overflow
            int seed = unchecked(_randomSeed ^ (levelIndex * (int)2654435761u));
            var rng = new System.Random(seed);

            // Fisher-Yates partial shuffle — O(count)
            for (int i = 0; i < count; i++)
            {
                int j = i + rng.Next(pool.Count - i);
                FoodItemData tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            return pool.GetRange(0, count);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TÊN LEVEL — phân bổ đều theo _levelCount
        // ═════════════════════════════════════════════════════════════════════

        private string GetLevelName(int index, int totalLevels)
        {
            float t = totalLevels <= 1 ? 1f : (float)(index - 1) / (totalLevels - 1);

            string[] easyNames = { "Morning Snack", "Breakfast", "Brunch", "Lunch Break", "Picnic", "Garden Party" };
            string[] mediumNames = { "Afternoon Rush", "Teatime", "Happy Hour", "Dinner Prep", "Street Food", "Night Market", "Weekend Special", "Food Court" };
            string[] hardNames = { "Chef's Challenge", "Grand Buffet", "VIP Banquet", "Food Festival", "Michelin Star", "Iron Chef", "Ultimate Feast", "Legend Mode", "Master Class", "World Cuisine" };

            if (t < 0.35f)
            {
                int i = Mathf.FloorToInt(t / 0.35f * easyNames.Length);
                return easyNames[Mathf.Clamp(i, 0, easyNames.Length - 1)];
            }
            else if (t < 0.7f)
            {
                int i = Mathf.FloorToInt((t - 0.35f) / 0.35f * mediumNames.Length);
                return mediumNames[Mathf.Clamp(i, 0, mediumNames.Length - 1)];
            }
            else
            {
                int i = Mathf.FloorToInt((t - 0.7f) / 0.3f * hardNames.Length);
                return hardNames[Mathf.Clamp(i, 0, hardNames.Length - 1)];
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private void DrawHorizontalLine()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }

        private void AutoFindDatabases()
        {
            string[] lvlGuids = AssetDatabase.FindAssets("t:LevelDatabase");
            if (lvlGuids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(lvlGuids[0]);
                _database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(path);
            }

            string[] foodGuids = AssetDatabase.FindAssets("t:FoodDatabase");
            if (foodGuids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(foodGuids[0]);
                _foodDb = AssetDatabase.LoadAssetAtPath<FoodDatabase>(path);
            }
        }

        private void ClearOldLevels()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelConfig", new[] { _savePath });
            foreach (string guid in guids)
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            AssetDatabase.Refresh();
        }
    }
}
#endif