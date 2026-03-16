using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FoodMatch.Managers;

namespace FoodMatch.UI
{
    /// <summary>
    /// Hiển thị icon tim HP và đếm ngược thời gian hồi.
    /// - Icon tim: sprite đổi theo HP hiện tại / max.
    /// - HP Text: hiển thị dạng "current/max" (ví dụ: "3/5").
    /// - Timer: "20:00" → "00:00", khi full hiện "Full".
    /// Attach lên GameObject HP trên HUD / Home screen.
    /// </summary>
    public class HPBarUI : MonoBehaviour
    {
        [Header("─── Heart Icon ─────────────────────")]
        [Tooltip("Image hiện icon tim (1 icon duy nhất đổi sprite).")]
        [SerializeField] private Image heartIcon;
        [SerializeField] private Sprite heartFullSprite;
        [SerializeField] private Sprite heartEmptySprite;

        [Header("─── HP Count Text ───────────────────")]
        [Tooltip("TMP_Text hiện số tim dạng 'current/max', ví dụ '3/5'.")]
        [SerializeField] private TMP_Text hpCountText;

        [Header("─── Timer Text ──────────────────────")]
        [Tooltip("TMP_Text hiện đếm ngược dạng 'MM:SS' hoặc 'Full' khi đầy.")]
        [SerializeField] private TMP_Text timerText;

        // ─────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            HPManager.OnHPChanged += OnHPChanged;
            HPManager.OnHPFull += OnHPFull;

            // FIX: Refresh ngay khi panel được enable (kể cả lần re-enable).
            // Tránh trường hợp Start() đã chạy rồi nhưng panel bị tắt/bật lại.
            if (HPManager.Instance != null)
                OnHPChanged(HPManager.Instance.CurrentHP, HPManager.Instance.MaxHP);
        }

        private void OnDisable()
        {
            HPManager.OnHPChanged -= OnHPChanged;
            HPManager.OnHPFull -= OnHPFull;
        }

        // FIX: Bỏ Start() riêng — OnEnable đã xử lý cả lần đầu lẫn re-enable.
        // Nếu Instance chưa tồn tại lúc OnEnable chạy, event OnHPChanged sẽ
        // tự cập nhật UI khi HPManager khởi tạo xong và fire event lần đầu.

        private void Update()
        {
            TickTimer();
        }

        // ─── Callbacks ────────────────────────────────────────────────────────

        private void OnHPChanged(int current, int max)
        {
            // Đổi sprite tim
            if (heartIcon != null)
                heartIcon.sprite = (current > 0) ? heartFullSprite : heartEmptySprite;

            // FIX: Cập nhật text số tim dạng "current/max"
            if (hpCountText != null)
                hpCountText.text = $"{current}/{max}";

            if (current >= max)
                SetTimerFull();
        }

        private void OnHPFull()
        {
            if (heartIcon != null && heartFullSprite != null)
                heartIcon.sprite = heartFullSprite;

            SetTimerFull();
        }

        // ─── Timer ────────────────────────────────────────────────────────────

        private void TickTimer()
        {
            if (HPManager.Instance == null) return;
            if (HPManager.Instance.CurrentHP >= HPManager.Instance.MaxHP) return;

            if (timerText != null)
                timerText.text = FormatTime(HPManager.Instance.SecondsUntilNextRegen);
        }

        private void SetTimerFull()
        {
            if (timerText != null)
                timerText.text = "Full";
        }

        // "1200s" → "20:00",  "65s" → "01:05"
        private static string FormatTime(float totalSeconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(totalSeconds));
            int m = total / 60;
            int s = total % 60;
            return $"{m:00}:{s:00}";
        }
    }
}