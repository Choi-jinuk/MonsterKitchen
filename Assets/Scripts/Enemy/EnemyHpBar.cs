using MonsterKitchen.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterKitchen.Enemy
{
    /// <summary>
    /// 적 머리 위에 표시되는 월드 스페이스 HP 슬라이더.
    ///
    /// Unity Slider 컴포넌트 기반으로 구성된다.
    /// 구조: Canvas → Border → BG → FillArea → Fill  (Handle 없음)
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyHpBar : MonoBehaviour
    {
        [Header("레이아웃")]
        [SerializeField] Vector3 m_BarOffset = new Vector3(0f, 0.7f, 0f);
        [SerializeField] float   m_BarWidth  = 0.8f;
        [SerializeField] float   m_BarHeight = 0.1f;

        [Header("색상")]
        [SerializeField] Color m_ColHigh   = new Color(0.15f, 0.85f, 0.15f, 1f); // 초록
        [SerializeField] Color m_ColMid    = new Color(0.95f, 0.80f, 0.05f, 1f); // 노랑
        [SerializeField] Color m_ColLow    = new Color(0.90f, 0.15f, 0.10f, 1f); // 빨강
        [SerializeField] Color m_ColBg     = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        [SerializeField] Color m_ColBorder = new Color(0f,    0f,    0f,    1f);

        Health m_Health;
        Slider m_Slider;
        Image  m_Fill;

        // ── Unity 생명주기 ────────────────────────────────────────────

        void Awake()
        {
            m_Health = GetComponent<Health>();
            BuildSlider();

            if (m_Health != null)
            {
                m_Health.OnHpChanged += UpdateBar;
                UpdateBar(m_Health.CurrentHp, m_Health.MaxHp);
            }
        }

        void OnDestroy()
        {
            if (m_Health != null)
                m_Health.OnHpChanged -= UpdateBar;
        }

        // ── 슬라이더 빌드 ─────────────────────────────────────────────

        void BuildSlider()
        {
            // ── 루트 캔버스 (월드 스페이스) ──────────────────────────
            var canvasGO = new GameObject("HpBar_Canvas");
            canvasGO.transform.SetParent(transform);
            canvasGO.transform.localPosition = m_BarOffset;
            canvasGO.transform.localRotation = Quaternion.identity;
            canvasGO.transform.localScale    = Vector3.one * 0.01f; // 픽셀→월드 단위 변환

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;

            var canvasRt = canvasGO.GetComponent<RectTransform>();
            // 픽셀 단위로 설계 후 scale 0.01 로 월드 단위 변환
            // barWidth=0.8 → 80픽셀, barHeight=0.1 → 10픽셀
            canvasRt.sizeDelta = new Vector2(m_BarWidth * 100f, m_BarHeight * 100f);

            // ── 외곽선 (Border) ───────────────────────────────────────
            var borderGO  = new GameObject("Border");
            borderGO.transform.SetParent(canvasGO.transform, false);
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.color = m_ColBorder;
            Stretch(borderImg.rectTransform, 0, 0);

            // ── 배경 (BG) ─────────────────────────────────────────────
            var bgGO  = new GameObject("BG");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = m_ColBg;
            Stretch(bgImg.rectTransform, 1, 1); // 1px 안쪽 = 외곽선 효과

            // ── Slider ────────────────────────────────────────────────
            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(canvasGO.transform, false);
            m_Slider           = sliderGO.AddComponent<Slider>();
            m_Slider.minValue  = 0f;
            m_Slider.maxValue  = 1f;
            m_Slider.value     = 1f;
            m_Slider.wholeNumbers = false;
            m_Slider.direction = Slider.Direction.LeftToRight;
            Stretch(sliderGO.GetComponent<RectTransform>(), 2, 2); // 배경보다 1px 더 안쪽

            // Fill Area
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            var fillAreaRt = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = Vector2.zero;
            fillAreaRt.offsetMax = Vector2.zero;

            // Fill
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            m_Fill = fillGO.AddComponent<Image>();
            m_Fill.color = m_ColHigh;
            var fillRt = m_Fill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            // Slider에 fillRect 연결
            m_Slider.fillRect = fillRt;
        }

        static void Stretch(RectTransform rt, float insetH, float insetV)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2( insetH,  insetV);
            rt.offsetMax = new Vector2(-insetH, -insetV);
        }

        // ── 갱신 ─────────────────────────────────────────────────────

        void UpdateBar(int current, int max)
        {
            if (m_Slider == null || m_Fill == null) return;
            float ratio = max > 0 ? (float)current / max : 0f;
            m_Slider.value = ratio;
            m_Fill.color   = ratio > 0.5f ? m_ColHigh
                           : ratio > 0.25f ? m_ColMid
                           : m_ColLow;
        }
    }
}
