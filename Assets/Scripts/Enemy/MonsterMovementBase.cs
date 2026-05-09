using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  MonsterMovementBase — 몬스터 이동 패턴 추상 베이스 (컴포넌트 패턴)
    //
    //  ▶ MonsterAI 가 Patrol / Chase 상태에서 이동을 이 컴포넌트에 위임한다.
    //  ▶ 구체적인 이동 방식(대시, 직진, 점프 등)을 서브클래스로 구현한다.
    //
    //  사용 방법
    //    1. MonsterMovementBase 를 상속한 컴포넌트를 같은 GameObject 에 추가.
    //    2. MonsterAI Inspector 의 Movement 슬롯에 해당 컴포넌트를 연결.
    //    3. Movement 가 없으면 MonsterAI 는 단순 직선 이동으로 fallback.
    //
    //  파일명 규칙: SlimeMovement, OrcMovement …
    // ====================================================================

    public abstract class MonsterMovementBase : MonoBehaviour
    {
        protected Rigidbody2D _rb;
        protected Animator    _anim;
        protected MonsterAI   _ai;   // ApplySeparation 호출용

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
        /// 스폰 직후 호출. 첫 이동 타이밍 분산 등 스폰 시 처리를 여기서 수행.
        /// </summary>
        public virtual void OnSpawned() { }

        /// <summary>상태 전환 시 내부 타이머 / 플래그를 초기화한다.</summary>
        public abstract void ResetMovement();

        // ================================================================
        //  상태별 틱 — MonsterAI 가 매 FixedUpdate 에서 호출
        // ================================================================

        /// <summary>Patrol 상태 틱.</summary>
        /// <param name="dt">Time.fixedDeltaTime</param>
        /// <param name="patrolTarget">현재 패트롤 목표 위치</param>
        public abstract void TickPatrol(float dt, Vector2 patrolTarget);

        /// <summary>Chase 상태 틱.</summary>
        /// <param name="dt">Time.fixedDeltaTime</param>
        /// <param name="playerPos">플레이어 현재 위치</param>
        /// <param name="preferredDistance">MonsterAI 의 선호 접근 거리 (참고용)</param>
        public abstract void TickChase(float dt, Vector2 playerPos, float preferredDistance);

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
