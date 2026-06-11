// ====================================================================
//  LoadingSceneController — 로딩 화면 진행률 + 팁 텍스트 제어
//
//  ▶ SceneLoader.OnProgressChanged 구독 → progress-bar-fill width 갱신
//  ▶ 씬 진입 시 랜덤 팁 1개 선택 → tip-label 표시
//  ▶ Loading.uxml 에 UIDocument 로 연결 (LoadingScene 직접 배치)
// ====================================================================

using MonsterKitchen.Core;
using MonsterKitchen.UI.Mobile;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class LoadingSceneController : MonoBehaviour
    {
        static readonly string[] s_Tips =
        {
            "팁: 재료 품질이 높을수록 음식 등급도 높아집니다.",
            "팁: 손님이 오기 전에 메뉴를 미리 등록해두세요.",
            "팁: 테이블이 더러우면 손님 만족도가 낮아집니다.",
            "팁: 같은 레시피를 반복하면 조리 속도가 빨라집니다.",
            "팁: CC 처치로 잡은 재료는 품질이 더 높습니다.",
            "팁: 가방 업그레이드로 던전에서 더 많은 재료를 담을 수 있습니다.",
            "팁: 명성이 쌓이면 하루에 더 많은 손님이 찾아옵니다.",
            "팁: 대시 중에는 무적 프레임이 적용됩니다.",
            "팁: 속성 무기로 약점을 공략하면 재료 품질이 올라갑니다.",
            "팁: 궁극기 게이지는 피격 및 처치 시 쌓입니다.",
        };

        VisualElement m_Fill;
        Label         m_TipLabel;

        void Awake()
        {
            MobileHUD.Instance?.Hide();

            var root = GetComponent<UIDocument>()?.rootVisualElement;
            if (root == null) return;

            m_Fill     = root.Q<VisualElement>("progress-bar-fill");
            m_TipLabel = root.Q<Label>("tip-label");

            // 랜덤 팁 표시
            if (m_TipLabel != null)
                m_TipLabel.text = s_Tips[Random.Range(0, s_Tips.Length)];

            // 초기 진행률 설정 (이미 진행 중일 수 있음)
            SetProgress(SceneLoader.Instance?.LoadingProgress ?? 0f);
        }

        void OnEnable()
        {
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.OnProgressChanged += SetProgress;
        }

        void OnDisable()
        {
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.OnProgressChanged -= SetProgress;
        }

        void SetProgress(float value)
        {
            if (m_Fill == null) return;
            m_Fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
        }
    }
}
