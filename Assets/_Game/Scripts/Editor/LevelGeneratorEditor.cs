#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using FoodMatch.Data;

namespace FoodMatch.Editor
{
    /// <summary>
    /// Custom Editor Window để tự động tạo 20 LevelConfig SO.
    /// Mở qua menu: FoodMatch > Level Generator
    /// </summary>
    public class LevelGeneratorEditor : EditorWindow
    {
        // ─── Config ───────────────────────────────────────────────────────────
        private string _savePath = "Assets/_Game/ScriptableObjects/Levels";
        private LevelDatabase _database = null;
        private FoodDatabase _foodDb = null;
        private int _levelCount = 20;
        private bool _clearExisting = false;

        // Scroll position cho cửa sổ
        private Vector2 _scroll;

        // ─── Menu Item ────────────────────────────────────────────────────────
        [MenuItem("FoodMatch/Level Generator")]
        public static void OpenWindow()
        {
            var window = GetWindow<LevelGeneratorEditor>("Level Generator");
            window.minSize = new Vector2(420, 500);
            window.Show();
        }

        // ─── GUI ──────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // ── Header ──
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

            // ── References ──
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            _database = (LevelDatabase)EditorGUILayout.ObjectField(
                "Level Database", _database, typeof(LevelDatabase), false);
            _foodDb = (FoodDatabase)EditorGUILayout.ObjectField(
                "Food Database", _foodDb, typeof(FoodDatabase), false);
            EditorGUILayout.Space(5);

            // ── Settings ──
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            _savePath = EditorGUILayout.TextField("Save Path", _savePath);
            _levelCount = EditorGUILayout.IntSlider("Số levels cần tạo", _levelCount, 1, 30);
            _clearExisting = EditorGUILayout.Toggle(
                new GUIContent("Xóa levels cũ trước",
                    "Nếu tick: xóa toàn bộ file Level_XX cũ trước khi tạo mới"),
                _clearExisting);

            EditorGUILayout.Space(10);
            DrawHorizontalLine();

            // ── Preview ──
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Preview cấu hình sẽ được tạo:", EditorStyles.boldLabel);

            ShowLevelPreview(1, 5, "🟢 Dễ (Tutorial)", 2, 9, 1, 5);
            ShowLevelPreview(6, 12, "🟡 Trung bình", 3, 12, 2, 5);
            ShowLevelPreview(13, 20, "🔴 Khó", 4, 15, 2, 5);

            EditorGUILayout.Space(10);
            DrawHorizontalLine();
            EditorGUILayout.Space(10);

            // ── Buttons ──
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

        // ─── Helpers GUI ──────────────────────────────────────────────────────

