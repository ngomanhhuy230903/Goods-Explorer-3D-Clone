using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace FoodMatch.UI
{
    /// <summary>
    /// Gắn vào vùng background/dim của popup để cho phép "peek" (giữ để nhìn qua).
    /// Khi người dùng giữ xuống vùng không có object → ẩn popup tạm thời.
    /// Khi thả ra → popup hiện lại. Game vẫn pause.
    /// </summary>
    public class PeekBlocker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("─── Peek Settings ────────────────────")]
        [Tooltip("Canvas Group của popup cha — dùng để fade ẩn/hiện.")]
        [SerializeField] private CanvasGroup targetCanvasGroup;

        [Tooltip("Thời gian fade khi ẩn/hiện popup.")]
        [SerializeField] private float fadeDuration = 0.15f;

        [Tooltip("Alpha khi đang peek (0 = ẩn hoàn toàn).")]
        [SerializeField] [Range(0f, 1f)] private float peekAlpha = 0f;

        [Tooltip("Tắt interaction trong lúc peek để tránh click xuyên UI.")]
        [SerializeField] private bool blockInteractionWhilePeeking = true;

        private bool _isPeeking = false;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Khởi tạo từ code — gán CanvasGroup và override các tham số nếu muốn.
        /// </summary>
        public void Init(CanvasGroup cg, float fadeDur = -1f, float hiddenAlpha = -1f)
        {
            targetCanvasGroup = cg;
            if (fadeDur >= 0f) fadeDuration = fadeDur;
            if (hiddenAlpha >= 0f) peekAlpha = hiddenAlpha;
        }

        // ── Pointer Events ────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isPeeking) return;
            _isPeeking = true;
            PeekHide();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPeeking) return;
            _isPeeking = false;
            PeekShow();
        }

        // ── Nếu pointer rời màn hình (edge case) ─────────────────────────────
        private void OnDisable()
        {
            if (_isPeeking)
            {
                _isPeeking = false;
                PeekShowInstant();
            }
        }

        // ── Fade Logic ────────────────────────────────────────────────────────

        private void PeekHide()
        {
            if (targetCanvasGroup == null) return;

            targetCanvasGroup.DOKill();
            targetCanvasGroup.DOFade(peekAlpha, fadeDuration)
                .SetUpdate(true); // unscaledTime — hoạt động khi game pause

            if (blockInteractionWhilePeeking)
            {
                targetCanvasGroup.interactable = false;
                targetCanvasGroup.blocksRaycasts = false;
            }
        }

        private void PeekShow()
        {
            if (targetCanvasGroup == null) return;

            targetCanvasGroup.DOKill();
            targetCanvasGroup.DOFade(1f, fadeDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (blockInteractionWhilePeeking)
                    {
                        targetCanvasGroup.interactable = true;
                        targetCanvasGroup.blocksRaycasts = true;
                    }
                });
        }

        private void PeekShowInstant()
        {
            if (targetCanvasGroup == null) return;
            targetCanvasGroup.DOKill();
            targetCanvasGroup.alpha = 1f;
            if (blockInteractionWhilePeeking)
            {
                targetCanvasGroup.interactable = true;
                targetCanvasGroup.blocksRaycasts = true;
            }
        }
    }
}