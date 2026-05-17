using MonsterKitchen.Navigation;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  MonsterMovementBase — 몬스터 이동 패턴 추상 베이스 (컴포넌트 패턴)
    //
    //  ▶ MonsterAI 가 Patrol / Chase 상태에서 이동을 이 컴포넌트에 위임한다.
    //  ▶ 구체적인 이동 방식(대시, 직진, 점프 등)을 서브클래스로 구현한다.
    //
    //  Nav2D 통합
    //    MonsterAI.Init() 에서 InitNavAgent() 를 호출하면 내부 NavAgent 가
    //    생성된다. 씬에 NavGrid 가 없으면 agent 가 없어도 직선 이동으로 폴백.
    //    서브클래스는 GetNavDirection(target) 을 호출해 A* 경로의
    //    다음 경유지 방향을 얻는다.
    //
    //  파일명 규칙: SlimeMovement, OrcMovement …
    // ====================================================================

    public abstract class MonsterMovementBase : MonoBehaviour
    {
        // ── 의존성 ──────────────────────────────────────────────────────
        protected Rigidbody2D _rb;
        protected Animator    _anim;
        protected MonsterAI   _ai;

        // ── NavAgent ────────────────────────────────────────────────────
        NavAgent _navAgent;

        // ================================================================
        //  초기화
        // ================================================================

        /// <summary>MonsterAI.Init() 에서 호출. 의존성 주입.</summary>
        public virtual void Init(Rigidbody2D rb, Animator anim, MonsterAI ai)
        {
            _rb   = rb;
            _anim = anim;
            _ai   = ai;
        }

        /// <summary>
        /// MonsterAI.Init() 에서 호출. NavGrid 가 씬에 있을 때만 에이전트 생성.
        /// </summary>
        public virtual void InitNavAgent()
        {
            if (NavGrid.Instance == null) return;
            _navAgent = new NavAgent(transform);
        }

        /// <summary>스폰 직후 호출. 첫 이동 타이밍 분산 등 처리.</summary>
        public virtual void OnSpawned() { }

        /// <summary>상태 전환 시 내부 타이머·플래그·nav 경로를 초기화한다.</summary>
        public abstract void ResetMovement();

        // ================================================================
        //  상태별 틱 — MonsterAI 가 매 FixedUpdate 에서 호출
        // ================================================================

        public abstract void TickPatrol(float dt, Vector2 patrolTarget);
        public abstract void TickChase(float dt, Vector2 playerPos, float preferredDistance);

        // ================================================================
        //  Nav 헬퍼 — 서브클래스에서 사용
        // ================================================================

        /// <summary>
        /// A* 경로의 다음 경유지를 향한 정규화 방향 벡터를 반환한다.
        /// NavGrid 가 없거나 경로를 못 찾으면 target 으로의 직선 방향으로 폴백.
        /// </summary>
        protected Vector2 GetNavDirection(Vector2 target)
        {
            if (_navAgent == null)
                return (target - (Vector2)transform.position).normalized;

            _navAgent.SetDestination(target);
            var dir = _navAgent.GetDirection();

            // 경로가 없거나 목적지 도달 → 직선 폴백
            if (dir == Vector2.zero)
                dir = (target - (Vector2)transform.position).normalized;

            return dir;
        }

        /// <summary>nav 경로를 즉시 초기화한다 (상태 전환 시 호출).</summary>
        protected void ResetNavState() => _navAgent?.Stop();

        // ================================================================
        //  공용 헬퍼 — 서브클래스에서 사용
        // ================================================================

        protected void Move(Vector2 velocity)
        {
            _rb.linearVelocity = velocity;
            SetMoveAnim(velocity);
        }

        protected void Stop()
        {
            _rb.linearVelocity = Vector2.zero;
            SetMoveAnim(Vector2.zero);
        }

        void SetMoveAnim(Vector2 velocity)
        {
            _anim.SetFloat(HashMoveX, velocity.x);
            _anim.SetFloat(HashMoveY, velocity.y);
            _anim.SetFloat(HashSpeed, velocity.magnitude);
        }

        static readonly int HashMoveX = Animator.StringToHash("MoveX");
        static readonly int HashMoveY = Animator.StringToHash("MoveY");
        static readonly int HashSpeed = Animator.StringToHash("Speed");
    }
}
