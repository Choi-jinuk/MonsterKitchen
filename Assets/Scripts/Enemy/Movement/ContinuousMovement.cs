using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  ContinuousMovement — 연속 속도 조향 이동 (footman/orc 형)
    //
    //  ▶ TickChase: nav 경로 방향 → ApplySeparation 보정 → StepMove 로 이동.
    //  ▶ 슬라임의 dash 사이클과 달리 매 틱 연속 이동.
    // ====================================================================
    public class ContinuousMovement : MonsterMovementBase
    {
        public override void OnSpawned() { }

        public override void ResetMovement()
        {
            Stop();
            ResetNavState();
        }

        public override void TickPatrol(float dt, Vector2 patrolTarget)
        {
            Vector2 navDir = GetNavDirection(patrolTarget);
            if (navDir == Vector2.zero) { Stop(); return; }
            StepMove(navDir, m_MoveSpeed, dt);
        }

        public override void TickChase(float dt, Vector2 playerPos, float preferredDistance)
        {
            Vector2 navDir = GetNavDirection(playerPos);
            if (navDir == Vector2.zero) { Stop(); return; }

            Vector2 steer = m_Sep != null ? m_Sep.ApplySeparation(navDir) : navDir;
            StepMove(steer, m_MoveSpeed, dt);
        }
    }
}
