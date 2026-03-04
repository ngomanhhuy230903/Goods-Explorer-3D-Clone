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

        // Lưu màu gốc để restore lại khi lên layer 0
        private Color _originalColor;
        private Collider _collider;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _collider = GetComponent<Collider>();

            // Tự tìm MeshRenderer nếu chưa gán
            if (meshRenderer == null)
                meshRenderer = GetComponentInChildren<MeshRenderer>();

            // Lưu màu gốc của material
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
            // Restore màu gốc trước khi trả về pool
            RestoreOriginalColor();
            OwnerSlot = null;
            Data = null;
            LayerIndex = 0;
        }

        // ─── Public API ───────────────────────────────────────────────────────
        public void Initialize(FoodItemData data, int layerIndex)
        {
            Data = data;

            // Lưu lại màu gốc mỗi lần Initialize
            // (vì prefab khác nhau có màu gốc khác nhau)
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
            transform.localScale = Data?.normalScale ?? Vector3.one;

            // Restore màu gốc
            RestoreOriginalColor();

            if (_collider != null)
                _collider.enabled = true;
        }

        private void ApplyGreyedState()
        {
            gameObject.SetActive(true);
            transform.localScale = Data?.greyedScale ?? new Vector3(0.8f, 0.8f, 0.8f);

            // Chỉ tint xám màu material hiện tại, không đổi material
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
    }
}