#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using FoodMatch.Level;
using FoodMatch.Core;

namespace FoodMatch.Editor
{
    /// <summary>
    /// Editor window để test level flow trong Play Mode.
    /// Menu: FoodMatch ▶ Level Test Window
    /// </summary>
    public class LevelTestEditorWindow : EditorWindow
    {
        // ─── State ────────────────────────────────────────────────────────────
        private int _targetLevel = 1;
        private string _statusLog = "Chưa chạy Play Mode.";
        private Vector2 _scrollPos;

        // Style cache
        private GUIStyle _headerStyle;
        private GUIStyle _logStyle;
        private GUIStyle _winBtnStyle;
        private GUIStyle _loseBtnStyle;
        private GUIStyle _normalBtnStyle;
        private bool _stylesInitialized;

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("FoodMatch/Level Test Window")]
        public static void OpenWindow()
        {
            var window = GetWindow<LevelTestEditorWindow>("Level Test");
            window.minSize = new Vector2(280, 420);
            window.Show();
        }

        private void OnEnable()
        {
            // Theo dõi state thay đổi khi Play Mode đang chạy
            GameManager.OnGameStateChanged += OnGameStateChanged;
        }

        private void OnDisable()
        {
            GameManager.OnGameStateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameState state)
        {
            _statusLog = $"[GameState] → {state}  (frame {Time.frameCount})";
            Repaint();
        }

        // ─── GUI ──────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            InitStyles();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            DrawDivider();
            DrawLevelControls();
            DrawDivider();
            DrawSimulateSection();
            DrawDivider();
            DrawStatusSection();

            EditorGUILayout.EndScrollView();
        }

        // ─── Sections ─────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("🍔  FoodMatch — Level Tester", _headerStyle);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Vào Play Mode để sử dụng các nút bên dưới.", MessageType.Info);
            }
            else
            {
                var gm = GameManager.Instance;
                string stateLabel = gm != null ? gm.CurrentState.ToString() : "N/A";
                EditorGUILayout.HelpBox($"Play Mode  |  GameState: {stateLabel}", MessageType.None);
            }
        }

        private void DrawLevelControls()
        {
            EditorGUILayout.LabelField("LOAD LEVEL", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // Level index input
            EditorGUI.BeginDisabledGroup(!Application.isPlaying);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Level Index", GUILayout.Width(80));
            _targetLevel = EditorGUILayout.IntField(_targetLevel, GUILayout.Width(60));
            _targetLevel = Mathf.Max(1, _targetLevel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Load / Restart / Next buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("▶ Load Level", _normalBtnStyle, GUILayout.Height(32)))
                TryLoadLevel(_targetLevel);

            if (GUILayout.Button("↺ Restart", _normalBtnStyle, GUILayout.Height(32)))
                TryRestartLevel();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (GUILayout.Button("⏭  Next Level", _normalBtnStyle, GUILayout.Height(32)))
                TryNextLevel();

            EditorGUI.EndDisabledGroup();
        }

        private void DrawSimulateSection()
        {
            EditorGUILayout.LabelField("SIMULATE OUTCOME", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🏆  Auto WIN", _winBtnStyle, GUILayout.Height(36)))
                TriggerWin();

            if (GUILayout.Button("💀  Auto LOSE", _loseBtnStyle, GUILayout.Height(36)))
                TriggerLose();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUI.EndDisabledGroup();
        }

        private void DrawStateButton(string label, GameState state)
        {
            if (GUILayout.Button(label, EditorStyles.miniButton))
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ChangeState(state);
                    Log($"Force state → {state}");
                }
                else
                    LogError("GameManager.Instance is null.");
            }
        }

        private void DrawStatusSection()
        {
            EditorGUILayout.LabelField("STATUS LOG", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(_statusLog, _logStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Runtime info
            if (Application.isPlaying)
            {
                var lm = LevelManager.Instance;
                if (lm != null)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"Current Level : {lm.CurrentLevelIndex}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Config        : {(lm.CurrentConfig != null ? lm.CurrentConfig.GetDisplayName() : "null")}", EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                }
            }
        }

        // ─── Actions ──────────────────────────────────────────────────────────

        private void TryLoadLevel(int index)
        {
            var lm = LevelManager.Instance;
            if (lm == null) { LogError("LevelManager.Instance is null."); return; }
            lm.RequestLoadLevel(index);
            Log($"RequestLoadLevel({index})");
        }

        private void TryRestartLevel()
        {
            var lm = LevelManager.Instance;
            if (lm == null) { LogError("LevelManager.Instance is null."); return; }
            lm.RestartCurrentLevel();
            Log($"RestartCurrentLevel() → Level {lm.CurrentLevelIndex}");
        }

        private void TryNextLevel()
        {
            var lm = LevelManager.Instance;
            if (lm == null) { LogError("LevelManager.Instance is null."); return; }
            lm.LoadNextLevel();
            Log("LoadNextLevel()");
        }

        private void TriggerWin()
        {
            var gm = GameManager.Instance;
            if (gm == null) { LogError("GameManager.Instance is null."); return; }
            // Dùng EventBus để trigger đúng flow (giống runtime thật)
            EventBus.RaiseAllOrdersCompleted();
            Log("EventBus.RaiseAllOrdersCompleted() → WIN");
        }

        private void TriggerLose()
        {
            var gm = GameManager.Instance;
            if (gm == null) { LogError("GameManager.Instance is null."); return; }
            EventBus.RaiseBackupFull();
            Log("EventBus.RaiseBackupTrayFull() → LOSE");
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private void Log(string msg)
        {
            _statusLog = $"✅ {msg}";
            Debug.Log($"[LevelTestWindow] {msg}");
            Repaint();
        }

        private void LogError(string msg)
        {
            _statusLog = $"❌ {msg}";
            Debug.LogWarning($"[LevelTestWindow] {msg}");
            Repaint();
        }

        private void DrawDivider()
        {
            EditorGUILayout.Space(6);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            EditorGUILayout.Space(6);
        }

        // ─── Style Init ───────────────────────────────────────────────────────

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };

            _logStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                richText = true
            };

            _normalBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };

            _winBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.1f, 0.7f, 0.2f) },
                hover = { textColor = Color.green }
            };

            _loseBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.85f, 0.2f, 0.2f) },
                hover = { textColor = Color.red }
            };

            _stylesInitialized = true;
        }
    }
}
#endif