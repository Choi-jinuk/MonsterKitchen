using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  FlockSteering — 순수 군집 스티어링 함수 (부수효과 없음, 테스트 가능)
    //
    //  ▶ neighbors: self 제외한 이웃 위치 배열. count 만큼만 유효.
    //  ▶ 모든 함수는 desired velocity(또는 force) 를 반환만 한다.
    // ====================================================================
    public static class FlockSteering
    {
        const float Epsilon = 0.0001f;

        /// <summary>역제곱 가중 separation force. 정규화 후 sepWeight 곱.</summary>
        public static Vector2 Separation(
            Vector2 selfPos, Vector2[] neighbors, int count, float sepRadius, float sepWeight)
        {
            Vector2 force      = Vector2.zero;
            float   sqrRadius  = sepRadius * sepRadius;

            for (int i = 0; i < count; i++)
            {
                Vector2 away   = selfPos - neighbors[i];
                float   sqrDst = away.sqrMagnitude;
                if (sqrDst > sqrRadius || sqrDst < Epsilon) continue;

                float dist = Mathf.Sqrt(sqrDst);
                force += (away / dist) / Mathf.Max(sqrDst, Epsilon);  // 역제곱
            }

            if (force.sqrMagnitude < Epsilon) return Vector2.zero;
            return force.normalized * sepWeight;
        }

        /// <summary>추격: nav 방향 seek + separation. moveSpeed 로 clamp.</summary>
        public static Vector2 ComputeChase(
            Vector2 selfPos, Vector2 navDir, Vector2[] neighbors, int count,
            float moveSpeed, in FlockWeights w)
        {
            Vector2 seek       = navDir.sqrMagnitude > Epsilon ? navDir.normalized * w.SeekWeight : Vector2.zero;
            Vector2 separation = Separation(selfPos, neighbors, count, w.SepRadius, w.SepWeight);
            Vector2 desired    = seek + separation;
            if (desired.sqrMagnitude < Epsilon) return Vector2.zero;
            return Vector2.ClampMagnitude(desired.normalized * moveSpeed, moveSpeed);
        }

        /// <summary>
        /// 둘러싸기: arrival(링 반경 유지) + tangential(빈 각도로 회전) + separation.
        /// </summary>
        public static Vector2 ComputeEncircle(
            Vector2 selfPos, Vector2 playerPos, float ringRadius, Vector2[] neighbors, int count,
            float moveSpeed, in FlockWeights w)
        {
            Vector2 toPlayer = playerPos - selfPos;
            float   dist     = toPlayer.magnitude;
            Vector2 dir      = dist > Epsilon ? toPlayer / dist : Vector2.right;

            // ── arrival: 링 반경 기준 안쪽이면 바깥, 밖이면 안쪽 ──
            float   toRing   = dist - ringRadius;                       // 양수=링 밖
            float   arrAmt   = Mathf.Clamp(toRing / Mathf.Max(w.SlowRadius, Epsilon), -1f, 1f);
            Vector2 arrival  = dir * (arrAmt * w.ArrivalWeight);        // dir=플레이어 방향

            // ── separation ──
            Vector2 separation = Separation(selfPos, neighbors, count, w.SepRadius, w.SepWeight);

            // ── tangential: 링 근처일수록 강하게, 이웃 밀집 반대쪽으로 회전 ──
            Vector2 tangent    = new Vector2(-dir.y, dir.x);            // 플레이어 기준 접선
            float   side       = Vector2.Dot(separation, tangent);
            float   sign       = side >= 0f ? 1f : -1f;
            float   ringProx   = 1f - Mathf.Clamp01(Mathf.Abs(toRing) / Mathf.Max(w.RingBand, Epsilon));
            Vector2 tangential = tangent * (sign * w.TangentWeight * ringProx);

            Vector2 desired = arrival + separation + tangential;
            if (desired.sqrMagnitude < Epsilon) return Vector2.zero;
            return Vector2.ClampMagnitude(desired.normalized * moveSpeed, moveSpeed);
        }
    }
}
