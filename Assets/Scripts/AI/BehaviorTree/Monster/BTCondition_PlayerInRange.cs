using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Monster
{
    /// <summary>
    /// 플레이어가 지정 범위 안에 있으면 Success, 밖이면 Failure.
    ///
    /// [SerializeField]
    ///   range      — 리터럴 판정 거리. rangeBBKey 가 없을 때 사용 (기본 5).
    ///   rangeBBKey — 이 키가 있으면 블랙보드 값을 거리로 사용.
    /// </summary>
    [BTNode("Monster/Condition/PlayerInRange")]
    public class BTCondition_PlayerInRange : BTNode
    {
        [SerializeField] float  range      = 5f;
        [SerializeField] string rangeBBKey = "";

        protected override BTStatus Execute(BTContext ctx)
        {
            if (!ctx.Blackboard.TryGet<Transform>("Player", out var player) || player == null)
                return BTStatus.Failure;

            float r = !string.IsNullOrEmpty(rangeBBKey)
                ? ctx.Blackboard.Get<float>(rangeBBKey)
                : range;

            float dist = Vector2.Distance(ctx.Owner.transform.position, player.position);
            return dist <= r ? BTStatus.Success : BTStatus.Failure;
        }

#if UNITY_EDITOR
        public override string DebugLabel
        {
            get
            {
                string r = string.IsNullOrEmpty(rangeBBKey) ? $"{range:F1}" : $"BB:{rangeBBKey}";
                return $"? PlayerInRange ({r})";
            }
        }
#endif
    }
}
