#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using FoodMatch.Items;
using FoodMatch.Core;

namespace FoodMatch.Editor
{
    /// <summary>
    /// Custom Inspector cho BoosterManager.
    /// Hiển thị bảng debug trực quan với buttons tăng/reset từng booster.
    /// Tác dụng ngay trong Play Mode — không cần restart game.
    /// </summary>
    [CustomEditor(typeof(BoosterManager))]
    public class BoosterManagerEditor : UnityEditor.Editor
    {
        // ── Style cache ───────────────────────────────────────────────────────
        private GUIStyle _headerStyle;
        private GUIStyle _rowLockedStyle;
        private GUIStyle _rowUnlockedStyle;
        private GUIStyle _qtyStyle;

        private bool _stylesInit = false;

        private void InitStyles()
        {
            if (_stylesInit) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
            };

            _rowLockedStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(6, 6, 4, 4),
                margin = new RectOffset(0, 0, 2, 2),
            };

            _qtyStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
            };

            _stylesInit = true;
        }

        public override void OnInspectorGUI()
        {
            // Vẽ Inspector mặc định
            DrawDefaultInspector();

            var manager = (BoosterManager)target;

            // Chỉ hiện debug panel khi đang Play
            if (!Application.isPlaying)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox("▶ Debug panel chỉ hoạt động trong Play Mode.", MessageType.Info);
                return;
            }

            var db = manager.Database;
            if (db == null)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox("⚠ Chưa gán BoosterDatabase vào field Database.", MessageType.Warning);
                return;
            }

            InitStyles();

            EditorGUILayout.Space(10);

            // ── Header ────────────────────────────────────────────────────────
            var headerRect = EditorGUILayout.GetControlRect(false, 28);
            EditorGUI.DrawRect(headerRect, new Color(0.15f, 0.15f, 0.2f));
            EditorGUI.LabelField(headerRect, "  🎮  BOOSTER DEBUG PANEL", new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.9f, 0.85f, 0.4f) },
                alignment = TextAnchor.MiddleLeft,
            });

            EditorGUILayout.Space(4);

            // ── Global buttons ────────────────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
                if (GUILayout.Button("+3 TẤT CẢ", GUILayout.Height(28)))
                {
                    foreach (var d in db.Boosters)
                        manager.AddBoosterQuantity(d.boosterName, 3);
                }

                GUI.backgroundColor = new Color(0.9f, 0.4f, 0.3f);
                if (GUILayout.Button("RESET TẤT CẢ", GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog("Reset Boosters",
                        "Xóa toàn bộ PlayerPrefs booster?", "OK", "Hủy"))
                        BoosterInventory.ResetAll(db);
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(6);

            // ── Per-booster rows ──────────────────────────────────────────────
            foreach (var data in db.Boosters)
            {
                bool unlocked = BoosterInventory.IsEverUnlocked(data);
                int qty = BoosterInventory.GetQuantity(data);

                // Row background
                var rowColor = unlocked
                    ? new Color(0.18f, 0.25f, 0.18f)
                    : new Color(0.22f, 0.18f, 0.18f);

                var rowRect = EditorGUILayout.BeginVertical();
                EditorGUI.DrawRect(new Rect(rowRect.x - 2, rowRect.y, rowRect.width + 4, rowRect.height + 8), rowColor);

                using (new EditorGUILayout.HorizontalScope())
                {
                    // Lock / unlock icon
                    string statusIcon = unlocked ? "✅" : $"🔒 Lv.{data.requiredLevel}";
                    GUILayout.Label(statusIcon, GUILayout.Width(70));

                    // Booster name
                    GUILayout.Label(data.boosterName, EditorStyles.boldLabel, GUILayout.Width(110));

                    // Quantity badge
                    var qtyColor = qty > 0
                        ? new Color(0.3f, 0.9f, 0.4f)
                        : new Color(0.9f, 0.4f, 0.3f);

                    var savedColor = GUI.contentColor;
                    GUI.contentColor = qtyColor;
                    GUILayout.Label($"x{qty}", _qtyStyle, GUILayout.Width(40));
                    GUI.contentColor = savedColor;

                    GUILayout.FlexibleSpace();

                    // +1 button
                    GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                    if (GUILayout.Button("+1", GUILayout.Width(36), GUILayout.Height(22)))
                        manager.AddBoosterQuantity(data.boosterName, 1);

                    // +5 button
                    GUI.backgroundColor = new Color(0.3f, 0.85f, 0.5f);
                    if (GUILayout.Button("+5", GUILayout.Width(36), GUILayout.Height(22)))
                        manager.AddBoosterQuantity(data.boosterName, 5);

                    // Reset row
                    GUI.backgroundColor = new Color(0.8f, 0.35f, 0.3f);
                    if (GUILayout.Button("✕", GUILayout.Width(26), GUILayout.Height(22)))
                    {
                        BoosterInventory.SetQuantity(data, 0);
                        // Fire event để UI refresh ngay
                        EventBus.RaiseBoosterActivated(data.boosterName);
                    }

                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
            }

            // Auto-repaint để qty update realtime
            Repaint();
        }
    }
}
#endif