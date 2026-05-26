using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    [Serializable]
    public struct RecipeIngredient
    {
        [Tooltip("재료 ID (TableData.Ingredients 키)")]
        public uint ingredientId;
        public int  quantity;
    }

    [Serializable]
    public class RecipeData
    {
        [Header("Identity")]
        public uint   id;
        public string displayName;
        [TextArea] public string description;

        [Header("Recipe")]
        public RecipeIngredient[] ingredients;

        [Tooltip("완성 음식 ID (TableData.Foods 키)")]
        public uint resultFoodId;
        public int  cookTimeSeconds;

        [Header("Unlock")]
        public int  unlockDay;
        public bool isUnlockedByDefault;
    }
}
