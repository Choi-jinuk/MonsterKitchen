using System.Collections.Generic;

namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>
    /// 런타임 블랙보드 — 문자열 키로 임의 타입 값을 저장한다.
    /// BTContext 를 통해 모든 노드가 공유한다.
    /// </summary>
    public class BTBlackboard
    {
        readonly Dictionary<string, object> m_Data = new();

        public void Set<T>(string key, T value) => m_Data[key] = value;

        public bool Has(string key) => m_Data.ContainsKey(key);

        public void Clear(string key) => m_Data.Remove(key);

        public T Get<T>(string key)
        {
            if (m_Data.TryGetValue(key, out var val) && val is T typed)
                return typed;
            return default;
        }

        public bool TryGet<T>(string key, out T result)
        {
            if (m_Data.TryGetValue(key, out var val) && val is T typed)
            {
                result = typed;
                return true;
            }
            result = default;
            return false;
        }

#if UNITY_EDITOR
        public IEnumerable<KeyValuePair<string, object>> EditorEntries => m_Data;
#endif
    }
}
