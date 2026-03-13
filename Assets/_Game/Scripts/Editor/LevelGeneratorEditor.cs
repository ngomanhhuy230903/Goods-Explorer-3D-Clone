// Editor/LevelGeneratorEditor.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Editor
{
    // ═════════════════════════════════════════════════════════════════════════
    // SERIALIZABLE DATA — dùng cho JSON save/load
    // ═════════════════════════════════════════════════════════════════════════

    [Serializable]
    public class ObstacleBandConfig
    {
        public bool enabled = false;
        public enum Kind { Lock, Tube, Conveyor }
        public Kind kind = Kind.Lock;

        public int lockCountMin = 1, lockCountMax = 3;
        public int lockHpMin = 1, lockHpMax = 5;

        public int tubeCountMin = 1, tubeCountMax = 3;
        public int tubeFoodMin = 3, tubeFoodMax = 8;

        public int conveyorCountMin = 2, conveyorCountMax = 6;
        public int conveyorFoodPerTrayMin = 1, conveyorFoodPerTrayMax = 3;
        public float conveyorSpeedMin = 1f, conveyorSpeedMax = 4f;

        [NonSerialized] public bool foldout = true;
    }

    [Serializable]
    public class LevelBand
    {
        public string label = "Band";
        public int fromLevel = 1;
        public int toLevel = 50;

        // Food
        public int foodMin = 30;
        public int foodMax = 120;

        // New: auto food mode dựa theo % capacity của level cuối band
        public bool autoFoodFromCapacity = true;
        // % capacity của level cuối band → dùng làm foodMax (0–100)
        public int capacityPercent = 70;

        // Grid
        public int colMin = 4, colMax = 6;
        public int rowMin = 2, rowMax = 4;
        public int layerMin = 2, layerMax = 4;

        public List<ObstacleBandConfig> obstacles = new List<ObstacleBandConfig>();

        [NonSerialized] public bool foldout = true;
        [NonSerialized] public bool obstaclesFoldout = false;
    }

    /// <summary>Wrapper để JsonUtility serialize List</summary>
    [Serializable]
    public class GeneratorPreset
    {
        public string name = "Preset";
        public int totalLevels = 100;
        public int randomSeed = 42069;
        public bool useRandomSeed = true;
        public string savePath = "Assets/_Game/ScriptableObjects/Levels";
        public List<LevelBand> bands = new List<LevelBand>();
    }

    [Serializable]
    internal class PresetList { public List<string> names = new List<string>(); }

    // ═════════════════════════════════════════════════════════════════════════
    // EDITOR WINDOW
    // ═════════════════════════════════════════════════════════════════════════

    public class LevelGeneratorEditor : EditorWindow
    {
        // ─── Tabs ─────────────────────────────────────────────────────────────
        private enum Tab { Design, Preview, Generate }
        private Tab _tab = Tab.Design;

        // ─── Global ───────────────────────────────────────────────────────────
        private string _savePath = "Assets/_Game/ScriptableObjects/Levels";
        private LevelDatabase _database;
        private FoodDatabase _foodDb;
        private int _totalLevels = 100;
        private bool _clearExisting = false;
        private int _randomSeed = 42069;
        private bool _useRandomSeed = true;

        // ─── Auto-split ────────────────────────────────────────────────
        private int _bandCount = 3;
        private bool _showSplitBar = false;

        // ─── Bands ────────────────────────────────────────────────────────────
        private List<LevelBand> _bands = new List<LevelBand>();

        // ─── Scroll ───────────────────────────────────────────────────────────
        private Vector2 _scrollDesign;
        private Vector2 _scrollPreview;
        private Vector2 _scrollPresets;

        // ─── Preview ──────────────────────────────────────────────────────────
        private struct LevelPreviewRow
        {
            public int level, food, cols, rows, layers, cap;
            public string obstacles, bandLabel;
        }
        private List<LevelPreviewRow> _previewRows = new List<LevelPreviewRow>();
        private bool _previewDirty = true;
        private int _previewJump = 1;

        // ─── Preset management ────────────────────────────────────────────────
        private const string PREFS_KEY_LIST = "SLGE_PresetNames";
        private const string PREFS_KEY_PREFIX = "SLGE_Preset_";
        private string _newPresetName = "My Preset";
        private string _selectedPreset = "";
        private bool _showPresetsPanel = false;

        private const int ANCHORS_PER_LAYER = 3;
        private const int MIN_FOOD_TYPES = 3;

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("FoodMatch/Super Level Generator")]
        public static void Open()
        {
            var w = GetWindow<LevelGeneratorEditor>("⚡ Super Level Gen");
            w.minSize = new Vector2(640, 700);
            w.Show();
        }

        private void OnEnable()
        {
            if (_bands.Count == 0) ResetToDefault();
        }

        // ═════════════════════════════════════════════════════════════════════
        // MAIN GUI
        // ═════════════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            DrawTopBar();
            DrawHLine(new Color(0.25f, 0.55f, 1f));
            EditorGUILayout.Space(3);

            switch (_tab)
            {
                case Tab.Design: DrawDesignTab(); break;
                case Tab.Preview: DrawPreviewTab(); break;
                case Tab.Generate: DrawGenerateTab(); break;
            }
        }

        // ─── Top bar ──────────────────────────────────────────────────────────
        private void DrawTopBar()
        {
            EditorGUILayout.Space(5);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.45f, 0.85f, 1f) }
            };
            EditorGUILayout.LabelField("⚡ FoodMatch — Super Level Generator", titleStyle);
            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Total Levels:", GUILayout.Width(85));
            int newTotal = EditorGUILayout.IntField(_totalLevels, GUILayout.Width(60));
            if (newTotal != _totalLevels)
            {
                _totalLevels = Mathf.Clamp(newTotal, 1, 1000);
                _previewDirty = true;
            }

            GUILayout.Space(12);
            EditorGUILayout.LabelField("Bands:", GUILayout.Width(45));
            _bandCount = EditorGUILayout.IntField(_bandCount, GUILayout.Width(40));
            _bandCount = Mathf.Clamp(_bandCount, 1, 50);

            GUI.backgroundColor = new Color(0.3f, 0.75f, 0.4f);
            if (GUILayout.Button("⟳ Auto Split", GUILayout.Width(90)))
                AutoSplitBands();
            GUI.backgroundColor = Color.white;

            GUILayout.Space(12);

            GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button("💾 Presets", GUILayout.Width(82)))
                _showPresetsPanel = !_showPresetsPanel;
            GUI.backgroundColor = Color.white;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (_showPresetsPanel) DrawPresetsPanel();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (TabBtn("🎨 Design", _tab == Tab.Design)) _tab = Tab.Design;
            if (TabBtn("👁 Preview", _tab == Tab.Preview)) { _tab = Tab.Preview; RebuildPreview(); }
            if (TabBtn("🚀 Generate", _tab == Tab.Generate)) _tab = Tab.Generate;
            EditorGUILayout.EndHorizontal();
        }

        // ═════════════════════════════════════════════════════════════════════
        // PRESETS PANEL
        // ═════════════════════════════════════════════════════════════════════

        private void DrawPresetsPanel()
        {
            EditorGUILayout.BeginVertical(BoxStyle(new Color(0.12f, 0.1f, 0.05f)));
            EditorGUILayout.LabelField("💾  Preset Manager", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Save as:", GUILayout.Width(55));
            _newPresetName = EditorGUILayout.TextField(_newPresetName, GUILayout.Width(160));
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Save", GUILayout.Width(55)))
                SavePreset(_newPresetName);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            var names = LoadPresetNames();
            if (names.Count == 0)
            {
                EditorGUILayout.LabelField("  (No saved presets)", EditorStyles.miniLabel);
            }
            else
            {
                _scrollPresets = EditorGUILayout.BeginScrollView(_scrollPresets,
                    GUILayout.MaxHeight(120));
                foreach (var pname in names)
                {
                    bool isSel = pname == _selectedPreset;
                    GUI.backgroundColor = isSel
                        ? new Color(0.3f, 0.6f, 1f) : new Color(0.22f, 0.22f, 0.22f);
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    if (GUILayout.Button(pname,
                        new GUIStyle(EditorStyles.label)
                        { fontStyle = isSel ? FontStyle.Bold : FontStyle.Normal },
                        GUILayout.ExpandWidth(true)))
                        _selectedPreset = pname;

                    GUI.backgroundColor = new Color(0.3f, 0.65f, 0.9f);
                    if (GUILayout.Button("Load", GUILayout.Width(48)))
                    {
                        LoadPreset(pname);
                        _showPresetsPanel = false;
                    }
                    GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Preset",
                                $"Xóa preset '{pname}'?", "Delete", "Cancel"))
                            DeletePreset(pname);
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // ─── Save / Load helpers ──────────────────────────────────────────────

        private void SavePreset(string pname)
        {
            if (string.IsNullOrWhiteSpace(pname)) return;
            var preset = new GeneratorPreset
            {
                name = pname,
                totalLevels = _totalLevels,
                randomSeed = _randomSeed,
                useRandomSeed = _useRandomSeed,
                savePath = _savePath,
                bands = _bands
            };
            string json = JsonUtility.ToJson(preset, prettyPrint: true);
            EditorPrefs.SetString(PREFS_KEY_PREFIX + pname, json);

            var names = LoadPresetNames();
            if (!names.Contains(pname)) names.Add(pname);
            EditorPrefs.SetString(PREFS_KEY_LIST,
                JsonUtility.ToJson(new PresetList { names = names }));

            _selectedPreset = pname;
            Debug.Log($"[SuperLevelGen] Saved preset '{pname}'.");
        }

        private void LoadPreset(string pname)
        {
            string json = EditorPrefs.GetString(PREFS_KEY_PREFIX + pname, "");
            if (string.IsNullOrEmpty(json)) { Debug.LogWarning($"Preset '{pname}' not found."); return; }

            var preset = JsonUtility.FromJson<GeneratorPreset>(json);
            if (preset == null) return;

            _totalLevels = preset.totalLevels;
            _randomSeed = preset.randomSeed;
            _useRandomSeed = preset.useRandomSeed;
            _savePath = preset.savePath;
            _bands = preset.bands ?? new List<LevelBand>();

            foreach (var b in _bands) { b.foldout = true; b.obstaclesFoldout = false; }

            _previewDirty = true;
            _selectedPreset = pname;
            Debug.Log($"[SuperLevelGen] Loaded preset '{pname}'.");
        }

        private void DeletePreset(string pname)
        {
            EditorPrefs.DeleteKey(PREFS_KEY_PREFIX + pname);
            var names = LoadPresetNames();
            names.Remove(pname);
            EditorPrefs.SetString(PREFS_KEY_LIST,
                JsonUtility.ToJson(new PresetList { names = names }));
            if (_selectedPreset == pname) _selectedPreset = "";
        }

        private List<string> LoadPresetNames()
        {
            string raw = EditorPrefs.GetString(PREFS_KEY_LIST, "");
            if (string.IsNullOrEmpty(raw)) return new List<string>();
            var pl = JsonUtility.FromJson<PresetList>(raw);
            return pl?.names ?? new List<string>();
        }

        // ═════════════════════════════════════════════════════════════════════
        // AUTO SPLIT BANDS
        // ═════════════════════════════════════════════════════════════════════

        private void AutoSplitBands()
        {
            int n = Mathf.Clamp(_bandCount, 1, _totalLevels);
            int levelsPerBand = _totalLevels / n;
            int remainder = _totalLevels % n;

            var newBands = new List<LevelBand>();
            int cursor = 1;

            for (int i = 0; i < n; i++)
            {
                int size = levelsPerBand + (i == n - 1 ? remainder : 0);
                int from = cursor;
                int to = cursor + size - 1;
                cursor += size;

                float tBandCenter = n > 1 ? (float)i / (n - 1) : 0.5f;

                var obstacles = (i < _bands.Count)
                    ? _bands[i].obstacles
                    : new List<ObstacleBandConfig>();

                int colMin = LerpInt(4, 6, tBandCenter);
                int colMax = LerpInt(4, 6, Mathf.Clamp01(tBandCenter + 0.15f));
                int rowMin = LerpInt(2, 5, tBandCenter);
                int rowMax = LerpInt(2, 5, Mathf.Clamp01(tBandCenter + 0.15f));
                int layerMin = LerpInt(2, 5, tBandCenter);
                int layerMax = LerpInt(2, 5, Mathf.Clamp01(tBandCenter + 0.15f));

                colMax = Mathf.Max(colMin, colMax);
                rowMax = Mathf.Max(rowMin, rowMax);
                layerMax = Mathf.Max(layerMin, layerMax);

                // Giữ capacityPercent cũ nếu band đã tồn tại, mặc định 70
                int capPct = (i < _bands.Count) ? _bands[i].capacityPercent : 70;

                var band = new LevelBand
                {
                    label = $"Band {i + 1}",
                    fromLevel = from,
                    toLevel = to,
                    colMin = colMin,
                    colMax = colMax,
                    rowMin = rowMin,
                    rowMax = rowMax,
                    layerMin = layerMin,
                    layerMax = layerMax,
                    autoFoodFromCapacity = true,
                    capacityPercent = capPct,
                    obstacles = obstacles,
                    foldout = true,
                };
                newBands.Add(band);
            }

            _bands = newBands;

            // Recalculate foodMin/Max for all bands with new logic
            RecalcAllBandFood();
            _previewDirty = true;
        }

        // ═════════════════════════════════════════════════════════════════════
        // FOOD MIN/MAX CALCULATION — NEW LOGIC
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Tính lại foodMin/foodMax cho tất cả bands theo logic mới:
        ///   Band 1 : foodMin = 30
        ///            foodMax = Mult3Floor(cap_of_toLevel * capPercent / 100)
        ///   Band N : foodMin = foodMax của band N-1
        ///            foodMax = Mult3Floor(cap_of_toLevel * capPercent / 100)
        /// cap_of_toLevel = capacity tính theo grid MAX (colMax, rowMax, layerMax)
        /// vì toLevel là level khó nhất trong band → grid đã scale lên max.
        /// </summary>
        private void RecalcAllBandFood()
        {
            int prevMax = 30; // band 1 foodMin bắt đầu từ 30

            for (int i = 0; i < _bands.Count; i++)
            {
                var b = _bands[i];
                if (!b.autoFoodFromCapacity) { prevMax = b.foodMax; continue; }

                // Cap tại level cuối band = grid max
                int capAtEnd = ColRowLayerCap(b.colMax, b.rowMax, b.layerMax);
                int computedMax = Mult3Floor(Mathf.RoundToInt(capAtEnd * b.capacityPercent / 100f));
                computedMax = Mathf.Max(computedMax, 3);

                if (i == 0)
                    b.foodMin = 30;
                else
                    b.foodMin = prevMax; // min = max của band trước

                // Đảm bảo max >= min + 3 (ít nhất có 1 bước)
                b.foodMax = Mathf.Max(computedMax, b.foodMin + 3);
                b.foodMax = Mult3Floor(b.foodMax); // snap xuống mult3
                if (b.foodMax % 3 != 0) b.foodMax = Mult3Floor(b.foodMax);

                prevMax = b.foodMax;
            }
        }

        /// <summary>Recalc chỉ 1 band (khi user chỉnh capPercent hoặc grid của band đó),
        /// sau đó cascade toàn bộ bands phía sau.</summary>
        private void RecalcBandFoodFrom(int startBandIndex)
        {
            // Lấy prevMax từ band trước startBandIndex
            int prevMax = startBandIndex == 0
                ? 30
                : _bands[startBandIndex - 1].foodMax;

            for (int i = startBandIndex; i < _bands.Count; i++)
            {
                var b = _bands[i];
                if (!b.autoFoodFromCapacity) { prevMax = b.foodMax; continue; }

                int capAtEnd = ColRowLayerCap(b.colMax, b.rowMax, b.layerMax);
                int computedMax = Mult3Floor(Mathf.RoundToInt(capAtEnd * b.capacityPercent / 100f));
                computedMax = Mathf.Max(computedMax, 3);

                b.foodMin = (i == 0) ? 30 : prevMax;
                b.foodMax = Mathf.Max(computedMax, b.foodMin + 3);
                b.foodMax = Mult3Floor(b.foodMax);

                prevMax = b.foodMax;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // TAB: DESIGN
        // ═════════════════════════════════════════════════════════════════════

        private void DrawDesignTab()
        {
            // ── Refs ──────────────────────────────────────────────────────────
            EditorGUILayout.BeginVertical(BoxStyle(new Color(0.13f, 0.14f, 0.2f)));
            EditorGUILayout.LabelField("References & Settings", EditorStyles.boldLabel);
            _database = (LevelDatabase)EditorGUILayout.ObjectField(
                "Level Database", _database, typeof(LevelDatabase), false);
            _foodDb = (FoodDatabase)EditorGUILayout.ObjectField(
                "Food Database", _foodDb, typeof(FoodDatabase), false);
            _savePath = EditorGUILayout.TextField("Save Path", _savePath);
            EditorGUILayout.BeginHorizontal();
            _useRandomSeed = EditorGUILayout.Toggle("Random Seed", _useRandomSeed, GUILayout.Width(200));
            GUI.enabled = _useRandomSeed;
            _randomSeed = EditorGUILayout.IntField(_randomSeed, GUILayout.Width(80));
            if (GUILayout.Button("🎲", GUILayout.Width(28))) _randomSeed = UnityEngine.Random.Range(1, 99999);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // ── Band list header ──────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Level Bands ({_bands.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Band", GUILayout.Width(65)))
            {
                int lastTo = _bands.Count > 0 ? _bands[^1].toLevel : 0;
                int from = Mathf.Min(lastTo + 1, _totalLevels);
                int to = Mathf.Min(from + 19, _totalLevels);
                var b = new LevelBand
                {
                    label = $"Band {_bands.Count + 1}",
                    fromLevel = from,
                    toLevel = to,
                    foldout = true,
                    autoFoodFromCapacity = true,
                    capacityPercent = 70
                };
                _bands.Add(b);
                RecalcBandFoodFrom(_bands.Count - 1);
                _previewDirty = true;
            }
            EditorGUILayout.EndHorizontal();

            ValidateBandRanges();

            _scrollDesign = EditorGUILayout.BeginScrollView(_scrollDesign);

            for (int bi = 0; bi < _bands.Count; bi++)
            {
                if (DrawBand(bi)) _previewDirty = true;
            }

            DrawCoverageInfo();

            EditorGUILayout.EndScrollView();
        }

        // ─── Single Band ──────────────────────────────────────────────────────

        private bool DrawBand(int bi)
        {
            var band = _bands[bi];
            bool changed = false;

            float hue = _bands.Count > 1 ? (float)bi / (_bands.Count - 1) * 0.5f + 0.55f : 0.6f;
            var bgColor = Color.HSVToRGB(hue % 1f, 0.3f, 0.2f);

            EditorGUILayout.BeginVertical(BoxStyle(bgColor));

            // ── Header row ────────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            var foldStyle = new GUIStyle(EditorStyles.foldoutHeader) { fontStyle = FontStyle.Bold };
            band.foldout = EditorGUILayout.Foldout(band.foldout,
                $"  {band.label}   Lv{band.fromLevel}→{band.toLevel}" +
                $"   Food {band.foodMin}–{band.foodMax}" +
                $"   Grid {band.colMin}×{band.rowMin}→{band.colMax}×{band.rowMax}",
                true, foldStyle);
            GUILayout.FlexibleSpace();

            GUI.enabled = bi > 0;
            if (GUILayout.Button("▲", GUILayout.Width(22)))
            { (_bands[bi], _bands[bi - 1]) = (_bands[bi - 1], _bands[bi]); RecalcAllBandFood(); return true; }
            GUI.enabled = bi < _bands.Count - 1;
            if (GUILayout.Button("▼", GUILayout.Width(22)))
            { (_bands[bi], _bands[bi + 1]) = (_bands[bi + 1], _bands[bi]); RecalcAllBandFood(); return true; }
            GUI.enabled = true;

            GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                if (EditorUtility.DisplayDialog("Delete", $"Xóa '{band.label}'?", "OK", "Cancel"))
                {
                    _bands.RemoveAt(bi);
                    RecalcAllBandFood();
                    return true;
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (!band.foldout) { EditorGUILayout.EndVertical(); return changed; }

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();

            // Label + range
            band.label = EditorGUILayout.TextField("Label", band.label);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Level Range", GUILayout.Width(100));
            band.fromLevel = EditorGUILayout.IntField(band.fromLevel, GUILayout.Width(58));
            EditorGUILayout.LabelField("→", GUILayout.Width(16));
            band.toLevel = EditorGUILayout.IntField(band.toLevel, GUILayout.Width(58));
            int bSize = Mathf.Max(0, band.toLevel - band.fromLevel + 1);
            EditorGUILayout.LabelField($"({bSize} lvs)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // ── Grid ──────────────────────────────────────────────────────────
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Grid (start → end of band)", EditorStyles.boldLabel);

            bool gridChanged = false;
            EditorGUI.BeginChangeCheck();
            DrawMinMaxRow("Columns", ref band.colMin, ref band.colMax, 4, 8);
            DrawMinMaxRow("Rows", ref band.rowMin, ref band.rowMax, 2, 8);
            DrawMinMaxRow("Layers", ref band.layerMin, ref band.layerMax, 2, 6);
            if (EditorGUI.EndChangeCheck()) gridChanged = true;

            int capAtEnd = ColRowLayerCap(band.colMax, band.rowMax, band.layerMax);
            int capAtStart = ColRowLayerCap(band.colMin, band.rowMin, band.layerMin);

            // ── Auto Food from Capacity ───────────────────────────────────────
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Food Settings", EditorStyles.boldLabel);

            bool autoChanged = false;
            EditorGUI.BeginChangeCheck();
            band.autoFoodFromCapacity = EditorGUILayout.Toggle(
                new GUIContent("Auto Food from Capacity",
                    "Tự tính foodMin/Max dựa theo % capacity của level cuối band.\n" +
                    "Band 1: foodMin = 30\n" +
                    "Band 2+: foodMin = foodMax của band trước\n" +
                    "foodMax = Mult3Floor(capAtEnd × capacityPercent%)"),
                band.autoFoodFromCapacity, GUILayout.Width(300));
            if (EditorGUI.EndChangeCheck()) autoChanged = true;

            bool capPctChanged = false;
            if (band.autoFoodFromCapacity)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    new GUIContent("Capacity %",
                        "% capacity của level cuối band (grid max) → dùng làm foodMax.\n" +
                        "Ví dụ: 70 → foodMax = 70% cap của level cuối."),
                    GUILayout.Width(130));
                EditorGUI.BeginChangeCheck();
                int newPct = EditorGUILayout.IntSlider(band.capacityPercent, 10, 100, GUILayout.Width(200));
                if (EditorGUI.EndChangeCheck()) { band.capacityPercent = newPct; capPctChanged = true; }
                int previewMax = Mult3Floor(Mathf.RoundToInt(capAtEnd * band.capacityPercent / 100f));
                EditorGUILayout.LabelField($"→ foodMax ≈ {previewMax}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                // Cascade recalc khi grid hoặc % thay đổi
                if (gridChanged || capPctChanged || autoChanged)
                    RecalcBandFoodFrom(bi);

                // Show readonly foodMin / foodMax
                GUI.enabled = false;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("foodMin (auto)", GUILayout.Width(130));
                EditorGUILayout.IntField(band.foodMin, GUILayout.Width(58));
                GUILayout.Space(20);
                EditorGUILayout.LabelField("foodMax (auto)", GUILayout.Width(130));
                EditorGUILayout.IntField(band.foodMax, GUILayout.Width(58));
                EditorGUILayout.EndHorizontal();
                GUI.enabled = true;
            }
            else
            {
                // Manual mode — user nhập tay
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("foodMin (manual)", GUILayout.Width(130));
                band.foodMin = EditorGUILayout.IntField(band.foodMin, GUILayout.Width(58));
                GUILayout.Space(20);
                EditorGUILayout.LabelField("foodMax (manual)", GUILayout.Width(130));
                band.foodMax = EditorGUILayout.IntField(band.foodMax, GUILayout.Width(58));
                EditorGUILayout.EndHorizontal();
                band.foodMin = Mult3(Mathf.Max(3, band.foodMin));
                band.foodMax = Mult3(Mathf.Max(band.foodMin + 3, band.foodMax));

                // Cascade cho các band sau nếu chúng đang auto
                if (EditorGUI.EndChangeCheck())
                    RecalcBandFoodFrom(bi + 1);
            }

            // Thông tin capacity
            EditorGUILayout.HelpBox(
                $"Grid cap: start={capAtStart}  end={capAtEnd}" +
                $"   |   Food: {band.foodMin} → {band.foodMax}" +
                (band.autoFoodFromCapacity
                    ? $"   |   {band.capacityPercent}% of end-cap"
                    : "   |   manual"),
                MessageType.None);

            // ── Obstacles ─────────────────────────────────────────────────────
            if (band.autoFoodFromCapacity)
                EditorGUI.EndChangeCheck(); // close the outer BeginChangeCheck opened before grid

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            band.obstaclesFoldout = EditorGUILayout.Foldout(band.obstaclesFoldout,
                $"Obstacles ({band.obstacles.Count(o => o.enabled)}/{band.obstacles.Count} active)",
                true);
            GUILayout.FlexibleSpace();
            GUI.enabled = band.obstacles.Count < 3;
            if (GUILayout.Button("+ Add", GUILayout.Width(56)))
                band.obstacles.Add(new ObstacleBandConfig { enabled = true });
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (band.obstaclesFoldout)
                for (int oi = 0; oi < band.obstacles.Count; oi++)
                    DrawObstacleBand(band, oi);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);

            // changed nếu có bất kỳ thay đổi nào
            bool anyChange = gridChanged || capPctChanged || autoChanged;
            return anyChange;
        }

        // ─── Obstacle sub-panel ───────────────────────────────────────────────

        private void DrawObstacleBand(LevelBand band, int oi)
        {
            var obs = band.obstacles[oi];
            EditorGUILayout.BeginVertical(BoxStyle(new Color(0.07f, 0.1f, 0.16f)));

            EditorGUILayout.BeginHorizontal();
            obs.foldout = EditorGUILayout.Foldout(obs.foldout,
                $"  [{oi + 1}] {obs.kind}{(obs.enabled ? "" : "  ✗")}", true);
            obs.enabled = EditorGUILayout.Toggle(obs.enabled, GUILayout.Width(18));
            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                band.obstacles.RemoveAt(oi);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (!obs.foldout) { EditorGUILayout.EndVertical(); return; }

            EditorGUI.indentLevel++;
            obs.kind = (ObstacleBandConfig.Kind)EditorGUILayout.EnumPopup("Type", obs.kind);
            EditorGUILayout.LabelField(
                "Min = độ khó đầu band  |  Max = độ khó cuối band  (interpolate tuyến tính)",
                EditorStyles.miniLabel);

            switch (obs.kind)
            {
                case ObstacleBandConfig.Kind.Lock:
                    DrawMinMaxRow("lockedTrayCount", ref obs.lockCountMin, ref obs.lockCountMax, 1, 10);
                    DrawMinMaxRow("defaultLockHp", ref obs.lockHpMin, ref obs.lockHpMax, 1, 20);
                    break;
                case ObstacleBandConfig.Kind.Tube:
                    DrawMinMaxRow("tubeCount", ref obs.tubeCountMin, ref obs.tubeCountMax, 1, 4);
                    DrawMinMaxRow("foodPerTube", ref obs.tubeFoodMin, ref obs.tubeFoodMax, 1, 20);
                    break;
                case ObstacleBandConfig.Kind.Conveyor:
                    DrawMinMaxRow("conveyorCount", ref obs.conveyorCountMin, ref obs.conveyorCountMax, 1, 20);
                    DrawMinMaxRow("foodPerConveyor", ref obs.conveyorFoodPerTrayMin, ref obs.conveyorFoodPerTrayMax, 1, 6);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("speed", GUILayout.Width(120));
                    obs.conveyorSpeedMin = EditorGUILayout.FloatField(obs.conveyorSpeedMin, GUILayout.Width(55));
                    EditorGUILayout.LabelField("→", GUILayout.Width(16));
                    obs.conveyorSpeedMax = EditorGUILayout.FloatField(obs.conveyorSpeedMax, GUILayout.Width(55));
                    EditorGUILayout.EndHorizontal();
                    obs.conveyorSpeedMin = Mathf.Max(0.1f, obs.conveyorSpeedMin);
                    obs.conveyorSpeedMax = Mathf.Max(obs.conveyorSpeedMin, obs.conveyorSpeedMax);
                    break;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // ─── Coverage info ────────────────────────────────────────────────────

        private void DrawCoverageInfo()
        {
            if (_bands.Count == 0) return;
            int covered = _bands.Sum(b => Mathf.Max(0, b.toLevel - b.fromLevel + 1));
            if (covered < _totalLevels)
                EditorGUILayout.HelpBox(
                    $"⚠ Bao phủ {covered}/{_totalLevels} levels. " +
                    $"Lv {covered + 1}–{_totalLevels} dùng band cuối.", MessageType.Warning);
            else if (covered > _totalLevels)
                EditorGUILayout.HelpBox(
                    $"⚠ Bands vượt quá Total Levels (tổng {covered} > {_totalLevels}).", MessageType.Warning);
            else
                EditorGUILayout.HelpBox($"✅ Bao phủ đúng {_totalLevels} levels.", MessageType.Info);
        }

        // ═════════════════════════════════════════════════════════════════════
        // TAB: PREVIEW
        // ═════════════════════════════════════════════════════════════════════

        private void DrawPreviewTab()
        {
            if (_previewDirty) RebuildPreview();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"Preview — {_totalLevels} levels, {_bands.Count} bands",
                EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Jump:", GUILayout.Width(38));
            _previewJump = EditorGUILayout.IntField(_previewJump, GUILayout.Width(48));
            if (GUILayout.Button("Go", GUILayout.Width(32)))
                _scrollPreview = new Vector2(0, Mathf.Max(0, (_previewJump - 5)) * 21f);
            if (GUILayout.Button("↺", GUILayout.Width(26))) _previewDirty = true;
            EditorGUILayout.EndHorizontal();

            // Header
            EditorGUILayout.BeginHorizontal(new GUIStyle(EditorStyles.toolbar));
            TH("Lv", 44); TH("Band", 82); TH("Food", 52);
            TH("Cols", 40); TH("Rows", 40); TH("Layers", 48);
            TH("Cap", 50); TH("Obstacles", 230);
            EditorGUILayout.EndHorizontal();

            _scrollPreview = EditorGUILayout.BeginScrollView(_scrollPreview,
                GUILayout.Height(position.height - 170));

            string prevBand = null;
            for (int i = 0; i < _previewRows.Count; i++)
            {
                var row = _previewRows[i];
                if (row.bandLabel != prevBand)
                {
                    prevBand = row.bandLabel;
                    GUI.backgroundColor = new Color(0.2f, 0.4f, 0.65f, 0.5f);
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"── {row.bandLabel} ──",
                        new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.6f, 0.85f, 1f) } });
                    EditorGUILayout.EndHorizontal();
                    GUI.backgroundColor = Color.white;
                }

                bool hasObs = row.obstacles != "—";
                GUI.backgroundColor = hasObs
                    ? new Color(0.18f, 0.32f, 0.18f)
                    : (i % 2 == 0 ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.21f, 0.21f, 0.21f));

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                TC($"{row.level}", 44); TC(row.bandLabel, 82); TC($"{row.food}", 52);
                TC($"{row.cols}", 40); TC($"{row.rows}", 40); TC($"{row.layers}", 48);
                TC($"{row.cap}", 50); TC(row.obstacles, 230);
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();
        }

        // ═════════════════════════════════════════════════════════════════════
        // TAB: GENERATE
        // ═════════════════════════════════════════════════════════════════════

        private void DrawGenerateTab()
        {
            EditorGUILayout.BeginVertical(BoxStyle(new Color(0.09f, 0.17f, 0.1f)));
            EditorGUILayout.LabelField("Generate Settings", EditorStyles.boldLabel);
            _savePath = EditorGUILayout.TextField("Save Path", _savePath);
            _clearExisting = EditorGUILayout.Toggle("Clear Existing", _clearExisting);
            EditorGUILayout.Space(4);

            if (_database == null || _foodDb == null)
                EditorGUILayout.HelpBox("⚠ Chưa gán Level Database hoặc Food Database!", MessageType.Error);
            else
                EditorGUILayout.HelpBox(
                    $"✅ Ready: {_totalLevels} levels, {_bands.Count} bands, " +
                    $"{_foodDb.allFoods.Count} food types.", MessageType.Info);

            EditorGUILayout.Space(4);
            foreach (var b in _bands)
            {
                int act = b.obstacles.Count(o => o.enabled);
                string foodInfo = b.autoFoodFromCapacity
                    ? $"Food {b.foodMin}→{b.foodMax} ({b.capacityPercent}% cap)"
                    : $"Food {b.foodMin}→{b.foodMax} (manual)";
                EditorGUILayout.LabelField(
                    $"  • {b.label}: Lv{b.fromLevel}–{b.toLevel}  " +
                    foodInfo +
                    $"  Grid {b.colMin}×{b.rowMin}→{b.colMax}×{b.rowMax}  " +
                    $"{act} obstacle(s)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            bool canGen = _database != null && _foodDb != null && _bands.Count > 0;
            GUI.enabled = canGen;
            GUI.backgroundColor = canGen ? new Color(0.28f, 0.82f, 0.42f) : Color.gray;
            if (GUILayout.Button($"⚡ GENERATE {_totalLevels} LEVELS", GUILayout.Height(46)))
                if (EditorUtility.DisplayDialog("Confirm",
                        $"Tạo {_totalLevels} levels vào\n'{_savePath}' ?", "Generate!", "Cancel"))
                    DoGenerate();
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(0.45f, 0.6f, 1f);
            if (GUILayout.Button("🔄 Auto-Find Databases", GUILayout.Height(26))) AutoFindDatabases();
            GUI.backgroundColor = Color.white;
        }

        // ═════════════════════════════════════════════════════════════════════
        // GENERATE LOGIC
        // ═════════════════════════════════════════════════════════════════════

        private void DoGenerate()
        {
            if (!Directory.Exists(_savePath)) { Directory.CreateDirectory(_savePath); AssetDatabase.Refresh(); }
            if (_clearExisting) ClearOldLevels();
            if (_previewDirty) RebuildPreview();

            int maxFT = Mathf.Min(_foodDb.allFoods.Count, 9);
            var newLevels = new List<LevelConfig>();
            int created = 0;

            for (int i = 0; i < _previewRows.Count; i++)
            {
                var row = _previewRows[i];
                EditorUtility.DisplayProgressBar("Generating...",
                    $"Level {row.level}/{_totalLevels}", (float)i / _totalLevels);

                var cfg = CreateInstance<LevelConfig>();
                cfg.levelIndex = row.level;
                cfg.totalFoodCount = row.food;
                cfg.trayColumns = row.cols;
                cfg.trayRows = row.rows;
                cfg.layerCount = row.layers;
                cfg.maxActiveOrders = 2;
                cfg.backupTrayCapacity = 5;
                cfg.timeLimitSeconds = 0f;
                cfg.levelDisplayName = GetLevelName(row.level, _totalLevels);

                float tGlobal = _totalLevels > 1 ? (float)(row.level - 1) / (_totalLevels - 1) : 1f;
                int ftCount = Mathf.RoundToInt(Mathf.Lerp(MIN_FOOD_TYPES, maxFT, tGlobal));
                cfg.availableFoods = PickFoods(_foodDb.allFoods, ftCount, row.level);

                var band = GetBandForLevel(row.level);
                if (band != null)
                {
                    float tBand = GetBandT(band, row.level);
                    cfg.obstacles = BuildObstacles(band, tBand);
                }

                string path = $"{_savePath}/Level_{row.level:D4}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
                if (existing != null && !_clearExisting)
                {
                    EditorUtility.CopySerialized(cfg, existing);
                    EditorUtility.SetDirty(existing);
                    newLevels.Add(existing);
                    DestroyImmediate(cfg);
                }
                else
                {
                    AssetDatabase.CreateAsset(cfg, path);
                    newLevels.Add(cfg);
                }
                created++;
            }

            EditorUtility.ClearProgressBar();
            _database.levels = newLevels;
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Done!", $"✅ Created {created} levels.", "OK");
        }

        private List<ObstacleData> BuildObstacles(LevelBand band, float t)
        {
            var list = new List<ObstacleData>();
            foreach (var obs in band.obstacles)
            {
                if (!obs.enabled) continue;
                switch (obs.kind)
                {
                    case ObstacleBandConfig.Kind.Lock:
                        list.Add(new LockObstacleData
                        {
                            isEnabled = true,
                            lockedTrayCount = LerpInt(obs.lockCountMin, obs.lockCountMax, t),
                            defaultLockHp = LerpInt(obs.lockHpMin, obs.lockHpMax, t),
                            perTrayHpOverride = Array.Empty<int>()
                        });
                        break;
                    case ObstacleBandConfig.Kind.Tube:
                        list.Add(new TubeObstacleData
                        {
                            isEnabled = true,
                            tubeCount = LerpInt(obs.tubeCountMin, obs.tubeCountMax, t),
                            defaultFoodPerTube = LerpInt(obs.tubeFoodMin, obs.tubeFoodMax, t),
                            perTubeFoodCount = Array.Empty<int>()
                        });
                        break;
                    case ObstacleBandConfig.Kind.Conveyor:
                        list.Add(new ConveyorObstacleData
                        {
                            isEnabled = true,
                            conveyorCount = LerpInt(obs.conveyorCountMin, obs.conveyorCountMax, t),
                            foodPerConveyor = LerpInt(obs.conveyorFoodPerTrayMin, obs.conveyorFoodPerTrayMax, t),
                            speed = Mathf.Lerp(obs.conveyorSpeedMin, obs.conveyorSpeedMax, t)
                        });
                        break;
                }
            }
            return list;
        }

        // ═════════════════════════════════════════════════════════════════════
        // PREVIEW REBUILD
        // ═════════════════════════════════════════════════════════════════════

        private void RebuildPreview()
        {
            _previewRows.Clear();

            var foodMap = new Dictionary<int, int>();
            foreach (var band in _bands)
                ComputeBandFoodSeries(band, foodMap);

            for (int lvl = 1; lvl <= _totalLevels; lvl++)
            {
                var band = GetBandForLevel(lvl);
                if (band == null) continue;
                float t = GetBandT(band, lvl);

                int cols = StaggerLerp(band.colMin, band.colMax, t, 0.50f, 1.00f);
                int rows = StaggerLerp(band.rowMin, band.rowMax, t, 0.25f, 0.75f);
                int layers = StaggerLerp(band.layerMin, band.layerMax, t, 0.00f, 0.50f);
                int cap = ColRowLayerCap(cols, rows, layers);

                int food = foodMap.TryGetValue(lvl, out int f) ? f
                    : Mult3Nearest(Mathf.Clamp(LerpInt(band.foodMin, band.foodMax, t), 3, cap));

                var parts = new List<string>();
                foreach (var obs in band.obstacles)
                {
                    if (!obs.enabled) continue;
                    switch (obs.kind)
                    {
                        case ObstacleBandConfig.Kind.Lock:
                            parts.Add($"Lock({LerpInt(obs.lockCountMin, obs.lockCountMax, t)}×hp{LerpInt(obs.lockHpMin, obs.lockHpMax, t)})");
                            break;
                        case ObstacleBandConfig.Kind.Tube:
                            parts.Add($"Tube({LerpInt(obs.tubeCountMin, obs.tubeCountMax, t)}×f{LerpInt(obs.tubeFoodMin, obs.tubeFoodMax, t)})");
                            break;
                        case ObstacleBandConfig.Kind.Conveyor:
                            parts.Add($"Conv({LerpInt(obs.conveyorCountMin, obs.conveyorCountMax, t)}×{LerpInt(obs.conveyorFoodPerTrayMin, obs.conveyorFoodPerTrayMax, t)} s{Mathf.Lerp(obs.conveyorSpeedMin, obs.conveyorSpeedMax, t):F1})");
                            break;
                    }
                }

                _previewRows.Add(new LevelPreviewRow
                {
                    level = lvl,
                    food = food,
                    cols = cols,
                    rows = rows,
                    layers = layers,
                    cap = cap,
                    obstacles = parts.Count > 0 ? string.Join(" | ", parts) : "—",
                    bandLabel = band.label
                });
            }
            _previewDirty = false;
        }

        // ═════════════════════════════════════════════════════════════════════
        // FOOD SERIES — SEGMENT FILL (dùng foodMin/Max đã được tính sẵn)
        // ═════════════════════════════════════════════════════════════════════

        private void ComputeBandFoodSeries(LevelBand band, Dictionary<int, int> outMap)
        {
            int from = band.fromLevel;
            int to = band.toLevel;
            int n = to - from + 1;
            if (n <= 0) return;

            // Tính cap cho từng level trong band
            int[] caps = new int[n];
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? (float)i / (n - 1) : 0f;
                int cols = StaggerLerp(band.colMin, band.colMax, t, 0.50f, 1.00f);
                int rows = StaggerLerp(band.rowMin, band.rowMax, t, 0.25f, 0.75f);
                int layers = StaggerLerp(band.layerMin, band.layerMax, t, 0.00f, 0.50f);
                caps[i] = Mult3Floor(ColRowLayerCap(cols, rows, layers));
            }

            int[] foods = new int[n];

            // food[0] = foodMin của band (đã được tính bởi RecalcBandFoodFrom)
            int fStart = Mult3Nearest(Mathf.Clamp(band.foodMin, 3, caps[0]));
            foods[0] = Mathf.Max(3, fStart);

            if (n == 1) { outMap[from] = foods[0]; return; }

            // Tìm segments (đoạn liên tiếp cùng cap)
            var segS = new List<int>();
            var segE = new List<int>();
            int s = 0;
            for (int i = 1; i < n; i++)
            {
                if (caps[i] != caps[s]) { segS.Add(s); segE.Add(i - 1); s = i; }
            }
            segS.Add(s); segE.Add(n - 1);

            for (int si = 0; si < segS.Count; si++)
            {
                int ss = segS[si];
                int se = segE[si];
                int segCap = caps[ss];

                int count = se - ss;
                if (count > 0)
                {
                    // Ceiling = min(segCap, foodMax của band)
                    int ceiling = Mathf.Min(segCap, Mult3Floor(band.foodMax));
                    float fEnd = ceiling;
                    float step = (fEnd - foods[ss]) / count;
                    float acc = foods[ss];

                    for (int j = 1; j <= count; j++)
                    {
                        int idx = ss + j;
                        acc += step;
                        int target = Mult3Floor((int)acc);
                        int minOk = Mult3Ceil(foods[idx - 1] + 3);
                        int chosen = Mathf.Min(Mathf.Max(minOk, target), segCap);
                        // Clamp to foodMax so food never exceeds the band's declared max
                        chosen = Mathf.Min(chosen, Mult3Floor(band.foodMax));
                        foods[idx] = chosen;
                        if (chosen > (int)acc) acc = chosen;
                    }
                }

                // Transition: set food[ns] của segment tiếp
                if (si + 1 < segS.Count)
                {
                    int ns = segS[si + 1];
                    int nsCap = caps[ns];
                    foods[ns] = Mathf.Min(
                        Mult3Ceil(foods[se] + 3),
                        Mathf.Min(nsCap, Mult3Floor(band.foodMax)));
                    foods[ns] = Mathf.Max(3, foods[ns]);
                }
            }

            for (int i = 0; i < n; i++)
                outMap[from + i] = foods[i];
        }

        // ─── Stagger lerp ─────────────────────────────────────────────────────
        private static int StaggerLerp(int minVal, int maxVal,
                                        float t, float t0, float t1)
        {
            float tLocal = t1 > t0
                ? Mathf.Clamp01((t - t0) / (t1 - t0))
                : (t >= t1 ? 1f : 0f);
            return Mathf.RoundToInt(Mathf.Lerp(minVal, maxVal, tLocal));
        }

        // ═════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private LevelBand GetBandForLevel(int lvl)
        {
            foreach (var b in _bands)
                if (lvl >= b.fromLevel && lvl <= b.toLevel) return b;
            return _bands.Count > 0 ? _bands[^1] : null;
        }

        private float GetBandT(LevelBand b, int lvl)
            => Mathf.Clamp01((float)(lvl - b.fromLevel) / Mathf.Max(1, b.toLevel - b.fromLevel));

        private void ValidateBandRanges()
        {
            for (int i = 0; i < _bands.Count; i++)
            {
                _bands[i].fromLevel = Mathf.Max(1, _bands[i].fromLevel);
                _bands[i].toLevel = Mathf.Clamp(_bands[i].toLevel,
                    _bands[i].fromLevel, _totalLevels);
            }
        }

        private List<FoodItemData> PickFoods(List<FoodItemData> all, int count, int lvl)
        {
            count = Mathf.Clamp(count, 1, all.Count);
            if (!_useRandomSeed) return new List<FoodItemData>(all.GetRange(0, count));
            var pool = new List<FoodItemData>(all);
            int seed = unchecked(_randomSeed ^ (lvl * (int)2654435761u));
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                int j = i + rng.Next(pool.Count - i);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            return pool.GetRange(0, count);
        }

        private void ResetToDefault()
        {
            _totalLevels = 100; _bandCount = 3;
            _bands = new List<LevelBand>();
            AutoSplitBands();
            if (_bands.Count >= 2)
                _bands[1].obstacles.Add(new ObstacleBandConfig
                {
                    enabled = true,
                    kind = ObstacleBandConfig.Kind.Lock,
                    lockCountMin = 1,
                    lockCountMax = 3,
                    lockHpMin = 2,
                    lockHpMax = 6
                });
            if (_bands.Count >= 3)
            {
                _bands[2].obstacles.Add(new ObstacleBandConfig
                {
                    enabled = true,
                    kind = ObstacleBandConfig.Kind.Lock,
                    lockCountMin = 2,
                    lockCountMax = 4,
                    lockHpMin = 3,
                    lockHpMax = 8
                });
                _bands[2].obstacles.Add(new ObstacleBandConfig
                {
                    enabled = true,
                    kind = ObstacleBandConfig.Kind.Conveyor,
                    conveyorCountMin = 2,
                    conveyorCountMax = 6,
                    conveyorFoodPerTrayMin = 1,
                    conveyorFoodPerTrayMax = 3,
                    conveyorSpeedMin = 20f,
                    conveyorSpeedMax = 80f
                });
            }
            _previewDirty = true;
        }

        private void AutoFindDatabases()
        {
            var lg = AssetDatabase.FindAssets("t:LevelDatabase");
            if (lg.Length > 0) _database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(
                AssetDatabase.GUIDToAssetPath(lg[0]));
            var fg = AssetDatabase.FindAssets("t:FoodDatabase");
            if (fg.Length > 0) _foodDb = AssetDatabase.LoadAssetAtPath<FoodDatabase>(
                AssetDatabase.GUIDToAssetPath(fg[0]));
        }

        private void ClearOldLevels()
        {
            foreach (var g in AssetDatabase.FindAssets("t:LevelConfig", new[] { _savePath }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));
            AssetDatabase.Refresh();
        }

        private string GetLevelName(int idx, int total)
        {
            float t = total <= 1 ? 1f : (float)(idx - 1) / (total - 1);
            string[] easy = { "Morning Snack", "Breakfast", "Brunch", "Lunch Break", "Picnic", "Garden Party" };
            string[] medium = { "Afternoon Rush", "Teatime", "Happy Hour", "Dinner Prep", "Street Food", "Night Market", "Weekend Special", "Food Court" };
            string[] hard = { "Chef's Challenge", "Grand Buffet", "VIP Banquet", "Food Festival", "Michelin Star", "Iron Chef", "Ultimate Feast", "Legend Mode", "Master Class", "World Cuisine" };
            if (t < 0.35f) { int i = Mathf.FloorToInt(t / 0.35f * easy.Length); return easy[Mathf.Clamp(i, 0, easy.Length - 1)]; }
            if (t < 0.70f) { int i = Mathf.FloorToInt((t - 0.35f) / 0.35f * medium.Length); return medium[Mathf.Clamp(i, 0, medium.Length - 1)]; }
            { int i = Mathf.FloorToInt((t - 0.70f) / 0.30f * hard.Length); return hard[Mathf.Clamp(i, 0, hard.Length - 1)]; }
        }

        // ─── Static utils ─────────────────────────────────────────────────────

        private static int LerpInt(int a, int b, float t)
            => Mathf.RoundToInt(Mathf.Lerp(a, b, Mathf.Clamp01(t)));

        private static int Mult3(int v) => ((v + 1) / 3) * 3;
        private static int Mult3Nearest(int v) => ((v + 1) / 3) * 3;
        private static int Mult3Floor(int v) => (v / 3) * 3;
        private static int Mult3Ceil(int v) => ((v + 2) / 3) * 3;

        private static int ColRowLayerCap(int c, int r, int l)
            => c * r * l * ANCHORS_PER_LAYER;

        private static void DrawMinMaxRow(string label,
            ref int min, ref int max, int hardMin, int hardMax)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(130));
            min = EditorGUILayout.IntField(min, GUILayout.Width(52));
            EditorGUILayout.LabelField("→", GUILayout.Width(16));
            max = EditorGUILayout.IntField(max, GUILayout.Width(52));
            min = Mathf.Clamp(min, hardMin, hardMax);
            max = Mathf.Clamp(max, min, hardMax);
            EditorGUILayout.EndHorizontal();
        }

        private static bool TabBtn(string label, bool active)
        {
            var s = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
                fontSize = active ? 12 : 11
            };
            if (active) GUI.backgroundColor = new Color(0.28f, 0.6f, 1f);
            bool r = GUILayout.Button(label, s, GUILayout.Height(26));
            GUI.backgroundColor = Color.white;
            return r;
        }

        private static GUIStyle BoxStyle(Color bg)
        {
            var s = new GUIStyle(EditorStyles.helpBox);
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, bg); tex.Apply();
            s.normal.background = tex;
            return s;
        }

        private static void DrawHLine(Color c, float h = 1f)
        {
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, h), c);
        }

        private static void TH(string t, float w) =>
            EditorGUILayout.LabelField(t, EditorStyles.toolbarButton, GUILayout.Width(w));
        private static void TC(string t, float w) =>
            EditorGUILayout.LabelField(t, EditorStyles.miniLabel, GUILayout.Width(w));
    }
}
#endif