using UnityEngine;

namespace MonsterKitchen.Data
{
    [CreateAssetMenu(menuName = "MonsterKitchen/Data/FoodData", fileName = "FOOD_")]
    public class FoodData : ScriptableObject
    {
        [Header("Identity")]
        public string id;           // FOOD_001
        public string displayName;
        [TextArea] public string description;

        [Header("Stats")]
        public int   basePrice;
        public int   hpRestore;
        public AttributeType buffAttribute;
        public float buffMultiplier;
        public int   buffDurationDays;

        [Header("Grade Multipliers")]
        public float goodMultiplier      = 1.2f;
        public float perfectMultiplier   = 1.5f;
        public float legendaryMultiplier = 2.0f;

        [Header("Visuals")]
        public Sprite sprite;
    }
}
