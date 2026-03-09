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

        // ── QUAN TRỌNG: phải khớp với FoodTray.anchorsPerLayer trong scene ──
        // Log cho thấy: 8 trays, mỗi tray 6 anchors, 2 layers → 3 anchors/layer
        // Nếu FoodTray của bạn có số anchor khác, sửa hằng số này.
        private const int ANCHORS_PER_LAYER = 3;

        private Vector2 _scroll;

        [MenuItem("FoodMatch/Level Generator")]
        public static void OpenWindow()
        {
            var window = GetWindow<LevelGeneratorEditor>("Level Generator");
            window.minSize = new Vector2(480, 560);
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

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                $"ANCHORS_PER_LAYER = {ANCHORS_PER_LAYER}  " +
                $"(phải khớp FoodTray.anchorsPerLayer trong scene — xem log '[FoodTraySpawner] Tray[x] → N foods')",
                MessageType.Info);

            EditorGUILayout.Space(10);
            DrawHorizontalLine();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Preview thông số cân bằng (Auto-Calc):", EditorStyles.boldLabel);
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

        private void DrawDynamicPreviews()
        {
            int maxFoods = _foodDb != null ? _foodDb.allFoods.Count : 20;

            GetLevelStats(1, maxFoods, out int f1, out int l1, out int c1, out int r1, out int cu1, out int t1);
            GetLevelStats(5, maxFoods, out int f5, out int l5, out int c5, out int r5, out int cu5, out int t5);
            ShowTierPreview("🟢 Dễ (Lv 1-5)", l1, l5, f1, f5, c1, r1, c5, r5, cu1, cu5, t1, t5);

            GetLevelStats(6, maxFoods, out int f6, out int l6, out int c6, out int r6, out int cu6, out int t6);
            GetLevelStats(12, maxFoods, out int f12, out int l12, out int c12, out int r12, out int cu12, out int t12);
            ShowTierPreview("🟡 T.Bình (Lv 6-12)", l6, l12, f6, f12, c6, r6, c12, r12, cu6, cu12, t6, t12);

            GetLevelStats(13, maxFoods, out int f13, out int l13, out int c13, out int r13, out int cu13, out int t13);
            GetLevelStats(20, maxFoods, out int f20, out int l20, out int c20, out int r20, out int cu20, out int t20);
            ShowTierPreview("🔴 Khó (Lv 13-20)", l13, l20, f13, f20, c13, r13, c20, r20, cu13, cu20, t13, t20);
        }

        private void ShowTierPreview(string label,
            int lMin, int lMax, int fMin, int fMax,
            int cMin, int rMin, int cMax, int rMax,
            int cuMin, int cuMax, int tMin, int tMax)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• Layers: {lMin}{(lMin == lMax ? "" : $"–{lMax}")} | Khách: {cuMin}{(cuMin == cuMax ? "" : $"–{cuMax}")}");
            EditorGUILayout.LabelField($"• Tổng đồ ăn: {fMin} → {fMax}  (= grid capacity, chia hết cho foodTypes×3)");
            EditorGUILayout.LabelField($"• Kích thước khay (Col×Row): {cMin}×{rMin} → {cMax}×{rMax}");
            EditorGUILayout.LabelField($"• Số loại đồ ăn: {tMin} → {tMax} loại");
            EditorGUILayout.EndVertical();
        }

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
                LevelConfig config = CreateLevelConfig(i);
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

            EditorUtility.DisplayDialog("Hoàn thành!", $"Đã tạo {created} levels!\n\nMỗi level: totalFood = cols×rows×layers×{ANCHORS_PER_LAYER}", "OK");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  THUẬT TOÁN CHÍNH — totalFood tính NGƯỢC từ grid capacity thực tế
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// FIX HOÀN TOÀN: totalFood KHÔNG còn tính từ công thức tăng dần rồi fit vào grid.
        ///
        /// Thay vào đó:
        ///   1. Tính grid size (cols, rows) tăng dần theo level
        ///   2. Tính grid capacity thực tế = cols × rows × layers × ANCHORS_PER_LAYER
        ///   3. totalFood = làm tròn XUỐNG capacity về bội số của (foodTypes × 3)
        ///      → đảm bảo: totalFood ≤ capacity (không bao giờ thừa)
        ///                  totalFood % (foodTypes×3) = 0 (chia đều từng loại)
        ///
        /// Ví dụ Level 5: cols=4, rows=2, layers=2, anchors=3
        ///   capacity = 4×2×2×3 = 48
        ///   foodTypes=5, divisor=15 → totalFood = floor(48/15)×15 = 3×15 = 45
        ///   → 5 loại × 9 food/loại = 45 ✅  (9/3=3 orders/loại)
        /// </summary>
        private void GetLevelStats(int levelIndex, int maxDbFoods,
            out int totalFood, out int layers, out int cols, out int rows,
            out int customers, out int foodTypes)
        {
            // ── 1. Tier: layers & customers ───────────────────────────────────
            if (levelIndex <= 5) { layers = 2; customers = 1; }
            else if (levelIndex <= 12) { layers = 3; customers = 2; }
            else { layers = 4; customers = 2; }

            // ── 2. Grid size tăng dần theo level ─────────────────────────────
            // Bắt đầu từ 4×2 (nhỏ nhất), tăng đều lên đến 6×4 (lớn nhất)
            // Sử dụng bảng cố định để kiểm soát độ khó rõ ràng hơn công thức
            GetGridSize(levelIndex, out cols, out rows);

            // ── 3. Capacity thực tế của grid ─────────────────────────────────
            // Đây là SỐ LƯỢNG FOOD TỐI ĐA mà FoodTray có thể chứa
            // ANCHORS_PER_LAYER phải khớp với FoodTray prefab trong scene
            int gridCapacity = cols * rows * layers * ANCHORS_PER_LAYER;

            // ── 4. foodTypes ──────────────────────────────────────────────────
            // Tính dựa trên capacity để tỉ lệ loại/food hợp lý
            int idealTypes = Mathf.Max(3, gridCapacity / 9);
            foodTypes = Mathf.Clamp(idealTypes, 3, maxDbFoods);

            // ── 5. totalFood = FLOOR capacity về bội số của (foodTypes × 3) ──
            // Làm tròn XUỐNG (không phải lên) để đảm bảo totalFood ≤ gridCapacity
            int divisor = foodTypes * 3;
            totalFood = (gridCapacity / divisor) * divisor;

            // Đảm bảo có ít nhất 1 bộ đầy đủ (tối thiểu = divisor)
            if (totalFood < divisor)
                totalFood = divisor;
        }

        /// <summary>
        /// Grid size tăng dần theo level.
        /// Dùng bảng cố định để đảm bảo kiểm soát được độ khó.
        ///
        /// Level  1– 3: 4×2 = 8  cells (nhỏ nhất)
        /// Level  4– 6: 5×2 = 10 cells
        /// Level  7– 9: 5×3 = 15 cells
        /// Level 10–12: 6×3 = 18 cells
        /// Level 13–15: 6×4 = 24 cells (lớn nhất)
        /// Level 16–20: 6×4 = 24 cells (giữ nguyên, tăng độ khó qua layers)
        /// </summary>
        private static void GetGridSize(int levelIndex, out int cols, out int rows)
        {
            if (levelIndex <= 3) { cols = 4; rows = 2; }
            else if (levelIndex <= 6) { cols = 5; rows = 2; }
            else if (levelIndex <= 9) { cols = 5; rows = 3; }
            else if (levelIndex <= 12) { cols = 6; rows = 3; }
            else { cols = 6; rows = 4; }
        }

        private LevelConfig CreateLevelConfig(int index)
        {
            var config = CreateInstance<LevelConfig>();
            config.levelIndex = index;
            int maxFoodTypes = _foodDb != null ? _foodDb.allFoods.Count : 10;

            GetLevelStats(index, maxFoodTypes,
                out int totalFood, out int layers, out int cols, out int rows,
                out int orders, out int types);

            config.totalFoodCount = totalFood;
            config.layerCount = layers;
            config.trayColumns = cols;
            config.trayRows = rows;
            config.maxActiveOrders = orders;
            config.backupTrayCapacity = 5;
            config.timeLimitSeconds = 0f;

            if (index <= 5) config.levelDisplayName = GetEasyLevelName(index);
            else if (index <= 12) config.levelDisplayName = GetMediumLevelName(index);
            else config.levelDisplayName = GetHardLevelName(index);

            if (_foodDb != null && _foodDb.allFoods.Count > 0)
                config.availableFoods = _foodDb.allFoods.GetRange(0, types);

            // Log để verify
            int capacity = cols * rows * layers * ANCHORS_PER_LAYER;
            Debug.Log($"[LevelGen] Level {index:D2}: grid={cols}×{rows} layers={layers} " +
                      $"capacity={capacity} totalFood={totalFood} types={types} " +
                      $"(food/type={totalFood / types} orders/type={totalFood / types / 3})");

            return config;
        }

        private string GetEasyLevelName(int i)
        {
            string[] names = { "Morning Snack", "Breakfast", "Brunch", "Lunch Break", "Picnic" };
            return names[Mathf.Clamp(i - 1, 0, names.Length - 1)];
        }

        private string GetMediumLevelName(int i)
        {
            string[] names = { "Afternoon Rush", "Teatime", "Happy Hour", "Dinner Prep", "Street Food", "Night Market", "Weekend Special" };
            return names[Mathf.Clamp(i - 6, 0, names.Length - 1)];
        }

        private string GetHardLevelName(int i)
        {
            string[] names = { "Chef's Challenge", "Grand Buffet", "VIP Banquet", "Food Festival", "Michelin Star", "Iron Chef", "Ultimate Feast", "Legend Mode" };
            return names[Mathf.Clamp(i - 13, 0, names.Length - 1)];
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