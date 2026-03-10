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

        private const int ANCHORS_PER_LAYER = 3;

        // ─── Giới hạn tham số thiết kế ────────────────────────────────────────
        private const int MIN_COLS = 4; private const int MAX_COLS = 6;
        private const int MIN_ROWS = 2; private const int MAX_ROWS = 5;
        private const int MIN_LAYERS = 2; private const int MAX_LAYERS = 5;
        private const int MIN_FOOD_TYPES = 3;

        // ─── Food progression ─────────────────────────────────────────────────
        private const int FOOD_START = 30; // food tối thiểu ở level đầu

        // Cache series
        private int[] _foodSeries = null;
        private (int c, int r, int l, int cap)[] _gridSeries = null;
        private int _seriesForCount = -1;

        private Vector2 _scroll;

        [MenuItem("FoodMatch/Level Generator")]
        public static void OpenWindow()
        {
            var w = GetWindow<LevelGeneratorEditor>("Level Generator");
            w.minSize = new Vector2(500, 640);
            w.Show();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  STAGGER LERP — tăng từng chiều grid lệch pha nhau
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Nội suy từ min→max nhưng chỉ trong khoảng [t0, t1] của timeline.
        /// Ngoài khoảng đó: giữ min (trước t0) hoặc max (sau t1).
        /// </summary>
        private int StaggerLerp(int minVal, int maxVal, float tGlobal, float t0, float t1)
        {
            float tLocal = t1 > t0 ? Mathf.Clamp01((tGlobal - t0) / (t1 - t0)) : 1f;
            return Mathf.RoundToInt(Mathf.Lerp(minVal, maxVal, tLocal));
        }

        // ═════════════════════════════════════════════════════════════════════
        //  COMPUTE GRID + FOOD SERIES
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Tính trước toàn bộ grid và food series.
        ///
        /// Thuật toán food ("segment-based fill"):
        ///
        ///   Grid tạo ra các "plateau" — đoạn liên tiếp nhiều level cùng cap.
        ///   Trong mỗi plateau, food tăng đều từ food_đầu → cap (đầy vào cuối).
        ///   Chuyển sang plateau mới: food bắt đầu = food_cuối_plateau_trước + 3.
        ///
        ///   Đảm bảo:
        ///   • Mỗi level tăng ≥ 3, chia hết 3
        ///   • Không bao giờ vượt capacity
        ///   • Tăng đều trong từng plateau, không nhảy vọt giữa các level
        /// </summary>
        private void ComputeSeries(int n)
        {
            if (_foodSeries != null && _seriesForCount == n) return;

            _gridSeries = new (int, int, int, int)[n];
            _foodSeries = new int[n];

            // ── Grid: stagger cols/rows/layers để capacity tăng đều ───────────
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? (float)i / (n - 1) : 1f;
                int l = StaggerLerp(MIN_LAYERS, MAX_LAYERS, t, 0.00f, 0.50f);
                int r = StaggerLerp(MIN_ROWS, MAX_ROWS, t, 0.25f, 0.75f);
                int c = StaggerLerp(MIN_COLS, MAX_COLS, t, 0.50f, 1.00f);
                _gridSeries[i] = (c, r, l, c * r * l * ANCHORS_PER_LAYER);
            }

            if (n == 1)
            {
                int c0 = Mult3Floor(_gridSeries[0].cap);
                _foodSeries[0] = Mathf.Min(Mult3Floor(FOOD_START), c0);
                if (_foodSeries[0] < 3) _foodSeries[0] = 3;
                _seriesForCount = n;
                return;
            }

            // ── Precompute caps ───────────────────────────────────────────────
            int[] caps = new int[n];
            for (int i = 0; i < n; i++) caps[i] = Mult3Floor(_gridSeries[i].cap);

            // ── Food[0] ───────────────────────────────────────────────────────
            int start = Mathf.Min(Mult3Floor(FOOD_START), caps[0]);
            if (start < 3) start = 3;
            _foodSeries[0] = start;

            // ── Tìm segments (đoạn liên tiếp cùng cap) ───────────────────────
            // Mỗi segment = (segStart, segEnd)
            var segStarts = new System.Collections.Generic.List<int>();
            var segEnds = new System.Collections.Generic.List<int>();
            int s = 0;
            for (int i = 1; i < n; i++)
            {
                if (caps[i] != caps[s])
                {
                    segStarts.Add(s); segEnds.Add(i - 1);
                    s = i;
                }
            }
            segStarts.Add(s); segEnds.Add(n - 1);

            // ── Fill từng segment ─────────────────────────────────────────────
            for (int si = 0; si < segStarts.Count; si++)
            {
                int segS = segStarts[si];
                int segE = segEnds[si];
                int segCap = caps[segS];
                int count = segE - segS; // số bước trong segment (không kể điểm đầu)

                if (count == 0)
                {
                    // Single-level segment: set điểm đầu segment tiếp
                    if (si + 1 < segStarts.Count)
                    {
                        int ns = segStarts[si + 1];
                        _foodSeries[ns] = Mathf.Min(Mult3Ceil(_foodSeries[segE] + 3), caps[ns]);
                    }
                    continue;
                }

                // food[segS] đã được set; fill đều đến segCap
                float fStart = _foodSeries[segS];
                float fEnd = segCap;
                float step = (fEnd - fStart) / count;

                float acc = fStart;
                for (int j = 1; j <= count; j++)
                {
                    int idx = segS + j;
                    acc += step;
                    int target = Mult3Floor((int)acc);
                    int minOk = Mult3Ceil(_foodSeries[idx - 1] + 3);
                    int chosen = Mathf.Min(Mathf.Max(minOk, target), segCap);
                    _foodSeries[idx] = chosen;
                    if (chosen > (int)acc) acc = chosen;
                }

                // Transition: set điểm đầu segment tiếp
                if (si + 1 < segStarts.Count)
                {
                    int ns = segStarts[si + 1];
                    _foodSeries[ns] = Mathf.Min(Mult3Ceil(_foodSeries[segE] + 3), caps[ns]);
                }
            }

            _seriesForCount = n;
        }

        private static int Mult3Floor(int v) => (v / 3) * 3;
        private static int Mult3Ceil(int v) => ((v + 2) / 3) * 3;


        // ═════════════════════════════════════════════════════════════════════
        //  GET LEVEL STATS
        // ═════════════════════════════════════════════════════════════════════

        private void GetLevelStats(int levelIndex, int totalLevels, int maxDbFoods,
            out int totalFood, out int layers, out int cols, out int rows,
            out int customers, out int foodTypes)
        {
            ComputeSeries(totalLevels);
            int i = levelIndex - 1;

            cols = _gridSeries[i].c;
            rows = _gridSeries[i].r;
            layers = _gridSeries[i].l;
            totalFood = _foodSeries[i];

            int maxFoodTypes = Mathf.Min(maxDbFoods, 9);
            float t = totalLevels > 1 ? (float)i / (totalLevels - 1) : 1f;
            foodTypes = Mathf.RoundToInt(Mathf.Lerp(MIN_FOOD_TYPES, maxFoodTypes, t));
            customers = 2; // mặc định luôn là 2
        }

        // ═════════════════════════════════════════════════════════════════════
        //  GUI
        // ═════════════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(10);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 16, alignment = TextAnchor.MiddleCenter };
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

            int newCount = EditorGUILayout.IntSlider("Số levels cần tạo", _levelCount, 1, 50);
            if (newCount != _levelCount) { _levelCount = newCount; _seriesForCount = -1; }

            _clearExisting = EditorGUILayout.Toggle(
                new GUIContent("Xóa levels cũ trước"), _clearExisting);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            _useRandomSeed = EditorGUILayout.Toggle(
                new GUIContent("Random food theo seed"),
                _useRandomSeed, GUILayout.Width(280));
            GUI.enabled = _useRandomSeed;
            _randomSeed = EditorGUILayout.IntField(_randomSeed);
            if (GUILayout.Button("🎲", GUILayout.Width(30)))
                _randomSeed = Random.Range(1, 99999);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Hiển thị thông tin series
            ComputeSeries(_levelCount);
            int capLast = _gridSeries[_levelCount - 1].cap;
            int minStep = _levelCount > 1
                ? (_foodSeries[_levelCount - 1] - _foodSeries[0]) / (_levelCount - 1)
                : 0;

            EditorGUILayout.HelpBox(
                $"Grid: layers tăng trước → rows → cols (so le để capacity tăng đều)\n" +
                $"Food: bắt đầu {_foodSeries[0]}, kết thúc {_foodSeries[_levelCount - 1]} / cap {Mult3Floor(capLast)} ({_levelCount} levels)\n" +
                $"Ceiling = 85% cap (tránh plateau) • Mỗi level tăng ≥ 3 • Chia hết 3 • Không vượt cap\n" +
                $"maxActiveOrders = 2 (tất cả levels)",
                MessageType.Info);

            EditorGUILayout.Space(10);
            DrawHorizontalLine();
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);
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
        //  PREVIEW
        // ═════════════════════════════════════════════════════════════════════

        private void DrawDynamicPreviews()
        {
            int maxFoods = _foodDb != null ? _foodDb.allFoods.Count : 9;
            int n = Mathf.Max(1, _levelCount);
            ComputeSeries(n);

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

            // Tính step tăng thực tế trong tier này
            int stepMin = lMin > 1 ? _foodSeries[lMin - 1] - _foodSeries[lMin - 2] : 0;
            int stepMax = lMax > 1 ? _foodSeries[lMax - 1] - _foodSeries[lMax - 2] : 0;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{label}  (Lv {lMin}–{lMax})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• Grid: {cMin}×{rMin} → {cMax}×{rMax}  |  Layers: {layMin} → {layMax}");
            EditorGUILayout.LabelField($"• Capacity: {capMin} → {capMax}  |  Loại food: {tMin} → {tMax}");
            EditorGUILayout.LabelField($"• Total food: {fMin} → {fMax}  (tăng {stepMin}–{stepMax}/level)");
            EditorGUILayout.LabelField($"• maxActiveOrders: 2");
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

            // Tính lại series sạch
            _seriesForCount = -1;
            ComputeSeries(_levelCount);

            // Log toàn bộ series
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[LevelGen] Food series ({_levelCount} levels):");
            for (int i = 0; i < _levelCount; i++)
            {
                int delta = i > 0 ? _foodSeries[i] - _foodSeries[i - 1] : 0;
                var g = _gridSeries[i];
                sb.AppendLine($"  Lv{i + 1:D2}: food={_foodSeries[i]:D3} (+{delta:D2})  cap={g.cap:D3}  grid={g.c}×{g.r}  layers={g.l}");
            }
            Debug.Log(sb.ToString());

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
                $"Đã tạo {created} levels!\nXem Console để kiểm tra food series.", "OK");
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

            return config;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  RANDOM FOOD SELECTION
        // ═════════════════════════════════════════════════════════════════════

        private List<FoodItemData> PickFoodsForLevel(List<FoodItemData> allFoods, int count, int levelIndex)
        {
            count = Mathf.Clamp(count, 1, allFoods.Count);
            if (!_useRandomSeed)
                return new List<FoodItemData>(allFoods.GetRange(0, count));

            var pool = new List<FoodItemData>(allFoods);
            int seed = unchecked(_randomSeed ^ (levelIndex * (int)2654435761u));
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                int j = i + rng.Next(pool.Count - i);
                FoodItemData tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
            }
            return pool.GetRange(0, count);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TÊN LEVEL
        // ═════════════════════════════════════════════════════════════════════

        private string GetLevelName(int index, int totalLevels)
        {
            float t = totalLevels <= 1 ? 1f : (float)(index - 1) / (totalLevels - 1);

            string[] easy = { "Morning Snack", "Breakfast", "Brunch", "Lunch Break", "Picnic", "Garden Party" };
            string[] medium = { "Afternoon Rush", "Teatime", "Happy Hour", "Dinner Prep", "Street Food", "Night Market", "Weekend Special", "Food Court" };
            string[] hard = { "Chef's Challenge", "Grand Buffet", "VIP Banquet", "Food Festival", "Michelin Star", "Iron Chef", "Ultimate Feast", "Legend Mode", "Master Class", "World Cuisine" };

            if (t < 0.35f)
            {
                int i = Mathf.FloorToInt(t / 0.35f * easy.Length);
                return easy[Mathf.Clamp(i, 0, easy.Length - 1)];
            }
            else if (t < 0.7f)
            {
                int i = Mathf.FloorToInt((t - 0.35f) / 0.35f * medium.Length);
                return medium[Mathf.Clamp(i, 0, medium.Length - 1)];
            }
            else
            {
                int i = Mathf.FloorToInt((t - 0.7f) / 0.3f * hard.Length);
                return hard[Mathf.Clamp(i, 0, hard.Length - 1)];
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
                _database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(AssetDatabase.GUIDToAssetPath(lvlGuids[0]));

            string[] foodGuids = AssetDatabase.FindAssets("t:FoodDatabase");
            if (foodGuids.Length > 0)
                _foodDb = AssetDatabase.LoadAssetAtPath<FoodDatabase>(AssetDatabase.GUIDToAssetPath(foodGuids[0]));
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