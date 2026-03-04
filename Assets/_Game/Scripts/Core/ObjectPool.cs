using System.Collections.Generic;
using UnityEngine;

namespace FoodMatch.Core
{
    /// <summary>
    /// Generic Object Pool dùng chung cho mọi loại GameObject.
    /// Cách dùng: pool.Get() để lấy object, pool.Return(obj) để trả lại.
    /// </summary>
    public class ObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _container;
        private readonly Queue<GameObject> _pool = new Queue<GameObject>();

        /// <param name="prefab">Prefab sẽ được Instantiate</param>
        /// <param name="container">Transform cha để chứa các object trong pool (giữ Hierarchy gọn)</param>
        /// <param name="preloadCount">Số lượng khởi tạo sẵn lúc đầu</param>
        public ObjectPool(GameObject prefab, Transform container, int preloadCount = 0)
        {
            _prefab = prefab;
            _container = container;

            for (int i = 0; i < preloadCount; i++)
            {
                var obj = CreateNew();
                obj.SetActive(false);
                _pool.Enqueue(obj);
            }
        }

        /// <summary>Lấy 1 object từ pool. Nếu pool trống thì tạo mới.</summary>
        public GameObject Get(Vector3 position = default, Quaternion rotation = default)
        {
            GameObject obj;

            if (_pool.Count > 0)
            {
                obj = _pool.Dequeue();
            }
            else
            {
                obj = CreateNew();
                Debug.LogWarning($"[ObjectPool] Pool của '{_prefab.name}' hết – tạo mới runtime.");
            }

            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
            if (obj.TryGetComponent<IPoolable>(out var poolable))
                poolable.OnSpawn();
            return obj;
        }

        /// <summary>Trả 1 object về pool.</summary>
        public void Return(GameObject obj)
        {
            if (obj == null) return;
            obj.SetActive(false);
            obj.transform.SetParent(_container);
            if (obj.TryGetComponent<IPoolable>(out var poolable))
                poolable.OnDespawn();
            _pool.Enqueue(obj);
        }

        /// <summary>Trả tất cả object đang active về pool (dùng khi reset level).</summary>
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