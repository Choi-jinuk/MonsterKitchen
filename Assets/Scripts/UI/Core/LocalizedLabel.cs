// ====================================================================
//  LocalizedLabel — UIToolkit 로컬라이제이션 레이블 커스텀 컨트롤
//
//  ▶ UXML 사용법
//    xmlns:mk="MonsterKitchen.UI" 선언 후:
//    <mk:LocalizedLabel string-id="UI_TITLE" class="my-class" />
//
//  ▶ 동작
//    string-id → LocaleManager.Get() → text 자동 적용.
//    LocaleManager 미로드 시 string-id 값 그대로 표시 (에디터 프리뷰용).
//
//  ▶ 언어 변경 대응
//    UIPanel.OnOpen/OnClose 에서 LocaleManager.OnLanguageChanged 구독/해제.
//    언어 변경 시 패널 내 모든 LocalizedLabel 이 자동 갱신됨.
// ====================================================================

using MonsterKitchen.Core;
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
            set { m_StringId = value; RefreshText(); }
        }

        string m_StringId;

        public LocalizedLabel()
        {
            // 패널 계층에 붙을 때 (런타임 Open 시) 다시 갱신 — LocaleManager 로드 완료 시점
            RegisterCallback<AttachToPanelEvent>(_ => RefreshText());
        }

        /// <summary>현재 언어로 text 를 갱신한다. UIPanel.RefreshLocale() 에서 호출됨.</summary>
        public void Refresh() => RefreshText();

        void RefreshText()
        {
            if (string.IsNullOrEmpty(m_StringId)) return;
            text = LocaleManager.Get(m_StringId);
        }
    }
}
