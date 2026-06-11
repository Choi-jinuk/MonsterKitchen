// ====================================================================
//  UIPanel — 모든 UI 패널의 베이스 클래스
//
//  ▶ 사용법
//    1. 이 클래스를 상속한다.
//    2. Inspector 에서 m_Layer / m_IsPopup / m_ToggleKey 설정.
//    3. UIManager.Instance.Open/Close/Toggle(PanelId) 로 제어.
//    4. UXML 요소 쿼리 및 콜백 등록 → OnFirstOpen() 에서 수행.
//    5. 열기·닫기 추가 동작 → OnOpen / OnClose override.
//
//  ▶ 레이어
//    UIDocument.sortingOrder = UILayer 값.
//    HUD=10 / Panel=20 / Popup=30 / Overlay=100
//
//  ▶ 팝업 스택
//    m_IsPopup=true → UIManager 팝업 스택 push/pop.
//    OnBlur: 위에 팝업 쌓일 때 / OnFocus: 최상위 됐을 때.
//
//  ▶ 동적 로딩
//    UIManager.m_PanelDefs 에 등록된 패널은 Open 시 자동 Instantiate.
//    OnFirstOpen()이 첫 열기 때 1회 호출되므로 Start() 의존 불필요.
// ====================================================================

using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    [RequireComponent(typeof(UIDocument))]
    public abstract class UIPanel : MonoBehaviour
    {
        [Header("UI Panel 설정")]
        [SerializeField] UILayer m_Layer   = UILayer.Panel;
        [SerializeField] bool    m_IsPopup = false;

        [Header("단축키 (None = 사용 안 함)")]
        [SerializeField] Key m_ToggleKey = Key.None;

        // ── 프로퍼티 ──────────────────────────────────────────────────
        public string  PanelId => gameObject.name;
        public UILayer Layer   => m_Layer;
        public bool    IsPopup => m_IsPopup;
        public bool    IsOpen  { get; private set; }

        protected UIDocument    Document { get; private set; }
        protected VisualElement Root     { get; private set; }

        // ── 내부 상태 ─────────────────────────────────────────────────
        bool m_RegisteredByManager; // UIManager가 직접 등록했으면 true → Start 에서 중복 등록 방지
        bool m_Initialized;         // OnFirstOpen 호출 여부

        // ── Unity 생명주기 ────────────────────────────────────────────

        protected virtual void Awake()
        {
            Document = GetComponent<UIDocument>();
            Root     = Document?.rootVisualElement;

            if (Document != null)
                Document.sortingOrder = (int)m_Layer;

            // 패널 루트를 전체 화면에 꽉 채워 절대 배치.
            // UIDocument 가 다른 UIDocument 에 parented 되면 크기가 0이 되므로
            // 명시적으로 100% 지정해야 position:absolute 오버레이가 올바르게 동작한다.
            if (Root != null)
            {
                Root.style.position = Position.Absolute;
                Root.style.top      = 0;
                Root.style.left     = 0;
                Root.style.width    = new StyleLength(new Length(100, LengthUnit.Percent));
                Root.style.height   = new StyleLength(new Length(100, LengthUnit.Percent));
            }

            SetVisible(false);
        }

        protected virtual void Start()
        {
            // UIManager 가 Instantiate 후 직접 등록한 경우 중복 등록 건너뜀.
            if (!m_RegisteredByManager)
                UIManager.Instance?.Register(this);

            // 씬 직접 배치 패널의 단축키 등록 (동적 로딩 패널은 UIData.ToggleKey 경유).
            if (m_ToggleKey != Key.None)
                InputManager.Instance?.RegisterUIToggle(PanelId, m_ToggleKey);
        }

        protected virtual void OnDestroy()
        {
            LocaleManager.OnLanguageChanged -= RefreshLocale;
            UIManager.Instance?.Unregister(this);
            InputManager.Instance?.UnregisterUIToggle(PanelId);
        }

        // ── UIManager 가 호출하는 생명주기 ───────────────────────────
        // ※ 직접 호출 금지 — UIManager.Open / Close / Toggle 사용.

        /// <summary>패널이 열릴 때 UIManager 가 호출한다.</summary>
        public virtual void OnOpen()
        {
            // 첫 열기 시 UXML 쿼리 및 콜백 등록 수행.
            if (!m_Initialized)
            {
                m_Initialized = true;
                OnFirstOpen();
            }

            IsOpen = true;
            SetVisible(true);
            RefreshLocale();

            // 열려 있는 동안 언어 변경에 즉시 반응.
            LocaleManager.OnLanguageChanged += RefreshLocale;
        }

        /// <summary>패널이 닫힐 때 UIManager 가 호출한다.</summary>
        public virtual void OnClose()
        {
            LocaleManager.OnLanguageChanged -= RefreshLocale;
            IsOpen = false;
            SetVisible(false);
        }

        /// <summary>팝업 스택에서 이 패널이 최상위가 됐을 때 호출된다.</summary>
        public virtual void OnFocus() { }

        /// <summary>팝업 스택에서 위에 다른 팝업이 쌓일 때 호출된다.</summary>
        public virtual void OnBlur() { }

        // ── 서브클래스 훅 ────────────────────────────────────────────

        /// <summary>
        /// 첫 번째 OnOpen 시 한 번만 호출된다.
        /// UXML 요소 쿼리, 버튼 콜백 등록 등 초기화 코드를 여기에 작성한다.
        /// Start() 대신 이 메서드를 사용하면 동적 로딩 시 타이밍 문제가 없다.
        /// </summary>
        protected virtual void OnFirstOpen() { }

        // ── UIManager 내부 전용 ───────────────────────────────────────

        /// <summary>UIManager 가 동적 Instantiate 후 직접 호출. Start 중복 등록 방지용.</summary>
        internal void NotifyRegisteredByManager()
        {
            m_RegisteredByManager = true;
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────

        /// <summary>Root 내 모든 LocalizedLabel 을 현재 언어로 갱신한다.</summary>
        protected virtual void RefreshLocale()
        {
            Root?.Query<LocalizedLabel>().ForEach(l => l.Refresh());
        }

        /// <summary>Root VisualElement 의 display 를 토글한다.</summary>
        protected void SetVisible(bool visible)
        {
            if (Root == null) return;
            Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>특정 VisualElement 의 display 를 토글한다.</summary>
        protected static void SetVisible(VisualElement element, bool visible)
        {
            if (element == null) return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
