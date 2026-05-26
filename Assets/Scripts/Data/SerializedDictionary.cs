using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    /// <summary>
    /// Unity Inspector에서 편집 가능한 직렬화 딕셔너리.
    /// ISerializationCallbackReceiver로 key/value 리스트 ↔ Dictionary 변환.
    /// </summary>
    [Serializable]
    public class SerializedDictionary<TKey, TValue>
        : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] List<TKey>   _keys   = new();
        [SerializeField] List<TValue> _values = new();

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            _keys.Clear();
            _values.Clear();
            foreach (var kv in this)
            {
                _keys.Add(kv.Key);
                _values.Add(kv.Value);
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            Clear();
            int count = Math.Min(_keys.Count, _values.Count);
            for (int i = 0; i < count; i++)
                TryAdd(_keys[i], _values[i]);
        }
    }
}
