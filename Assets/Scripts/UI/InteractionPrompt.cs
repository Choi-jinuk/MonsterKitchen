using TMPro; // C-10
using UnityEngine;
using UnityEngine.UI;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  InteractionPrompt — C-10 상호작용 근접 UI
    //
    //  상호작용 가능한 오브젝트에 추가하면, 플레이어가 Trigger에 진입했을 때
    //  오브젝트 위에 "[E] 상호작용" 같은 프롬프트를 월드 스페이스로 표시한다.
    //
    //  ▶ 사용법
    //    - 상호작용 오브젝트에 이 컴포넌트를 추가.
    //    - promptText에 표시할 문자열 입력 (예: "[E] 저녁 영업 시작").
    //    - 해당 오브젝트의 Collider2D가 isTrigger = true 이어야 한다.
    //
    //  ▶ 외부 제어
    //    - Show() / Hide() 로 코드에서 직접 표시/숨김 가능.
    //      (예: CookingStation 요리 중에는 Hide() 호출)
    // ====================================================================

    public class InteractionPrompt : MonoBehaviour
    {
        [SerializeField] string  promptText  = "[E] 상호작용";
        [SerializeField] Vector3 worldOffset = new Vector3(0f, 1.0f, 0f);
        [SerializeField] Color   textColor   = Color.white;

        // 월드 스페이스 Canvas 설정
        const float CanvasScale  = 0.01f;   // 1px = 0.01 유닛
        const float CanvasWidth  = 220f;    // px
        const float CanvasHeight = 44f;     // px
        const float FontSize     = 24f;     // px

        GameObject    _canvasGO;
        bool          _playerInside;

        // ----------------------------------------------------------------

        void Awake()
        {
            BuildPromptCanvas();
            _canvasGO.SetActive(false);
        }

        void BuildPromptCanvas()
        {
            // ── Canvas ──────────────────────────────────────────────────
            _canvasGO = new GameObject("_InteractionPrompt");
            _canvasGO.transform.SetParent(transform);
            _canvasGO.transform.localPosition = worldOffset;
            _canvasGO.transform.localRotation = Quaternion.identity;
            _canvasGO.transform.localScale    = Vector3.one * CanvasScale;

            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;                     // 스프라이트 위에 렌더링

            var rt = _canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

            // ── 배경 패널 (반투명 검정) ─────────────────────────────────
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(_canvasGO.transform, false);

            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

            var img = bgGO.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.55f);

            // ── 텍스트 ─────────────────────────────────────────────────
            var textGO = new GameObject("Label");
            textGO.transform.SetParent(_canvasGO.transform, false);

            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = textRT.offsetMax = Vector2.zero;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text               = promptText;
            tmp.fontSize           = FontSize;
            tmp.color              = textColor;
            tmp.fontStyle          = FontStyles.Bold;
            tmp.alignment          = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
        }

        // ----------------------------------------------------------------
        //  Trigger 감지
        // ----------------------------------------------------------------

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = true;
            _canvasGO?.SetActive(true);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = false;
            _canvasGO?.SetActive(false);
        }

        // ----------------------------------------------------------------
        //  공개 API
        // ----------------------------------------------------------------

        /// <summary>플레이어가 범위 안에 있을 때만 표시한다.</summary>
        public void Show()
        {
            if (_playerInside) _canvasGO?.SetActive(true);
        }

        /// <summary>강제로 숨긴다 (요리 중, 컷씬 등).</summary>
        public void Hide() => _canvasGO?.SetActive(false);

        /// <summary>표시 텍스트를 런타임에 변경한다.</summary>
        public void SetText(string text)
        {
            promptText = text;
            if (_canvasGO == null) return;
            var tmp = _canvasGO.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = text;
        }
    }
}
