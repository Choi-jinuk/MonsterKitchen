using System;
using System.Collections;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Restaurant
{
    /// <summary>
    /// 손님 FSM: Entering → Seated → Waiting → Served → Leaving
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    public class CustomerAI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] float moveSpeed    = 2.5f;
        [SerializeField] float patience     = 20f;    // 음식 대기 허용 시간(초)
        [SerializeField] float eatDuration  = 3f;     // 식사 시간

        [Header("Order")]
        [SerializeField] uint[] possibleOrderIds;   // 주문 가능 음식 ID 목록 (FoodData.id)

        public FoodData    OrderedFood  { get; private set; }
        public bool        IsWaiting    => _state == State.Waiting;

        public event Action OnGuestFinished;

        enum State { Entering, Seated, Waiting, Served, Leaving, Done }

        State            _state = State.Entering;
        RestaurantTable  _table;
        Rigidbody2D      _rb;
        Animator         _anim;
        float            _patienceTimer;
        Vector3          _exitPoint;

        static readonly int HashSpeed  = Animator.StringToHash("Speed");
        static readonly int HashEat    = Animator.StringToHash("Eat");
        static readonly int HashLeave  = Animator.StringToHash("Leave");

        public void Init(RestaurantTable table)
        {
            _table = table;
            _table.Occupy(this);
            _exitPoint = transform.position;   // 스폰 위치 = 퇴장 위치
        }

        void Awake()
        {
            _rb   = GetComponent<Rigidbody2D>();
            _anim = GetComponent<Animator>();
        }

        void Update()
        {
            switch (_state)
            {
                case State.Entering: TickEntering(); break;
                case State.Waiting:  TickWaiting();  break;
            }
        }

        // ── Entering ──────────────────────────────────────────────
        void TickEntering()
        {
            Vector2 target = _table.SeatPoint.position;
            Vector2 dir    = (target - (Vector2)transform.position);

            if (dir.magnitude < 0.1f)
            {
                _rb.linearVelocity = Vector2.zero;
                transform.position = target;
                EnterSeated();
                return;
            }

            _rb.linearVelocity = dir.normalized * moveSpeed;
            _anim.SetFloat(HashSpeed, moveSpeed);
        }

        void EnterSeated()
        {
            _state = State.Seated;
            _anim.SetFloat(HashSpeed, 0f);
            StartCoroutine(SitAndOrder());
        }

        IEnumerator SitAndOrder()
        {
            yield return new WaitForSeconds(0.5f);

            // 주문할 음식 결정 — FoodInventory 에 있는 것 우선
            OrderedFood = PickOrder();
            if (OrderedFood == null)
            {
                Debug.Log("[Customer] 메뉴 없음 — 자리 이탈");
                StartCoroutine(LeaveRoutine(paid: false));
                yield break;
            }

            Debug.Log($"[Customer] 주문: {OrderedFood.displayName}");
            _patienceTimer = patience;
            _state         = State.Waiting;
        }

        FoodData PickOrder()
        {
            if (possibleOrderIds == null || possibleOrderIds.Length == 0) return null;

            var fi       = FoodInventory.Instance;
            var registry = DataRegistry.Instance;
            foreach (var foodId in possibleOrderIds)
            {
                if (foodId == 0u) continue;
                if (fi != null && fi.GetCount(foodId) > 0)
                    return registry?.GetFood(foodId);
            }
            // FoodInventory에 없어도 첫 번째 메뉴로 주문 (MVP 폴백)
            return registry?.GetFood(possibleOrderIds[0]);
        }

        // ── Waiting ───────────────────────────────────────────────
        void TickWaiting()
        {
            _patienceTimer -= Time.deltaTime;
            if (_patienceTimer <= 0f)
            {
                Debug.Log("[Customer] 인내심 소진 — 퇴장");
                StartCoroutine(LeaveRoutine(paid: false));
            }
        }

        // ── Served (외부 호출) ────────────────────────────────────
        /// <summary>ServingSystem이 등급과 함께 호출한다.</summary>
        public void Serve(FoodData food, FoodGrade grade)
        {
            if (_state != State.Waiting) return;
            if (food.id != OrderedFood.id)
            {
                Debug.Log($"[Customer] 잘못된 음식: {food.displayName}");
                return;
            }

            _state = State.Served;
            FoodInventory.Instance?.Remove(food.id);
            StartCoroutine(EatAndPay(food, grade));
        }

        IEnumerator EatAndPay(FoodData food, FoodGrade grade)
        {
            _anim.SetTrigger(HashEat);
            yield return new WaitForSeconds(eatDuration);

            // 등급 배율 적용
            float gradeMult = grade switch
            {
                FoodGrade.Good      => food.goodMultiplier,
                FoodGrade.Perfect   => food.perfectMultiplier,
                FoodGrade.Legendary => food.legendaryMultiplier,
                _                   => 1f,
            };

            float shopMult = ShopManager.Instance != null ? ShopManager.Instance.TipMultiplier : 1f;
            int   pay      = Mathf.RoundToInt(food.basePrice * gradeMult * shopMult);
            GoldManager.Instance?.Earn(pay);
            Debug.Log($"[Customer] {food.displayName} [{grade}] 식사 완료. 지불: {pay}G " +
                      $"(base {food.basePrice} × grade {gradeMult:F2} × shop {shopMult:F2})");

            StartCoroutine(LeaveRoutine(paid: true));
        }

        IEnumerator LeaveRoutine(bool paid)
        {
            _state = State.Leaving;
            _table.Vacate();
            _anim.SetTrigger(HashLeave);

            Vector2 exit = _exitPoint;
            while (Vector2.Distance(transform.position, exit) > 0.15f)
            {
                Vector2 dir = (exit - (Vector2)transform.position).normalized;
                _rb.linearVelocity = dir * moveSpeed;
                _anim.SetFloat(HashSpeed, moveSpeed);
                yield return null;
            }

            _state = State.Done;
            OnGuestFinished?.Invoke();
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            if (_table == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, _table.SeatPoint.position);
        }
    }
}
