// ====================================================================
//  VFXTrigger — Health 이벤트 → VFXManager 브릿지
//
//  ▶ Health.OnDamaged → VFXManager.PlayHit(position)
//  ▶ Health.OnDeath   → VFXManager.PlayDeath(position)
//  ▶ DamagePopupTrigger 와 같은 방식으로 Player/Monster 프리팹에 추가
// ====================================================================

using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    [RequireComponent(typeof(Health))]
    public class VFXTrigger : MonoBehaviour
    {
        Health m_Health;

        void Awake()
        {
            m_Health = GetComponent<Health>();
        }

        void OnEnable()
        {
            if (m_Health == null) return;
            m_Health.OnDamaged += OnDamaged;
            m_Health.OnDeath   += OnDied;
        }

        void OnDisable()
        {
            if (m_Health == null) return;
            m_Health.OnDamaged -= OnDamaged;
            m_Health.OnDeath   -= OnDied;
        }

        // ── handlers ──────────────────────────────────────────────────

        void OnDamaged(int amount, AttributeType attr)
        {
            VFXManager.Instance?.PlayHit(transform.position);
        }

        void OnDied(AttributeType attr)
        {
            VFXManager.Instance?.PlayDeath(transform.position);
        }
    }
}
