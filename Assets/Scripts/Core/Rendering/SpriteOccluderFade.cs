using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  SpriteOccluderFade — 플레이어를 가리는 prop 반투명 처리
    //
    //  ▶ 부착 대상: 나무·동상 등 지형 prop, 채집 노드.
    //  ▶ 판정: 플레이어가 스프라이트 bounds 안 + 플레이어 Y > prop Y (= 뒤).
    //  ▶ m_FadedAlpha 프리셋: 채집 노드 0.55 (채집 대상 시인성) / 데코 지형 0.3.
    //  ▶ 틴트 RGB 유지, 알파만 보간 — ResourceNode m_NodeTint 와 공존.
    // ====================================================================

    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteOccluderFade : MonoBehaviour
    {
        [Header("페이드 설정")]
        [SerializeField, Range(0.05f, 0.95f)] float m_FadedAlpha = 0.3f;
        [SerializeField]                      float m_FadeTime   = 0.15f;

        SpriteRenderer m_Sprite;
        Transform      m_Player;
        float          m_CurrentAlpha = 1f;

        /// <summary>플레이어가 prop 에 가려졌는가 — bounds 안 + prop 기준 뒤(Y 큼).</summary>
        public static bool ShouldFade(Bounds propBounds, float propY, Vector2 playerPos)
        {
            if (playerPos.x < propBounds.min.x || playerPos.x > propBounds.max.x) return false;
            if (playerPos.y < propBounds.min.y || playerPos.y > propBounds.max.y) return false;
            return playerPos.y > propY;
        }

        void Awake() => m_Sprite = GetComponent<SpriteRenderer>();

        void LateUpdate()
        {
            if (m_Player == null)
            {
                m_Player = PlayerManager.Instance?.Player != null
                    ? PlayerManager.Instance.Player.transform : null;
                if (m_Player == null) return;
            }

            bool  fade   = ShouldFade(m_Sprite.bounds, transform.position.y, m_Player.position);
            float target = fade ? m_FadedAlpha : 1f;

            if (Mathf.Approximately(m_CurrentAlpha, target)) return;

            float step     = m_FadeTime > 0f ? Time.deltaTime / m_FadeTime : 1f;
            m_CurrentAlpha = Mathf.MoveTowards(m_CurrentAlpha, target, step);

            var c = m_Sprite.color;
            c.a = m_CurrentAlpha;
            m_Sprite.color = c;
        }
    }
}
