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
        readonly T         m_Prefab;
        readonly Transform m_Parent;
        readonly int       m_MaxSize;
        readonly Queue<T>  m_Idle = new();

        int m_TotalCreated;

        /// <param name="prefab">풀링할 프리팹 (비활성 상태여도 무방)</param>
        /// <param name="parent">생성된 오브젝트의 부모 Transform</param>
        /// <param name="initialSize">사전 생성 수</param>
        /// <param name="maxSize">풀 최대 크기 (0 = 무제한)</param>
        public ObjectPool(T prefab, Transform parent, int initialSize = 0, int maxSize = 0)
        {
            m_Prefab  = prefab;
            m_Parent  = parent;
            m_MaxSize = maxSize;

            for (int i = 0; i < initialSize; i++)
                m_Idle.Enqueue(CreateNew());
        }

        /// <summary>풀에서 인스턴스를 꺼낸다. 비어 있으면 새로 생성한다.</summary>
        public T Get(Vector3 position = default, Quaternion rotation = default)
        {
            T obj;
            if (m_Idle.Count > 0)
            {
                obj = m_Idle.Dequeue();
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

            if (m_MaxSize > 0 && m_Idle.Count >= m_MaxSize)
            {
                Object.Destroy(obj.gameObject);
                m_TotalCreated--;
                return;
            }

            obj.transform.SetParent(m_Parent);
            m_Idle.Enqueue(obj);
        }

        /// <summary>현재 대기 중인 인스턴스 수.</summary>
        public int IdleCount => m_Idle.Count;

        // ── internal ──────────────────────────────────────────────────

        T CreateNew()
        {
            var go = Object.Instantiate(m_Prefab, m_Parent);
            go.gameObject.SetActive(false);
            m_TotalCreated++;
            return go;
        }
    }
}
