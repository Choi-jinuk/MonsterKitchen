using MonsterKitchen.Core;
using MonsterKitchen.Navigation;  // NavAgent, NavGrid
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
    //    생성된다. 씬에 NavGrid 가 없으면 이동 불가 (직선 폴백 없음).
    //    서브클래스는 GetNavDirection(target) 을 호출해 A* 경로의
    //    다음 경유지 방향을 얻는다. Vector2.zero 반환 시 이동 스킵.
    //
    //  파일명 규칙: SlimeMovement, OrcMovement …
    // ====================================================================

    public abstract class MonsterMovementBase : MonoBehaviour
    {
        // ── 의존성 ──────────────────────────────────────────────────────
        protected Rigidbody2D       m_Rb;
        protected Animator          m_Anim;
        protected IMonsterSeparation m_Sep;

        // ── NavAgent ────────────────────────────────────────────────────
        NavAgent m_NavAgent;

        // ================================================================
        //  초기화
        // ================================================================

        // ── 이동 속도 ────────────────────────────────────────────────────
        protected float m_MoveSpeed = 2.5f;

        /// <summary>Init() 에서 호출. 의존성 주입.</summary>
        public virtual void Init(Rigidbody2D rb, Animator anim, IMonsterSeparation sep, float moveSpeed = 2.5f)
        {
            m_Rb        = rb;
            m_Anim      = anim;
            m_Sep       = sep;
            m_MoveSpeed = moveSpeed;
        }

        /// <summary>
        /// MonsterAI.Init() 에서 호출. NavGrid 필수 — 없으면 에러 로그.
        /// </summary>
        public virtual void InitNavAgent()
        {
            if (NavGrid.Instance == null)
            {
                DebugUtil.LogError($"[{name}] NavGrid 인스턴스가 없습니다. 몬스터 이동 불가.", this);
                return;
            }
            m_NavAgent = new NavAgent(transform);
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
        /// 비유효 목표는 NavAgent.SetDestination 내부에서 자동 스냅된다.
        /// NavAgent 가 없거나 경로를 찾지 못하면 Vector2.zero 반환.
        /// </summary>
        // 경로 없음 경고 쿨타임 — 매 프레임 스팸 방지
        float m_NavWarnCooldown;

        protected Vector2 GetNavDirection(Vector2 target)
        {
            if (m_NavAgent == null)
                return Vector2.zero;

            m_NavAgent.SetDestination(target);  // 내부에서 비유효 목표 스냅 처리
            var dir = m_NavAgent.GetDirection();

            if (dir == Vector2.zero)
            {
                m_NavWarnCooldown -= Time.fixedDeltaTime;
                if (m_NavWarnCooldown <= 0f)
                {
                    DebugUtil.LogWarning(
                        $"[{name}] NavGrid: 경로 없음 ({(Vector2)transform.position} → {target})", this);
                    m_NavWarnCooldown = 5f; // 5초당 1회
                }
            }
            else
            {
                m_NavWarnCooldown = 0f;
            }

            return dir;
        }

        /// <summary>nav 경로를 즉시 초기화한다 (상태 전환 시 호출).</summary>
        protected void ResetNavState() => m_NavAgent?.Stop();

        // ================================================================
        //  공용 헬퍼 — 서브클래스에서 사용
        // ================================================================

        /// <summary>
        /// 속도 기반 이동. 고속 이동 시 터널링 가능 — 일반 순찰/추적에 사용.
        /// </summary>
        protected void Move(Vector2 velocity)
        {
            m_Rb.linearVelocity = velocity;
            SetMoveAnim(velocity);
        }

        /// <summary>
        /// 위치 기반 이동 스텝 — 벽 슬라이딩 포함 (NavAgent.ComputeStep 위임).
        /// 순찰·추적 등 일반 이동에 사용.
        /// 이동 가능하면 true, 완전히 막혔으면 false 반환.
        /// </summary>
        protected bool StepMove(Vector2 dir, float speed, float dt)
        {
            Vector2 from    = m_Rb.position;
            Vector2 nextPos = NavAgent.ComputeStep(from, dir, speed, dt);

            m_Rb.linearVelocity = Vector2.zero;

            if (nextPos == from)
            {
                SetMoveAnim(Vector2.zero);
                return false;
            }

            m_Rb.MovePosition(nextPos);
            SetMoveAnim((nextPos - from) / dt);
            return true;
        }

        /// <summary>
        /// 위치 기반 직선 이동 스텝 — 슬라이딩 없음.
        /// 대시처럼 방향이 잠긴 이동에 사용. 막혔으면 false 반환.
        /// </summary>
        protected bool DirectStepMove(Vector2 dir, float speed, float dt)
        {
            if (dir.sqrMagnitude < 0.001f) { Stop(); return false; }

            Vector2 from    = m_Rb.position;
            Vector2 nextPos = from + dir.normalized * (speed * dt);

            if (!NavAgent.IsValid(nextPos))
            {
                SetMoveAnim(Vector2.zero);
                return false;
            }

            m_Rb.linearVelocity = Vector2.zero;
            m_Rb.MovePosition(nextPos);
            SetMoveAnim((nextPos - from) / dt);
            return true;
        }

        protected void Stop()
        {
            m_Rb.linearVelocity = Vector2.zero;
            SetMoveAnim(Vector2.zero);
        }

        void SetMoveAnim(Vector2 velocity)
        {
            m_Anim.SetFloat(s_HashMoveX, velocity.x);
            m_Anim.SetFloat(s_HashMoveY, velocity.y);
            m_Anim.SetFloat(s_HashSpeed, velocity.magnitude);
        }

        static readonly int s_HashMoveX = Animator.StringToHash("MoveX");
        static readonly int s_HashMoveY = Animator.StringToHash("MoveY");
        static readonly int s_HashSpeed = Animator.StringToHash("Speed");
    }
}
