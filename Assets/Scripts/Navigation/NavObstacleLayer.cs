using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Navigation
{
    // ====================================================================
    //  NavObstacleLayer — NavPolyObstacle 정적 레지스트리
    //
    //  ▶ NavPolyObstacle 이 OnEnable/OnDisable 에서 자동 Register/Unregister.
    //  ▶ NavGrid.PointIsValid() 내부에서 호출되므로 외부에서 직접 쓸 일은 없음.
    //  ▶ MonoBehaviour 없는 정적 클래스 — 씬 전환 시 List 가 살아있을 수 있으나
    //    각 NavPolyObstacle 의 OnDisable 이 자동 해제를 보장한다.
    // ====================================================================
    public static class NavObstacleLayer
    {
        static readonly List<NavPolyObstacle> s_Obstacles = new List<NavPolyObstacle>();

        public static void Register(NavPolyObstacle obstacle)
        {
            if (!s_Obstacles.Contains(obstacle))
                s_Obstacles.Add(obstacle);
        }

        public static void Unregister(NavPolyObstacle obstacle)
        {
            s_Obstacles.Remove(obstacle);
        }

        /// <summary>등록된 장애물 중 하나라도 worldPos 를 포함하면 true.</summary>
        public static bool IsBlocked(Vector2 worldPos)
        {
            for (int i = 0; i < s_Obstacles.Count; i++)
            {
                var obs = s_Obstacles[i];
                if (obs != null && obs.isActiveAndEnabled && obs.ContainsPoint(worldPos))
                    return true;
            }
            return false;
        }
    }
}
