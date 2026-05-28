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
        Health _health;

        void Awake()
        {
            _health = GetComponent<Health>();
        }

        void OnEnable()
        {
            if (_health != null) _health.OnDamaged += OnDamaged;
        }

        void OnDisable()
        {
            if (_health != null) _health.OnDamaged -= OnDamaged;
        }

        void OnDamaged(int amount, AttributeType attr)
        {
            if (DamagePopupManager.Instance == null) return;
            DamagePopupManager.Instance.ShowPopup(transform.position, amount, isCrit: false);
        }
    }
}
