using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    [Serializable]
    public class FoodData
    {
        [Header("Identity")]
        public uint   Id;
        public string DisplayName;
        [TextArea] public string Description;

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
}
