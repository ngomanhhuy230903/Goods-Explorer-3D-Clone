using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using FoodMatch.Items;
using FoodMatch.Core;

namespace FoodMatch.UI
{
    /// <summary>
    /// UI cho 1 nút booster trong gameplay HUD.
    /// Tự đọc quantity từ BoosterManager, hiển thị badge + lock state.
    /// 
    /// Prefab cần: Button, Icon (Image), QuantityBadge (GameObject + TMP),
    ///             LockOverlay (GameObject), LevelText (TMP "Lv.X").
    /// </summary>
    public class BoosterButtonUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("─── Config ──────────────────────────")]
        [Tooltip("Phải khớp với BoosterData.boosterName và IBooster.BoosterName.")]
        [SerializeField] private string boosterName;

        [Header("─── References ─────────────────────")]
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject quantityBadge;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private TextMeshProUGUI lockLevelText;   // "Lv.5"
        [SerializeField] private GameObject outOfStockOverlay;    // icon dấu + / mua thêm

        // ── Runtime ───────────────────────────────────────────────────────────
        private BoosterData _data;
        private bool _isUnlocked;

        // ── Unity ─────────────────────────────────────────────────────────────
        private void Start()
        {
            BindData();
            RefreshUI();

            button.onClick.AddListener(OnClick);
        }

        private void OnEnable()
        {
            EventBus.OnBoosterActivated += OnBoosterActivated;
            EventBus.OnBoosterUnlocked += OnBoosterUnlocked;
            EventBus.OnBoosterOutOfStock += OnOutOfStock;
        }

        private void OnDisable()
        {
            EventBus.OnBoosterActivated -= OnBoosterActivated;
            EventBus.OnBoosterUnlocked -= OnBoosterUnlocked;
            EventBus.OnBoosterOutOfStock -= OnOutOfStock;
        }

        // ── Public ────────────────────────────────────────────────────────────

        public void RefreshUI()
        {
            if (_data == null) { BindData(); if (_data == null) return; }

            int qty = BoosterManager.Instance != null
                ? BoosterManager.Instance.GetQuantity(boosterName)
                : 0;

            // Lock state
            bool unlocked = BoosterInventory.IsEverUnlocked(_data);
            _isUnlocked = unlocked;

            if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
            if (lockLevelText != null) lockLevelText.text = $"Lv.{_data.requiredLevel}";

            // Icon
            if (iconImage != null && _data.icon != null)
                iconImage.sprite = _data.icon;

            // Quantity badge
            bool hasQty = qty > 0;
            if (quantityBadge != null) quantityBadge.SetActive(unlocked && hasQty);
            if (quantityText != null) quantityText.text = qty.ToString();

            // Out of stock
            if (outOfStockOverlay != null)
                outOfStockOverlay.SetActive(unlocked && !hasQty);

            // Interactable
            button.interactable = unlocked && hasQty;
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void BindData()
        {
            if (BoosterManager.Instance?.Database == null) return;
            _data = BoosterManager.Instance.Database.GetByName(boosterName);
            if (_data == null)
                Debug.LogWarning($"[BoosterButtonUI] Không tìm thấy BoosterData: '{boosterName}'");
        }

        private void OnClick()
        {
            if (BoosterManager.Instance == null) return;
            BoosterManager.Instance.UseBooster(boosterName);

            // Press feedback
            transform.DOKill();
            transform
                .DOScale(Vector3.one * 0.88f, 0.08f)
                .OnComplete(() => transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack));
        }

        private void OnBoosterActivated(string name)
        {
            if (name != boosterName) return;
            RefreshUI();

            // Flash feedback
            if (iconImage != null)
            {
                iconImage.DOColor(Color.yellow, 0.1f)
                    .OnComplete(() => iconImage.DOColor(Color.white, 0.2f));
            }
        }

        private void OnBoosterUnlocked(string name)
        {
            if (name != boosterName) return;
            RefreshUI();

            // Unlock animation
            transform.DOKill();
            transform
                .DOScale(Vector3.one * 1.2f, 0.2f).SetEase(Ease.OutBack)
                .OnComplete(() => transform.DOScale(Vector3.one, 0.15f));

            if (lockOverlay != null)
            {
                var cg = lockOverlay.GetComponent<CanvasGroup>()
                         ?? lockOverlay.AddComponent<CanvasGroup>();
                cg.DOFade(0f, 0.3f).OnComplete(() => lockOverlay.SetActive(false));
            }
        }

        private void OnOutOfStock(string name)
        {
            if (name != boosterName) return;

            // Shake để gợi ý mua thêm
            transform.DOKill();
            transform.DOShakePosition(0.4f, new Vector3(8f, 0, 0), 20, 90f, false, true);
            RefreshUI();
        }
    }
}