#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using FoodMatch.Items;

namespace FoodMatch.Editor
{
    /// <summary>
    /// Custom Inspector cho BoosterInstaller.
    /// Test từng booster trực tiếp trong Play Mode mà không cần UI.
    /// </summary>
    [CustomEditor(typeof(BoosterInstaller))]
    public class BoosterDebugEditor : UnityEditor.Editor
    {
        // Foldout state
        private bool _showDebug = true;
        private bool _showStatus = true;

        // Styles (khởi tạo lazy để tránh lỗi ngoài Play Mode)
        private GUIStyle _headerStyle;
        private GUIStyle _successStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _warningStyle;

        private void InitStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };

            _successStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.2f, 0.8f, 0.2f) }
            };

            _errorStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.9f, 0.2f, 0.2f) }
            };

            _warningStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.9f, 0.7f, 0.1f) }
            };
        }

        public override void OnInspectorGUI()
        {
            // Vẽ Inspector mặc định
            DrawDefaultInspector();

            InitStyles();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("── Booster Debug Tool ──", _headerStyle);
            EditorGUILayout.Space(4);

            // ── Status Panel ──────────────────────────────────────────────────
            _showStatus = EditorGUILayout.Foldout(_showStatus, "📊 Registry Status", true);
            if (_showStatus)
            {
                EditorGUI.indentLevel++;
                DrawRegistryStatus();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // ── Test Buttons ──────────────────────────────────────────────────
            _showDebug = EditorGUILayout.Foldout(_showDebug, "🎮 Test Boosters (Play Mode only)", true);
            if (_showDebug)
            {
                EditorGUI.indentLevel++;
                DrawTestButtons();
                EditorGUI.indentLevel--;
            }
        }

        // ─── Registry Status ──────────────────────────────────────────────────

        private void DrawRegistryStatus()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Vào Play Mode để xem trạng thái registry.",
                    MessageType.Info);
                return;
            }

            if (BoosterManager.Instance == null)
            {
                EditorGUILayout.LabelField("❌ BoosterManager.Instance = null", _errorStyle);
                return;
            }

            // Lấy registry qua reflection để hiển thị (chỉ dùng trong Editor)
            var registryField = typeof(BoosterManager).GetField(
                "_registry",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (registryField == null)
            {
                EditorGUILayout.LabelField("⚠️ Không đọc được _registry", _warningStyle);
                return;
            }

            var registry = registryField.GetValue(BoosterManager.Instance)
                as System.Collections.Generic.Dictionary<string, IBooster>;

            if (registry == null || registry.Count == 0)
            {
                EditorGUILayout.LabelField("⚠️ Registry trống — chưa AutoRegister?", _warningStyle);
                return;
            }

            EditorGUILayout.LabelField(
                $"✅ {registry.Count} booster đã đăng ký:", _successStyle);

            EditorGUILayout.Space(2);

            foreach (var kv in registry)
            {
                bool canExec = kv.Value.CanExecute();

                EditorGUILayout.BeginHorizontal();

                // Tên booster
                EditorGUILayout.LabelField(
                    $"  • {kv.Key}",
                    GUILayout.Width(140));

                // Trạng thái CanExecute
                GUIStyle statusStyle = canExec ? _successStyle : _warningStyle;
                string statusText = canExec ? "✅ Ready" : "⛔ Cannot Execute";
                EditorGUILayout.LabelField(statusText, statusStyle);

                EditorGUILayout.EndHorizontal();
            }
        }

        // ─── Test Buttons ─────────────────────────────────────────────────────

        private void DrawTestButtons()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Các nút test chỉ hoạt động trong Play Mode.",
                    MessageType.Warning);
                return;
            }

            if (BoosterManager.Instance == null)
            {
                EditorGUILayout.HelpBox(
                    "BoosterManager.Instance chưa sẵn sàng.",
                    MessageType.Error);
                return;
            }

            EditorGUILayout.Space(2);

            // ── Item 1 ────────────────────────────────────────────────────────
            DrawBoosterButton(
                label: "➕  Item 1 — Extra Tray",
                boosterName: "ExtraTray",
                color: new Color(0.3f, 0.7f, 1f));

            EditorGUILayout.Space(2);

            // ── Item 2 ────────────────────────────────────────────────────────
            DrawBoosterButton(
                label: "🧲  Item 2 — Magnet",
                boosterName: "Magnet",
                color: new Color(1f, 0.6f, 0.2f));

            EditorGUILayout.Space(2);

            // ── Item 3 ────────────────────────────────────────────────────────
            DrawBoosterButton(
                label: "🔀  Item 3 — Shuffle",
                boosterName: "Shuffle",
                color: new Color(0.6f, 0.4f, 1f));

            EditorGUILayout.Space(2);

            // ── Item 4 ────────────────────────────────────────────────────────
            DrawBoosterButton(
                label: "🧹  Item 4 — Clear Tray",
                boosterName: "ClearTray",
                color: new Color(0.3f, 0.9f, 0.5f));

            EditorGUILayout.Space(6);

            // ── Divider ───────────────────────────────────────────────────────
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // ── Force Execute (bỏ qua CanExecute) ────────────────────────────
            EditorGUILayout.LabelField("⚡ Force Execute (bỏ qua CanExecute):",
                EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            DrawForceButton("ExtraTray");
            DrawForceButton("Magnet");
            DrawForceButton("Shuffle");
            DrawForceButton("ClearTray");
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private void DrawBoosterButton(string label, string boosterName, Color color)
        {
            bool canExec = CheckCanExecute(boosterName);

            EditorGUILayout.BeginHorizontal();

            // Button với màu
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = canExec ? color : Color.grey;

            GUI.enabled = canExec;
            if (GUILayout.Button(label, GUILayout.Height(28)))
            {
                BoosterManager.Instance.UseBooster(boosterName);
                Debug.Log($"[BoosterDebug] Kích hoạt: {boosterName}");
            }
            GUI.enabled = true;

            GUI.backgroundColor = oldBg;

            // Badge CanExecute
            GUIStyle badge = canExec ? _successStyle : _errorStyle;
            EditorGUILayout.LabelField(
                canExec ? "✅" : "⛔",
                badge,
                GUILayout.Width(20));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawForceButton(string boosterName)
        {
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);

            if (GUILayout.Button($"Force: {boosterName}", GUILayout.Height(22)))
                ForceExecute(boosterName);

            GUI.backgroundColor = oldBg;
        }

        private bool CheckCanExecute(string boosterName)
        {
            if (BoosterManager.Instance == null) return false;

            var registryField = typeof(BoosterManager).GetField(
                "_registry",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (registryField == null) return false;

            var registry = registryField.GetValue(BoosterManager.Instance)
                as System.Collections.Generic.Dictionary<string, IBooster>;

            if (registry == null) return false;

            return registry.TryGetValue(boosterName, out var b) && b.CanExecute();
        }

        private void ForceExecute(string boosterName)
        {
            var registryField = typeof(BoosterManager).GetField(
                "_registry",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (registryField == null) return;

            var registry = registryField.GetValue(BoosterManager.Instance)
                as System.Collections.Generic.Dictionary<string, IBooster>;

            if (registry == null || !registry.TryGetValue(boosterName, out var booster))
            {
                Debug.LogWarning($"[BoosterDebug] Không tìm thấy: {boosterName}");
                return;
            }

            booster.Execute();
            Debug.Log($"[BoosterDebug] Force execute: {boosterName}");
        }
    }
}
#endif