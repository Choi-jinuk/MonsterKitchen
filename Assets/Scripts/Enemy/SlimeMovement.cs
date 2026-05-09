using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  SlimeMovement — 슬라임 전용 이동 패턴
    //
    //  ▶ 정지 → 윈드업 → 대시 사이클 (Patrol / Chase 공용)
    //    쿨다운 대기 중: 완전 정지 (슬라이딩 방지)
    //    윈드업 중:      목표 위치 잠금 후 정지
    //    대시 중:        dashSpeed 로 목표를 향해 이동
    //
    //  ▶ Separation Steering 은 MonsterAI.ApplySeparation() 에 위임.
    //
    //  MonsterAI Inspector 의 Movement 슬롯에 이 컴포넌트를 연결하면 활성화.
    // ====================================================================

    public class SlimeMovement : MonsterMovementBase
    {
        [Header("Dash")]
        [Tooltip("대시 전 목표 잠금 후 정지 대기 시간 (초).")]
        [SerializeField] float dashWindupDuration = 0.55f;
        [Tooltip("대시 이동 속도.")]
        [SerializeField] float dashSpeed          = 7f;
        [Tooltip("대시 이동 지속 시간 (초).")]
        [SerializeField] float dashDuration       = 0.35f;
        [Tooltip("대시 종료 후 다음 대시까지 대기 시간 (초).")]
        [SerializeField] float dashCooldown       = 0.6f;

        // ── 런타임 상태 ─────────────────────────────────────────────────
        Vector2 _dashTarget;
        float   _dashWindupTimer;
        float   _dashMoveTimer;
        float   _dashCoolTimer;
        bool    _isDashing;

        // ================================================================
        //  MonsterMovementBase 구현
        // ================================================================

        public override void OnSpawned()
        {
            // 동시 스폰 시 전체 슬라임이 같은 프레임에 대시하는 것을 방지
            _dashCoolTimer = Random.Range(0f, dashCooldown);
        }

        public override void ResetMovement()
        {
            _isDashing       = false;
            _dashWindupTimer = 0f;
            _dashMoveTimer   = 0f;
        }

        public override void TickPatrol(float dt, Vector2 patrolTarget)
        {
            RunDashCycle(dt, patrolTarget);
        }

        public override void TickChase(float dt, Vector2 playerPos, float preferredDistance)
        {
            RunDashCycle(dt, playerPos);
        }

        // ================================================================
        //  대시 사이클 (Patrol / Chase 공용)
        // ================================================================

        void RunDashCycle(float dt, Vector2 target)
        {
            _dashCoolTimer -= dt;

            // ── 대시 이동 중 ──────────────────────────────────────────
            if (_isDashing)
            {
                _dashMoveTimer -= dt;
                Vector2 toDash = _dashTarget - (Vector2)transform.position;

                if (_dashMoveTimer <= 0f || toDash.magnitude < 0.1f)
                {
                    Stop();
                    _isDashing     = false;
                    _dashCoolTimer = dashCooldown;
                    return;
                }

                Move(_ai.ApplySeparation(toDash.normalized) * dashSpeed);
                return;
            }

            // ── 윈드업 중 (정지) ─────────────────────────────────────
            if (_dashWindupTimer > 0f)
            {
                _dashWindupTimer -= dt;
                Stop();

                if (_dashWindupTimer <= 0f)
                {
                    _isDashing     = true;
                    _dashMoveTimer = dashDuration;
                }
                return;
            }

            // ── 쿨다운 완료 → 윈드업 시작 / 쿨다운 대기 중 정지 ─────
            if (_dashCoolTimer <= 0f)
            {
                _dashTarget      = target;              // 현재 목표 위치 잠금
                _dashWindupTimer = dashWindupDuration;
                Stop();
            }
            else
            {
                Stop();  // 쿨다운 중 완전 정지 (슬라이딩 방지)
            }
        }
    }
}
