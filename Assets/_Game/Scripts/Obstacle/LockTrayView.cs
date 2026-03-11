// LockTrayView.cs
using UnityEngine;
using DG.Tweening;

namespace FoodMatch.Obstacle
{
    public class LockTrayView : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("─── References ─────────────────────")]
        [SerializeField] private SpriteRenderer lockIconRenderer;
        [SerializeField] private TMPro.TextMeshPro hpText;

        [Header("─── Sprites ─────────────────────────")]
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Sprite unlockingSprite;

        [Header("─── Animation ────────────────────────")]
        [SerializeField] private float hitShakeDuration = 0.3f;
        [SerializeField] private float hitShakeStrength = 8f;
        [SerializeField] private int hitShakeVibrato = 10;
        [SerializeField] private float unlockScaleDuration = 0.4f;
        [SerializeField] private float unlockFadeDuration = 0.3f;

        // ─── Runtime ──────────────────────────────────────────────────────────

        public int CurrentHp { get; private set; }
        public bool IsLocked => CurrentHp > 0;

        /// <summary>
        /// localScale đã được tính đúng bởi LockObstacleController trước khi gọi Setup().
        /// Cache lại để dùng làm base cho animation unlock (scale lên 1.4x rồi fade).
        /// </summary>
        private Vector3 _baseLocalScale;

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gọi từ LockObstacleController SAU KHI đã set localScale đúng.
        /// KHÔNG reset localScale ở đây — scale do controller quản lý.
        /// </summary>
        public void Setup(int hp)
        {
            CurrentHp = hp;

            // Cache scale tại thời điểm setup (đã được controller tính đúng)
            _baseLocalScale = transform.localScale;

            if (lockIconRenderer != null)
            {
                lockIconRenderer.sprite = lockedSprite;
                lockIconRenderer.color = Color.white;
                lockIconRenderer.gameObject.SetActive(true);
            }

            RefreshHpText();
        }

        /// <summary>Giảm 1 HP. Trả về true nếu vừa unlock (HP = 0).</summary>
        public bool TakeHit()
        {
            if (!IsLocked) return false;

            CurrentHp--;
            RefreshHpText();

            if (CurrentHp <= 0)
            {
                PlayUnlockAnimation();
                return true;
            }

            PlayHitAnimation();
            return false;
        }

        /// <summary>Ẩn ngay lập tức khi Reset.</summary>
        public void HideImmediate()
        {
            DOTween.Kill(transform);
            if (lockIconRenderer != null)
                lockIconRenderer.gameObject.SetActive(false);
            if (hpText != null)
                hpText.gameObject.SetActive(false);
        }

        // ─── Animations ───────────────────────────────────────────────────────

        private void PlayHitAnimation()
        {
            if (lockIconRenderer == null) return;

            DOTween.Kill(transform, complete: false);
            transform
                .DOShakeRotation(hitShakeDuration,
                    new Vector3(0f, 0f, hitShakeStrength),
                    hitShakeVibrato, fadeOut: true)
                .SetEase(Ease.OutBounce);

            lockIconRenderer.DOColor(Color.red, hitShakeDuration * 0.3f)
                .SetLoops(2, LoopType.Yoyo)
                .SetEase(Ease.OutSine);
        }

        private void PlayUnlockAnimation()
        {
            if (lockIconRenderer == null)
            {
                gameObject.SetActive(false);
                return;
            }

            DOTween.Kill(transform, complete: false);

            if (unlockingSprite != null)
                lockIconRenderer.sprite = unlockingSprite;

            // Scale lên 1.4× từ _baseLocalScale (không hardcode Vector3.one)
            Vector3 targetScale = _baseLocalScale * 1.4f;

            DOTween.Sequence()
                .Append(transform
                    .DOScale(targetScale, unlockScaleDuration)
                    .SetEase(Ease.OutBack))
                .Join(lockIconRenderer
                    .DOFade(0f, unlockFadeDuration)
                    .SetDelay(unlockScaleDuration * 0.5f)
                    .SetEase(Ease.InSine))
                .OnComplete(() =>
                {
                    if (hpText != null) hpText.gameObject.SetActive(false);
                    if (lockIconRenderer != null) lockIconRenderer.gameObject.SetActive(false);
                });
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private void RefreshHpText()
        {
            if (hpText == null) return;
            hpText.gameObject.SetActive(IsLocked);
            hpText.text = CurrentHp.ToString();
        }
    }
}