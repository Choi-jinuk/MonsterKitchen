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
        [SerializeField] float m_DashWindupDuration = 0.55f;
        [Tooltip("대시 이동 속도.")]
        [SerializeField] float m_DashSpeed          = 7f;
        [Tooltip("대시 이동 지속 시간 (초).")]
        [SerializeField] float m_DashDuration       = 0.35f;
        [Tooltip("대시 종료 후 다음 대시까지 대기 시간 (초).")]
        [SerializeField] float m_DashCooldown       = 0.6f;

        // ── 런타임 상태 ─────────────────────────────────────────────────
        Vector2 m_DashDir;       // 윈드업 시점에 잠근 대시 방향
        float   m_DashWindupTimer;
        float   m_DashMoveTimer;
        float   m_DashCoolTimer;
        bool    m_IsDashing;

        // ================================================================
        //  MonsterMovementBase 구현
        // ================================================================

        public override void OnSpawned()
        {
            // 동시 스폰 시 전체 슬라임이 같은 프레임에 대시하는 것을 방지
            m_DashCoolTimer = RandomUtil.Range(0f, m_DashCooldown);
        }

        public override void ResetMovement()
        {
            m_IsDashing       = false;
            m_DashWindupTimer = 0f;
            m_DashMoveTimer   = 0f;
            m_DashCoolTimer   = 0f;  // 즉시 새 대시 사이클 시작 (CC 해제 후 즉각 반응)
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
            m_DashCoolTimer -= dt;

            // ── 대시 이동 중 ─────────────────────────────────────────
            if (m_IsDashing)
            {
                m_DashMoveTimer -= dt;

                if (m_DashMoveTimer <= 0f)
                {
                    Stop();
                    m_IsDashing     = false;
                    m_DashCoolTimer = m_DashCooldown;
                    return;
                }

                Move(m_Sep != null ? m_Sep.ApplySeparation(m_DashDir) * m_DashSpeed : m_DashDir * m_DashSpeed);
                return;
            }

            // ── 윈드업 중 (정지) ─────────────────────────────────────
            if (m_DashWindupTimer > 0f)
            {
                m_DashWindupTimer -= dt;
                Stop();

                if (m_DashWindupTimer <= 0f)
                {
                    m_IsDashing     = true;
                    m_DashMoveTimer = m_DashDuration;
                }
                return;
            }

            // ── 쿨다운 완료 → 윈드업 시작 ────────────────────────────
            if (m_DashCoolTimer <= 0f)
            {
                // Nav: A* 경로 첫 경유지 방향을 잠근다.
                // NavGrid 없으면 GetNavDirection 이 직선 방향으로 폴백.
                m_DashDir         = GetNavDirection(target);
                m_DashWindupTimer = m_DashWindupDuration;
                Stop();
            }
            else
            {
                Stop(); // 쿨다운 중 정지
            }
        }
    }
}
