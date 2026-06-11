using MonsterKitchen.Core;
using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Monster
{
    // ====================================================================
    //  BTAction_Patrol — 스폰 지점 주변 랜덤 순찰 액션
    //
    //  ▶ 개선 사항
    //    · OnEnter: 컴포넌트 1회 캐시, 상태 초기화
    //    · OnExit:  이동 정지 + 애니메이션 리셋 보장
    //      → BTSelector 가 이 노드를 중단할 때 OnExit 가 자동으로 호출된다.
    //    · ctx.DeltaTime 사용 (이전: Time.deltaTime)
    //
    //  항상 Running 을 반환한다 (Selector 최종 fallback).
    // ====================================================================

    [BTNode("Monster/Action/Patrol")]
    public class BTAction_Patrol : BTNode
    {
        [SerializeField] float m_PatrolRadius = 3f;
        [SerializeField] float m_PatrolWait   = 1.5f;

        static readonly int s_HashMoveX = Animator.StringToHash("MoveX");
        static readonly int s_HashMoveY = Animator.StringToHash("MoveY");
        static readonly int s_HashSpeed = Animator.StringToHash("Speed");

        class State
        {
            public BTMonsterController Ctrl;
            public Animator            Anim;
            public MonsterMovementBase Movement;
            public bool    Initialized;
            public bool    Moving;
            public Vector2 Target;
            public float   WaitTimer;
        }

        // ── 라이프사이클 ─────────────────────────────────────────────────

        protected override void OnEnter(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Initialized) return;

            s.Ctrl        = ctx.Owner.GetComponent<BTMonsterController>();
            s.Anim        = ctx.Owner.GetComponent<Animator>();
            s.Movement    = s.Ctrl != null ? s.Ctrl.Movement : null;
            s.WaitTimer   = m_PatrolWait;
            s.Initialized = true;
        }

        protected override BTStatus Execute(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);

            if (s.Moving) TickMove(ctx, s);
            else          TickWait(ctx, s);

            return BTStatus.Running;
        }

        protected override void OnExit(BTContext ctx)
        {
            StopMovement(ctx.GetOrCreateState<State>(this));
        }

        // ── 대기 / 이동 ──────────────────────────────────────────────────

        void TickWait(BTContext ctx, State s)
        {
            StopMovement(s);

            s.WaitTimer -= ctx.DeltaTime;
            if (s.WaitTimer <= 0f)
            {
                PickTarget(ctx, s);
                s.Moving = true;
            }
        }

        void TickMove(BTContext ctx, State s)
        {
            Vector2 toTarget = s.Target - (Vector2)ctx.Owner.transform.position;
            if (toTarget.magnitude < 0.2f)
            {
                StopMovement(s);
                s.WaitTimer = m_PatrolWait;
                s.Moving    = false;
                return;
            }

            // MonsterMovementBase 위임 (SlimeMovement 등 WindUp/Dash 사이클 포함)
            if (s.Movement != null)
            {
                s.Movement.TickChase(ctx.DeltaTime, (Vector3)s.Target, 0.2f);
                return;
            }

            // 폴백: 직선 이동
            float   moveSpeed = ctx.Blackboard.Get<float>("MoveSpeed");
            Vector2 dir       = toTarget.normalized;
            Vector2 velocity  = (s.Ctrl != null ? s.Ctrl.ApplySeparation(dir) : dir)
                                * (moveSpeed * 0.6f);

            if (s.Ctrl != null) s.Ctrl.Rb.linearVelocity = velocity;
            SetMoveAnim(s, velocity);
        }

        static void StopMovement(State s)
        {
            if (s.Movement != null)
            {
                s.Movement.ResetMovement();
                return;
            }
            if (s.Ctrl?.Rb != null) s.Ctrl.Rb.linearVelocity = Vector2.zero;
            SetMoveAnim(s, Vector2.zero);
        }

        void PickTarget(BTContext ctx, State s)
        {
            Vector2 offset = RandomUtil.InCircle(m_PatrolRadius);
            s.Target = (Vector2)ctx.Owner.transform.position + offset;
        }

        static void SetMoveAnim(State s, Vector2 v)
        {
            if (s.Anim == null) return;
            s.Anim.SetFloat(s_HashMoveX, v.x);
            s.Anim.SetFloat(s_HashMoveY, v.y);
            s.Anim.SetFloat(s_HashSpeed,  v.magnitude);
        }

#if UNITY_EDITOR
        public override string DebugLabel => $"↻ Patrol (r={m_PatrolRadius:F1})";
#endif
    }
}
