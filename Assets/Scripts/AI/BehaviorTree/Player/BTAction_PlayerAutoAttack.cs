using MonsterKitchen.Player;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Player
{
    // ====================================================================
    //  BTAction_PlayerAutoAttack — 사거리 내 적 공격 (쿨타임 대기 포함)
    //
    //  ▶ BTCondition_PlayerEnemyInAttackRange Decorator 아래에 배치.
    //    Condition 이 공격 범위 진입을 보장하므로 이 노드는
    //    "공격 or 쿨타임 대기" 만 담당한다.
    //
    //  ▶ 이동 억제 정책
    //    · 이 노드가 Running 인 동안 자동 추적(chase)을 비활성화한다.
    //    · SetBtMoveDir(userInput) — 유저 수동 입력만 반영, 몬스터를 밀치지 않음.
    //
    //  ▶ 반환값
    //    · Running : 항상 (Condition 이 범위 이탈 시 Abort 처리)
    //
    //  ▶ 이전 Parallel 방식과의 차이
    //    이전 : Move 와 AutoAttack 이 동시 실행 → 공격 중 chase 지속 → 밀침
    //    현재 : Selector 에서 이 노드가 Running 이면 Move 가 실행되지 않음
    // ====================================================================

    [BTNode("Player/Action/AutoAttack")]
    public class BTAction_PlayerAutoAttack : BTNode
    {
        class State
        {
            public PlayerController Ctrl;
        }

        protected override void OnEnter(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Ctrl == null) s.Ctrl = ctx.Owner.GetComponent<PlayerController>();
            // 공격 브랜치 진입 즉시 자동 추적 억제
            s.Ctrl?.SetBtMoveDir(ctx.Blackboard.Get<Vector2>("MoveInput"));
        }

        protected override BTStatus Execute(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Ctrl == null || !s.Ctrl.IsInitialized) return BTStatus.Running;

            if (s.Ctrl.IsDashing) return BTStatus.Running;

            // 쿨타임 대기 중 — 유저 입력만 반영, 자동 추적 억제
            if (!s.Ctrl.CanAttack)
            {
                s.Ctrl.SetBtMoveDir(ctx.Blackboard.Get<Vector2>("MoveInput"));
                return BTStatus.Running;
            }

            // 공격 실행
            float  searchRange = s.Ctrl.GetCurrentSearchRange();
            var    target      = s.Ctrl.FindNearestEnemy(searchRange);

            if (target != null)
                s.Ctrl.ExecuteAutoAttack(target);
            else
                s.Ctrl.DoHarvestNode();

            // 공격 직후에도 자동 추적 억제 유지 (쿨타임 동안 이동 방지)
            s.Ctrl.SetBtMoveDir(ctx.Blackboard.Get<Vector2>("MoveInput"));

            return BTStatus.Running;
        }

#if UNITY_EDITOR
        public override string DebugLabel => "⚔ AutoAttack";
#endif
    }
}
