using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Restaurant
{
    /// <summary>
    /// 플레이어가 대기 중인 손님에게 음식을 서빙한다.
    /// Interact 키를 누르면 근처 Waiting 손님에게 서빙.
    /// </summary>
    public class ServingSystem : MonoBehaviour
    {
        static readonly Collider2D[] _overlapBuffer = new Collider2D[16];

        [SerializeField] float serveRadius = 1.5f;

        void OnEnable()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract += TryServe;
        }

        void OnDisable()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract -= TryServe;
        }

        void TryServe()
        {
            Vector2 origin = PlayerManager.Instance?.Player != null
                ? (Vector2)PlayerManager.Instance.Player.transform.position
                : (Vector2)transform.position;

            var serveFilter = ContactFilter2D.noFilter;
            int hitCount = Physics2D.OverlapCircle(origin, serveRadius, serveFilter, _overlapBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                var col = _overlapBuffer[i];
                var customer = col.GetComponent<CustomerAI>();
                if (customer == null || !customer.IsWaiting) continue;

                FoodData food = customer.OrderedFood;
                if (food == null) continue;

                if (FoodInventory.Instance == null || FoodInventory.Instance.GetCount(food.id) <= 0)
                {
                    Debug.Log($"[Serving] {food.displayName} 재고 없음.");
                    return;
                }

                customer.Serve(food, FoodInventory.Instance.TakeGrade(food.id));
                Debug.Log($"[Serving] {food.displayName} 서빙 완료.");
                return;
            }

            Debug.Log("[Serving] 근처에 대기 중인 손님 없음.");
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, serveRadius);
        }
    }
}
