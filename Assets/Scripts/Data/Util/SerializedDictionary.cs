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
        [SerializeField] List<TKey>   m_Keys   = new();
        [SerializeField] List<TValue> m_Values = new();

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            m_Keys.Clear();
            m_Values.Clear();
            foreach (var kv in this)
            {
                m_Keys.Add(kv.Key);
                m_Values.Add(kv.Value);
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            Clear();
            int count = Math.Min(m_Keys.Count, m_Values.Count);
            for (int i = 0; i < count; i++)
                TryAdd(m_Keys[i], m_Values[i]);
        }
    }
}
