using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    /// <summary>
    /// uint ID → T 단일 테이블. TableData 의 각 데이터 타입 필드에 사용.
    /// SerializedDictionary 를 캡슐화하고 Get / All / Contains / Count 등
    /// 공통 조회 API 를 제공한다.
    ///
    /// 새 데이터 타입을 추가할 때 TableData 에 DataTable&lt;NewData&gt; 필드 하나만
    /// 추가하면 되고, 별도 Get/All 메서드는 작성할 필요가 없다.
    /// </summary>
    [Serializable]
    public class DataTable<T> where T : class
    {
        [SerializeField] SerializedDictionary<uint, T> _dict = new();

        // ── 내부 딕셔너리 ────────────────────────────────────────────────
        /// <summary>
        /// 내부 SerializedDictionary 직접 접근.
        /// ScriptableObjectSync 등 파이프라인 도구에서만 사용한다.
        /// </summary>
        public SerializedDictionary<uint, T> Dict => _dict;

        // ── 조회 API ─────────────────────────────────────────────────────

        /// <summary>ID 로 데이터를 조회한다. 없으면 null 반환.</summary>
        public T Get(uint id) => _dict.GetValueOrDefault(id);

        /// <summary>모든 데이터 값을 순회한다.</summary>
        public IEnumerable<T> All => _dict.Values;

        /// <summary>모든 ID(키)를 순회한다.</summary>
        public IEnumerable<uint> AllIds => _dict.Keys;

        /// <summary>테이블에 등록된 항목 수.</summary>
        public int Count => _dict.Count;

        /// <summary>해당 ID 가 테이블에 존재하는지 확인한다.</summary>
        public bool Contains(uint id) => _dict.ContainsKey(id);
    }
}