        private void ShowLevelPreview(int from, int to, string label,
            int layers, int foods, int customers, int backup)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{label}  (Level {from}-{to})", GUILayout.Width(220));
            EditorGUILayout.LabelField($"Layers:{layers}  Foods:{foods}  Cust:{customers}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHorizontalLine()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }

        // ─── Auto Find ────────────────────────────────────────────────────────

        private void AutoFindDatabases()
        {
            // Tìm LevelDatabase
            string[] lvlGuids = AssetDatabase.FindAssets("t:LevelDatabase");
            if (lvlGuids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(lvlGuids[0]);
                _database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(path);
                Debug.Log($"[LevelGenerator] Tìm thấy LevelDatabase: {path}");
            }
            else
                Debug.LogWarning("[LevelGenerator] Không tìm thấy LevelDatabase asset.");

            // Tìm FoodDatabase
            string[] foodGuids = AssetDatabase.FindAssets("t:FoodDatabase");
            if (foodGuids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(foodGuids[0]);
                _foodDb = AssetDatabase.LoadAssetAtPath<FoodDatabase>(path);
                Debug.Log($"[LevelGenerator] Tìm thấy FoodDatabase: {path}");
            }
            else
                Debug.LogWarning("[LevelGenerator] Không tìm thấy FoodDatabase asset.");
        }

        // ─── Main Generator ───────────────────────────────────────────────────

        private void GenerateLevels()
        {
            // Validation
            if (_database == null)
            {
                EditorUtility.DisplayDialog("Lỗi",
                    "Chưa gán LevelDatabase! Hãy kéo thả vào hoặc dùng nút Auto Find.", "OK");
                return;
            }
            if (_foodDb == null || _foodDb.allFoods.Count == 0)
            {
                EditorUtility.DisplayDialog("Lỗi",
                    "FoodDatabase trống hoặc chưa gán! Cần ít nhất 1 FoodItemData.", "OK");
                return;
            }

            // Tạo thư mục nếu chưa có
            if (!Directory.Exists(_savePath))
            {
                Directory.CreateDirectory(_savePath);
                AssetDatabase.Refresh();
            }

            // Xóa levels cũ nếu được yêu cầu
            if (_clearExisting)
            {
                ClearOldLevels();
            }

            // Bắt đầu tạo
            int created = 0;
            var newLevels = new List<LevelConfig>();

            for (int i = 1; i <= _levelCount; i++)
            {
                LevelConfig config = CreateLevelConfig(i);
                if (config == null) continue;

                string filePath = $"{_savePath}/Level_{i:D2}.asset";

                // Nếu file đã tồn tại thì update, không tạo mới
                LevelConfig existing =
                    AssetDatabase.LoadAssetAtPath<LevelConfig>(filePath);

                if (existing != null && !_clearExisting)
                {
                    // Cập nhật existing asset
                    EditorUtility.CopySerialized(config, existing);
                    EditorUtility.SetDirty(existing);
                    newLevels.Add(existing);
                    DestroyImmediate(config); // Hủy config tạm
                }
                else
                {
                    // Tạo asset mới
                    AssetDatabase.CreateAsset(config, filePath);
                    newLevels.Add(config);
                }

                created++;
                EditorUtility.DisplayProgressBar(
                    "Đang tạo levels...",
                    $"Level {i}/{_levelCount}",
                    (float)i / _levelCount);
            }

            EditorUtility.ClearProgressBar();

            // Cập nhật LevelDatabase
            _database.levels = newLevels;
            EditorUtility.SetDirty(_database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Hoàn thành!",
                $"Đã tạo thành công {created} levels!\n" +
                $"Đã cập nhật LevelDatabase.", "OK");

            Debug.Log($"[LevelGenerator] ✅ Tạo xong {created} levels tại: {_savePath}");
        }

        // ─── Level Config Builder ─────────────────────────────────────────────

        private LevelConfig CreateLevelConfig(int index)
        {
            var config = CreateInstance<LevelConfig>();
            config.levelIndex = index;

            // Lấy danh sách foods từ FoodDatabase
            var allFoods = _foodDb.allFoods;
            int maxFoodTypes = allFoods.Count;

            // ── Level 1-5: Dễ (Tutorial) ──────────────────────────────────
            if (index <= 5)
            {
                config.levelDisplayName = GetEasyLevelName(index);
                config.layerCount = 2;
                config.trayColumns = 3;
                config.trayRows = 3;
                config.totalFoodCount = 9;
                config.maxActiveCustomers = 1;
                config.backupTrayCapacity = 5;
                config.timeLimitSeconds = 0f;

                // Dùng 3 loại food đầu tiên
                int foodTypeCount = Mathf.Min(3, maxFoodTypes);
                config.availableFoods = allFoods.GetRange(0, foodTypeCount);
            }
            // ── Level 6-12: Trung bình ────────────────────────────────────
            else if (index <= 12)
            {
                config.levelDisplayName = GetMediumLevelName(index);
                config.layerCount = 3;
                config.trayColumns = 4;
                config.trayRows = 3;
                config.totalFoodCount = 12;
                config.maxActiveCustomers = 2;
                config.backupTrayCapacity = 5;
                config.timeLimitSeconds = 0f;

                // Dùng 4-5 loại food
                int foodTypeCount = Mathf.Min(4 + (index - 6) / 3, maxFoodTypes);
                foodTypeCount = Mathf.Max(foodTypeCount, 1);
                config.availableFoods = allFoods.GetRange(0, foodTypeCount);
            }
            // ── Level 13-20: Khó ──────────────────────────────────────────
            else
            {
                config.levelDisplayName = GetHardLevelName(index);
                config.layerCount = 4;
                config.trayColumns = 4;
                config.trayRows = 4;
                config.totalFoodCount = 15 + ((index - 13) / 2) * 3; // 15,15,18,18,21...
                config.totalFoodCount = (config.totalFoodCount / 3) * 3; // đảm bảo chia hết 3
                config.maxActiveCustomers = 2;
                config.backupTrayCapacity = 5;
                config.timeLimitSeconds = 0f;

                // Dùng tất cả food types có sẵn
                int foodTypeCount = Mathf.Min(5 + (index - 13) / 2, maxFoodTypes);
                foodTypeCount = Mathf.Max(foodTypeCount, 1);
                config.availableFoods = allFoods.GetRange(0, foodTypeCount);
            }

            return config;
        }

        // ─── Level Names ──────────────────────────────────────────────────────

        private string GetEasyLevelName(int i)
        {
            string[] names = { "Morning Snack", "Breakfast", "Brunch", "Lunch Break", "Picnic" };
            return names[Mathf.Clamp(i - 1, 0, names.Length - 1)];
        }

        private string GetMediumLevelName(int i)
        {
            string[] names = {
                "Afternoon Rush", "Teatime", "Happy Hour",
                "Dinner Prep", "Street Food", "Night Market", "Weekend Special"
            };
            return names[Mathf.Clamp(i - 6, 0, names.Length - 1)];
        }

        private string GetHardLevelName(int i)
        {
            string[] names = {
                "Chef's Challenge", "Grand Buffet", "VIP Banquet",
                "Food Festival", "Michelin Star", "Iron Chef",
                "Ultimate Feast", "Legend Mode"
            };
            return names[Mathf.Clamp(i - 13, 0, names.Length - 1)];
        }

        // ─── Clear Old Levels ─────────────────────────────────────────────────

        private void ClearOldLevels()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelConfig", new[] { _savePath });
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.DeleteAsset(path);
                count++;
            }

            if (count > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"[LevelGenerator] Đã xóa {count} level cũ.");
            }
        }
    }
}
#endif