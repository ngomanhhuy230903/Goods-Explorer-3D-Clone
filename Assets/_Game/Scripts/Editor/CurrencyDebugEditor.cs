#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FoodMatch.Managers;

namespace FoodMatch.Editor
{
    /// <summary>
    /// Editor window để debug HP và Coin trong Play Mode.
    /// Mở qua: FoodMatch > Currency Debug
    /// </summary>
    public class CurrencyDebugEditor : EditorWindow
    {
        [MenuItem("FoodMatch/Currency Debug")]
        public static void ShowWindow()
        {
            GetWindow<CurrencyDebugEditor>("Currency Debug");
        }

        private int _addHPAmount = 1;
        private int _removeHPAmount = 1;
        private long _addCoinAmount = 50;
        private long _removeCoinAmount = 30;

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Chỉ hoạt động trong Play Mode.", MessageType.Info);
                return;
            }

            // ── HP ──────────────────────────────────────────────────────
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("─── HP ───────────────────────────────", EditorStyles.boldLabel);

            if (HPManager.Instance != null)
            {
                EditorGUILayout.LabelField($"Current HP: {HPManager.Instance.CurrentHP} / {HPManager.Instance.MaxHP}");
                EditorGUILayout.LabelField($"Regen in: {HPManager.Instance.SecondsUntilNextRegen:F1}s");
            }
            else
            {
                EditorGUILayout.HelpBox("HPManager.Instance = null", MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            _addHPAmount = EditorGUILayout.IntField("Amount", _addHPAmount);
            if (GUILayout.Button("Add HP"))
                HPManager.Instance?.AddHP(_addHPAmount);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _removeHPAmount = EditorGUILayout.IntField("Amount", _removeHPAmount);
            if (GUILayout.Button("Remove HP"))
                HPManager.Instance?.DeductHP(_removeHPAmount);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Max HP (fill)"))
                HPManager.Instance?.AddHP(99);

            // ── Coin ─────────────────────────────────────────────────────
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("─── Coin ─────────────────────────────", EditorStyles.boldLabel);

            if (CoinManager.Instance != null)
                EditorGUILayout.LabelField($"Current Coins: {CoinManager.Instance.CurrentCoins}");
            else
                EditorGUILayout.HelpBox("CoinManager.Instance = null", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            _addCoinAmount = EditorGUILayout.LongField("Amount", _addCoinAmount);
            if (GUILayout.Button("Add Coins"))
                CoinManager.Instance?.AddCoins(_addCoinAmount);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _removeCoinAmount = EditorGUILayout.LongField("Amount", _removeCoinAmount);
            if (GUILayout.Button("Spend Coins"))
                CoinManager.Instance?.SpendCoins(_removeCoinAmount);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Simulate Win (ClaimReward)"))
                CoinManager.Instance?.ClaimWinReward();

            if (GUILayout.Button("Simulate Double Reward (Ads)"))
                CoinManager.Instance?.DoubleWinReward();

            if (GUILayout.Button("Simulate Revive"))
                CoinManager.Instance?.TryRevive();

            // ── Danger Zone ──────────────────────────────────────────────
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("─── Danger Zone ──────────────────────", EditorStyles.boldLabel);

            Color old = GUI.color;
            GUI.color = Color.red;
            if (GUILayout.Button("DELETE ALL SAVE DATA"))
            {
                CurrencySaveManager.DeleteAll();
                Debug.Log("[CurrencyDebug] Đã xóa toàn bộ dữ liệu HP & Coin.");
            }
            GUI.color = old;

            Repaint(); // tự refresh mỗi frame
        }
    }
}
#endif