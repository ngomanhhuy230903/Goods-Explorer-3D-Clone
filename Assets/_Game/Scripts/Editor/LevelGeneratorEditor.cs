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

        private Vector2 _scroll;

        [MenuItem("FoodMatch/Level Generator")]
        public static void OpenWindow()
        {
            var window = GetWindow<LevelGeneratorEditor>("Level Generator");
            window.minSize = new Vector2(480, 520);
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
            _clearExisting = EditorGUILayout.Toggle(new GUIContent("Xóa levels cũ trước", "Xóa file cũ trước khi tạo mới"), _clearExisting);

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
            {
                GenerateLevels();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            GUI.backgroundColor = new Color(0.9f, 0.5f, 0.3f);
            if (GUILayout.Button("🔄  Tự động tìm Database assets", GUILayout.Height(30)))
            {
                AutoFindDatabases();
            }
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

        private void ShowTierPreview(string label, int lMin, int lMax, int fMin, int fMax, int cMin, int rMin, int cMax, int rMax, int cuMin, int cuMax, int tMin, int tMax)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• Layers: {lMin}{(lMin == lMax ? "" : $"-{lMax}")} | Khách: {cuMin}{(cuMin == cuMax ? "" : $"-{cuMax}")}");
            EditorGUILayout.LabelField($"• Tổng đồ ăn: {fMin} -> {fMax} (Chia hết cho 3)");

            string gridMinStr = $"{cMin}x{rMin}";
            string gridMaxStr = $"{cMax}x{rMax}";
            EditorGUILayout.LabelField($"• Kích thước khay (Cột x Hàng): {gridMinStr} -> {gridMaxStr}");

            EditorGUILayout.LabelField($"• Số loại đồ ăn: {tMin} -> {tMax} loại");
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

            EditorUtility.DisplayDialog("Hoàn thành!", $"Đã cân bằng và tạo {created} levels!", "OK");
        }

        // ─── THUẬT TOÁN CÂN BẰNG GRID ĐÃ SỬA LẠI (TĂNG ĐỀU) ───────────────────
        private void GetLevelStats(int levelIndex, int maxDbFoods, out int totalFood, out int layers, out int cols, out int rows, out int customers, out int foodTypes)
        {
            // 1. Số đồ ăn
            float rawFoodCount = 30f * Mathf.Pow(1.15f, levelIndex - 1);
            totalFood = Mathf.RoundToInt(rawFoodCount);
            int remainder = totalFood % 3;
            if (remainder == 1) totalFood -= 1;
            else if (remainder == 2) totalFood += 1;

            // 2. Phân loại theo Level
            if (levelIndex <= 5) { layers = 2; customers = 1; }
            else if (levelIndex <= 12) { layers = 3; customers = 2; }
            else { layers = 4; customers = 2; }

            // 3. Setup Kích thước Grid siêu nén (Bắt đầu từ 4x2)
            cols = 4;
            rows = 2;

            // Giả định 1 layer trong 1 box có thể nhồi được tối đa ~4 món ăn.
            // Sức chứa của 1 box = số layer * 4.
            int maxItemsPerBox = layers * 4;

            // Cờ để luân phiên tăng: true = tăng cột, false = tăng hàng
            bool expandColNext = true;

            // Vòng lặp: Luân phiên nới rộng cột và hàng khi tổng đồ ăn vượt quá sức chứa
            while (true)
            {
                int currentCapacity = cols * rows * maxItemsPerBox;

                if (totalFood <= currentCapacity) break; // Đã đủ sức chứa
                if (cols >= 6 && rows >= 4) break;       // Chạm trần kích thước khay 6x4

                if (expandColNext)
                {
                    if (cols < 6) cols++;
                    else if (rows < 4) rows++; // Chữa cháy nếu cột đã max mà hàng vẫn còn tăng được
                }
                else
                {
                    if (rows < 4) rows++;
                    else if (cols < 6) cols++; // Chữa cháy nếu hàng đã max mà cột vẫn còn tăng được
                }

                expandColNext = !expandColNext; // Đảo lượt
            }

            // 4. Số loại đồ ăn
            int idealTypes = Mathf.Max(3, totalFood / 9);
            foodTypes = Mathf.Min(idealTypes, maxDbFoods);
        }

        private LevelConfig CreateLevelConfig(int index)
        {
            var config = CreateInstance<LevelConfig>();
            config.levelIndex = index;
            int maxFoodTypes = _foodDb != null ? _foodDb.allFoods.Count : 10;

            GetLevelStats(index, maxFoodTypes, out int totalFood, out int layers, out int cols, out int rows, out int customers, out int types);

            config.totalFoodCount = totalFood;
            config.layerCount = layers;
            config.trayColumns = cols;
            config.trayRows = rows;
            config.maxActiveCustomers = customers;
            config.backupTrayCapacity = 5;
            config.timeLimitSeconds = 0f;

            if (index <= 5) config.levelDisplayName = GetEasyLevelName(index);
            else if (index <= 12) config.levelDisplayName = GetMediumLevelName(index);
            else config.levelDisplayName = GetHardLevelName(index);

            if (_foodDb != null && _foodDb.allFoods.Count > 0)
            {
                config.availableFoods = _foodDb.allFoods.GetRange(0, types);
            }

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
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }
            AssetDatabase.Refresh();
        }
    }
}
#endif