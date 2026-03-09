using System.Collections.Generic;
using UnityEngine;

namespace FoodMatch.Core
{
    public class ObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _container;
        private readonly Queue<GameObject> _pool = new Queue<GameObject>();
        private readonly HashSet<int> _inPoolSet = new HashSet<int>();

        public ObjectPool(GameObject prefab, Transform container, int preloadCount = 0)
        {
            _prefab = prefab;
            _container = container;
            for (int i = 0; i < preloadCount; i++)
            {
                var obj = CreateNew();
                obj.SetActive(false);
                _pool.Enqueue(obj);
                _inPoolSet.Add(obj.GetInstanceID());
            }
        }

        public GameObject Get(Vector3 position = default, Quaternion rotation = default)
        {
            GameObject obj = null;
            while (_pool.Count > 0)
            {
                var candidate = _pool.Dequeue();
                if (candidate != null)
                {
                    _inPoolSet.Remove(candidate.GetInstanceID());
                    obj = candidate;
                    break;
                }
                Debug.LogWarning($"[ObjectPool] Bỏ qua object đã bị destroy trong pool '{_prefab.name}'.");
            }

            if (obj == null)
            {
                obj = CreateNew();
                Debug.LogWarning($"[ObjectPool] Pool của '{_prefab.name}' hết – tạo mới runtime.");
            }

            if (position != Vector3.zero || rotation != Quaternion.identity)
                obj.transform.SetPositionAndRotation(position, rotation);

            obj.SetActive(true);
            if (obj.TryGetComponent<IPoolable>(out var poolable))
                poolable.OnSpawn();
            return obj;
        }

        public void Return(GameObject obj)
        {
            if (obj == null) return;

            int id = obj.GetInstanceID();
            if (_inPoolSet.Contains(id))
            {
                Debug.LogWarning($"[ObjectPool] Double-return detected cho '{_prefab.name}' (id={id}). Bỏ qua.");
                return;
            }

            obj.SetActive(false);
            obj.transform.SetParent(_container, false);
            obj.transform.localScale = Vector3.one;

            if (obj.TryGetComponent<IPoolable>(out var poolable))
                poolable.OnDespawn();

            _pool.Enqueue(obj);
            _inPoolSet.Add(id);
        }

        public void ReturnAll(List<GameObject> activeList)
        {
            foreach (var obj in activeList)
                Return(obj);
            activeList.Clear();
        }

        public int CountAvailable => _pool.Count;

        private GameObject CreateNew()
        {
            var obj = Object.Instantiate(_prefab, _container);
            obj.name = _prefab.name;
            return obj;
        }
    }
}