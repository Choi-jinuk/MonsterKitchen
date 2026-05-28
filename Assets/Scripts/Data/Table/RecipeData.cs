using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    [Serializable]
    public struct RecipeIngredient
    {
        [Tooltip("재료 ID (TableData.Ingredients 키)")]
        public uint IngredientId;
        public int  Quantity;
    }

    [Serializable]
    public class RecipeData
    {
        [Header("Identity")]
        public uint   Id;
        public string DisplayName;
        [TextArea] public string Description;

        [Header("Recipe")]
        public RecipeIngredient[] Ingredients;

        [Tooltip("완성 음식 ID (TableData.Foods 키)")]
        public uint ResultFoodId;
        public int  CookTimeSeconds;

        [Header("Unlock")]
        public int  UnlockDay;
        public bool IsUnlockedByDefault;
    }
}
