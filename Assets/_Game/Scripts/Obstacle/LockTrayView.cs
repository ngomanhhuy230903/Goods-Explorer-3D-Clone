// LockTrayView.cs
using UnityEngine;
using DG.Tweening;

namespace FoodMatch.Obstacle
{
    /// <summary>
    /// Spawn vào neutralContainer — không bị scale bởi FoodTray.
    /// LateUpdate chỉ copy POSITION từ anchor + offset Y/Z.
    /// Rotation giữ nguyên như prefab gốc, không bị ảnh hưởng bởi tray xoay.
    /// </summary>
    public class LockTrayView : MonoBehaviour
    {
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

        public int CurrentHp { get; private set; }
        public bool IsLocked => CurrentHp > 0;

        private Transform _anchor;
        private Vector3 _offset;          // chỉ Y và Z được dùng
        private bool _isFollowing;
        private Vector3 _baseScale;
        private Quaternion _prefabRotation; // rotation gốc từ prefab, không bao giờ thay đổi

        // ─────────────────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (!_isFollowing || _anchor == null) return;

            // Chỉ copy position + offset, KHÔNG đụng rotation
            transform.position = _anchor.position + _offset;
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// offset.y = khoảng cách trên/dưới so với anchor.
        /// offset.z = khoảng cách gần/xa camera so với anchor.
        /// </summary>
        public void Follow(Transform anchor, Vector3 offset)
        {
            _anchor = anchor;
            _offset = offset;
            _isFollowing = true;
        }

        public void StopFollowing()
        {
            _isFollowing = false;
            _anchor = null;
        }

        public void Setup(int hp)
        {
            CurrentHp = hp;
            _baseScale = transform.localScale;
            _prefabRotation = transform.rotation; // cache rotation gốc prefab

            if (lockIconRenderer != null)
            {
                lockIconRenderer.sprite = lockedSprite;
                lockIconRenderer.color = Color.white;
                lockIconRenderer.gameObject.SetActive(true);
            }

            RefreshHpText();
        }

        public bool TakeHit()
        {
            if (!IsLocked) return false;

            CurrentHp--;
            RefreshHpText();

            if (CurrentHp <= 0) { PlayUnlockAnimation(); return true; }

            PlayHitAnimation();
            return false;
        }

        public void HideImmediate()
        {
            StopFollowing();
            DOTween.Kill(transform);
            if (lockIconRenderer != null) lockIconRenderer.gameObject.SetActive(false);
            if (hpText != null) hpText.gameObject.SetActive(false);
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
                .SetEase(Ease.OutBounce)
                .OnComplete(() => transform.rotation = _prefabRotation); // restore sau shake

            lockIconRenderer.DOColor(Color.red, hitShakeDuration * 0.3f)
                .SetLoops(2, LoopType.Yoyo)
                .SetEase(Ease.OutSine);
        }

        private void PlayUnlockAnimation()
        {
            if (lockIconRenderer == null) { gameObject.SetActive(false); return; }

            StopFollowing();
            DOTween.Kill(transform, complete: false);

            if (unlockingSprite != null)
                lockIconRenderer.sprite = unlockingSprite;

            DOTween.Sequence()
                .Append(transform
                    .DOScale(_baseScale * 1.4f, unlockScaleDuration)
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