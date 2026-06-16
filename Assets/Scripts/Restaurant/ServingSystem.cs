using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Restaurant
{
    /// <summary>
    /// 플레이어가 대기 중인 손님에게 음식을 서빙한다.
    /// InteractionHub 후보로 상시 등록 — E키 입력 시 반경 내 가장 가까운
    /// Waiting 손님이 다른 후보(테이블 청소 등)보다 가까우면 서빙이 실행된다.
    /// 음식 소모 요청은 NetworkManager.RequestServeFood() 를 통해 서버에 전달된다.
    /// </summary>
    public class ServingSystem : MonoBehaviour, IInteractable
    {
        static readonly Collider2D[] s_OverlapBuffer = new Collider2D[16];

        [SerializeField] float m_ServeRadius = 1.5f;

        CustomerAI m_NearestWaiting;   // CanInteract 에서 갱신, Interact 에서 사용

        // ── IInteractable ────────────────────────────────────────────

        public bool CanInteract
        {
            get
            {
                m_NearestWaiting = FindNearestWaitingCustomer();
                return m_NearestWaiting != null;
            }
        }

        public Vector3 InteractPosition =>
            m_NearestWaiting != null ? m_NearestWaiting.transform.position : transform.position;

        public void Interact()
        {
            var customer = m_NearestWaiting;
            if (customer == null || !customer.IsWaiting) return;

            FoodData food = customer.OrderedFood;
            if (food == null) return;

            NetworkManager.Instance?.RequestServeFood(food.Id, (success, grade, _) =>
            {
                if (!success)
                {
                    DebugUtil.Log($"[Serving] {food.DisplayName} 재고 없음.");
                    return;
                }
                customer.Serve(food, grade);
                DebugUtil.Log($"[Serving] {food.DisplayName} 서빙 완료.");
            });
        }

        // ── Mono ─────────────────────────────────────────────────────

        void OnEnable()  => InteractionHub.Register(this);
        void OnDisable() => InteractionHub.Unregister(this);

        // ── 내부 ─────────────────────────────────────────────────────

        CustomerAI FindNearestWaitingCustomer()
        {
            Vector2 origin = PlayerManager.Instance?.Player != null
                ? (Vector2)PlayerManager.Instance.Player.transform.position
                : (Vector2)transform.position;

            var serveFilter = ContactFilter2D.noFilter;
            int hitCount = Physics2D.OverlapCircle(origin, m_ServeRadius, serveFilter, s_OverlapBuffer);

            CustomerAI nearest = null;
            float      minDist = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                var customer = s_OverlapBuffer[i].GetComponent<CustomerAI>();
                if (customer == null || !customer.IsWaiting || customer.OrderedFood == null) continue;

                float d = ((Vector2)customer.transform.position - origin).sqrMagnitude;
                if (d < minDist) { minDist = d; nearest = customer; }
            }
            return nearest;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, m_ServeRadius);
        }
    }
}
