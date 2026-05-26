using MonsterKitchen.Core;
using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Monster
{
    /// <summary>
    /// 스폰 지점 주변 랜덤 순찰 액션. 항상 Running 을 반환한다.
    ///
    /// [SerializeField]
    ///   patrolRadius — 스폰 지점 기준 순찰 반경 (기본 3).
    ///   patrolWait   — 목표 도달 후 대기 시간(초) (기본 1.5).
    /// </summary>
    [BTNode("Monster/Action/Patrol")]
    public class BTAction_Patrol : BTNode
    {
        [SerializeField] float patrolRadius = 3f;
        [SerializeField] float patrolWait   = 1.5f;

        static readonly int HashMoveX = Animator.StringToHash("MoveX");
        static readonly int HashMoveY = Animator.StringToHash("MoveY");
        static readonly int HashSpeed = Animator.StringToHash("Speed");

        class State
        {
            public BTMonsterController ctrl;
            public Animator            anim;
            public bool    initialized;
            public bool    moving;
            public Vector2 target;
            public float   waitTimer;
        }

        protected override BTStatus Execute(BTContext ctx)
        {
            var state = ctx.GetOrCreateState<State>(this);

            if (!state.initialized)
            {
                state.ctrl        = ctx.Owner.GetComponent<BTMonsterController>();
                state.anim        = ctx.Owner.GetComponent<Animator>();
                state.waitTimer   = patrolWait;
                state.initialized = true;
            }

            if (state.moving) TickMove(ctx, state);
            else              TickWait(ctx, state);

            return BTStatus.Running;
        }

        void TickWait(BTContext ctx, State state)
        {
            if (state.ctrl != null) state.ctrl.Rb.linearVelocity = Vector2.zero;
            SetMoveAnim(state, Vector2.zero);

            state.waitTimer -= Time.deltaTime;
            if (state.waitTimer <= 0f)
            {
                PickTarget(ctx, state);
                state.moving = true;
            }
        }

        void TickMove(BTContext ctx, State state)
        {
            Vector2 toTarget = state.target - (Vector2)ctx.Owner.transform.position;
            if (toTarget.magnitude < 0.2f)
            {
                if (state.ctrl != null) state.ctrl.Rb.linearVelocity = Vector2.zero;
                SetMoveAnim(state, Vector2.zero);
                state.waitTimer = patrolWait;
                state.moving    = false;
                return;
            }

            float   moveSpeed = ctx.Blackboard.Get<float>("MoveSpeed");
            Vector2 dir       = toTarget.normalized;
            Vector2 velocity  = (state.ctrl != null ? state.ctrl.ApplySeparation(dir) : dir)
                                * (moveSpeed * 0.6f);

            if (state.ctrl != null) state.ctrl.Rb.linearVelocity = velocity;
            SetMoveAnim(state, velocity);
        }

        void PickTarget(BTContext ctx, State state)
        {
            Vector3 spawnPos = ctx.Blackboard.Get<Vector3>("SpawnPos");
            Vector2 offset   = RandomUtil.InCircle(patrolRadius);
            state.target = (Vector2)spawnPos + offset;
        }

        void SetMoveAnim(State state, Vector2 velocity)
        {
            if (state.anim == null) return;
            state.anim.SetFloat(HashMoveX, velocity.x);
            state.anim.SetFloat(HashMoveY, velocity.y);
            state.anim.SetFloat(HashSpeed,  velocity.magnitude);
        }

        public override void Abort(BTContext ctx)
        {
            var state = ctx.GetOrCreateState<State>(this);
            if (state.ctrl?.Rb != null)
                state.ctrl.Rb.linearVelocity = Vector2.zero;
        }

#if UNITY_EDITOR
        public override string DebugLabel => $"↻ Patrol (r={patrolRadius:F1})";
#endif
    }
}
