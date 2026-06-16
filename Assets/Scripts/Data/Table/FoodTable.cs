using System;
using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.Localization;

namespace MonsterKitchen.Data
{
    [Serializable]
    public class FoodData
    {
        [Header("Identity")]
        public uint   Id;
        [Tooltip("로컬라이제이션 키. 예: FOOD_001_NAME → StringData 조회")]
        public string NameKey;
        [Tooltip("로컬라이제이션 키. 예: FOOD_001_DESC → StringData 조회")]
        public string DescKey;

        [NonSerialized] LocalizedString m_NameLs;
        [NonSerialized] LocalizedString m_DescLs;

        /// <summary>현재 언어에 맞는 이름. 미등록 키 → NameKey 반환.</summary>
        public string DisplayName => Loc.Get(ref m_NameLs, NameKey);
        /// <summary>현재 언어에 맞는 설명. 미등록 키 → DescKey 반환.</summary>
        public string Description => Loc.Get(ref m_DescLs, DescKey);

        [Header("Stats")]
        public int           BasePrice;
        public int           HpRestore;
        public AttributeType BuffAttribute;
        public float         BuffMultiplier;
        public int           BuffDurationDays;

        [Header("Grade Multipliers")]
        public float GoodMultiplier      = 1.2f;
        public float PerfectMultiplier   = 1.5f;
        public float LegendaryMultiplier = 2.0f;

        [Header("Assets — AssetManifest 등록 키")]
        [Tooltip("스프라이트 주소. 형식: sprite/food/{id}")]
        public string SpriteAddress;
    }

    [Serializable]
    public class FoodTable : DataTable<FoodData> { }
}
