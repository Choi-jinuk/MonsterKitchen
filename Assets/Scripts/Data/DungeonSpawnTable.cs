using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  던전 스폰 테이블 ScriptableObject
    //  메뉴: Create → MonsterKitchen → Dungeon Spawn Table
    //
    //  구조:
    //    DungeonSpawnTable  (던전 하나)
    //      └─ RoomSpawnConfig[]  (방마다 하나, roomId로 DungeonRoom과 매칭)
    //           └─ MonsterSpawnEntry[]  (몬스터 종류 × 개수)
    //
    //  사용법:
    //    SpawnManager Inspector → spawnTable 에 이 SO 를 연결한다.
    //    DungeonRoom Inspector → roomId 를 여기서 정의한 roomId 와 일치시킨다.
    // ====================================================================

    [CreateAssetMenu(menuName = "MonsterKitchen/Dungeon Spawn Table", fileName = "DungeonSpawnTable")]
    public class DungeonSpawnTable : ScriptableObject
    {
        [Tooltip("이 테이블이 적용되는 던전 식별자 (씬 이름과 맞추는 것을 권장)")]
        public string dungeonId;

        public List<RoomSpawnConfig> rooms = new();

        /// <summary>roomId 로 방 설정을 검색한다. 없으면 null.</summary>
        public RoomSpawnConfig GetRoom(string roomId)
            => rooms.Find(r => r.roomId == roomId);
    }

    // ────────────────────────────────────────────────────────────────────
    //  방 단위 스폰 설정
    // ────────────────────────────────────────────────────────────────────

    [Serializable]
    public class RoomSpawnConfig
    {
        [Tooltip("DungeonRoom.RoomId 와 일치해야 함")]
        public string roomId;

        public List<MonsterSpawnEntry> monsters = new();
    }

    // ────────────────────────────────────────────────────────────────────
    //  개별 몬스터 스폰 항목
    // ────────────────────────────────────────────────────────────────────

    [Serializable]
    public class MonsterSpawnEntry
    {
        [Tooltip("소환할 MonsterAI 프리팹.\n※ 프리팹 루트를 비활성화해 두어야 Init() 전에 Awake가 실행되지 않는다.")]
        public Enemy.MonsterAI prefab;

        [Tooltip("MonsterData 오버라이드. 비워 두면 프리팹에 설정된 MonsterData를 사용.")]
        public MonsterData dataOverride;

        [Tooltip("소환 수")]
        [Min(1)] public int count = 1;

        [Tooltip("스폰 포인트를 중심으로 한 랜덤 오프셋 반경 (단위: Unity Units)")]
        [Min(0)] public float spawnRadius = 0.5f;
    }
}
