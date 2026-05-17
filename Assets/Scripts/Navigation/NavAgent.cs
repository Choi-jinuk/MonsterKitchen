using UnityEngine;

namespace MonsterKitchen.Navigation
{
    // ====================================================================
    //  NavAgent — 엔티티별 A* 경로 추적 에이전트 (MonoBehaviour 아님)
    //
    //  사용법
    //    var agent = new NavAgent(transform);
    //    agent.SetDestination(target);          // 목표 설정 (자동 재경로)
    //    Vector2 dir = agent.GetDirection();    // 다음 경유지 방향 반환
    //    agent.Stop();                          // 경로 초기화
    //
    //  NavGrid 가 씬에 없으면 SetDestination 은 조용히 실패하고
    //  GetDirection 은 Vector2.zero 를 반환한다.
    //  호출 측에서 zero 여부를 확인해 직선 이동으로 폴백할 것.
    // ====================================================================

    public sealed class NavAgent
    {
        // ── 설정 ─────────────────────────────────────────────────────────
        /// <summary>마지막 경유지 도달로 판정하는 거리.</summary>
        public float StoppingDistance  = 0.15f;
        /// <summary>목표가 이 거리 이상 이동해야 재경로 계산.</summary>
        public float RepathThreshold   = 0.5f;
        /// <summary>중간 경유지 도달 판정 거리.</summary>
        public float WaypointProximity = 0.1f;

        // ── 내부 상태 ────────────────────────────────────────────────────
        readonly Transform _transform;
        Vector2[] _path;
        int       _index;
        Vector2   _lastGoal = Vector2.positiveInfinity;

        // ================================================================
        //  생성
        // ================================================================

        public NavAgent(Transform transform)
        {
            _transform = transform;
        }

        // ================================================================
        //  공개 프로퍼티
        // ================================================================

        /// <summary>현재 유효한 경로가 있는지.</summary>
        public bool HasPath => _path != null && _index < _path.Length;

        /// <summary>다음 경유지 (경로가 없으면 현재 위치 반환).</summary>
        public Vector2 NextWaypoint => HasPath ? _path[_index] : (Vector2)_transform.position;

        // ================================================================
        //  공개 API
        // ================================================================

        /// <summary>
        /// 목표를 설정한다.
        /// 이전 목표와의 거리가 RepathThreshold 미만이고 이미 경로가 있으면
        /// 재계산하지 않는다 (성능 최적화).
        /// </summary>
        public void SetDestination(Vector2 goal)
        {
            bool goalChanged = (_lastGoal - goal).sqrMagnitude
                               > RepathThreshold * RepathThreshold;

            if (!goalChanged && HasPath) return;

            _lastGoal = goal;
            ComputePath(goal);
        }

        /// <summary>목표와 관계없이 즉시 재경로를 계산한다.</summary>
        public void ForceRepath(Vector2 goal)
        {
            _lastGoal = goal;
            ComputePath(goal);
        }

        /// <summary>
        /// 현재 위치에서 다음 경유지까지의 정규화 방향 벡터를 반환한다.
        /// 도달한 경유지는 자동으로 전진한다.
        /// 경로가 없거나 목적지에 도달했으면 Vector2.zero 반환.
        /// </summary>
        public Vector2 GetDirection()
        {
            if (!HasPath) return Vector2.zero;

            Vector2 pos = _transform.position;

            // 도달한 경유지 전진
            while (HasPath)
            {
                Vector2 wp    = _path[_index];
                bool    isLast = (_index == _path.Length - 1);
                float   prox  = isLast ? StoppingDistance : WaypointProximity;

                if ((pos - wp).magnitude <= prox)
                    _index++;
                else
                    break;
            }

            if (!HasPath) return Vector2.zero;

            return (_path[_index] - pos).normalized;
        }

        /// <summary>경로와 상태를 초기화한다.</summary>
        public void Stop()
        {
            _path      = null;
            _index     = 0;
            _lastGoal  = Vector2.positiveInfinity;
        }

        // ================================================================
        //  내부
        // ================================================================

        void ComputePath(Vector2 goal)
        {
            _path  = NavPathfinder.FindPath(_transform.position, goal);
            _index = 0;

            if (_path == null || _path.Length == 0) return;

            // 출발 셀이 현재 위치와 매우 가까우면 건너뜀
            if (((Vector2)_transform.position - _path[0]).magnitude < WaypointProximity)
                _index = 1;
        }
    }
}
