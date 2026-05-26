using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    [Serializable]
    public class MonsterData
    {
        [Header("Identity")]
        public uint   id;
        public string displayName;
        [TextArea] public string description;

        [Header("Combat")]
        public int           hp;
        public int           attack;
        public int           defense;
        public AttributeType attribute;
        public RarityType    rarity;

        [Header("Skills (최대 3개)")]
        [Tooltip("전투 스킬 목록. 인덱스 0 = 최우선. missileSpeed > 0 이면 원거리.")]
        public SkillData[] skills = Array.Empty<SkillData>();

        [Header("Drop")]
        [Tooltip("드롭 테이블 ID (TableData.DropTables 키)")]
        public uint dropTableId;

        [Header("Assets — AssetManifest 등록 키")]
        [Tooltip("프리팹 주소. 형식: prefab/monster/{id}")]
        public string prefabAddress;

        [Tooltip("스프라이트 주소. 형식: sprite/monster/{id}")]
        public string spriteAddress;

        [Header("CC 면역")]
        [Tooltip("true 이면 넉백이 걸리지 않는다.")]
        public bool immuneToKnockback;
        [Tooltip("true 이면 스턴이 걸리지 않는다.")]
        public bool immuneToStun;
        [Tooltip("true 이면 풀인이 걸리지 않는다.")]
        public bool immuneToPullIn;
    }
}
