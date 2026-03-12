// Editor/ObstacleBulkEditor.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Editor
{
    public class ObstacleBulkEditor : EditorWindow
    {
        // ─── Tabs ─────────────────────────────────────────────────────────────
        private enum Tab { ApplyPreset, QuickRange, Overview }
        private Tab _currentTab = Tab.ApplyPreset;

        // ─── Shared ───────────────────────────────────────────────────────────
        private LevelDatabase _database;
        private Vector2 _scroll;

        // ─── Tab 1: Apply Preset ──────────────────────────────────────────────
        private ObstaclePreset _preset;
        private int _presetFrom = 1;
        private int _presetTo = 10;
        private bool _onlyEmpty = true;

        // ─── Tab 2: Quick Range Setter ────────────────────────────────────────
        private enum ObstacleKind { Lock, Tube, Conveyor }
        private ObstacleKind _kind = ObstacleKind.Lock;
        private int _rangeFrom = 1;
        private int _rangeTo = 10;
        private bool _enableObstacle = true;

        // Lock fields
        private int _lockCount = 1;
        private int _lockHp = 3;
        private bool _useHpOverride;
        private string _hpOverrideRaw = ""; // "3,5,2" → parse thành int[]

        // Tube fields
        private int _tubeCount = 2;
        private int _tubeFoodDefault = 4;
        private bool _useTubeFoodOverride;
        private string _tubeFoodOverrideRaw = "";

        // Conveyor fields
        private int _conveyorFood = 6;
        private float _conveyorSpeed = 2f;

        // ─── Tab 3: Overview ──────────────────────────────────────────────────
        private bool _overviewLock;
        private bool _overviewTube;
        private bool _overviewConveyor;

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("FoodMatch/Obstacle Bulk Editor")]
        public static void Open() => GetWindow<ObstacleBulkEditor>("Obstacle Bulk Editor");

        private void OnGUI()
        {
            EditorGUILayout.Space(4);

            // ── Database field (luôn hiển thị) ────────────────────────────────
            _database = (LevelDatabase)EditorGUILayout.ObjectField(
                "Level Database", _database, typeof(LevelDatabase), false);

            if (_database == null)
            {
                EditorGUILayout.HelpBox("Kéo LevelDatabase SO vào đây để bắt đầu.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);

            // ── Tabs ──────────────────────────────────────────────────────────
            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab,
                new[] { "Apply Preset", "Quick Range", "Overview" });

            EditorGUILayout.Space(6);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            switch (_currentTab)
            {
                case Tab.ApplyPreset: DrawApplyPresetTab(); break;
                case Tab.QuickRange: DrawQuickRangeTab(); break;
                case Tab.Overview: DrawOverviewTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ═════════════════════════════════════════════════════════════════════
        // TAB 1 — Apply Preset
        // ═════════════════════════════════════════════════════════════════════
        private void DrawApplyPresetTab()
        {
            EditorGUILayout.LabelField("Apply Preset cho nhiều level", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Copy toàn bộ obstacle settings từ 1 preset vào range level.",
                MessageType.None);

            EditorGUILayout.Space(4);
            _preset = (ObstaclePreset)EditorGUILayout.ObjectField(
                "Preset", _preset, typeof(ObstaclePreset), false);

            DrawRangeFields(ref _presetFrom, ref _presetTo);
            _onlyEmpty = EditorGUILayout.Toggle(
                new GUIContent("Only Empty Levels", "Chỉ apply cho level chưa có obstacle"),
                _onlyEmpty);

            EditorGUILayout.Space(6);

            if (_preset == null)
            {
                EditorGUILayout.HelpBox("Chọn 1 Preset để tiếp tục.", MessageType.Warning);
                return;
            }

            var targets = GetLevelsInRange(_presetFrom, _presetTo, _onlyEmpty);
            DrawPreview(targets);

            EditorGUILayout.Space(4);
            DrawActionButtons(
                applyLabel: $"Apply '{_preset.name}' → {targets.Count} Levels",
                onApply: () =>
                {
                    foreach (var lvl in targets)
                    {
                        lvl.obstacles = _preset.CloneObstacles();
                        EditorUtility.SetDirty(lvl);
                    }
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("Done",
                        $"Đã apply '{_preset.name}' cho {targets.Count} levels.", "OK");
                },
                onClear: () =>
                {
                    foreach (var lvl in GetLevelsInRange(_presetFrom, _presetTo, false))
                    {
                        lvl.obstacles?.Clear();
                        EditorUtility.SetDirty(lvl);
                    }
                    AssetDatabase.SaveAssets();
                },
                targets.Count);
        }

        // ═════════════════════════════════════════════════════════════════════
        // TAB 2 — Quick Range Setter
        // ═════════════════════════════════════════════════════════════════════
        private void DrawQuickRangeTab()
        {
            EditorGUILayout.LabelField("Quick Range Setter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Set thẳng thông số 1 loại obstacle cho range level.\n" +
                "Obstacle chưa có sẽ được tạo mới. Đã có sẽ được cập nhật.",
                MessageType.None);

            EditorGUILayout.Space(4);

            // ── Loại obstacle ─────────────────────────────────────────────────
            _kind = (ObstacleKind)EditorGUILayout.EnumPopup("Obstacle Type", _kind);
            _enableObstacle = EditorGUILayout.Toggle("isEnabled", _enableObstacle);

            EditorGUILayout.Space(4);

            // ── Fields theo loại ──────────────────────────────────────────────
            switch (_kind)
            {
                case ObstacleKind.Lock: DrawLockFields(); break;
                case ObstacleKind.Tube: DrawTubeFields(); break;
                case ObstacleKind.Conveyor: DrawConveyorFields(); break;
            }

            EditorGUILayout.Space(6);
            DrawRangeFields(ref _rangeFrom, ref _rangeTo);

            EditorGUILayout.Space(4);
            var targets = GetLevelsInRange(_rangeFrom, _rangeTo, false);
            DrawPreview(targets);

            EditorGUILayout.Space(4);

            GUI.enabled = targets.Count > 0;
            if (GUILayout.Button(
                $"Apply {_kind} Settings → {targets.Count} Levels",
                GUILayout.Height(32)))
            {
                ApplyQuickRange(targets);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Done",
                    $"Đã cập nhật {_kind} cho {targets.Count} levels.", "OK");
            }
            GUI.enabled = true;

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button($"Remove {_kind} khỏi {targets.Count} Levels"))
            {
                if (EditorUtility.DisplayDialog("Confirm",
                    $"Xóa {_kind} obstacle khỏi {targets.Count} levels?", "OK", "Cancel"))
                {
                    RemoveObstacleKind(targets, _kind);
                    AssetDatabase.SaveAssets();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawLockFields()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🔒 Lock Settings", EditorStyles.boldLabel);
            _lockCount = EditorGUILayout.IntSlider("lockedTrayCount", _lockCount, 1, 10);
            _lockHp = EditorGUILayout.IntSlider("defaultLockHp", _lockHp, 1, 20);

            _useHpOverride = EditorGUILayout.Toggle(
                new GUIContent("Override per-tray HP",
                    "Nhập HP từng tray, cách nhau bằng dấu phẩy.\nVD: 3,5,2"),
                _useHpOverride);

            if (_useHpOverride)
            {
                EditorGUILayout.LabelField(
                    $"HP từng tray (cách nhau dấu phẩy, {_lockCount} giá trị):",
                    EditorStyles.miniLabel);
                _hpOverrideRaw = EditorGUILayout.TextField(_hpOverrideRaw);
                EditorGUILayout.LabelField(
                    $"Preview: [{_hpOverrideRaw}]", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawTubeFields()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🧪 Tube Settings", EditorStyles.boldLabel);
            _tubeCount = EditorGUILayout.IntSlider("tubeCount", _tubeCount, 1, 4);
            _tubeFoodDefault = EditorGUILayout.IntSlider("defaultFoodPerTube", _tubeFoodDefault, 1, 20);

            _useTubeFoodOverride = EditorGUILayout.Toggle(
                new GUIContent("Override per-tube food",
                    "Nhập số food từng ống, cách nhau bằng dấu phẩy.\nVD: 3,7,4"),
                _useTubeFoodOverride);

            if (_useTubeFoodOverride)
            {
                EditorGUILayout.LabelField(
                    $"Food từng ống (cách nhau dấu phẩy, {_tubeCount} giá trị):",
                    EditorStyles.miniLabel);
                _tubeFoodOverrideRaw = EditorGUILayout.TextField(_tubeFoodOverrideRaw);
                EditorGUILayout.LabelField(
                    $"Preview: [{_tubeFoodOverrideRaw}]", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawConveyorFields()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎢 Conveyor Settings", EditorStyles.boldLabel);
            _conveyorFood = EditorGUILayout.IntSlider("foodCount", _conveyorFood, 1, 30);
            _conveyorSpeed = EditorGUILayout.Slider("speed", _conveyorSpeed, 0.1f, 10f);
            EditorGUILayout.EndVertical();
        }

        private void ApplyQuickRange(List<LevelConfig> targets)
        {
            foreach (var lvl in targets)
            {
                if (lvl.obstacles == null) lvl.obstacles = new();

                switch (_kind)
                {
                    case ObstacleKind.Lock:
                        {
                            var data = GetOrCreate<LockObstacleData>(lvl);
                            data.isEnabled = _enableObstacle;
                            data.lockedTrayCount = _lockCount;
                            data.defaultLockHp = _lockHp;
                            data.perTrayHpOverride = _useHpOverride
                                ? ParseIntArray(_hpOverrideRaw)
                                : Array.Empty<int>();
                            break;
                        }
                    case ObstacleKind.Tube:
                        {
                            var data = GetOrCreate<TubeObstacleData>(lvl);
                            data.isEnabled = _enableObstacle;
                            data.tubeCount = _tubeCount;
                            data.defaultFoodPerTube = _tubeFoodDefault;
                            data.perTubeFoodCount = _useTubeFoodOverride
                                ? ParseIntArray(_tubeFoodOverrideRaw)
                                : Array.Empty<int>();
                            break;
                        }
                    case ObstacleKind.Conveyor:
                        {
                            var data = GetOrCreate<ConveyorObstacleData>(lvl);
                            data.isEnabled = _enableObstacle;
                            data.conveyorCount = _conveyorFood;
                            data.speed = _conveyorSpeed;
                            break;
                        }
                }
                EditorUtility.SetDirty(lvl);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // TAB 3 — Overview
        // ═════════════════════════════════════════════════════════════════════
        private void DrawOverviewTab()
        {
            EditorGUILayout.LabelField("Overview — Obstacles theo level", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _overviewLock = EditorGUILayout.ToggleLeft("🔒 Lock", _overviewLock, GUILayout.Width(80));
            _overviewTube = EditorGUILayout.ToggleLeft("🧪 Tube", _overviewTube, GUILayout.Width(80));
            _overviewConveyor = EditorGUILayout.ToggleLeft("🎢 Conveyor", _overviewConveyor, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Level", EditorStyles.toolbarButton, GUILayout.Width(50));
            EditorGUILayout.LabelField("Name", EditorStyles.toolbarButton, GUILayout.Width(120));
            if (_overviewLock)
            {
                EditorGUILayout.LabelField("Locks", EditorStyles.toolbarButton, GUILayout.Width(40));
                EditorGUILayout.LabelField("HP", EditorStyles.toolbarButton, GUILayout.Width(40));
            }
            if (_overviewTube)
            {
                EditorGUILayout.LabelField("Tubes", EditorStyles.toolbarButton, GUILayout.Width(40));
                EditorGUILayout.LabelField("Food/T", EditorStyles.toolbarButton, GUILayout.Width(50));
            }
            if (_overviewConveyor)
            {
                EditorGUILayout.LabelField("Conv.", EditorStyles.toolbarButton, GUILayout.Width(40));
                EditorGUILayout.LabelField("Speed", EditorStyles.toolbarButton, GUILayout.Width(50));
            }
            EditorGUILayout.EndHorizontal();

            // Rows
            foreach (var lvl in _database.levels)
            {
                if (lvl == null) continue;
                var lockData = lvl.GetObstacle<LockObstacleData>();
                var tubeData = lvl.GetObstacle<TubeObstacleData>();
                var conveyorData = lvl.GetObstacle<ConveyorObstacleData>();

                // Tô màu row có obstacle
                bool hasAny = lockData != null || tubeData != null || conveyorData != null;
                if (hasAny) GUI.backgroundColor = new Color(0.85f, 1f, 0.85f);

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                EditorGUILayout.LabelField($"{lvl.levelIndex}", GUILayout.Width(50));
                EditorGUILayout.LabelField(lvl.GetDisplayName(), GUILayout.Width(120));

                if (_overviewLock)
                {
                    EditorGUILayout.LabelField(
                        lockData != null ? $"{lockData.lockedTrayCount}" : "-",
                        GUILayout.Width(40));
                    EditorGUILayout.LabelField(
                        lockData != null ? $"{lockData.defaultLockHp}" : "-",
                        GUILayout.Width(40));
                }
                if (_overviewTube)
                {
                    EditorGUILayout.LabelField(
                        tubeData != null ? $"{tubeData.tubeCount}" : "-",
                        GUILayout.Width(40));
                    EditorGUILayout.LabelField(
                        tubeData != null ? $"{tubeData.defaultFoodPerTube}" : "-",
                        GUILayout.Width(50));
                }
                if (_overviewConveyor)
                {
                    EditorGUILayout.LabelField(
                        conveyorData != null ? "ON" : "-",
                        GUILayout.Width(40));
                    EditorGUILayout.LabelField(
                        conveyorData != null ? $"{conveyorData.speed:F1}" : "-",
                        GUILayout.Width(50));
                }

                // Nút ping để select LevelConfig trong Project
                if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
                    EditorGUIUtility.PingObject(lvl);

                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Helpers
        // ═════════════════════════════════════════════════════════════════════

        private void DrawRangeFields(ref int from, ref int to)
        {
            EditorGUILayout.BeginHorizontal();
            from = EditorGUILayout.IntField("From Level", from);
            to = EditorGUILayout.IntField("To Level", to);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreview(List<LevelConfig> targets)
        {
            if (targets.Count == 0)
            {
                EditorGUILayout.HelpBox("Không có level nào trong range này.", MessageType.Warning);
                return;
            }
            EditorGUILayout.HelpBox(
                $"Sẽ ảnh hưởng {targets.Count} levels: " +
                string.Join(", ", targets.Take(10).Select(l => $"Lv{l.levelIndex}")) +
                (targets.Count > 10 ? $" ... (+{targets.Count - 10})" : ""),
                MessageType.Info);
        }

        private void DrawActionButtons(string applyLabel, Action onApply,
            Action onClear, int count)
        {
            GUI.enabled = count > 0;
            if (GUILayout.Button(applyLabel, GUILayout.Height(32)))
                onApply?.Invoke();
            GUI.enabled = true;

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("Clear Obstacles (Range)"))
            {
                if (EditorUtility.DisplayDialog("Confirm",
                    "Xóa obstacles của range này?", "OK", "Cancel"))
                    onClear?.Invoke();
            }
            GUI.backgroundColor = Color.white;
        }

        private List<LevelConfig> GetLevelsInRange(int from, int to, bool onlyEmpty)
        {
            return _database.levels
                .Where(l => l != null
                    && l.levelIndex >= from
                    && l.levelIndex <= to
                    && (!onlyEmpty || l.obstacles == null || l.obstacles.Count == 0))
                .ToList();
        }

        private static T GetOrCreate<T>(LevelConfig lvl) where T : ObstacleData, new()
        {
            var existing = lvl.obstacles.OfType<T>().FirstOrDefault();
            if (existing != null) return existing;
            var newOne = new T();
            lvl.obstacles.Add(newOne);
            return newOne;
        }

        private static void RemoveObstacleKind(List<LevelConfig> targets, ObstacleKind kind)
        {
            Type type = kind switch
            {
                ObstacleKind.Lock => typeof(LockObstacleData),
                ObstacleKind.Tube => typeof(TubeObstacleData),
                ObstacleKind.Conveyor => typeof(ConveyorObstacleData),
                _ => null
            };
            if (type == null) return;
            foreach (var lvl in targets)
            {
                lvl.obstacles?.RemoveAll(o => o?.GetType() == type);
                EditorUtility.SetDirty(lvl);
            }
            AssetDatabase.SaveAssets();
        }

        private static int[] ParseIntArray(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<int>();
            return raw.Split(',')
                .Select(s => int.TryParse(s.Trim(), out int v) ? v : 0)
                .Where(v => v > 0)
                .ToArray();
        }
    }
}
#endif