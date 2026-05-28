using System;
using System.Collections.Generic;
using MonsterKitchen.Combat;
using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonRoom — 방 1개의 클리어 조건 관리
    //
    //  ▶ 사용법
    //    1. 빈 GO 에 이 컴포넌트를 추가.
    //    2. DungeonSceneController 가 Start() 에서 RegisterMonsters() 를 호출한다.
    //    3. 등록된 몬스터가 전부 사망하면 OnRoomCleared 를 발생시킨다.
    //
    //  ▶ 빈 방 처리
    //    RegisterMonsters() 에 빈 목록이 전달되면 즉시 클리어 처리한다.
    //
    //  ▶ 리스폰 주의
    //    DungeonSceneController 는 MonsterRespawnManager 를 초기화하되
    //    방 몬스터는 Track 하지 않는다. 따라서 사망 후 재소환이 없어
    //    클리어 카운트가 정상 동작한다.
    // ====================================================================

    public class DungeonRoom : MonoBehaviour
    {
        public event Action OnRoomCleared;

        int  m_AliveCount;
        bool m_Cleared;

        public bool IsCleared => m_Cleared;

        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>
        /// 이 방에 속한 몬스터 목록을 등록한다.
        /// 각 몬스터의 Health.OnDeath 를 구독해 생존 수를 추적한다.
        /// </summary>
        public void RegisterMonsters(IEnumerable<MonsterBase> monsters)
        {
            m_AliveCount = 0;
            foreach (var m in monsters)
            {
                if (m == null) continue;
                var hp = m.GetComponent<Health>();
                if (hp == null) continue;
                hp.OnDeath += _ => OnMonsterDied();
                m_AliveCount++;
            }

            if (m_AliveCount == 0)
                TriggerCleared();
        }

        // ================================================================
        //  Internal
        // ================================================================

        void OnMonsterDied()
        {
            if (m_Cleared) return;
            m_AliveCount = Mathf.Max(0, m_AliveCount - 1);
            if (m_AliveCount == 0) TriggerCleared();
        }

        void TriggerCleared()
        {
            m_Cleared = true;
            Debug.Log($"[DungeonRoom] '{name}' 클리어!");
            OnRoomCleared?.Invoke();
        }
    }
}
