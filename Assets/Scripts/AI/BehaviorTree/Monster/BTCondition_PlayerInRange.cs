using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Monster
{
    // ====================================================================
    //  BTCondition_PlayerInRange — 플레이어 사거리 감지 Condition Decorator
    //
    //  ▶ 이전 구조 vs 개선 구조 (BTCondition 참조)
    //    이전: BTSequence → [BTCondition_PlayerInRange(Leaf), AttackSubTree]
    //    지금: BTCondition_PlayerInRange(Decorator) → AttackSubTree
    //
    //    Selector 에서:
    //      [BTCondition_PlayerInRange → AttackSubTree,  Patrol]
    //
    //  ▶ 자동 인터럽트 동작
    //    매 틱 Check() 를 먼저 평가하므로:
    //    · 플레이어가 사거리 이탈 → Child.Abort() → 이동 정지 → Patrol 전환
    //    · 플레이어 재진입 → BTSelector 가 Patrol.Abort() → 공격 서브트리 재개
    //
    //  [SerializeField]
    //    m_Range      — 리터럴 판정 거리. m_RangeBBKey 가 없을 때 사용 (기본 5).
    //    m_RangeBBKey — 이 키가 있으면 블랙보드 값을 거리로 사용.
    // ====================================================================

    [BTNode("Monster/Condition/PlayerInRange")]
    public class BTCondition_PlayerInRange : BTCondition
    {
        [SerializeField] float  m_Range      = 5f;
        [SerializeField] string m_RangeBBKey = "";

        protected override bool Check(BTContext ctx)
        {
            if (!ctx.Blackboard.TryGet<Transform>("Player", out var player) || player == null)
                return false;

            float r = !string.IsNullOrEmpty(m_RangeBBKey)
                ? ctx.Blackboard.Get<float>(m_RangeBBKey)
                : m_Range;

            return Vector2.Distance(ctx.Owner.transform.position, player.position) <= r;
        }

#if UNITY_EDITOR
        public override string DebugLabel
        {
            get
            {
                string r = string.IsNullOrEmpty(m_RangeBBKey) ? $"{m_Range:F1}" : $"BB:{m_RangeBBKey}";
                return $"? PlayerInRange ({r})";
            }
        }
#endif
    }
}
