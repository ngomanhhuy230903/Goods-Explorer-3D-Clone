// LockTrayView.cs
using UnityEngine;
using DG.Tweening;

namespace FoodMatch.Obstacle
{
    /// <summary>
    /// Hiển thị lock icon + HP cho FoodTray bị khóa.
    /// Spawn vào neutralContainer (KHÔNG phải child của FoodTray) để tránh scale bị nhân.
    /// Follow vị trí FoodTray qua Update() giống SlotFollower của FoodItem.
    /// </summary>
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

        private Transform _followTarget;
        private Vector3 _followOffset;
        private bool _isFollowing = false;
        private Vector3 _baseLocalScale;

        // ─────────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_isFollowing || _followTarget == null) return;
            // Bám theo world position của FoodTray + offset
            transform.position = _followTarget.position + _followOffset;
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>Bắt đầu follow target (FoodTray.transform) với offset world.</summary>
        public void Follow(Transform target, Vector3 worldOffset)
        {
            _followTarget = target;
            _followOffset = worldOffset;
            _isFollowing = true;
        }

        /// <summary>Dừng follow — gọi khi Reset.</summary>
        public void StopFollowing()
        {
            _isFollowing = false;
            _followTarget = null;
        }

        /// <summary>
        /// Khởi tạo view với HP.
        /// Gọi SAU khi đã Instantiate — scale đã đúng vì không phải child của FoodTray.
        /// </summary>
        public void Setup(int hp)
        {
            CurrentHp = hp;
            _baseLocalScale = transform.localScale; // cache scale gốc prefab

            if (lockIconRenderer != null)
            {
                lockIconRenderer.sprite = lockedSprite;
                lockIconRenderer.color = Color.white;
                lockIconRenderer.gameObject.SetActive(true);
            }

            RefreshHpText();
        }

        /// <summary>Giảm 1 HP. Trả về true nếu vừa unlock.</summary>
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

        /// <summary>Ẩn và dừng follow ngay lập tức khi Reset.</summary>
        public void HideImmediate()
        {
            StopFollowing();
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

            // Dừng follow để icon không nhảy lung tung lúc animate
            StopFollowing();
            DOTween.Kill(transform, complete: false);

            if (unlockingSprite != null)
                lockIconRenderer.sprite = unlockingSprite;

            // Scale lên 1.4× từ scale gốc rồi fade
            DOTween.Sequence()
                .Append(transform
                    .DOScale(_baseLocalScale * 1.4f, unlockScaleDuration)
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