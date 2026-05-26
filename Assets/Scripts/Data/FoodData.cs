using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    [Serializable]
    public class FoodData
    {
        [Header("Identity")]
        public uint   id;
        public string displayName;
        [TextArea] public string description;

        [Header("Stats")]
        public int           basePrice;
        public int           hpRestore;
        public AttributeType buffAttribute;
        public float         buffMultiplier;
        public int           buffDurationDays;

        [Header("Grade Multipliers")]
        public float goodMultiplier      = 1.2f;
        public float perfectMultiplier   = 1.5f;
        public float legendaryMultiplier = 2.0f;

        [Header("Assets — AssetManifest 등록 키")]
        [Tooltip("스프라이트 주소. 형식: sprite/food/{id}")]
        public string spriteAddress;
    }
}
