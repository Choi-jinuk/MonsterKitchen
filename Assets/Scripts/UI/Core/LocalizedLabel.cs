// ====================================================================
//  LocalizedLabel — UIToolkit 로컬라이제이션 레이블 커스텀 컨트롤
//
//  ▶ UXML 사용법
//    xmlns:mk="MonsterKitchen.UI" 선언 후:
//    <mk:LocalizedLabel string-id="UI_TITLE" class="my-class" />
//
//  ▶ 동작
//    string-id → LocalizedString("Strings", key) → StringChanged 콜백으로
//    text 자동 적용. 로케일 변경 시 자가 갱신 (UIPanel 개입 불필요).
//    Localization 미초기화 시 string-id 값 그대로 표시 (에디터 프리뷰용).
// ====================================================================

using MonsterKitchen.Core;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    [UxmlElement]
    public partial class LocalizedLabel : Label
    {
        [UxmlAttribute("string-id")]
        public string StringId
        {
            get => m_StringId;
            set { m_StringId = value; Bind(); }
        }

        string          m_StringId;
        LocalizedString m_Localized;

        public LocalizedLabel()
        {
            RegisterCallback<AttachToPanelEvent>(_ => Bind());
            RegisterCallback<DetachFromPanelEvent>(_ => Unbind());
        }

        void Bind()
        {
            Unbind();
            if (string.IsNullOrEmpty(m_StringId)) return;

            text = m_StringId; // 초기값 — 로드 완료 시 StringChanged 가 덮어씀

            if (panel == null) return; // 미부착 상태 — Attach 시 재바인딩
            m_Localized = Loc.Create(m_StringId);
            m_Localized.StringChanged += OnStringChanged;
        }

        void Unbind()
        {
            if (m_Localized == null) return;
            m_Localized.StringChanged -= OnStringChanged;
            m_Localized = null;
        }

        void OnStringChanged(string value) => text = value;
    }
}
