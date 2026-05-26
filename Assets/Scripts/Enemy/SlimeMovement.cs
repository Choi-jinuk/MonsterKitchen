using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  SlimeMovement — 슬라임 전용 이동 패턴
    //
    //  ▶ 정지 → 윈드업 → 대시 사이클 (Patrol / Chase 공용)
    //    쿨다운 대기:  완전 정지
    //    윈드업:       목표 방향 잠금 후 정지
    //    대시:         잠금된 방향으로 dashSpeed 이동
    //
    //  ▶ Nav 통합
    //    윈드업 시작 시 GetNavDirection 으로 A* 경로의 첫 경유지 방향을 잠근다.
    //    장애물이 있어도 경로를 돌아가 대시하므로 벽에 박히지 않는다.
    //    NavGrid 가 없으면 기존 직선 대시로 폴백한다.
    //
    //  ▶ Separation Steering 은 MonsterAI.ApplySeparation() 에 위임.
    // ====================================================================

    public class SlimeMovement : MonsterMovementBase
    {
        [Header("Dash")]
        [Tooltip("대시 전 목표 방향 잠금 후 정지 대기 시간 (초).")]
        [SerializeField] float dashWindupDuration = 0.55f;
        [Tooltip("대시 이동 속도.")]
        [SerializeField] float dashSpeed          = 7f;
        [Tooltip("대시 이동 지속 시간 (초).")]
        [SerializeField] float dashDuration       = 0.35f;
        [Tooltip("대시 종료 후 다음 대시까지 대기 시간 (초).")]
        [SerializeField] float dashCooldown       = 0.6f;

        // ── 런타임 상태 ─────────────────────────────────────────────────
        Vector2 _dashDir;       // 윈드업 시점에 잠근 대시 방향
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
            _dashCoolTimer = RandomUtil.Range(0f, dashCooldown);
        }

        public override void ResetMovement()
        {
            _isDashing       = false;
            _dashWindupTimer = 0f;
            _dashMoveTimer   = 0f;
            _dashCoolTimer   = 0f;  // 즉시 새 대시 사이클 시작 (CC 해제 후 즉각 반응)
            ResetNavState();
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

            // ── 대시 이동 중 ─────────────────────────────────────────
            if (_isDashing)
            {
                _dashMoveTimer -= dt;

                if (_dashMoveTimer <= 0f)
                {
                    Stop();
                    _isDashing     = false;
                    _dashCoolTimer = dashCooldown;
                    return;
                }

                Move(_ai.ApplySeparation(_dashDir) * dashSpeed);
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

            // ── 쿨다운 완료 → 윈드업 시작 ────────────────────────────
            if (_dashCoolTimer <= 0f)
            {
                // Nav: A* 경로 첫 경유지 방향을 잠근다.
                // NavGrid 없으면 GetNavDirection 이 직선 방향으로 폴백.
                _dashDir         = GetNavDirection(target);
                _dashWindupTimer = dashWindupDuration;
                Stop();
            }
            else
            {
                Stop(); // 쿨다운 중 정지
            }
        }
    }
}
