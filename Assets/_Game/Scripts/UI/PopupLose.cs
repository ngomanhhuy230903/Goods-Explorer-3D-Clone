using UnityEngine;
using TMPro;
using DG.Tweening;

namespace FoodMatch.UI
{
    /// <summary>
    /// Gắn vào root GameObject "PopupLose".
    ///
    /// Hierarchy thực tế:
    ///   PopupLose
    ///     ├── Title_Lose
    ///     │     ├── Level_Text       (TextMeshProUGUI - "Level X")
    ///     │     └── SubTitle_Text    (TextMeshProUGUI - "Khay đã đầy!")
    ///     ├── Button_GoHome
    ///     │     └── Home_Text
    ///     └── Button_TryAgain
    ///           └── TryAgain_Text
    /// </summary>
    public class PopupLose : MonoBehaviour
    {
        [Header("─── References ─────────────────────")]
        [Tooltip("Kéo TextMeshProUGUI Level_Text vào đây.")]
        [SerializeField] private TextMeshProUGUI levelText;

        [Tooltip("Kéo TextMeshProUGUI SubTitle_Text vào đây.")]
        [SerializeField] private TextMeshProUGUI subTitleText;

        [Tooltip("Kéo Transform của Title_Lose để animate cả group.")]
        [SerializeField] private Transform titleLoseTransform;

        [Header("─── Text ───────────────────────────")]
        [SerializeField] private string subTitleMessage = "Khay đã đầy! Thử lại nhé.";

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Chạy mỗi lần popup được SetActive(true).</summary>
        private void OnEnable()
        {
            ResetVisuals();
            PlayEnterAnimation();
        }

        private void OnDisable()
        {
            transform.DOKill();
            if (titleLoseTransform != null) titleLoseTransform.DOKill();
        }

        // ─── Visuals ──────────────────────────────────────────────────────────

        private void ResetVisuals()
        {
            // Lấy level hiện tại từ LevelManager
            if (levelText != null)
            {
                int currentLevel = FoodMatch.Level.LevelManager.Instance != null
                    ? FoodMatch.Level.LevelManager.Instance.CurrentLevelIndex
                    : 1;
                levelText.text = $"Level {currentLevel}";
            }

            if (subTitleText != null)
                subTitleText.text = subTitleMessage;

            if (titleLoseTransform != null)
                titleLoseTransform.localScale = Vector3.zero;
        }

        private void PlayEnterAnimation()
        {
            // 1. Title_Lose group scale in
            if (titleLoseTransform != null)
            {
                titleLoseTransform
                    .DOScale(Vector3.one, 0.45f)
                    .SetDelay(0.1f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true); // chạy khi timeScale = 0
            }

            // 2. Shake toàn popup để nhấn mạnh cảm giác thua
            DOVirtual.DelayedCall(0.35f, () =>
            {
                transform
                    .DOShakePosition(
                        duration: 0.5f,
                        strength: new Vector3(18f, 10f, 0f),
                        vibrato: 12,
                        randomness: 60f)
                    .SetUpdate(true);
            }, ignoreTimeScale: true);
        }
    }
}