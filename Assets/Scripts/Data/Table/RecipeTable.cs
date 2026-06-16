using System;
using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.Localization;

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
        [Tooltip("로컬라이제이션 키. 예: RCP_001_NAME → StringData 조회")]
        public string NameKey;
        [Tooltip("로컬라이제이션 키. 예: RCP_001_DESC → StringData 조회")]
        public string DescKey;

        [NonSerialized] LocalizedString m_NameLs;
        [NonSerialized] LocalizedString m_DescLs;

        /// <summary>현재 언어에 맞는 이름. 미등록 키 → NameKey 반환.</summary>
        public string DisplayName => Loc.Get(ref m_NameLs, NameKey);
        /// <summary>현재 언어에 맞는 설명. 미등록 키 → DescKey 반환.</summary>
        public string Description => Loc.Get(ref m_DescLs, DescKey);

        [Header("Recipe")]
        public RecipeIngredient[] Ingredients;

        [Tooltip("완성 음식 ID (TableData.Foods 키)")]
        public uint ResultFoodId;
        public int  CookTimeSeconds;

        [Header("Unlock")]
        public int  UnlockDay;
        public bool IsUnlockedByDefault;
    }

    [Serializable]
    public class RecipeTable : DataTable<RecipeData> { }
}
