using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Monster
{
    /// <summary>플레이어가 지정 범위 안에 있으면 Success, 밖이면 Failure.</summary>
    public class BTCondition_PlayerInRange : BTNode
    {
        readonly MonsterBTAgent _agent;
        readonly float          _range;

        public BTCondition_PlayerInRange(MonsterBTAgent agent, float range)
        {
            _agent = agent;
            _range = range;
        }

        public override BTStatus Tick()
        {
            var player = _agent.PlayerTransform;
            if (player == null) return BTStatus.Failure;

            float dist = Vector2.Distance(_agent.transform.position, player.position);
            return dist <= _range ? BTStatus.Success : BTStatus.Failure;
        }
    }
}
