using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Monster
{
    // ====================================================================
    //  BTAction_MoveToPlayer — 플레이어를 향해 이동하는 액션
    //
    //  ▶ MonsterMovementBase 컴포넌트(예: SlimeMovement)가 있으면
    //    TickChase() 에 이동을 위임한다 (WindUp → Dash 사이클 등).
    //    없으면 단순 직선 이동 폴백.
    //
    //  DetectRange 밖이면 Failure. PreferredDistance 이내이면 Success.
    // ====================================================================

    [BTNode("Monster/Action/MoveToPlayer")]
    public class BTAction_MoveToPlayer : BTNode
    {
        static readonly int s_HashMoveX = Animator.StringToHash("MoveX");
        static readonly int s_HashMoveY = Animator.StringToHash("MoveY");
        static readonly int s_HashSpeed = Animator.StringToHash("Speed");

        class State
        {
            public BTMonsterController Ctrl;
            public Animator            Anim;
            public MonsterMovementBase Movement;
        }

        // ── 라이프사이클 ─────────────────────────────────────────────────

        protected override void OnEnter(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Ctrl     == null) s.Ctrl     = ctx.Owner.GetComponent<BTMonsterController>();
            if (s.Anim     == null) s.Anim     = ctx.Owner.GetComponent<Animator>();
            if (s.Movement == null) s.Movement = s.Ctrl != null ? s.Ctrl.Movement : null;
        }

        protected override BTStatus Execute(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);

            if (!ctx.Blackboard.TryGet<Transform>("Player", out var player) || player == null)
                return BTStatus.Failure;

            float detect   = ctx.Blackboard.Get<float>("DetectRange");
            float prefDist = ctx.Blackboard.Get<float>("PreferredDistance");

            float dist = Vector2.Distance(ctx.Owner.transform.position, player.position);

            if (dist > detect * 1.5f) return BTStatus.Failure;
            if (dist <= prefDist)
            {
                StopMovement(s);
                return BTStatus.Success;
            }

            // ── 이동 위임: MonsterMovementBase(SlimeMovement 등) ──────────
            if (s.Movement != null)
            {
                s.Movement.TickChase(ctx.DeltaTime, player.position, prefDist);
                return BTStatus.Running;
            }

            // ── 폴백: 직선 이동 ───────────────────────────────────────────
            float   moveSpeed = ctx.Blackboard.Get<float>("MoveSpeed");
            Vector2 dir       = ((Vector2)player.position - (Vector2)ctx.Owner.transform.position).normalized;
            Vector2 sepDir    = s.Ctrl != null ? s.Ctrl.ApplySeparation(dir) : dir;
            Vector2 velocity  = sepDir * moveSpeed;

            if (s.Ctrl != null) s.Ctrl.Rb.linearVelocity = velocity;
            SetMoveAnim(s, velocity);

            return BTStatus.Running;
        }

        protected override void OnExit(BTContext ctx)
        {
            StopMovement(ctx.GetOrCreateState<State>(this));
        }

        // ── Helper ───────────────────────────────────────────────────────

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

        static void SetMoveAnim(State s, Vector2 v)
        {
            if (s.Anim == null) return;
            s.Anim.SetFloat(s_HashMoveX, v.x);
            s.Anim.SetFloat(s_HashMoveY, v.y);
            s.Anim.SetFloat(s_HashSpeed,  v.magnitude);
        }

#if UNITY_EDITOR
        public override string DebugLabel => "→ MoveToPlayer";
#endif
    }
}
