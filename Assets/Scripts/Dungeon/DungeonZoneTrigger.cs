using System.Collections;
using MonsterKitchen.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonZoneTrigger — 구역 진입 피드백 (토스트 + 조명 보간)
    //
    //  ▶ 구역당 1개, 트리거 BoxCollider2D 로 영역 정의.
    //  ▶ 플레이어 진입: 구역 이름 토스트 + Global Light 2D intensity 보간.
    //  ▶ 같은 구역 연속 재진입은 무시 (s_Current 캐시).
    // ====================================================================

    [RequireComponent(typeof(BoxCollider2D))]
    public class DungeonZoneTrigger : MonoBehaviour
    {
        [Header("구역 설정")]
        [SerializeField] string m_ZoneName = "구역";
        [SerializeField, Range(0.2f, 1.5f)] float m_LightIntensity = 1f;

        [Header("조명 — Inspector 직접 연결")]
        [SerializeField] Light2D m_GlobalLight;

        const float LerpDuration = 0.5f;

        static DungeonZoneTrigger s_Current;
        Coroutine m_LightRoutine;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (s_Current == this) return;
            s_Current = this;

            GameHUD.Instance?.ShowNotification(m_ZoneName, 2f);

            if (m_GlobalLight != null)
            {
                if (m_LightRoutine != null) StopCoroutine(m_LightRoutine);
                m_LightRoutine = StartCoroutine(LerpLight(m_LightIntensity));
            }
        }

        IEnumerator LerpLight(float target)
        {
            float start = m_GlobalLight.intensity;
            float t = 0f;
            while (t < LerpDuration)
            {
                t += Time.deltaTime;
                m_GlobalLight.intensity = Mathf.Lerp(start, target, t / LerpDuration);
                yield return null;
            }
            m_GlobalLight.intensity = target;
        }

        void OnDestroy()
        {
            if (s_Current == this) s_Current = null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col != null && !col.isTrigger)
                Core.DebugUtil.LogWarning("[DungeonZoneTrigger] BoxCollider2D 는 isTrigger 여야 합니다.", this);
        }
#endif
    }
}
