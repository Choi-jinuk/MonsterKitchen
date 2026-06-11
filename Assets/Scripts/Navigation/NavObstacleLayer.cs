using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Navigation
{
    // ====================================================================
    //  NavObstacleLayer — NavPolyObstacle 정적 레지스트리
    //
    //  ▶ NavPolyObstacle 이 OnEnable/OnDisable 에서 자동 Register/Unregister.
    //  ▶ NavGrid.PointIsValid() 내부에서 호출되므로 외부에서 직접 쓸 일은 없음.
    //  ▶ MonoBehaviour 없는 정적 클래스 — 씬 전환 시 Set 이 살아있을 수 있으나
    //    각 NavPolyObstacle 의 OnDisable 이 자동 해제를 보장한다.
    //  ▶ HashSet 사용 — Register/Unregister O(1), 중복 방지 내장
    // ====================================================================
    public static class NavObstacleLayer
    {
        static readonly HashSet<NavPolyObstacle> s_Obstacles = new HashSet<NavPolyObstacle>();

        public static void Register(NavPolyObstacle obstacle)
        {
            if (obstacle != null) s_Obstacles.Add(obstacle);   // HashSet: 중복 자동 방지
        }

        public static void Unregister(NavPolyObstacle obstacle)
        {
            s_Obstacles.Remove(obstacle);
        }

        /// <summary>등록된 장애물 중 하나라도 worldPos 를 포함하면 true.</summary>
        public static bool IsBlocked(Vector2 worldPos)
        {
            if (s_Obstacles.Count == 0) return false;

            // HashSet foreach → Enumerator 는 struct이므로 박싱 없음 (C# 컴파일러 최적화)
            foreach (var obs in s_Obstacles)
            {
                if (obs != null && obs.isActiveAndEnabled && obs.ContainsPoint(worldPos))
                    return true;
            }
            return false;
        }
    }
}
