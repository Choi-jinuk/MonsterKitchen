using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    /// <summary>
    /// 모든 UI 패널의 베이스 클래스.
    ///
    /// ┌─ 사용법 ────────────────────────────────────────────────────────┐
    /// │ 1. 이 클래스를 상속한다.                                         │
    /// │ 2. Inspector 에서 uiLayer / isPopup / toggleKey 설정.           │
    /// │ 3. UIManager.Instance.Open/Close/Toggle(PanelId) 로 제어.       │
    /// │    또는 toggleKey 를 설정하면 키 입력이 자동으로 연결된다.        │
    /// │ 4. 열기·닫기 시 동작은 OnOpen / OnClose 를 override 한다.       │
    /// └────────────────────────────────────────────────────────────────┘
    ///
    /// ┌─ 레이어 ────────────────────────────────────────────────────────┐
    /// │ UIDocument.sortingOrder 를 UILayer 값으로 자동 설정.             │
    /// │ HUD=10 / Panel=20 / Popup=30 / Overlay=100                     │
    /// └────────────────────────────────────────────────────────────────┘
    ///
    /// ┌─ 팝업 스택 ─────────────────────────────────────────────────────┐
    /// │ isPopup=true → UIManager 팝업 스택에 push/pop.                  │
    /// │ 뒤쪽 팝업 : OnBlur 호출 / 앞으로 오면 : OnFocus 호출.           │
    /// └────────────────────────────────────────────────────────────────┘
    ///
    /// ┌─ 단축키 ────────────────────────────────────────────────────────┐
    /// │ toggleKey 를 설정하면 InputManager 에 자동 등록된다.             │
    /// │ 입력 처리는 InputManager.Update() 가 일괄 담당.                 │
    /// └────────────────────────────────────────────────────────────────┘
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public abstract class UIPanel : MonoBehaviour
    {
        [Header("UI Panel 설정")]
        [SerializeField] UILayer _layer    = UILayer.Panel;
        [SerializeField] bool    _isPopup  = false;

        [Header("단축키 (None = 사용 안 함)")]
        [SerializeField] Key _toggleKey = Key.None;

        // ── 프로퍼티 ──────────────────────────────────────────────────
        public string  PanelId  => gameObject.name;
        public UILayer Layer    => _layer;
        public bool    IsPopup  => _isPopup;
        public bool    IsOpen   { get; private set; }

        protected UIDocument    Document { get; private set; }
        protected VisualElement Root     { get; private set; }

        // ── Unity 생명주기 ────────────────────────────────────────────

        protected virtual void Awake()
        {
            Document = GetComponent<UIDocument>();
            Root     = Document?.rootVisualElement;

            if (Document != null)
                Document.sortingOrder = (int)_layer;

            // 패널 루트를 부모(HUD UIDocument 루트)에 꽉 채워 절대 배치한다.
            // UIDocument가 다른 UIDocument에 parented 되면, 루트 VisualElement는
            // 부모의 flex child가 되어 크기가 0이 된다. 명시적으로 100% 크기를 지정해야
            // 자식의 position:absolute 오버레이가 올바르게 동작한다.
            if (Root != null)
            {
                Root.style.position = Position.Absolute;
                Root.style.top      = 0;
                Root.style.left     = 0;
                Root.style.width    = new StyleLength(new Length(100, LengthUnit.Percent));
                Root.style.height   = new StyleLength(new Length(100, LengthUnit.Percent));
            }

            // 패널은 기본적으로 숨김 상태로 시작한다. OnOpen() 호출 시 표시된다.
            SetVisible(false);
        }

        protected virtual void Start()
        {
            UIManager.Instance?.Register(this);

            // toggleKey 가 지정된 경우 InputManager 에 자동 등록
            if (_toggleKey != Key.None)
                InputManager.Instance?.RegisterUIToggle(PanelId, _toggleKey);
        }

        protected virtual void OnDestroy()
        {
            UIManager.Instance?.Unregister(this);
            InputManager.Instance?.UnregisterUIToggle(PanelId);
        }

        // ── UIManager 가 호출하는 생명주기 ───────────────────────────
        // ※ 직접 호출 금지 — UIManager.Open / Close / Toggle 을 사용할 것.

        /// <summary>패널이 열릴 때 UIManager 가 호출한다.</summary>
        public virtual void OnOpen()
        {
            IsOpen = true;
            SetVisible(true);
        }

        /// <summary>패널이 닫힐 때 UIManager 가 호출한다.</summary>
        public virtual void OnClose()
        {
            IsOpen = false;
            SetVisible(false);
        }

        /// <summary>팝업 스택에서 이 패널이 최상위가 됐을 때 호출된다.</summary>
        public virtual void OnFocus() { }

        /// <summary>팝업 스택에서 위에 다른 팝업이 쌓일 때 호출된다.</summary>
        public virtual void OnBlur() { }

        // ── 헬퍼 ──────────────────────────────────────────────────────

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
