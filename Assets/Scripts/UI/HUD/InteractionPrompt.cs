using TMPro; // C-10
using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  InteractionPrompt — C-10 상호작용 근접 UI
    //
    //  상호작용 가능한 오브젝트에 추가하면, 플레이어가 Trigger에 진입했을 때
    //  오브젝트 위에 "[E] 상호작용" 같은 프롬프트를 월드 스페이스로 표시한다.
    //
    //  ▶ 씬 셋업 (필수)
    //    이 컴포넌트가 붙은 오브젝트의 자식으로 아래 구조를 미리 만들어둔다:
    //
    //      PromptRoot          ← Canvas (RenderMode = WorldSpace, SortingOrder = 50)
    //        Background        ← Image (반투명 배경)
    //        Label             ← TextMeshProUGUI (프롬프트 전체 텍스트)
    //
    //    Inspector에서 _promptRoot, _label 을 각 오브젝트에 연결한다.
    //    위치/크기는 씬에서 직접 조정한다.
    //
    //  ▶ 키 표시 자동화
    //    Awake 시 InputSystem_Actions.Player.Interact 바인딩을 읽어
    //    "[E] 던전 입장" 처럼 실제 바인딩된 키 이름을 자동으로 반영한다.
    //    Interact 키를 Space 등으로 변경하면 "[Space] 던전 입장" 으로 자동 변경.
    //
    //  ▶ 외부 제어
    //    - Show() / Hide() 로 코드에서 직접 표시/숨김 가능.
    //    - SetActionText(string) 으로 키 뒤에 오는 설명 문자열 변경 가능.
    // ====================================================================

    public class InteractionPrompt : MonoBehaviour
    {
        [Header("씬 오브젝트 참조")]
        [SerializeField] GameObject      _promptRoot;  // Canvas 루트 오브젝트
        [SerializeField] TextMeshProUGUI _label;       // 프롬프트 전체 텍스트

        [Header("설정")]
        [SerializeField] string _actionText = "상호작용";  // 키 이름 뒤에 붙는 설명

        bool _playerInside;

        // ----------------------------------------------------------------

        void Awake()
        {
            if (_promptRoot != null) _promptRoot.SetActive(false);
        }

        void Start()
        {
            // InputManager 는 GlobalController 가 초기화하므로 Start 시점에 참조한다.
            RefreshLabel();
        }

        // 현재 Interact 바인딩 키 이름을 읽어 레이블을 갱신한다.
        void RefreshLabel()
        {
            if (_label == null) return;

            string keyName = GetInteractKeyName();
            _label.text = keyName != null ? $"[{keyName}] {_actionText}" : _actionText;
        }

        // InputManager 가 보유한 Interact 액션에서 키 이름을 읽는다.
        // 바인딩을 찾지 못하면 null 을 반환한다.
        string GetInteractKeyName()
        {
            if (InputManager.Instance == null) return null;

            InputAction action = InputManager.Instance.InteractAction;
            return TryGetDisplayString(action, "Keyboard")
                ?? TryGetDisplayString(action, null);
        }

        // group 이 null 이면 그룹 무관하게 첫 번째 단순 바인딩을 반환한다.
        static string TryGetDisplayString(InputAction action, string group)
        {
            foreach (var binding in action.bindings)
            {
                if (binding.isComposite || binding.isPartOfComposite) continue;
                if (string.IsNullOrEmpty(binding.effectivePath)) continue;
                if (group != null && !binding.groups.Contains(group)) continue;

                return InputControlPath.ToHumanReadableString(
                    binding.effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
            return null;
        }

        // ----------------------------------------------------------------
        //  Trigger 감지
        // ----------------------------------------------------------------

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = true;
            _promptRoot?.SetActive(true);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = false;
            _promptRoot?.SetActive(false);
        }

        // ----------------------------------------------------------------
        //  공개 API
        // ----------------------------------------------------------------

        /// <summary>플레이어가 범위 안에 있을 때만 표시한다.</summary>
        public void Show()
        {
            if (_playerInside) _promptRoot?.SetActive(true);
        }

        /// <summary>강제로 숨긴다 (요리 중, 컷씬 등).</summary>
        public void Hide() => _promptRoot?.SetActive(false);

        /// <summary>키 이름 뒤에 오는 설명 텍스트를 변경하고 레이블을 즉시 갱신한다.</summary>
        public void SetActionText(string text)
        {
            _actionText = text;
            RefreshLabel();
        }
    }
}
