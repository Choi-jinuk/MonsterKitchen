using MonsterKitchen.Combat;
using UnityEngine;

namespace MonsterKitchen.UI
{
    /// <summary>
    /// Health 컴포넌트의 OnDamaged 이벤트를 구독하여
    /// DamagePopupManager 에 팝업 요청을 전달하는 브릿지 컴포넌트.
    ///
    /// 사용법: Player 프리팹과 Monster 프리팹에 이 컴포넌트를 추가한다.
    /// 크리티컬 연출은 별도 확률·배율 시스템 추가 후 구현 예정.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DamagePopupTrigger : MonoBehaviour
    {
        Health m_Health;

        void Awake()
        {
            m_Health = GetComponent<Health>();
        }

        void OnEnable()
        {
            if (m_Health != null) m_Health.OnDamaged += OnDamaged;
        }

        void OnDisable()
        {
            if (m_Health != null) m_Health.OnDamaged -= OnDamaged;
        }

        void OnDamaged(int amount, AttributeType attr)
        {
            if (DamagePopupManager.Instance == null) return;
            DamagePopupManager.Instance.ShowPopup(transform.position, amount, isCrit: false);
        }
    }
}
