using UnityEngine;
using TMPro;
using DG.Tweening;
using FoodMatch.Managers;

namespace FoodMatch.UI
{
    /// <summary>
    /// Hiển thị số coin hiện tại với animation punch scale khi thay đổi.
    /// Attach lên Text hoặc parent chứa coin icon + text.
    /// </summary>
    public class CoinDisplayUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text coinText;
        [SerializeField] private RectTransform animTarget; // để punch scale

        [Header("Animation")]
        [SerializeField] private bool animateOnChange = true;
        [SerializeField] private float punchScale = 0.2f;
        [SerializeField] private float punchDuration = 0.3f;

        private long _displayedCoins = -1;

        private void OnEnable()
        {
            CoinManager.OnCoinChanged += UpdateDisplay;
        }

        private void OnDisable()
        {
            CoinManager.OnCoinChanged -= UpdateDisplay;
        }

        private void Start()
        {
            if (CoinManager.Instance != null)
                UpdateDisplay(CoinManager.Instance.CurrentCoins);
        }

        private void UpdateDisplay(long coins)
        {
            if (coinText != null)
                coinText.text = FormatCoins(coins);

            if (animateOnChange && _displayedCoins >= 0 && coins != _displayedCoins)
            {
                RectTransform target = animTarget != null ? animTarget : GetComponent<RectTransform>();
                if (target != null)
                    target.DOPunchScale(Vector3.one * punchScale, punchDuration, 5, 0.5f);
            }

            _displayedCoins = coins;
        }

        private static string FormatCoins(long amount)
        {
            if (amount >= 1_000_000) return $"{amount / 1_000_000f:0.#}M";
            if (amount >= 1_000) return $"{amount / 1_000f:0.#}K";
            return amount.ToString();
        }
    }
}