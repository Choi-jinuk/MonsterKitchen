using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Monster
{
    /// <summary>
    /// 스폰 지점 주변 랜덤 순찰 액션.
    /// 순찰 목표 도달 → 대기 → 새 목표 선택을 반복하며 항상 Running 을 반환한다.
    /// (Selector 의 마지막 자식이므로 Failure 반환 불필요)
    /// </summary>
    public class BTAction_Patrol : BTNode
    {
        readonly MonsterBTAgent _agent;

        enum PatrolState { Moving, Waiting }

        PatrolState _state      = PatrolState.Waiting;
        Vector2     _target;
        float       _waitTimer;

        static readonly int HashMoveX = Animator.StringToHash("MoveX");
        static readonly int HashMoveY = Animator.StringToHash("MoveY");
        static readonly int HashSpeed = Animator.StringToHash("Speed");

        public BTAction_Patrol(MonsterBTAgent agent)
        {
            _agent     = agent;
            _waitTimer = agent.PatrolWait;
        }

        public override BTStatus Tick()
        {
            switch (_state)
            {
                case PatrolState.Waiting: TickWait(); break;
                case PatrolState.Moving:  TickMove(); break;
            }
            return BTStatus.Running;
        }

        void TickWait()
        {
            _agent.Controller.Rb.linearVelocity = Vector2.zero;
            SetMoveAnim(Vector2.zero);

            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f)
            {
                PickTarget();
                _state = PatrolState.Moving;
            }
        }

        void TickMove()
        {
            Vector2 toTarget = _target - (Vector2)_agent.transform.position;
            if (toTarget.magnitude < 0.2f)
            {
                _agent.Controller.Rb.linearVelocity = Vector2.zero;
                SetMoveAnim(Vector2.zero);
                _waitTimer = _agent.PatrolWait;
                _state     = PatrolState.Waiting;
                return;
            }

            Vector2 dir      = toTarget.normalized;
            Vector2 velocity = _agent.Controller.ApplySeparation(dir) * (_agent.MoveSpeed * 0.6f);
            _agent.Controller.Rb.linearVelocity = velocity;
            SetMoveAnim(velocity);
        }

        void PickTarget()
        {
            Vector2 offset = Random.insideUnitCircle * _agent.PatrolRadius;
            _target = (Vector2)_agent.SpawnPos + offset;
        }

        void SetMoveAnim(Vector2 velocity)
        {
            var anim = _agent.Anim;
            anim.SetFloat(HashMoveX, velocity.x);
            anim.SetFloat(HashMoveY, velocity.y);
            anim.SetFloat(HashSpeed, velocity.magnitude);
        }

        public override void Abort()
        {
            if (_agent.Controller?.Rb != null)
                _agent.Controller.Rb.linearVelocity = Vector2.zero;
        }
    }
}
