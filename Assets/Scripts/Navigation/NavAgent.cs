using UnityEngine;

namespace MonsterKitchen.Navigation
{
    // ====================================================================
    //  NavAgent — 엔티티별 A* 경로 추적 에이전트 (MonoBehaviour 아님)
    //
    //  인스턴스 사용법
    //    var agent = new NavAgent(transform);
    //    agent.SetDestination(target);       // 목표 설정 (비유효 목표 자동 스냅)
    //    Vector2 dir = agent.GetDirection(); // 다음 경유지 방향 반환
    //    agent.Stop();                       // 경로 초기화
    //
    //  정적 유틸리티 (플레이어·스폰·몬스터 공용)
    //    NavAgent.IsValid(pos)                          — PointIsValid 래퍼
    //    NavAgent.GetNearestValid(pos, radius)          — BFS 최근접 유효 위치
    //    NavAgent.ComputeStep(from, dir, speed, dt)     — 벽 슬라이딩 이동 스텝
    //
    //  최적화 포인트
    //    · SetDestination: RepathThreshold 조기 종료 → IsValid/snap 비용 절감
    //    · ComputeStep: Atan2 제거 → X/Y 축 직접 검사
    // ====================================================================

    public sealed class NavAgent
    {
        // ── 설정 ─────────────────────────────────────────────────────────
        public float StoppingDistance  = 0.15f;
        public float RepathThreshold   = 0.5f;
        public float WaypointProximity = 0.1f;

        // ── 내부 상태 ────────────────────────────────────────────────────
        readonly Transform m_Transform;
        Vector2[] m_Path;
        int       m_Index;
        Vector2   m_LastRawGoal = Vector2.positiveInfinity;  // 비교용 원시 목표

        // ================================================================
        //  생성
        // ================================================================

        public NavAgent(Transform transform) => m_Transform = transform;

        // ================================================================
        //  공개 프로퍼티
        // ================================================================

        public bool    HasPath     => m_Path != null && m_Index < m_Path.Length;
        public Vector2 NextWaypoint => HasPath ? m_Path[m_Index] : (Vector2)m_Transform.position;

        // ================================================================
        //  공개 API — 인스턴스
        // ================================================================

        /// <summary>
        /// 목표를 설정한다.
        /// ① RepathThreshold 미만 이동 + 경로 존재 → 즉시 반환 (IsValid 호출 없음).
        /// ② 목표가 비유효이면 가장 가까운 유효 위치로 스냅 후 A* 재계산.
        /// </summary>
        public void SetDestination(Vector2 goal)
        {
            // ── ① 조기 종료: 목표가 크게 바뀌지 않았으면 재계산 불필요 ──
            float sqrThreshold = RepathThreshold * RepathThreshold;
            if ((m_LastRawGoal - goal).sqrMagnitude <= sqrThreshold && HasPath)
                return;

            m_LastRawGoal = goal;

            // ── ② 비유효 목표 스냅 ──────────────────────────────────────
            Vector2 snapped = IsValid(goal) ? goal : GetNearestValid(goal);
            ComputePath(snapped);
        }

        /// <summary>목표와 관계없이 즉시 재경로를 계산한다.</summary>
        public void ForceRepath(Vector2 goal)
        {
            m_LastRawGoal = goal;
            Vector2 snapped = IsValid(goal) ? goal : GetNearestValid(goal);
            ComputePath(snapped);
        }

        /// <summary>
        /// 다음 경유지 방향 벡터를 반환한다.
        /// 도달한 경유지를 자동으로 전진한다.
        /// 경로 없음 또는 목적지 도달 → Vector2.zero.
        /// </summary>
        public Vector2 GetDirection()
        {
            if (!HasPath) return Vector2.zero;

            Vector2 pos = m_Transform.position;

            while (HasPath)
            {
                float prox = m_Index == m_Path.Length - 1 ? StoppingDistance : WaypointProximity;
                if ((pos - m_Path[m_Index]).magnitude <= prox)
                    m_Index++;
                else
                    break;
            }

            return HasPath ? (m_Path[m_Index] - pos).normalized : Vector2.zero;
        }

        /// <summary>경로와 상태를 초기화한다.</summary>
        public void Stop()
        {
            m_Path        = null;
            m_Index       = 0;
            m_LastRawGoal = Vector2.positiveInfinity;
        }

        // ================================================================
        //  공개 API — 정적 유틸리티
        // ================================================================

        /// <summary>
        /// 월드 좌표가 유효 이동 위치인지 반환한다.
        /// 유효 = IsWalkable(타일맵) AND NavObstacleLayer 장애물 없음.
        /// NavGrid 없는 씬에서는 항상 true.
        /// </summary>
        public static bool IsValid(Vector2 worldPos)
        {
            var grid = NavGrid.Instance;
            return grid == null || grid.PointIsValid(worldPos);
        }

        /// <summary>
        /// BFS 로 가장 가까운 유효 위치(IsValid 기준)를 반환한다.
        /// searchRadius 내에 없으면 worldPos 를 그대로 반환한다.
        /// NavGrid 없는 씬에서는 worldPos 를 그대로 반환한다.
        /// </summary>
        public static Vector2 GetNearestValid(Vector2 worldPos, int searchRadius = 5)
        {
            var grid = NavGrid.Instance;
            return grid == null ? worldPos : grid.GetNearestValid(worldPos, searchRadius);
        }

        /// <summary>
        /// 이동 방향을 적용한 다음 위치를 계산한다.
        /// 목표 위치가 비유효이면 X → Y 축 순으로 슬라이딩을 시도한다.
        /// NavGrid 없는 씬에서는 단순 직선 이동.
        /// </summary>
        public static Vector2 ComputeStep(Vector2 from, Vector2 inputDir, float speed, float dt)
        {
            if (inputDir.sqrMagnitude < 0.01f) return from;

            var     grid = NavGrid.Instance;
            Vector2 norm = inputDir.normalized;
            float   step = speed * dt;

            if (grid == null) return from + norm * step;

            // ── 직선 이동 ─────────────────────────────────────────────
            Vector2 full = from + norm * step;
            if (grid.PointIsValid(full)) return full;

            // ── X축 슬라이딩 ──────────────────────────────────────────
            if (norm.x != 0f)
            {
                Vector2 xSlide = new Vector2(from.x + norm.x * step, from.y);
                if (grid.PointIsValid(xSlide)) return xSlide;
            }

            // ── Y축 슬라이딩 ──────────────────────────────────────────
            if (norm.y != 0f)
            {
                Vector2 ySlide = new Vector2(from.x, from.y + norm.y * step);
                if (grid.PointIsValid(ySlide)) return ySlide;
            }

            return from; // 완전히 막힘
        }

        // ================================================================
        //  내부
        // ================================================================

        void ComputePath(Vector2 goal)
        {
            m_Path  = NavPathfinder.FindPath(m_Transform.position, goal);
            m_Index = 0;

            if (m_Path == null || m_Path.Length == 0) return;

            if (((Vector2)m_Transform.position - m_Path[0]).magnitude < WaypointProximity)
                m_Index = 1;
        }
    }
}
