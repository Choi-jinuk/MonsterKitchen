using System.Collections.Generic;

namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>
    /// 런타임 블랙보드 — 문자열 키로 임의 타입 값을 저장한다.
    /// BTContext 를 통해 모든 노드가 공유한다.
    /// </summary>
    public class BTBlackboard
    {
        readonly Dictionary<string, object> _data = new();

        public void Set<T>(string key, T value) => _data[key] = value;

        public bool Has(string key) => _data.ContainsKey(key);

        public void Clear(string key) => _data.Remove(key);

        public T Get<T>(string key)
        {
            if (_data.TryGetValue(key, out var val) && val is T typed)
                return typed;
            return default;
        }

        public bool TryGet<T>(string key, out T result)
        {
            if (_data.TryGetValue(key, out var val) && val is T typed)
            {
                result = typed;
                return true;
            }
            result = default;
            return false;
        }

#if UNITY_EDITOR
        /// <summary>에디터에서 블랙보드 전체 항목을 열람한다.</summary>
        public System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object>>
            EditorEntries => _data;
#endif
    }
}
