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
        // Trạng thái menu
        private bool _showMenu = false;
        private int _currentTab = 0; // 0: Currency, 1: Level, 2: Boosters
        private string[] _tabs = { "💰 Currency", "🗺️ Level", "🚀 Boosters" };

        // Inputs cho Currency
        private string _hpInput = "1";
        private string _coinInput = "50";

        // Inputs cho Level
        private string _levelInput = "1";

        // Scroll
        private Vector2 _scrollPosition;

        // Custom Styles
        private Texture2D _whiteBackground;
        private GUIStyle _boxStyle;

        private void Awake()
        {
            // Đảm bảo Menu này không bị destroy khi chuyển Scene
            DontDestroyOnLoad(gameObject);
        }

        private void OnGUI()
        {
            // Scale UI để nhìn rõ trên màn hình điện thoại độ phân giải cao
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 480f, Screen.height / 800f, 1));

            // Khởi tạo và áp dụng theme nền trắng chữ đen
            SetupWhiteTheme();

            // Nút bật/tắt Menu ở góc trên bên trái
            if (GUI.Button(new Rect(10, 10, 100, 40), _showMenu ? "Close Debug" : "🔧 Debug"))
            {
                _showMenu = !_showMenu;
            }

            if (!_showMenu) return;

            // Khu vực chứa Menu - Dùng _boxStyle (nền trắng) thay vì GUI.skin.box mặc định
            GUILayout.BeginArea(new Rect(10, 60, 460, 700), _boxStyle);

            // Tab Selection
            _currentTab = GUILayout.Toolbar(_currentTab, _tabs, GUILayout.Height(40));
            GUILayout.Space(10);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            switch (_currentTab)
            {
                case 0: DrawCurrencyTab(); break;
                case 1: DrawLevelTab(); break;
                case 2: DrawBoosterTab(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        #region THEME SETUP
        private void SetupWhiteTheme()
        {
            // Chỉ tạo Texture 1 lần duy nhất
            if (_whiteBackground == null)
            {
                _whiteBackground = new Texture2D(1, 1);
                // Tạo màu nền trắng hơi ngả xám nhạt (0.95) để đỡ chói mắt, độ đục (alpha) = 1f
                _whiteBackground.SetPixel(0, 0, new Color(0.95f, 0.95f, 0.95f, 1f));
                _whiteBackground.Apply();
            }

            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle(GUI.skin.box);
                _boxStyle.normal.background = _whiteBackground;
            }

            // Cập nhật màu chữ đen cho tất cả element mặc định
            GUI.skin.label.normal.textColor = Color.black;
            GUI.skin.button.normal.textColor = Color.black;
            GUI.skin.textField.normal.textColor = Color.black;
            GUI.skin.toggle.normal.textColor = Color.black;
        }
        #endregion

        #region TAB 1: CURRENCY (HP & COIN)
        private void DrawCurrencyTab()
        {
            GUILayout.Label("─── HP MANAGER ───", DefaultLabelStyle());
            if (HPManager.Instance != null)
            {
                GUILayout.Label($"HP: {HPManager.Instance.CurrentHP} / {HPManager.Instance.MaxHP}");
            }
            else GUILayout.Label("HPManager = NULL", ErrorStyle());

            GUILayout.BeginHorizontal();
            _hpInput = GUILayout.TextField(_hpInput, GUILayout.Width(50));
            if (GUILayout.Button("+ Add HP"))
            {
                if (int.TryParse(_hpInput, out int val)) HPManager.Instance?.AddHP(val);
            }
            if (GUILayout.Button("- Remove HP"))
            {
                if (int.TryParse(_hpInput, out int val)) HPManager.Instance?.DeductHP(val);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("FILL MAX HP (99)")) HPManager.Instance?.AddHP(99);

            GUILayout.Space(15);
            GUILayout.Label("─── COIN MANAGER ───", DefaultLabelStyle());

            if (CoinManager.Instance != null)
                GUILayout.Label($"Coins: {CoinManager.Instance.CurrentCoins}");
            else GUILayout.Label("CoinManager = NULL", ErrorStyle());

            GUILayout.BeginHorizontal();
            _coinInput = GUILayout.TextField(_coinInput, GUILayout.Width(50));
            if (GUILayout.Button("+ Add Coins"))
            {
                if (long.TryParse(_coinInput, out long val)) CoinManager.Instance?.AddCoins(val);
            }
            if (GUILayout.Button("- Spend Coins"))
            {
                if (long.TryParse(_coinInput, out long val)) CoinManager.Instance?.SpendCoins(val);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Simulate Win (Claim Reward)")) CoinManager.Instance?.ClaimWinReward();
            if (GUILayout.Button("Simulate x2 Reward (Ads)")) CoinManager.Instance?.DoubleWinReward();
            if (GUILayout.Button("Simulate Revive")) CoinManager.Instance?.TryRevive();

            GUILayout.Space(15);
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("DELETE ALL SAVE DATA (DANGER)", GUILayout.Height(40)))
            {
                CurrencySaveManager.DeleteAll();
                Debug.Log("Deleted all save data");
            }
            GUI.backgroundColor = Color.white;
        }
        #endregion

        #region TAB 2: LEVEL
        private void DrawLevelTab()
        {
            GUILayout.Label("─── LEVEL CONTROLS ───", DefaultLabelStyle());

            if (LevelManager.Instance != null)
                GUILayout.Label($"Current Level: {LevelManager.Instance.CurrentLevelIndex}");
            else GUILayout.Label("LevelManager = NULL", ErrorStyle());

            if (GameManager.Instance != null)
                GUILayout.Label($"Game State: {GameManager.Instance.CurrentState}");

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Level Index:", GUILayout.Width(80));
            _levelInput = GUILayout.TextField(_levelInput, GUILayout.Width(50));
            if (GUILayout.Button("Load Level"))
            {
                if (int.TryParse(_levelInput, out int val)) LevelManager.Instance?.RequestLoadLevel(val);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("↺ Restart")) LevelManager.Instance?.RestartCurrentLevel();
            if (GUILayout.Button("⏭ Next Level")) LevelManager.Instance?.LoadNextLevel();
            GUILayout.EndHorizontal();

            GUILayout.Space(15);
            GUILayout.Label("─── SIMULATE GAMEPLAY ───", DefaultLabelStyle());

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("🏆 AUTO WIN (Raise Completed)", GUILayout.Height(40)))
            {
                EventBus.RaiseAllOrdersCompleted();
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("💀 AUTO LOSE (Raise Backup Full)", GUILayout.Height(40)))
            {
                EventBus.RaiseBackupFull();
            }
            GUI.backgroundColor = Color.white;
        }
        #endregion

        #region TAB 3: BOOSTERS
        private void DrawBoosterTab()
        {
            GUILayout.Label("─── INVENTORY (ADD/RESET) ───", DefaultLabelStyle());
            if (BoosterManager.Instance == null || BoosterManager.Instance.Database == null)
            {
                GUILayout.Label("BoosterManager or Database is NULL", ErrorStyle());
                return;
            }

            var db = BoosterManager.Instance.Database;

            GUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("+3 TẤT CẢ"))
            {
                foreach (var d in db.Boosters) BoosterManager.Instance.AddBoosterQuantity(d.boosterName, 3);
            }
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("RESET TẤT CẢ"))
            {
                BoosterInventory.ResetAll(db);
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Vẽ từng Booster
            foreach (var data in db.Boosters)
            {
                int qty = BoosterInventory.GetQuantity(data);
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"{data.boosterName} (x{qty})", GUILayout.Width(150));

                if (GUILayout.Button("+1", GUILayout.Width(40))) BoosterManager.Instance.AddBoosterQuantity(data.boosterName, 1);
                if (GUILayout.Button("+5", GUILayout.Width(40))) BoosterManager.Instance.AddBoosterQuantity(data.boosterName, 5);
                if (GUILayout.Button("Rst", GUILayout.Width(40)))
                {
                    BoosterInventory.SetQuantity(data, 0);
                    EventBus.RaiseBoosterActivated(data.boosterName);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(15);
            GUILayout.Label("─── FORCE EXECUTE (PLAY MODE) ───", DefaultLabelStyle());

            // Dùng Reflection để lấy Registry như code Editor cũ
            var registryField = typeof(BoosterManager).GetField("_registry", BindingFlags.NonPublic | BindingFlags.Instance);
            if (registryField != null)
            {
                var registry = registryField.GetValue(BoosterManager.Instance) as Dictionary<string, IBooster>;
                if (registry != null)
                {
                    foreach (var kv in registry)
                    {
                        bool canExec = kv.Value.CanExecute();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(kv.Key, GUILayout.Width(150));
                        GUILayout.Label(canExec ? "✅ Ready" : "⛔ Not Ready", GUILayout.Width(80));

                        GUI.backgroundColor = Color.yellow;
                        if (GUILayout.Button("Force Execute"))
                        {
                            kv.Value.Execute();
                        }
                        GUI.backgroundColor = Color.white;
                        GUILayout.EndHorizontal();
                    }
                }
            }
        }
        #endregion

        #region GUI Styles
        private GUIStyle DefaultLabelStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.black; // Ép màu đen cho Header
            return style;
        }

        private GUIStyle ErrorStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.red; // Lỗi vẫn để màu đỏ cho nổi bật
            return style;
        }
        #endregion
    }
}