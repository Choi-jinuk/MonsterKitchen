using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.AI
{
    // ====================================================================
    //  FlockManager — 군집 이웃 질의 싱글톤 (몬스터·동료 공용 단일 그리드)
    //
    //  ▶ 등록된 IFlockAgent 를 매 FixedUpdate SpatialHashGrid 에 재배치.
    //  ▶ QueryNeighbors 로 self 제외 반경 내 이웃 위치를 버퍼에 채운다.
    //  ▶ 진영 구분 없이 모든 에이전트가 단일 그리드에서 겹침 회피(WC3 방식).
    //  ▶ 첫 Register 시 lazy 부트스트랩 — 씬 사전 배치 불필요.
    // ====================================================================
    public sealed class FlockManager : MonoBehaviour
    {
        public static FlockManager Instance { get; private set; }

        const float CellSize = 0.9f;  // = 기본 SepRadius

        readonly List<IFlockAgent> m_Agents = new List<IFlockAgent>(128);
        SpatialHashGrid m_Grid;

        public static FlockManager GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[FlockManager]");
            return Instance = go.AddComponent<FlockManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            m_Grid   = new SpatialHashGrid(CellSize);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Register(IFlockAgent agent)
        {
            if (agent == null || m_Agents.Contains(agent)) return;
            m_Agents.Add(agent);
        }

        public void Unregister(IFlockAgent agent) => m_Agents.Remove(agent);

        void FixedUpdate()
        {
            if (m_Grid == null) return;
            m_Grid.Clear();

            // 역순 순회 — 파괴된 에이전트는 자동 정리.
            // 주의: 파괴된 MonoBehaviour 는 C# null 이 아니므로 멤버 접근(.FlockTransform)이
            //       MissingReferenceException 을 던진다. Unity 오버로드 == 로 먼저 걸러낸다.
            for (int i = m_Agents.Count - 1; i >= 0; i--)
            {
                var a = m_Agents[i];
                if (a == null || (a is Object uo && uo == null))
                {
                    m_Agents.RemoveAt(i);
                    continue;
                }

                var t = a.FlockTransform;
                if (t == null) continue;
                m_Grid.Insert(a.FlockPosition, t.GetInstanceID());
            }
        }

        /// <summary>self 제외 반경 내 이웃 위치를 buffer 에 채우고 개수 반환.</summary>
        public int QueryNeighbors(Vector2 pos, float radius, Transform self, Vector2[] buffer)
        {
            if (m_Grid == null) return 0;
            int excludeId = self != null ? self.GetInstanceID() : -1;
            return m_Grid.Query(pos, radius, excludeId, buffer);
        }
    }
}
