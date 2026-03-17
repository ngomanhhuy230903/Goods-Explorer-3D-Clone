using UnityEngine;
using FoodMatch.Managers;
using FoodMatch.Level;
using FoodMatch.Core;
using FoodMatch.Items;
using System.Reflection;
using System.Collections.Generic;

namespace FoodMatch.DebugTools
{
    public class InGameDebugMenu : MonoBehaviour
    {
        private bool _showMenu = false;
        private int _currentTab = 0;
        private string[] _tabs = { "Currency", "Level", "Boosters" };

        private string _hpInput = "1";
        private string _coinInput = "50";
        private string _levelInput = "1";

        // Chiều cao để 0, Unity sẽ tự động kéo giãn vừa khít nội dung bên trong
        private Rect _windowRect = new Rect(10, 60, 420, 0);

        private GUIStyle _windowStyle;
        private bool _themeInitialized = false;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 480f, Screen.height / 800f, 1));

            SetupMinimalTheme();

            GUI.color = Color.white;
            ResetBackgroundColor();

            if (GUI.Button(new Rect(10, 10, 100, 40), _showMenu ? "CLOSE" : "DEBUG"))
            {
                _showMenu = !_showMenu;
            }

            if (!_showMenu) return;

            _windowRect = GUILayout.Window(0, _windowRect, DrawDebugMenuWindow, "DEBUG MENU", _windowStyle);
        }

        #region THEME SETUP (TỐI GIẢN UI)
        private void SetupMinimalTheme()
        {
            if (_themeInitialized) return;

            Texture2D whiteTex = MakeTex(2, 2, Color.white);
            Texture2D grayTex = MakeTex(2, 2, new Color(0.9f, 0.9f, 0.9f, 1f));

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = whiteTex;
            _windowStyle.onNormal.background = whiteTex;
            _windowStyle.normal.textColor = Color.black;
            _windowStyle.onNormal.textColor = Color.black;
            _windowStyle.fontStyle = FontStyle.Bold;
            _windowStyle.padding = new RectOffset(10, 10, 20, 10);

            GUI.skin.button.normal.background = whiteTex;
            GUI.skin.button.hover.background = whiteTex;
            GUI.skin.button.active.background = whiteTex;
            GUI.skin.button.normal.textColor = Color.black;
            GUI.skin.button.hover.textColor = Color.black;
            GUI.skin.button.active.textColor = Color.black;
            GUI.skin.button.fontStyle = FontStyle.Bold;

            GUI.skin.textField.normal.background = grayTex;
            GUI.skin.textField.hover.background = grayTex;
            GUI.skin.textField.focused.background = grayTex;
            GUI.skin.textField.normal.textColor = Color.black;
            GUI.skin.textField.hover.textColor = Color.black;
            GUI.skin.textField.focused.textColor = Color.black;
            GUI.skin.textField.alignment = TextAnchor.MiddleCenter;

            GUI.skin.label.normal.textColor = Color.black;

            _themeInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void ResetBackgroundColor()
        {
            GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        }
        #endregion

        #region WINDOW CONTENT
        private void DrawDebugMenuWindow(int windowID)
        {
            GUILayout.BeginVertical();

            _currentTab = GUILayout.Toolbar(_currentTab, _tabs, GUILayout.Height(35));
            GUILayout.Space(10);

            // Đã xóa ScrollView, gọi trực tiếp các hàm vẽ Tab
            switch (_currentTab)
            {
                case 0: DrawCurrencyTab(); break;
                case 1: DrawLevelTab(); break;
                case 2: DrawBoosterTab(); break;
            }

            GUILayout.EndVertical();

            GUI.DragWindow();
        }
        #endregion

        #region TAB 1: CURRENCY
        private void DrawCurrencyTab()
        {
            GUILayout.Label("─── HP MANAGER ───", DefaultLabelStyle());
            GUILayout.Label($"HP: {(HPManager.Instance != null ? $"{HPManager.Instance.CurrentHP} / {HPManager.Instance.MaxHP}" : "NULL")}");

            GUILayout.BeginHorizontal();
            _hpInput = GUILayout.TextField(_hpInput, GUILayout.Width(60));
            if (GUILayout.Button("+ Add HP")) if (int.TryParse(_hpInput, out int v)) HPManager.Instance?.AddHP(v);
            if (GUILayout.Button("- Del HP")) if (int.TryParse(_hpInput, out int v)) HPManager.Instance?.DeductHP(v);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("FILL MAX HP (99)")) HPManager.Instance?.AddHP(99);

            GUILayout.Space(10);
            GUILayout.Label("─── COIN MANAGER ───", DefaultLabelStyle());
            GUILayout.Label($"Coins: {(CoinManager.Instance != null ? CoinManager.Instance.CurrentCoins.ToString() : "NULL")}");

            GUILayout.BeginHorizontal();
            _coinInput = GUILayout.TextField(_coinInput, GUILayout.Width(60));
            if (GUILayout.Button("+ Add Coins")) if (long.TryParse(_coinInput, out long v)) CoinManager.Instance?.AddCoins(v);
            if (GUILayout.Button("- Spend Coins")) if (long.TryParse(_coinInput, out long v)) CoinManager.Instance?.SpendCoins(v);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Simulate Win (Claim Reward)")) CoinManager.Instance?.ClaimWinReward();
            if (GUILayout.Button("Simulate x2 Reward (Ads)")) CoinManager.Instance?.DoubleWinReward();
            if (GUILayout.Button("Simulate Revive")) CoinManager.Instance?.TryRevive();

            GUILayout.Space(10);
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("DELETE ALL SAVE DATA", GUILayout.Height(35))) CurrencySaveManager.DeleteAll();
            ResetBackgroundColor();
        }
        #endregion

        #region TAB 2: LEVEL
        private void DrawLevelTab()
        {
            GUILayout.Label("─── LEVEL ───", DefaultLabelStyle());
            GUILayout.Label($"Current Level: {(LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelIndex.ToString() : "NULL")}");
            GUILayout.Label($"Game State: {(GameManager.Instance != null ? GameManager.Instance.CurrentState.ToString() : "NULL")}");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Load:", GUILayout.Width(40));
            _levelInput = GUILayout.TextField(_levelInput, GUILayout.Width(60));
            if (GUILayout.Button("Load")) if (int.TryParse(_levelInput, out int v)) LevelManager.Instance?.RequestLoadLevel(v);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Restart")) LevelManager.Instance?.RestartCurrentLevel();
            if (GUILayout.Button("Next Level")) LevelManager.Instance?.LoadNextLevel();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("─── SIMULATE ───", DefaultLabelStyle());

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("AUTO WIN", GUILayout.Height(35))) EventBus.RaiseAllOrdersCompleted();

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("AUTO LOSE", GUILayout.Height(35))) EventBus.RaiseBackupFull();
            ResetBackgroundColor();
        }
        #endregion

        #region TAB 3: BOOSTERS
        private void DrawBoosterTab()
        {
            GUILayout.Label("─── INVENTORY ───", DefaultLabelStyle());
            if (BoosterManager.Instance == null || BoosterManager.Instance.Database == null) return;

            var db = BoosterManager.Instance.Database;

            GUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("+3 TẤT CẢ")) foreach (var d in db.Boosters) BoosterManager.Instance.AddBoosterQuantity(d.boosterName, 3);

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("RESET TẤT CẢ")) BoosterInventory.ResetAll(db);
            ResetBackgroundColor();
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            foreach (var data in db.Boosters)
            {
                int qty = BoosterInventory.GetQuantity(data);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{data.boosterName} (x{qty})", GUILayout.Width(140));
                if (GUILayout.Button("+1", GUILayout.Width(40))) BoosterManager.Instance.AddBoosterQuantity(data.boosterName, 1);
                if (GUILayout.Button("+5", GUILayout.Width(40))) BoosterManager.Instance.AddBoosterQuantity(data.boosterName, 5);
                if (GUILayout.Button("Rst", GUILayout.Width(40))) { BoosterInventory.SetQuantity(data, 0); EventBus.RaiseBoosterActivated(data.boosterName); }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            GUILayout.Label("─── FORCE EXECUTE ───", DefaultLabelStyle());

            var registryField = typeof(BoosterManager).GetField("_registry", BindingFlags.NonPublic | BindingFlags.Instance);
            if (registryField != null && registryField.GetValue(BoosterManager.Instance) is Dictionary<string, IBooster> registry)
            {
                foreach (var kv in registry)
                {
                    bool canExec = kv.Value.CanExecute();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(kv.Key, GUILayout.Width(140));
                    GUILayout.Label(canExec ? "Ready" : "Wait", GUILayout.Width(40));

                    GUI.backgroundColor = Color.yellow;
                    if (GUILayout.Button("Force")) kv.Value.Execute();
                    ResetBackgroundColor();
                    GUILayout.EndHorizontal();
                }
            }
        }
        #endregion

        private GUIStyle DefaultLabelStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            return style;
        }
    }
}