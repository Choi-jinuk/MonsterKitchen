using MonsterKitchen.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsterKitchen.Restaurant
{
    /// <summary>
    /// 플레이어가 대기 중인 손님에게 음식을 서빙한다.
    /// E 키(또는 게임패드 South 버튼)를 누르면 근처 Waiting 손님에게 서빙.
    /// </summary>
    public class ServingSystem : MonoBehaviour
    {
        [SerializeField] float serveRadius = 1.5f;

        void Update()
        {
            bool interact = false;

            // 키보드 E
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                interact = true;

            // 게임패드 South(Xbox A / PS Cross)
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                interact = true;

            if (interact) TryServe();
        }

        void TryServe()
        {
            // Player 위치 기준으로 탐색 (ServingSystem이 Player와 다른 GO일 때도 동작)
            Vector2 origin = Core.PlayerManager.Instance?.Player != null
                ? (Vector2)Core.PlayerManager.Instance.Player.transform.position
                : (Vector2)transform.position;

            var cols = Physics2D.OverlapCircleAll(origin, serveRadius);
            foreach (var col in cols)
            {
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
