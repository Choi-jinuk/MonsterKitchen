// ====================================================================
//  StringData / StringTable — 로컬라이제이션 문자열 테이블 (CSV → TableData SO)
//
//  ▶ StringId : 코드/데이터에서 참조하는 고유 키 (예: "MON_001_NAME")
//  ▶ 언어 추가 : StringData 에 필드 추가(예: Ja) + LocaleManager.GetText switch 케이스 추가
//  ▶ ID 범위  : 8001 ~ (STR_xxx 키)
//  ▶ Fallback : 언어 필드가 비어 있으면 Ko 반환, 키 미등록 시 StringId 자체 반환
// ====================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    [Serializable]
    public class StringData
    {
        [Header("Identity")]
        public uint   Id;        // DataTable 키 (8001, 8002, ...)
        public string StringId;  // 코드·데이터에서 참조하는 키 (예: "MON_001_NAME")

        [Header("Localized Text")]
        public string Ko;        // 한국어 (기본)
        public string En;        // English
        // 언어 추가 시 여기에 필드 추가:
        // public string Ja;     // 日本語
        // public string ZhCn;   // 中文(简)
    }

    [Serializable]
    public class StringTable : DataTable<StringData>
    {
        // ── 캐시 — 문자열 ID 역방향 조회 ────────────────────────────────
        Dictionary<string, StringData> m_ByStringId;

        public override void RuntimeSetData()
        {
            m_ByStringId = new Dictionary<string, StringData>();
            foreach (var sd in All)
                if (!string.IsNullOrEmpty(sd.StringId))
                    m_ByStringId[sd.StringId] = sd;
        }

        /// <summary>StringId 키(예: "MON_001_NAME")로 조회. O(1).</summary>
        public StringData FindByStringId(string stringId)
        {
            if (string.IsNullOrEmpty(stringId) || m_ByStringId == null) return null;
            return m_ByStringId.GetValueOrDefault(stringId);
        }
    }
}
