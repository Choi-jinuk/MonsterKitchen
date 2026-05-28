using MonsterKitchen.Player;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Player
{
    // ====================================================================
    //  BTAction_PlayerMove — 이동 방향을 계산해 PlayerController 에 전달
    //
    //  ▶ 우선순위
    //    1. 유저 WASD 입력 ("MoveInput" 블랙보드 키)
    //    2. 던전 씬 자동 추적 (가장 가까운 적 방향)
    //    3. 정지
    //
    //  ▶ 실행 조건 (Selector 구조)
    //    공격 범위 내 적이 있으면 BTCondition_PlayerEnemyInAttackRange →
    //    BTAction_PlayerAutoAttack 이 Running 을 점유한다.
    //    이 노드는 그 외의 경우(적이 없거나 범위 밖)에만 실행된다.
    //    → stopDist 처리 불필요. 범위 도달 시 Selector 가 자동으로 AutoAttack 으로 전환.
    //
    //  ▶ 이동 실제 적용은 PlayerController.FixedUpdate 에서 이루어진다.
    //     이 노드는 m_BtMoveDir 만 설정하고 항상 Running 을 반환한다.
    // ====================================================================

    [BTNode("Player/Action/Move")]
    public class BTAction_PlayerMove : BTNode
    {
        class State
        {
            public PlayerController Ctrl;
        }

        protected override void OnEnter(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Ctrl == null) s.Ctrl = ctx.Owner.GetComponent<PlayerController>();
        }

        protected override BTStatus Execute(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Ctrl == null || !s.Ctrl.IsInitialized) return BTStatus.Running;

            if (s.Ctrl.IsDashing) return BTStatus.Running;

            var     input = ctx.Blackboard.Get<Vector2>("MoveInput");
            Vector2 dir   = input;

            // 유저 입력 없고 던전 씬이면 가장 가까운 적을 향해 자동 추적
            // 공격 범위 내 진입 시 Selector 가 AutoAttack 으로 자동 전환하므로
            // 여기서 stopDist 처리는 불필요
            if (dir.sqrMagnitude <= 0.01f && s.Ctrl.IsInDungeon)
            {
                var target = s.Ctrl.FindNearestEnemy(s.Ctrl.AutoChaseRange);
                if (target != null)
                    dir = ((Vector2)(target.position - s.Ctrl.transform.position)).normalized;
            }

            s.Ctrl.SetBtMoveDir(dir);
            return BTStatus.Running;
        }

        protected override void OnExit(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            s.Ctrl?.SetBtMoveDir(Vector2.zero);
        }

#if UNITY_EDITOR
        public override string DebugLabel => "▶ Move";
#endif
    }
}
