using MonsterKitchen.Player;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Player
{
    // ====================================================================
    //  BTAction_PlayerSkill — 스킬 슬롯 입력 플래그를 소비해 스킬 실행
    //
    //  ▶ m_Slot: 1 = 스킬1, 2 = 스킬2, 3 = 궁극기
    //  ▶ 블랙보드 키: "Skill1Requested" / "Skill2Requested" / "UltimateRequested"
    //  ▶ 대시 중이거나 플래그가 없으면 즉시 Success 반환 (다른 노드를 막지 않음).
    // ====================================================================

    [BTNode("Player/Action/Skill")]
    public class BTAction_PlayerSkill : BTNode
    {
        [SerializeField] int m_Slot = 1;  // 1·2·3(궁극기)

        static readonly string[] s_Keys = { "", "Skill1Requested", "Skill2Requested", "UltimateRequested" };

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
            if (s.Ctrl.IsDashing)                        return BTStatus.Success;

            int  slot    = Mathf.Clamp(m_Slot, 1, 3);
            string key   = s_Keys[slot];
            bool requested = ctx.Blackboard.Get<bool>(key);
            // 요청 없음 → 이 브랜치 건너뜀 (Selector 다음 자식으로)
            if (!requested) return BTStatus.Failure;

            // 플래그 소비
            ctx.Blackboard.Set(key, false);

            if (slot == 3)
            {
                if (s.Ctrl.TryUseUltimate(out var ultimate))
                    s.Ctrl.ExecuteSkillGroup(ultimate);
            }
            else
            {
                if (s.Ctrl.TryUseSkill(slot, out var skill))
                    s.Ctrl.ExecuteSkillGroup(skill);
            }

            return BTStatus.Success;
        }

#if UNITY_EDITOR
        public override string DebugLabel => m_Slot == 3 ? "★ Ultimate" : $"✦ Skill{m_Slot}";
#endif
    }
}
