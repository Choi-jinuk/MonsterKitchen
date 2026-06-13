using MonsterKitchen.AI;
using MonsterKitchen.AI.Companion;
using MonsterKitchen.Player;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Player
{
    // ====================================================================
    //  BTAction_CompanionEngage — 적 둘러싸며 공격
    //
    //  blackboard: DetectRange(float)
    //  적 없음 → Failure (Follow 로 폴백)
    // ====================================================================
    [BTNode("Companion/Action/Engage")]
    public class BTAction_CompanionEngage : BTNode
    {
        const float RingRadius = 1.2f;

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

            float detect = ctx.Blackboard.Get<float>("DetectRange");
            var   enemy  = s.Pc.FindNearestEnemy(detect);

            Vector2 self     = ctx.Owner.transform.position;
            float   enemyDst = enemy != null ? Vector2.Distance(self, enemy.position) : 0f;

            var state = CompanionStateDecision.Decide(enemy != null, enemyDst, detect);
            if (state != CompanionState.Engage || enemy == null)
                return BTStatus.Failure;   // Follow 로

            var w   = FlockWeights.Default;
            var mgr = FlockManager.Instance;
            int n   = mgr != null ? mgr.QueryNeighbors(self, w.SepRadius, ctx.Owner.transform, s.Buffer) : 0;

            Vector2 vel = enemyDst > RingRadius
                ? FlockSteering.ComputeChase(self, ((Vector2)enemy.position - self).normalized, s.Buffer, n, 1f, w)
                : FlockSteering.ComputeEncircle(self, enemy.position, RingRadius, s.Buffer, n, 1f, w);

            s.Pc.SetBtMoveDir(vel.normalized);
            s.Pc.ExecuteAutoAttack(enemy);   // 공격 타이밍/쿨다운은 내부 처리
            return BTStatus.Running;
        }

        protected override void OnExit(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            s.Pc?.SetBtMoveDir(Vector2.zero);
        }

#if UNITY_EDITOR
        public override string DebugLabel => "⚔ CompanionEngage";
#endif
    }
}
