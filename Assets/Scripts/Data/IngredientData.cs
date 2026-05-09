using UnityEngine;

namespace MonsterKitchen.Data
{
    [CreateAssetMenu(menuName = "MonsterKitchen/Data/IngredientData", fileName = "ING_")]
    public class IngredientData : ScriptableObject
    {
        [Header("Identity")]
        public string id;           // ING_001
        public string displayName;
        [TextArea] public string description;

        [Header("Properties")]
        public AttributeType  attribute;
        public RarityType     rarity;
        public IngredientState defaultState;

        [Header("Source")]
        public string sourceMonsterIds;   // 파이프('|') 구분 MON ID 목록

        [Header("Visuals")]
        public Sprite sprite;
    }
}
