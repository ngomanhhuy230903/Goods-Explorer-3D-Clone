using System;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Food;
using FoodMatch.Core;

namespace FoodMatch.Flow
{
    /// <summary>Interface Strategy cho mọi loại animation bay của food.</summary>
    public interface IFlyStrategy
    {
        void Execute(FoodItem food, Vector3 targetPos, Vector3 targetScale,
                     FlyConfig config, Action onComplete);
    }

    /// <summary>Config truyền vào strategy — không expose DOTween trực tiếp ra ngoài.</summary>
    [Serializable]
    public struct FlyConfig
    {
        public float jumpPower;
        public int jumpCount;
        public float duration;
        public Ease easeMove;
        public Ease easeScale;

        public static FlyConfig OrderDefault => new FlyConfig
        {
            jumpPower = 3.5f,
            jumpCount = 1,
            duration = 0.55f,
            easeMove = Ease.OutQuad,
            easeScale = Ease.InOutSine
        };

        public static FlyConfig BackupDefault => new FlyConfig
        {
            jumpPower = 2f,
            jumpCount = 1,
            duration = 0.40f,
            easeMove = Ease.OutQuad,
            easeScale = Ease.InOutSine
        };
    }

    // ─── Concrete Strategy: DOTween DOJump ────────────────────────────────────

    /// <summary>Strategy bay chuẩn dùng DOJump. Dùng cho OrderTray.</summary>
    public sealed class JumpFlyStrategy : IFlyStrategy
    {
        public void Execute(FoodItem food, Vector3 targetPos, Vector3 targetScale,
                            FlyConfig config, Action onComplete)
        {
            if (food == null) { onComplete?.Invoke(); return; }

            DisableCollider(food);

            DOTween.Sequence()
                .Append(food.transform
                    .DOJump(targetPos, config.jumpPower, config.jumpCount, config.duration)
                    .SetEase(config.easeMove))
                .Join(food.transform
                    .DOScale(targetScale, config.duration * 0.8f)
                    .SetEase(config.easeScale))
                .OnComplete(() =>
                {
                    // Hard snap để tránh floating point drift
                    food.transform.position = targetPos;
                    food.transform.localScale = targetScale;
                    onComplete?.Invoke();
                })
                .SetUpdate(false)
                .SetId($"FoodFly_{food.GetInstanceID()}")
                .Play();
        }

        private static void DisableCollider(FoodItem food)
        {
            var col = food.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }

    // ─── Concrete Strategy: Arc + Scale Punch (Premium feel) ─────────────────

    /// <summary>
    /// Strategy nâng cao: arc bezier + punch scale khi landing.
    /// Swap in khi muốn polish visual mà không đổi controller.
    /// </summary>
    public sealed class ArcPunchFlyStrategy : IFlyStrategy
    {
        private readonly float _punchStrength;

        public ArcPunchFlyStrategy(float punchStrength = 0.15f)
            => _punchStrength = punchStrength;

        public void Execute(FoodItem food, Vector3 targetPos, Vector3 targetScale,
                            FlyConfig config, Action onComplete)
        {
            if (food == null) { onComplete?.Invoke(); return; }

            DisableCollider(food);

            var startPos = food.transform.position;
            var midPos = Vector3.Lerp(startPos, targetPos, 0.5f)
                         + Vector3.up * config.jumpPower;

            DOTween.Sequence()
                .Append(food.transform
                    .DOPath(new[] { startPos, midPos, targetPos },
                            config.duration, PathType.CatmullRom)
                    .SetEase(config.easeMove))
                .Join(food.transform
                    .DOScale(targetScale * 0.9f, config.duration * 0.6f)
                    .SetEase(Ease.InSine))
                .Append(food.transform
                    .DOPunchScale(Vector3.one * _punchStrength, 0.25f, 5, 0.5f))
                .OnComplete(() =>
                {
                    food.transform.position = targetPos;
                    food.transform.localScale = targetScale;
                    onComplete?.Invoke();
                })
                .SetUpdate(false)
                .SetId($"FoodFly_{food.GetInstanceID()}")
                .Play();
        }

        private static void DisableCollider(FoodItem food)
        {
            var col = food.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }

    // ─── Strategy Factory ─────────────────────────────────────────────────────

    /// <summary>
    /// FACTORY PATTERN — Tạo strategy phù hợp theo loại delivery.
    /// Thay đổi visual mà không đụng controller.
    /// </summary>
    public static class FlyStrategyFactory
    {
        public static IFlyStrategy CreateOrderStrategy(bool usePremium = false)
            => usePremium ? new ArcPunchFlyStrategy() : new JumpFlyStrategy();

        public static IFlyStrategy CreateBackupStrategy()
            => new JumpFlyStrategy();
    }
}