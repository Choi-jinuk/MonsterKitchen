using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Monster
{
    /// <summary>
    /// 플레이어를 향해 이동하는 액션.
    /// DetectRange 밖이면 Failure (→ Selector 가 Patrol 로 전환).
    /// PreferredDistance 이내에 도달하면 Success (→ 다음 틱에 Attack 브랜치 실행).
    /// </summary>
    public class BTAction_MoveToPlayer : BTNode
    {
        readonly MonsterBTAgent _agent;

        static readonly int HashMoveX  = Animator.StringToHash("MoveX");
        static readonly int HashMoveY  = Animator.StringToHash("MoveY");
        static readonly int HashSpeed  = Animator.StringToHash("Speed");

        public BTAction_MoveToPlayer(MonsterBTAgent agent)
        {
            _agent = agent;
        }

        public override BTStatus Tick()
        {
            var player = _agent.PlayerTransform;
            if (player == null) return BTStatus.Failure;

            float dist     = Vector2.Distance(_agent.transform.position, player.position);
            float detect   = _agent.DetectRange;
            float prefDist = _agent.PreferredDistance;

            // 감지 범위 1.5배 밖으로 나가면 추적 중단 → Patrol 로 전환
            if (dist > detect * 1.5f) return BTStatus.Failure;

            // 선호 거리 이내 → 공격 브랜치가 처리하도록 Success 반환
            if (dist <= prefDist) return BTStatus.Success;

            // 플레이어 방향으로 이동
            Vector2 dir      = ((Vector2)player.position - (Vector2)_agent.transform.position).normalized;
            Vector2 sepDir   = _agent.Controller.ApplySeparation(dir);
            Vector2 velocity = sepDir * _agent.MoveSpeed;

            _agent.Controller.Rb.linearVelocity = velocity;

            var anim = _agent.Anim;
            anim.SetFloat(HashMoveX, velocity.x);
            anim.SetFloat(HashMoveY, velocity.y);
            anim.SetFloat(HashSpeed, velocity.magnitude);

            return BTStatus.Running;
        }

        public override void Abort()
        {
            if (_agent.Controller?.Rb != null)
                _agent.Controller.Rb.linearVelocity = Vector2.zero;
        }
    }
}
