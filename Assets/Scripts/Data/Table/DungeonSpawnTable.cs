using System;
using System.Collections.Generic;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  DungeonSpawnTableData — 던전 스폰 테이블 데이터
    //
    //  ▶ TableData.DungeonSpawnTables 에 id(uint) 키로 저장된다.
    //
    //  ▶ 몬스터 프리팹은 MonsterData.prefabAddress 키로 AssetLoadManager 에서 로드한다.
    //    DataRegistry.Instance.GetMonster(monsterId) → data.prefabAddress → AssetLoadManager.Load
    // ====================================================================

    [Serializable]
    public class DungeonSpawnTableData
    {
        public uint Id;

        public List<MonsterSpawnEntry> Monsters = new();
    }

    // ────────────────────────────────────────────────────────────────────
    //  개별 몬스터 스폰 항목
    // ────────────────────────────────────────────────────────────────────

    [Serializable]
    public class MonsterSpawnEntry
    {
        [UnityEngine.Tooltip("스폰할 MonsterData 의 id (TableData.Monsters 키)")]
        public uint MonsterId;

        [UnityEngine.Tooltip("소환 수")]
        [UnityEngine.Min(1)] public int Count = 1;

        [UnityEngine.Tooltip("스폰 포인트 중심 랜덤 오프셋 반경")]
        [UnityEngine.Min(0)] public float SpawnRadius = 0.5f;
    }
}
