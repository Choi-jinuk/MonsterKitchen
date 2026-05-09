using UnityEngine;

namespace MonsterKitchen.Data
{
    [System.Serializable]
    public struct RecipeIngredient
    {
        public IngredientData ingredient;
        public int            quantity;
    }

    [CreateAssetMenu(menuName = "MonsterKitchen/Data/RecipeData", fileName = "RCP_")]
    public class RecipeData : ScriptableObject
    {
        [Header("Identity")]
        public string id;           // RCP_001
        public string displayName;
        [TextArea] public string description;

        [Header("Recipe")]
        public RecipeIngredient[] ingredients;
        public FoodData           resultFood;
        public int                cookTimeSeconds;

        [Header("Unlock")]
        public int  unlockDay;
        public bool isUnlockedByDefault;
    }
}
