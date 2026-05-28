using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    [Serializable]
    public class MonsterData
    {
        [Header("Identity")]
        public uint   Id;
        public string DisplayName;
        [TextArea] public string Description;

        [Header("Combat")]
        public int           Hp;
        public int           Attack;
        public int           Defense;
        public float         MoveSpeed;
        public AttributeType Attribute;
        public RarityType    Rarity;

        [Header("Skills — SkillGroups 테이블 참조")]
        [Tooltip("사용할 스킬 그룹 문자열 ID 목록 (CSV: 파이프 구분). 예: SGD_004|SGD_005\n" +
                 "DataManagerWindow Sync 후 skillGroups 배열로 변환된다.")]
        public string[] SkillGroupIds = Array.Empty<string>();

        [Tooltip("런타임 스킬 그룹 목록. DataManagerWindow Sync 시 skillGroupIds 로 자동 채워진다.\n" +
                 "인덱스 0 = 최우선.")]
        public SkillGroupData[] SkillGroups = Array.Empty<SkillGroupData>();

        [Header("Drop")]
        [Tooltip("드롭 테이블 ID (TableData.DropTables 키)")]
        public uint DropTableId;

        [Header("Assets — AssetManifest 등록 키")]
        [Tooltip("프리팹 주소. 형식: prefab/monster/{id}")]
        public string PrefabAddress;

        [Tooltip("스프라이트 주소. 형식: sprite/monster/{id}")]
        public string SpriteAddress;

        [Tooltip("BehaviorTree 에셋 주소. 형식: bt/monster/{id}\n" +
                 "AssetManifest 에 등록된 BTAsset ScriptableObject 키.")]
        public string BtAssetAddress;

        [Header("CC 면역")]
        [Tooltip("true 이면 넉백이 걸리지 않는다.")]
        public bool ImmuneToKnockback;
        [Tooltip("true 이면 스턴이 걸리지 않는다.")]
        public bool ImmuneToStun;
        [Tooltip("true 이면 풀인이 걸리지 않는다.")]
        public bool ImmuneToPullIn;
    }
}
