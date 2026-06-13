using MonsterKitchen.AI;
using MonsterKitchen.Player;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Player
{
    // ====================================================================
    //  BTAction_CompanionFollow — 리더 추종 (flock chase + separation)
    //
    //  blackboard: Leader(Transform), FollowDistance(float)
    // ====================================================================
    [BTNode("Companion/Action/Follow")]
    public class BTAction_CompanionFollow : BTNode
    {
        class State
        {
            public PlayerController Pc;
            public Vector2[]        Buffer;
        }

        protected override void OnEnter(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Pc == null)     s.Pc     = ctx.Owner.GetComponent<PlayerController>();
            if (s.Buffer == null) s.Buffer = new Vector2[16];
        }

        protected override BTStatus Execute(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Pc == null) return BTStatus.Failure;

            if (!ctx.Blackboard.TryGet<Transform>("Leader", out var leader) || leader == null)
                return BTStatus.Failure;

            float   follow   = ctx.Blackboard.Get<float>("FollowDistance");
            Vector2 self     = ctx.Owner.transform.position;
            Vector2 toLeader = (Vector2)leader.position - self;
            float   dist     = toLeader.magnitude;

            if (dist <= follow)
            {
                s.Pc.SetBtMoveDir(Vector2.zero);
                return BTStatus.Success;
            }

            var w   = FlockWeights.Default;
            var mgr = FlockManager.Instance;
            int n   = mgr != null ? mgr.QueryNeighbors(self, w.SepRadius, ctx.Owner.transform, s.Buffer) : 0;

            Vector2 vel = FlockSteering.ComputeChase(self, toLeader.normalized, s.Buffer, n, 1f, w);
            s.Pc.SetBtMoveDir(vel.normalized);   // PlayerController 가 실제 속도/물리 적용
            return BTStatus.Running;
        }

        protected override void OnExit(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            s.Pc?.SetBtMoveDir(Vector2.zero);
        }

#if UNITY_EDITOR
        public override string DebugLabel => "→ CompanionFollow";
#endif
    }
}
