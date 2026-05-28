using MonsterKitchen.Player;

namespace MonsterKitchen.AI.BehaviorTree.Player
{
    // ====================================================================
    //  BTAction_PlayerDash — 대시 입력 플래그를 소비해 대시 실행
    //
    //  ▶ 블랙보드 "DashRequested"(bool) = true 이고 쿨타임이 끝났으면
    //    PlayerController.StartDash() 를 호출한다.
    //  ▶ 대시 중이면 Running 반환.
    //  ▶ 대기 중이면 Success 반환 (다른 노드를 막지 않음).
    // ====================================================================

    [BTNode("Player/Action/Dash")]
    public class BTAction_PlayerDash : BTNode
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
            if (s.Ctrl == null || !s.Ctrl.IsInitialized) return BTStatus.Success;

            // 대시 진행 중
            if (s.Ctrl.IsDashing) return BTStatus.Running;

            // 입력 플래그 체크
            bool requested = ctx.Blackboard.Get<bool>("DashRequested");
            if (requested && s.Ctrl.DashCooledDown)
            {
                ctx.Blackboard.Set("DashRequested", false);
                s.Ctrl.StartDash();
                return BTStatus.Running;
            }

            // 대시 요청 없음 → 이 브랜치 건너뜀 (Selector 다음 자식으로)
            return BTStatus.Failure;
        }

#if UNITY_EDITOR
        public override string DebugLabel => "⚡ Dash";
#endif
    }
}
