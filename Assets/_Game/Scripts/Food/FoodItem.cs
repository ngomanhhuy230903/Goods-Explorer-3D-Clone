using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;
using FoodMatch.Data;
using FoodMatch.Tray;

namespace FoodMatch.Food
{
    public class FoodItem : MonoBehaviour, IPoolable
    {
        // ─── References ──────────────────────────────────────────────────────
        [Header("─── References ─────────────────────")]
        [Tooltip("MeshRenderer chính của món ăn. Để trống = tự tìm trong children.")]
        [SerializeField] private MeshRenderer meshRenderer;

        // ─── Runtime Data ─────────────────────────────────────────────────────
        public FoodItemData Data { get; private set; }
        public int FoodID => Data != null ? Data.foodID : -1;
        public int LayerIndex { get; private set; } = 0;
        public TraySlot OwnerSlot { get; set; }
        public Transform AnchorRef { get; private set; }
        public FoodMatch.Tray.FoodTray OwnerTray { get; set; }

        private Color _originalColor;
        private Collider _collider;

        // Scale gốc lấy từ prefab — được set trong Initialize(), KHÔNG dùng Awake()
        // vì Awake() chạy lúc preload trong pool container nên localScale bị ảnh hưởng parent
        private Vector3 _originalScale;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _collider = GetComponent<Collider>();

            if (meshRenderer == null)
                meshRenderer = GetComponentInChildren<MeshRenderer>();

            if (meshRenderer != null)
                _originalColor = meshRenderer.material.color;
        }

        // ─── IPoolable ────────────────────────────────────────────────────────
        public void OnSpawn()
        {
            gameObject.SetActive(true);
        }

        public void OnDespawn()
        {
            RestoreOriginalColor();
            OwnerSlot = null;
            Data = null;
            LayerIndex = 0;
        }

        // ─── Public API ───────────────────────────────────────────────────────
        public void Initialize(FoodItemData data, int layerIndex)
        {
            Data = data;

            // Luôn lấy scale từ prefab gốc — không phụ thuộc vào parent hiện tại
            _originalScale = data.prefab.transform.localScale;

            if (meshRenderer != null)
                _originalColor = meshRenderer.material.color;

            SetLayerVisual(layerIndex);
        }

        public void SetLayerVisual(int layerIndex)
        {
            LayerIndex = layerIndex;

            switch (layerIndex)
            {
                case 0: ApplyActiveState(); break;
                case 1: ApplyGreyedState(); break;
                default: ApplyHiddenState(); break;
            }
        }

        // ─── Visual States ────────────────────────────────────────────────────
        private void ApplyActiveState()
        {
            gameObject.SetActive(true);
            // Không set scale ở đây — FoodTray lo việc set + pop-in animation
            RestoreOriginalColor();

            if (_collider != null)
                _collider.enabled = true;
        }

        private void ApplyGreyedState()
        {
            gameObject.SetActive(true);
            // Scale theo tỉ lệ từ prefab gốc, không dùng giá trị cứng
            transform.localScale = _originalScale * 0.8f;

            if (meshRenderer != null)
            {
                Color grey = Data != null
                    ? Data.lockedTintColor
                    : new Color(0.4f, 0.4f, 0.4f, 1f);
                meshRenderer.material.color = grey;
            }

            if (_collider != null)
                _collider.enabled = false;
        }

        private void ApplyHiddenState()
        {
            gameObject.SetActive(false);

            if (_collider != null)
                _collider.enabled = false;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private void RestoreOriginalColor()
        {
            if (meshRenderer != null)
                meshRenderer.material.color = _originalColor;
        }

        // ─── Bounce Animation ─────────────────────────────────────────────────
        public void PlayLockedBounce()
        {
            Vector3 randomDir = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0.3f, 1f),
                0f
            ).normalized;

            transform.DOPunchPosition(randomDir * 0.15f, 0.35f, 5, 0.5f);
        }

        public void SetAnchorRef(Transform anchor) => AnchorRef = anchor;
    }
}