using MonsterKitchen.Player;

namespace MonsterKitchen.AI.BehaviorTree.Player
{
    // ====================================================================
    //  BTCondition_PlayerEnemyInAttackRange — 공격 사거리 내 적 존재 여부
    //
    //  ▶ PlayerController.GetCurrentAttackRange() 반경 내 적이 있으면 Success.
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

            return s.Ctrl.FindNearestEnemy(s.Ctrl.GetCurrentAttackRange()) != null;
        }

#if UNITY_EDITOR
        public override string DebugLabel => "? EnemyInAttackRange";
#endif
    }
}
