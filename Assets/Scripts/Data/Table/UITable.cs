// ====================================================================
//  UIData — UI 패널 정적 정의 (CSV → TableData SO)
//
//  ▶ UIID 범위: UI_001 ~ (uint 9001~)
//  ▶ PanelId   = UIManager.Open/Close/Toggle 에 전달하는 문자열
//               = 패널 프리팹 루트 GameObject 이름과 일치해야 함
//  ▶ PrefabAddress = AssetManifest 등록 키 (prefab/ui/{panelId.lower})
//                    CSV에 비어 있으면 AssetKeys.UIPanelPrefab(PanelId) 로 자동 생성
// ====================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    [Serializable]
    public class UIData
    {
        [Header("Identity")]
        public uint   Id;           // DataTable 키 (9001, 9002, ...)
        public string PanelId;      // "CookingUI" — UIManager.Open 에 전달하는 키
        public string DisplayName;  // "조리대" — 에디터·디버그용 표시명

        [Header("Asset — AssetManifest 등록 키")]
        [Tooltip("프리팹 주소. 비어 있으면 prefab/ui/{panelId.lower} 로 자동 생성.")]
        public string PrefabAddress;

        [Header("Input")]
        [Tooltip("단축키 문자열 (UnityEngine.InputSystem.Key 열거형 이름). 빈 칸 = 단축키 없음.\n예: I, B, Escape")]
        public string ToggleKey;    // "I" → Key.I, 빈 칸 = 없음
    }

    [Serializable]
    public class UITable : DataTable<UIData>
    {
        // ── 캐시 — 문자열 ID 역방향 조회 ────────────────────────────────
        Dictionary<string, UIData>         m_UIsByPanelId;

        public override void RuntimeSetData()
        {
            m_UIsByPanelId ??= new Dictionary<string, UIData>();
            m_UIsByPanelId.Clear();
            
            foreach (var ui in All)
                if (!string.IsNullOrEmpty(ui.PanelId))
                    m_UIsByPanelId[ui.PanelId] = ui;
        }

        /// <summary>
        /// PanelId 문자열("CookingUI" 등)로 UIData 를 조회한다.
        /// 첫 호출 시 역방향 캐시를 빌드하고 이후 O(1) 조회.
        /// </summary>
        public UIData FindUIByPanelId(string panelId)
        {
            if (string.IsNullOrEmpty(panelId)) return null;

            return m_UIsByPanelId.GetValueOrDefault(panelId);
        }
    }
}
