using System;
using System.Collections;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.UI;
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
        [SerializeField] float m_MoveSpeed   = 2.5f;
        [SerializeField] float m_Patience    = 20f;
        [SerializeField] float m_EatDuration = 3f;

        [Header("Order")]
        [SerializeField] uint[] m_PossibleOrderIds;

        public FoodData OrderedFood  { get; private set; }
        public bool     IsWaiting    => m_State == State.Waiting;
        public int      Satisfaction => m_Satisfaction;

        public event Action OnGuestFinished;

        enum State { Entering, Seated, Waiting, Served, Leaving, Done }

        State           m_State = State.Entering;
        RestaurantTable m_Table;
        Rigidbody2D     m_Rb;
        Animator        m_Anim;
        float           m_PatienceTimer;
        float           m_PatienceDecayMult = 1.0f;
        int             m_Satisfaction      = 50;
        Vector3         m_ExitPoint;

        static readonly int s_HashSpeed = Animator.StringToHash("Speed");
        static readonly int s_HashEat   = Animator.StringToHash("Eat");
        static readonly int s_HashLeave = Animator.StringToHash("Leave");

        public void Init(RestaurantTable table)
        {
            m_Table    = table;
            m_Table.Occupy(this);
            m_ExitPoint = transform.position;
        }

        void Awake()
        {
            m_Rb   = GetComponent<Rigidbody2D>();
            m_Anim = GetComponent<Animator>();
        }

        void Update()
        {
            switch (m_State)
            {
                case State.Entering: TickEntering(); break;
                case State.Waiting:  TickWaiting();  break;
            }
        }

        // ── Entering ──────────────────────────────────────────────

        void TickEntering()
        {
            Vector2 target = m_Table.SeatPoint.position;
            Vector2 dir    = (target - (Vector2)transform.position);

            if (dir.magnitude < 0.1f)
            {
                m_Rb.linearVelocity = Vector2.zero;
                transform.position  = target;
                EnterSeated();
                return;
            }

            m_Rb.linearVelocity = dir.normalized * m_MoveSpeed;
            m_Anim.SetFloat(s_HashSpeed, m_MoveSpeed);
        }

        void EnterSeated()
        {
            m_State = State.Seated;
            m_Anim.SetFloat(s_HashSpeed, 0f);

            // 더러운 테이블 착석 페널티
            if (m_Table != null && m_Table.IsDirty)
            {
                AddSatisfaction(-5);
                m_PatienceDecayMult = 1.5f;
                DebugUtil.Log("[Customer] 더러운 테이블 착석 — 만족도 -5, 인내심 감소 1.5×");
            }

            StartCoroutine(SitAndOrder());
        }

        IEnumerator SitAndOrder()
        {
            yield return new WaitForSeconds(0.5f);

            OrderedFood = PickOrder();
            if (OrderedFood == null)
            {
                DebugUtil.Log("[Customer] 메뉴 없음 — 자리 이탈");
                StartCoroutine(LeaveRoutine(paid: false));
                yield break;
            }

            DebugUtil.Log($"[Customer] 주문: {OrderedFood.DisplayName}");
            m_PatienceTimer = m_Patience;
            m_State         = State.Waiting;
        }

        FoodData PickOrder()
        {
            if (m_PossibleOrderIds == null || m_PossibleOrderIds.Length == 0) return null;

            var inv      = PlayerDataManager.Instance?.Inventory;
            var menu     = PlayerDataManager.Instance?.DailyMenu;
            var registry = DataRegistry.Instance;

            foreach (var foodId in m_PossibleOrderIds)
            {
                if (foodId == 0u) continue;
                bool onMenu = menu == null || !menu.HasAnyItem() || menu.IsOnMenu(foodId);
                if (onMenu && inv != null && inv.GetFoodCount(foodId) > 0)
                    return registry?.Foods?.Get(foodId);
            }
            return registry?.Foods?.Get(m_PossibleOrderIds[0]);
        }

        // ── Waiting ───────────────────────────────────────────────

        void TickWaiting()
        {
            m_PatienceTimer -= Time.deltaTime * m_PatienceDecayMult;
            if (m_PatienceTimer <= 0f)
            {
                DebugUtil.Log("[Customer] 인내심 소진 — 퇴장");
                StartCoroutine(LeaveRoutine(paid: false));
            }
        }

        // ── Served (외부 호출) ────────────────────────────────────

        /// <summary>ServingSystem이 등급과 함께 호출한다.</summary>
        public void Serve(FoodData food, FoodGrade grade)
        {
            if (food == null)
            {
                Debug.LogError("[CustomerAI] Serve() — food is null", this);
                return;
            }
            if (OrderedFood == null)
            {
                Debug.LogError("[CustomerAI] Serve() — OrderedFood is null", this);
                return;
            }
            if (m_State != State.Waiting) return;
            if (food.Id != OrderedFood.Id)
            {
                DebugUtil.Log($"[Customer] 잘못된 음식: {food.DisplayName}");
                return;
            }

            // 서빙 타이밍 보너스/패널티
            float patienceRatio = m_PatienceTimer / m_Patience;
            if (patienceRatio >= 0.5f)
                AddSatisfaction(+10);
            else
                AddSatisfaction(-10);

            // 음식 등급 보너스
            switch (grade)
            {
                case FoodGrade.Perfect:   AddSatisfaction(+10); break;
                case FoodGrade.Legendary: AddSatisfaction(+15); break;
            }

            // 정확한 음식 서빙 기본 보너스
            AddSatisfaction(+20);

            m_State = State.Served;
            StartCoroutine(EatAndPay(food, grade));
        }

        IEnumerator EatAndPay(FoodData food, FoodGrade grade)
        {
            m_Anim.SetTrigger(s_HashEat);
            yield return new WaitForSeconds(m_EatDuration);

            float gradeMult = grade switch
            {
                FoodGrade.Good      => food.GoodMultiplier,
                FoodGrade.Perfect   => food.PerfectMultiplier,
                FoodGrade.Legendary => food.LegendaryMultiplier,
                _                   => 1f,
            };

            float shopMult    = PlayerDataManager.Instance?.Upgrades.ShopTipMultiplier ?? 1f;
            var   recipe      = DataRegistry.Instance?.GetRecipeByFoodId(food.Id);
            float masteryMult = recipe != null
                ? (PlayerDataManager.Instance?.Mastery?.GetPriceMultiplier(recipe.Id) ?? 1f)
                : 1f;
            int pay = Mathf.RoundToInt(food.BasePrice * gradeMult * shopMult * masteryMult);
            NetworkManager.Instance?.RequestEarnGold(pay);

            // 만족도 기반 팁
            int tip = CalculateTip(pay);
            if (tip > 0)
            {
                NetworkManager.Instance?.RequestEarnGold(tip);
                if (m_Satisfaction >= 100)
                    GameHUD.Instance?.ShowNotification("완벽한 서비스!", 2f);
            }

            DebugUtil.Log($"[Customer] {food.DisplayName} [{grade}] 식사 완료. 지불:{pay}G 팁:{tip}G 만족도:{m_Satisfaction}");

            DayManager.Instance?.RecordServing(pay, tip, m_Satisfaction);

            StartCoroutine(LeaveRoutine(paid: true));
        }

        int CalculateTip(int basePayment)
        {
            if (m_Satisfaction < 70)  return 0;
            if (m_Satisfaction < 85)  return Mathf.RoundToInt(basePayment * 0.10f);
            return Mathf.RoundToInt(basePayment * 0.20f);
        }

        void AddSatisfaction(int delta)
            => m_Satisfaction = Mathf.Clamp(m_Satisfaction + delta, 0, 100);

        IEnumerator LeaveRoutine(bool paid)
        {
            if (!paid)
            {
                AddSatisfaction(-30);
                DayManager.Instance?.RecordServing(0, 0, m_Satisfaction);
                DebugUtil.Log($"[Customer] 인내심 만료 퇴장. 만족도:{m_Satisfaction}");
            }

            m_State = State.Leaving;
            m_Table.Vacate();
            m_Table.SetDirty(true);
            m_Anim.SetTrigger(s_HashLeave);

            Vector2 exit = m_ExitPoint;
            while (Vector2.Distance(transform.position, exit) > 0.15f)
            {
                Vector2 dir = (exit - (Vector2)transform.position).normalized;
                m_Rb.linearVelocity = dir * m_MoveSpeed;
                m_Anim.SetFloat(s_HashSpeed, m_MoveSpeed);
                yield return null;
            }

            m_State = State.Done;
            OnGuestFinished?.Invoke();
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            if (m_Table == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, m_Table.SeatPoint.position);
        }
    }
}
