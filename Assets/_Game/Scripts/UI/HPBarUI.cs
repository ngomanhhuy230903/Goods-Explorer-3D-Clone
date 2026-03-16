using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FoodMatch.Managers;

namespace FoodMatch.UI
{
    /// <summary>
    /// Hiển thị thanh HP và đếm ngược thời gian hồi HP.
    /// Attach lên GameObject chứa UI HP (thường ở HUD / Home screen).
    /// </summary>
    public class HPBarUI : MonoBehaviour
    {
        [Header("HP Icons (tuỳ chọn – dùng icon thay progress bar)")]
        [Tooltip("Mảng icon HP. Index 0 = trái. Nên bằng maxHP trong config.")]
        [SerializeField] private Image[] hpIcons;
        [SerializeField] private Sprite hpFullSprite;
        [SerializeField] private Sprite hpEmptySprite;

        [Header("Progress Bar (tuỳ chọn – thay thế icon)")]
        [SerializeField] private Slider hpSlider;

        [Header("Labels")]
        [SerializeField] private TMP_Text hpCountText;       // "3/5"
        [SerializeField] private TMP_Text regenTimerText;    // "12:34" hoặc "FULL"

        [Header("Full HP Panel (ẩn timer khi đầy)")]
        [SerializeField] private GameObject timerPanel;

        private void OnEnable()
        {
            HPManager.OnHPChanged += Refresh;
            HPManager.OnHPFull += OnFull;
        }

        private void OnDisable()
        {
            HPManager.OnHPChanged -= Refresh;
            HPManager.OnHPFull -= OnFull;
        }

        private void Start()
        {
            if (HPManager.Instance != null)
                Refresh(HPManager.Instance.CurrentHP, HPManager.Instance.MaxHP);
        }

        private void Update()
        {
            UpdateTimer();
        }

        // ─── Callbacks ────────────────────────────────────────────────────────

        private void Refresh(int current, int max)
        {
            // Icons
            for (int i = 0; i < hpIcons.Length; i++)
            {
                if (hpIcons[i] == null) continue;
                hpIcons[i].sprite = i < current ? hpFullSprite : hpEmptySprite;
            }

            // Slider
            if (hpSlider != null)
            {
                hpSlider.maxValue = max;
                hpSlider.value = current;
            }

            // Text
            if (hpCountText != null)
                hpCountText.text = $"{current}/{max}";

            // Timer panel visibility
            bool isFull = current >= max;
            if (timerPanel != null)
                timerPanel.SetActive(!isFull);
        }

        private void OnFull()
        {
            if (regenTimerText != null) regenTimerText.text = "FULL";
            if (timerPanel != null) timerPanel.SetActive(false);
        }

        private void UpdateTimer()
        {
            if (HPManager.Instance == null) return;
            if (HPManager.Instance.CurrentHP >= HPManager.Instance.MaxHP) return;

            float seconds = HPManager.Instance.SecondsUntilNextRegen;
            if (regenTimerText != null)
                regenTimerText.text = FormatTime(seconds);
        }

        private static string FormatTime(float totalSeconds)
        {
            int m = (int)(totalSeconds / 60);
            int s = (int)(totalSeconds % 60);
            return $"{m:00}:{s:00}";
        }
    }
}