using UnityEngine;

namespace MonsterKitchen.Data
{
    [CreateAssetMenu(menuName = "MonsterKitchen/Data/MonsterData", fileName = "MON_")]
    public class MonsterData : ScriptableObject
    {
        [Header("Identity")]
        public string id;           // MON_001
        public string displayName;
        [TextArea] public string description;

        [Header("Combat")]
        public int   hp;
        public int   attack;
        public int   defense;
        public AttributeType attribute;
        public RarityType    rarity;

        [Header("Skills (최대 3개)")]
        [Tooltip("전투 스킬 목록. 인덱스 0 = 최우선. missileSpeed > 0 이면 원거리.")]
        public SkillData[] skills = new SkillData[0];

        [Header("Drop")]
        public DropTableData dropTable;

        [Header("Visuals")]
        public Sprite sprite;
    }
}
