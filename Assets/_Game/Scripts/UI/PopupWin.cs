using UnityEngine;
using TMPro;
using DG.Tweening;

namespace FoodMatch.UI
{
    /// <summary>
    /// Gắn vào root GameObject "PopupWin".
    ///
    /// Hierarchy thực tế:
    ///   PopupWin
    ///     ├── Button_GoHome
    ///     │     └── Home_Text
    ///     └── Title_Win
    ///           ├── Win_Title                  (TextMeshProUGUI)
    ///           ├── Label_TitleRibbon_00_Icon  (GameObject)
    ///           └── Label_TitleRibbon_00_Icon  (GameObject)
    /// </summary>
    public class PopupWin : MonoBehaviour
    {
        [Header("─── References ─────────────────────")]
        [Tooltip("Kéo TextMeshProUGUI Win_Title vào đây.")]
        [SerializeField] private TextMeshProUGUI winTitleText;

        [Tooltip("Kéo Transform của Title_Win để animate cả group.")]
        [SerializeField] private Transform titleWinTransform;

        [Tooltip("Kéo 2 Label_TitleRibbon_00_Icon vào 2 ô này.")]
        [SerializeField] private GameObject[] ribbonIcons;

        [Header("─── Text ───────────────────────────")]
        [SerializeField] private string winMessage = "CHIẾN THẮNG!";

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Chạy mỗi lần popup được SetActive(true).</summary>
        private void OnEnable()
        {
            ResetVisuals();
            PlayEnterAnimation();
        }

        private void OnDisable()
        {
            // Kill tween khi ẩn để không có tween zombie
            transform.DOKill();
            if (titleWinTransform != null) titleWinTransform.DOKill();
            if (ribbonIcons != null)
                foreach (var icon in ribbonIcons)
                    icon?.transform.DOKill();
        }

        // ─── Visuals ──────────────────────────────────────────────────────────

        private void ResetVisuals()
        {
            if (winTitleText != null)
                winTitleText.text = winMessage;

            if (titleWinTransform != null)
                titleWinTransform.localScale = Vector3.zero;

            if (ribbonIcons != null)
                foreach (var icon in ribbonIcons)
                    if (icon != null) icon.transform.localScale = Vector3.zero;
        }

        private void PlayEnterAnimation()
        {
            // 1. Title_Win group scale in
            if (titleWinTransform != null)
            {
                titleWinTransform
                    .DOScale(Vector3.one, 0.5f)
                    .SetDelay(0.1f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true); // chạy khi timeScale = 0
            }

            // 2. Ribbon icons xuất hiện lần lượt
            if (ribbonIcons != null)
            {
                for (int i = 0; i < ribbonIcons.Length; i++)
                {
                    if (ribbonIcons[i] == null) continue;
                    int idx = i;
                    ribbonIcons[idx].transform
                        .DOScale(Vector3.one, 0.4f)
                        .SetDelay(0.35f + idx * 0.12f)
                        .SetEase(Ease.OutBack)
                        .SetUpdate(true);
                }
            }

            // 3. Bounce nhẹ toàn popup sau khi mọi thứ vào xong
            DOVirtual.DelayedCall(0.75f, () =>
            {
                transform
                    .DOPunchScale(Vector3.one * 0.07f, 0.4f, 5, 0.5f)
                    .SetUpdate(true);
            }, ignoreTimeScale: true);
        }
    }
}