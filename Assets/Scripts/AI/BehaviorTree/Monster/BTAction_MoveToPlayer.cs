using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Monster
{
    /// <summary>
    /// 플레이어를 향해 이동하는 액션.
    /// DetectRange 밖이면 Failure. PreferredDistance 이내면 Success.
    /// </summary>
    [BTNode("Monster/Action/MoveToPlayer")]
    public class BTAction_MoveToPlayer : BTNode
    {
        static readonly int HashMoveX = Animator.StringToHash("MoveX");
        static readonly int HashMoveY = Animator.StringToHash("MoveY");
        static readonly int HashSpeed = Animator.StringToHash("Speed");

        class State
        {
            public BTMonsterController ctrl;
            public Animator            anim;
        }

        protected override BTStatus Execute(BTContext ctx)
        {
            var state = ctx.GetOrCreateState<State>(this);
            if (state.ctrl == null) state.ctrl = ctx.Owner.GetComponent<BTMonsterController>();
            if (state.anim == null) state.anim = ctx.Owner.GetComponent<Animator>();

            if (!ctx.Blackboard.TryGet<Transform>("Player", out var player) || player == null)
                return BTStatus.Failure;

            float detect    = ctx.Blackboard.Get<float>("DetectRange");
            float prefDist  = ctx.Blackboard.Get<float>("PreferredDistance");
            float moveSpeed = ctx.Blackboard.Get<float>("MoveSpeed");

            float dist = Vector2.Distance(ctx.Owner.transform.position, player.position);

            if (dist > detect * 1.5f) return BTStatus.Failure;
            if (dist <= prefDist)     return BTStatus.Success;

            Vector2 dir      = ((Vector2)player.position - (Vector2)ctx.Owner.transform.position).normalized;
            Vector2 sepDir   = state.ctrl != null ? state.ctrl.ApplySeparation(dir) : dir;
            Vector2 velocity = sepDir * moveSpeed;

            if (state.ctrl != null) state.ctrl.Rb.linearVelocity = velocity;

            if (state.anim != null)
            {
                state.anim.SetFloat(HashMoveX, velocity.x);
                state.anim.SetFloat(HashMoveY, velocity.y);
                state.anim.SetFloat(HashSpeed,  velocity.magnitude);
            }

            return BTStatus.Running;
        }

        public override void Abort(BTContext ctx)
        {
            var state = ctx.GetOrCreateState<State>(this);
            if (state.ctrl?.Rb != null)
                state.ctrl.Rb.linearVelocity = Vector2.zero;
        }

#if UNITY_EDITOR
        public override string DebugLabel => "→ MoveToPlayer";
#endif
    }
}
