using MonsterKitchen.Player;

namespace MonsterKitchen.AI.BehaviorTree.Player
{
    // ====================================================================
    //  BTCondition_PlayerEnemyInAttackRange — 교전 거리 내 적 존재 여부
    //
    //  ▶ PlayerController.GetCurrentEngageRange() 반경 내 적이 있으면 Success.
    //    교전 거리 = 투사체면 SearchRange, 근접이면 AttackRange.
    //    (SearchRange 고정 기준이었을 때 근접 무기가 타격 거리 밖에서
    //     이동을 멈추고 허공 공격하는 교착이 있었다)
    //  ▶ Selector 에서 AutoAttack 을 감싸는 Decorator 로 사용.
    //    · 적 진입 → AutoAttack 실행 (이동 억제 + 공격)
    //    · 적 이탈 → Child.Abort() 자동 호출 → Selector 가 Move 로 전환
    // ====================================================================

    [BTNode("Player/Condition/EnemyInAttackRange")]
    public class BTCondition_PlayerEnemyInAttackRange : BTCondition
    {
        class State { public PlayerController Ctrl; }

        protected override bool Check(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Ctrl == null) s.Ctrl = ctx.Owner.GetComponent<PlayerController>();
            if (s.Ctrl == null || !s.Ctrl.IsInitialized) return false;

            return s.Ctrl.FindNearestEnemy(s.Ctrl.GetCurrentEngageRange()) != null;
        }

#if UNITY_EDITOR
        public override string DebugLabel => "? EnemyInAttackRange";
#endif
    }
}
