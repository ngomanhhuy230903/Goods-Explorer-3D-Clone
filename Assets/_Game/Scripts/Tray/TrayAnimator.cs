using UnityEngine;
using DG.Tweening;
using FoodMatch.Food;

namespace FoodMatch.Tray
{
    /// <summary>
    /// Static helper: tất cả DOTween animation cho FoodTray và BackupTray.
    /// Tách animation ra khỏi logic (SRP).
    /// </summary>
    public static class TrayAnimator
    {
        // ─── Food layer shift ─────────────────────────────────────────────────

        /// <summary>
        /// Gọi khi 1 food được promote từ layer 1 lên layer 0.
        /// Scale từ greyed (0.8x) về full (1x) + punch.
        /// </summary>
        public static void PlayLayerShiftIn(FoodItem food)
        {
            if (food == null) return;

            Vector3 prefabScale = food.Data?.prefab?.transform.localScale ?? Vector3.one;

            Sequence seq = DOTween.Sequence();
            seq.Append(food.transform.DOScale(prefabScale, 0.25f).SetEase(Ease.OutBack));
            seq.Append(food.transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 4, 0.4f));
            seq.SetUpdate(false);
        }

        // ─── Win condition: tray shrink & disappear ───────────────────────────

        /// <summary>
        /// Animation khi level thắng: khay xoay, thu nhỏ và biến mất.
        /// </summary>
        public static void PlayTrayWinDisappear(Transform tray, System.Action onComplete = null)
        {
            if (tray == null) { onComplete?.Invoke(); return; }

            Sequence seq = DOTween.Sequence();

            seq.Append(tray.DORotate(new Vector3(0, 0, 360f), 0.6f, RotateMode.FastBeyond360)
                           .SetEase(Ease.InOutQuad));

            seq.Join(tray.DOScale(Vector3.zero, 0.6f).SetEase(Ease.InBack));

            seq.OnComplete(() =>
            {
                tray.gameObject.SetActive(false);
                onComplete?.Invoke();
            });

            seq.SetUpdate(false);
        }

        // ─── BackupTray warning shake ─────────────────────────────────────────

        /// <summary>
        /// Rung BackupTray khi gần đầy.
        /// </summary>
        public static void PlayWarningShake(Transform backupTray)
        {
            if (backupTray == null) return;
            backupTray.DOKill();
            backupTray.DOShakePosition(0.4f, strength: 0.15f, vibrato: 12, randomness: 40)
                      .SetUpdate(false);
        }

        // ─── Backup tray full flash ───────────────────────────────────────────

        /// <summary>
        /// Flash đỏ BackupTray khi đầy hoàn toàn.
        /// </summary>
        public static void PlayFullFlash(UnityEngine.UI.Image trayBg,
                                          Color warningColor, Color normalColor)
        {
            if (trayBg == null) return;
            trayBg.DOKill();
            trayBg.DOColor(warningColor, 0.15f)
                  .SetLoops(4, LoopType.Yoyo)
                  .SetUpdate(false)
                  .OnComplete(() => trayBg.color = normalColor);
        }

        // ─── Item use animation ───────────────────────────────────────────────

        /// <summary>Scale bounce khi dùng item.</summary>
        public static void PlayItemUsed(Transform itemButton)
        {
            if (itemButton == null) return;
            itemButton.DOKill();
            itemButton.DOPunchScale(Vector3.one * 0.3f, 0.35f, 7, 0.5f)
                      .SetUpdate(true); // UI dùng SetUpdate(true)
        }
    }
}