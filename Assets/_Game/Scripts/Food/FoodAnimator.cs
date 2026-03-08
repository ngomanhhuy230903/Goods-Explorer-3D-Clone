using UnityEngine;
using DG.Tweening;
using FoodMatch.Food;

namespace FoodMatch.Food
{
    /// <summary>
    /// Static helper class chứa tất cả animation DOTween cho FoodItem.
    /// Tách animation ra khỏi logic nghiệp vụ (Single Responsibility Principle).
    /// 
    /// Sử dụng:
    ///   FoodAnimator.PlayJumpToOrder(food, targetPos, targetScale, duration, onComplete);
    ///   FoodAnimator.PlayJumpToBackup(food, targetPos, targetScale, duration, onComplete);
    ///   FoodAnimator.PlayPopIn(food, finalScale, delay);
    /// </summary>
    public static class FoodAnimator
    {
        // ─── Jump to OrderTray ────────────────────────────────────────────────

        /// <summary>
        /// Food bay vòng cung lên OrderTray slot.
        /// Scale đồng thời về targetScale (= prefabScale × multiplier).
        /// </summary>
        public static Sequence PlayJumpToOrder(
            FoodItem food,
            Vector3 targetWorldPos,
            Vector3 targetScale,
            float jumpPower = 3.5f,
            float duration = 0.55f,
            System.Action onArrival = null)
        {
            if (food == null) return null;

            food.transform.DOKill();

            Sequence seq = DOTween.Sequence();

            // Arc jump
            seq.Append(
                food.transform.DOJump(targetWorldPos, jumpPower, 1, duration)
                    .SetEase(Ease.OutQuad)
            );

            // Scale thu nhỏ trong 80% thời gian bay
            seq.Join(
                food.transform.DOScale(targetScale, duration * 0.8f)
                    .SetEase(Ease.InOutSine)
            );

            seq.OnComplete(() =>
            {
                // Snap chính xác
                food.transform.position = targetWorldPos;
                food.transform.localScale = targetScale;
                onArrival?.Invoke();
            });

            seq.SetUpdate(false);
            return seq;
        }

        // ─── Jump to BackupTray ───────────────────────────────────────────────

        /// <summary>
        /// Food bay nhẹ nhàng hơn vào BackupTray.
        /// </summary>
        public static Sequence PlayJumpToBackup(
            FoodItem food,
            Vector3 targetWorldPos,
            Vector3 targetScale,
            float jumpPower = 2f,
            float duration = 0.4f,
            System.Action onArrival = null)
        {
            if (food == null) return null;

            food.transform.DOKill();

            Sequence seq = DOTween.Sequence();

            seq.Append(
                food.transform.DOJump(targetWorldPos, jumpPower, 1, duration)
                    .SetEase(Ease.OutQuad)
            );

            seq.Join(
                food.transform.DOScale(targetScale, duration * 0.8f)
                    .SetEase(Ease.InOutSine)
            );

            seq.OnComplete(() =>
            {
                food.transform.position = targetWorldPos;
                food.transform.localScale = targetScale;
                onArrival?.Invoke();
            });

            seq.SetUpdate(false);
            return seq;
        }

        // ─── Pop-in spawn animation ───────────────────────────────────────────

        /// <summary>
        /// Pop-in từ scale 0 lên finalScale với Ease.OutBack.
        /// </summary>
        public static Tween PlayPopIn(Transform target, Vector3 finalScale, float delay = 0f)
        {
            if (target == null) return null;

            target.localScale = Vector3.zero;
            return target.DOScale(finalScale, 0.3f)
                         .SetDelay(delay)
                         .SetEase(Ease.OutBack)
                         .SetUpdate(false);
        }

        // ─── Sparkle arrival pulse ────────────────────────────────────────────

        /// <summary>
        /// Pulse nhỏ khi food vừa đến slot (thay cho VFX nếu chưa có prefab).
        /// </summary>
        public static void PlayArrivalPulse(Transform target)
        {
            if (target == null) return;
            target.DOKill();
            target.DOPunchScale(Vector3.one * 0.25f, 0.3f, 5, 0.5f)
                  .SetUpdate(false);
        }

        // ─── Shake trays when backup is full ─────────────────────────────────

        /// <summary>
        /// Rung mạnh khi khay đầy (cảnh báo thua).
        /// </summary>
        public static void PlayShake(Transform target)
        {
            if (target == null) return;
            target.DOKill();
            target.DOShakePosition(0.5f, strength: 0.2f, vibrato: 15, randomness: 45)
                  .SetUpdate(false);
        }
    }
}