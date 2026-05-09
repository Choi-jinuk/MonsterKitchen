using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 제네릭 오브젝트 풀.
    /// 사용 방법:
    ///   var pool = new ObjectPool&lt;MyComponent&gt;(prefab, parent, initialSize: 10, maxSize: 50);
    ///   var obj  = pool.Get(position);
    ///   pool.Return(obj);
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        readonly T         _prefab;
        readonly Transform _parent;
        readonly int       _maxSize;
        readonly Queue<T>  _idle = new();

        int _totalCreated;

        /// <param name="prefab">풀링할 프리팹 (비활성 상태여도 무방)</param>
        /// <param name="parent">생성된 오브젝트의 부모 Transform</param>
        /// <param name="initialSize">사전 생성 수</param>
        /// <param name="maxSize">풀 최대 크기 (0 = 무제한)</param>
        public ObjectPool(T prefab, Transform parent, int initialSize = 0, int maxSize = 0)
        {
            _prefab  = prefab;
            _parent  = parent;
            _maxSize = maxSize;

            for (int i = 0; i < initialSize; i++)
                _idle.Enqueue(CreateNew());
        }

        /// <summary>풀에서 인스턴스를 꺼낸다. 비어 있으면 새로 생성한다.</summary>
        public T Get(Vector3 position = default, Quaternion rotation = default)
        {
            T obj;
            if (_idle.Count > 0)
            {
                obj = _idle.Dequeue();
            }
            else
            {
                obj = CreateNew();
            }

            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.gameObject.SetActive(true);
            return obj;
        }

        /// <summary>인스턴스를 풀에 반환한다. maxSize 초과 시 파괴한다.</summary>
        public void Return(T obj)
        {
            if (obj == null) return;
            obj.gameObject.SetActive(false);

            if (_maxSize > 0 && _idle.Count >= _maxSize)
            {
                Object.Destroy(obj.gameObject);
                _totalCreated--;
                return;
            }

            obj.transform.SetParent(_parent);
            _idle.Enqueue(obj);
        }

        /// <summary>현재 대기 중인 인스턴스 수.</summary>
        public int IdleCount => _idle.Count;

        // ── internal ──────────────────────────────────────────────────

        T CreateNew()
        {
            var go = Object.Instantiate(_prefab, _parent);
            go.gameObject.SetActive(false);
            _totalCreated++;
            return go;
        }
    }
}
