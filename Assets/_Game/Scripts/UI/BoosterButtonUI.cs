using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using FoodMatch.Items;

namespace FoodMatch.UI
{
    [RequireComponent(typeof(Button))]
    public class BoosterButtonUI : MonoBehaviour
    {
        [SerializeField] private string boosterName;
        [SerializeField] private float punchScale = 0.2f;
        [SerializeField] private float punchDuration = 0.25f;

        private Button _btn;
        private Vector3 _originalScale;

        private void Awake()
        {
            _btn = GetComponent<Button>();
            _originalScale = transform.localScale;
            _btn.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            if (BoosterManager.Instance == null) return;

            transform
                .DOPunchScale(Vector3.one * punchScale, punchDuration, 5, 0.5f)
                .OnComplete(() => transform.localScale = _originalScale);

            BoosterManager.Instance.UseBooster(boosterName);
        }

        private void OnDestroy() => _btn?.onClick.RemoveListener(OnClick);
    }
}