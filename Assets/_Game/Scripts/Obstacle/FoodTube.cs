using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FoodMatch.Data;
using FoodMatch.Food;

namespace FoodMatch.Obstacle
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class FoodTube : MonoBehaviour, IPointerClickHandler
    {
        [Header("─── Slot Anchor ─────────────────────────")]
        [SerializeField] private Transform headSlot;

        [Header("─── Animation ───────────────────────────")]
        [SerializeField] private float popDuration = 0.25f;
        [SerializeField] private float popScale = 1.15f;

        [Header("─── Debug ───────────────────────────────")]
        [SerializeField] private bool verboseLog = true;

        // ─── Events ──────────────────────────────────────────────────────────
        public System.Action<FoodTube> OnTubeEmpty;

        // ─── Runtime ─────────────────────────────────────────────────────────
        private RectTransform _rect;

        private FoodItem _headItem;
        private FoodItemData _headData;
        private readonly Queue<FoodItemData> _queue = new();

        private int _tubeIndex;
        private bool _isTaking;

        // ─── Public ──────────────────────────────────────────────────────────
        public int TubeIndex => _tubeIndex;
        public FoodItemData HeadData => _headData;
        public FoodItem HeadItem => _headItem;
        public bool IsEmpty => _headItem == null && _queue.Count == 0;
        public int RemainingCount => (_headItem != null ? 1 : 0) + _queue.Count;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        public void SetTubeAnchor(RectTransform anchorRect)
        {
            if (anchorRect == null) return;
            _rect.position = anchorRect.position;
            _rect.sizeDelta = anchorRect.sizeDelta;
            _rect.localRotation = anchorRect.localRotation;
            _rect.localScale = anchorRect.localScale;
        }

        public void Initialize(int index, List<FoodItemData> foods, Transform _ignored)
        {
            _tubeIndex = index;
            _isTaking = false;

            StopAllCoroutines();
            DestroyHead();
            _queue.Clear();

            if (foods == null || foods.Count == 0)
            {
                Log("Ống rỗng.");
                OnTubeEmpty?.Invoke(this);
                return;
            }

            foreach (var f in foods)
                if (f != null) _queue.Enqueue(f);

            SpawnNextHead();
            Log($"Init xong — {foods.Count} food | head=[{_headData?.name}] | queue={_queue.Count}");
        }

        // ─── Tap ─────────────────────────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_headItem == null || _isTaking) return;
            _isTaking = true;
            Log($"Tapped [{_headData?.name}]");

            FoodFlowController.Instance?.HandleTubeFoodTapped(_headItem, this, () =>
            {
                _isTaking = false;
            });
        }

        public void TakeHead()
        {
            if (_headItem == null) return;
            Log($"TakeHead [{_headData?.name}] — queue còn {_queue.Count}");

            DestroyHead();

            if (_queue.Count > 0)
                SpawnNextHead();
            else
            {
                Log("Hết food trong ống!");
                OnTubeEmpty?.Invoke(this);
            }
        }

        public void ClearTube()
        {
            StopAllCoroutines();
            DestroyHead();
            _queue.Clear();
            _isTaking = false;
            Log("Cleared.");
        }

        // ─── Spawn ───────────────────────────────────────────────────────────

        private void SpawnNextHead()
        {
            if (_queue.Count == 0) return;

            _headData = _queue.Dequeue();
            if (_headData?.prefab == null)
            {
                Log("Food data null, skip.");
                SpawnNextHead();
                return;
            }

            Vector3 spawnPos = headSlot != null ? headSlot.position : transform.position;
            Vector3 finalScale = _headData.prefab.transform.localScale;

            var go = Instantiate(_headData.prefab, spawnPos, Quaternion.identity);
            go.name = $"TubeHead[{_tubeIndex}]_{_headData.name}";

            _headItem = go.GetComponent<FoodItem>();
            if (_headItem == null)
            {
                Debug.LogError($"[FoodTube:{_tubeIndex}] Prefab [{_headData.name}] thiếu FoodItem!");
                Destroy(go);
                _headData = null;
                return;
            }

            _headItem.Initialize(_headData, layerIndex: 0);

            // Disable FoodInteractionHandler — click xử lý bởi FoodTube hoặc Forwarder
            var handler = go.GetComponent<FoodInteractionHandler>();
            if (handler != null) handler.enabled = false;

            // Thêm forwarder để forward click từ food 3D về tube này
            var forwarder = go.AddComponent<TubeHeadClickForwarder>();
            forwarder.SetOwnerTube(this);

            // Pop animation
            go.transform.localScale = Vector3.zero;
            StartCoroutine(PopAnim(go.transform, finalScale));

            Log($"Spawn head [{_headData.name}] tại {spawnPos} | còn {_queue.Count} trong queue");
        }

        private void DestroyHead()
        {
            if (_headItem != null)
            {
                Destroy(_headItem.gameObject);
                _headItem = null;
            }
            _headData = null;
        }

        // ─── Animation ───────────────────────────────────────────────────────

        private IEnumerator PopAnim(Transform t, Vector3 finalScale)
        {
            if (t == null) yield break;

            Vector3 big = finalScale * popScale;
            float half = popDuration * 0.5f;

            for (float e = 0f; e < half; e += Time.deltaTime)
            {
                if (t == null) yield break;
                t.localScale = Vector3.Lerp(Vector3.zero, big, e / half);
                yield return null;
            }
            for (float e = 0f; e < half; e += Time.deltaTime)
            {
                if (t == null) yield break;
                t.localScale = Vector3.Lerp(big, finalScale, e / half);
                yield return null;
            }
            if (t != null) t.localScale = finalScale;
        }

        private void Log(string msg)
        {
            if (verboseLog) Debug.Log($"[FoodTube:{_tubeIndex}] {msg}");
        }
    }
}