using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  DungeonSpawnTable — 던전 초기 스폰 설정 ScriptableObject
    //
    //  ▶ v2 (오픈월드 자유 탐험)
    //    방(DungeonRoom) 개념 폐기 → 던전 전체에 몬스터 목록을 일괄 스폰.
    //    SpawnManager.SpawnAll() 에서 이 테이블을 읽어 스폰 포인트에 배치.
    //
    //  메뉴: Create → MonsterKitchen → Dungeon Spawn Table
    // ====================================================================

    [CreateAssetMenu(menuName = "MonsterKitchen/Dungeon Spawn Table", fileName = "DungeonSpawnTable")]
    public class DungeonSpawnTable : ScriptableObject
    {
        [Tooltip("이 테이블이 적용되는 던전 식별자 (씬 이름 권장)")]
        public string dungeonId;

        [Tooltip("던전 시작 시 스폰할 몬스터 목록")]
        public List<MonsterSpawnEntry> monsters = new();
    }

    // ────────────────────────────────────────────────────────────────────
    //  개별 몬스터 스폰 항목
    // ────────────────────────────────────────────────────────────────────

    [Serializable]
    public class MonsterSpawnEntry
    {
        [Tooltip("소환할 MonsterAI 프리팹 (루트 비활성화 권장)")]
        public Enemy.MonsterAI prefab;

        [Tooltip("MonsterData 오버라이드. 비우면 프리팹의 데이터 사용.")]
        public MonsterData dataOverride;

        [Tooltip("소환 수")]
        [Min(1)] public int count = 1;

        [Tooltip("스폰 포인트 중심 랜덤 오프셋 반경")]
        [Min(0)] public float spawnRadius = 0.5f;
    }
}
