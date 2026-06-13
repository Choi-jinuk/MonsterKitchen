using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  MonsterFlockManager — 군집 이웃 질의 싱글톤
    //
    //  ▶ 등록된 IMonsterFlockAgent 를 매 FixedUpdate SpatialHashGrid 에 재배치.
    //  ▶ QueryNeighbors 로 self 제외 반경 내 이웃 위치를 버퍼에 채운다.
    //  ▶ 첫 Register 시 lazy 부트스트랩 — 씬 사전 배치 불필요.
    // ====================================================================
    public sealed class MonsterFlockManager : MonoBehaviour
    {
        public static MonsterFlockManager Instance { get; private set; }

        const float CellSize = 0.9f;  // = 기본 SepRadius

        readonly List<IMonsterFlockAgent> m_Agents = new List<IMonsterFlockAgent>(128);
        SpatialHashGrid m_Grid;

        public static MonsterFlockManager GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[MonsterFlockManager]");
            return Instance = go.AddComponent<MonsterFlockManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            m_Grid   = new SpatialHashGrid(CellSize);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Register(IMonsterFlockAgent agent)
        {
            if (agent == null || m_Agents.Contains(agent)) return;
            m_Agents.Add(agent);
        }

        public void Unregister(IMonsterFlockAgent agent) => m_Agents.Remove(agent);

        void FixedUpdate()
        {
            if (m_Grid == null) return;
            m_Grid.Clear();
            for (int i = 0; i < m_Agents.Count; i++)
            {
                var a = m_Agents[i];
                if (a?.FlockTransform == null) continue;
                m_Grid.Insert(a.FlockPosition, a.FlockTransform.GetInstanceID());
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
