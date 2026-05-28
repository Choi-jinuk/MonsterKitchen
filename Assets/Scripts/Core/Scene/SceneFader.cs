using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 화면 전체를 검은색으로 페이드 인/아웃하는 유틸리티 싱글톤.
    ///
    /// DontDestroyOnLoad — 씬 전환 후에도 유지.
    /// DungeonSceneController 등에서 FadeOut / FadeIn 코루틴을 yield 로 기다린다.
    ///
    /// 의존성: UnityEngine.UI (com.unity.ugui — Unity 엔진에 내장).
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneFader : MonoBehaviour
    {
        public static SceneFader Instance { get; private set; }

        Canvas m_Canvas;
        Image  m_Panel;
        bool   m_Initialized;

        // ================================================================
        //  Mono
        // ================================================================

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Build();
        }

        // ================================================================
        //  내부 UI 생성
        // ================================================================

        void Build()
        {
            if (m_Initialized) return;
            m_Initialized = true;

            // Canvas
            m_Canvas                = gameObject.AddComponent<Canvas>();
            m_Canvas.renderMode     = RenderMode.ScreenSpaceOverlay;
            m_Canvas.sortingOrder   = 9999;

            var scaler                    = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode            = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution    = new Vector2(1920, 1080);
            scaler.screenMatchMode        = CanvasScaler.ScreenMatchMode.Expand;

            // Panel
            var panelGO = new GameObject("FadePanel", typeof(Image));
            panelGO.transform.SetParent(transform, false);

            m_Panel = panelGO.GetComponent<Image>();
            m_Panel.color = new Color(0f, 0f, 0f, 0f);

            var rt = m_Panel.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            m_Panel.raycastTarget = true;  // 페이드 중 클릭 차단
            m_Panel.gameObject.SetActive(false);
        }

        // ================================================================
        //  공개 API
        // ================================================================

        /// <summary>화면을 검은색으로 점점 어둡게 한다. yield return 으로 완료 대기.</summary>
        public IEnumerator FadeOut(float duration = 0.4f)
        {
            m_Panel.gameObject.SetActive(true);
            m_Panel.color = new Color(0f, 0f, 0f, 0f);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                m_Panel.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / duration));
                yield return null;
            }
            m_Panel.color = new Color(0f, 0f, 0f, 1f);
        }

        /// <summary>검은 화면에서 점점 밝아진다. yield return 으로 완료 대기.</summary>
        public IEnumerator FadeIn(float duration = 0.4f)
        {
            m_Panel.gameObject.SetActive(true);
            m_Panel.color = new Color(0f, 0f, 0f, 1f);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                m_Panel.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(t / duration));
                yield return null;
            }
            m_Panel.color = new Color(0f, 0f, 0f, 0f);
            m_Panel.gameObject.SetActive(false);
        }

        /// <summary>즉시 검은 화면으로 만든다 (씬 로드 직후 호출용).</summary>
        public void SetBlack()
        {
            m_Panel.gameObject.SetActive(true);
            m_Panel.color = new Color(0f, 0f, 0f, 1f);
        }

        /// <summary>즉시 투명하게 만든다.</summary>
        public void SetClear()
        {
            m_Panel.color = new Color(0f, 0f, 0f, 0f);
            m_Panel.gameObject.SetActive(false);
        }
    }
}
