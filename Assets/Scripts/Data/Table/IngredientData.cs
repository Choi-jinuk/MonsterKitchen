using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    [Serializable]
    public class IngredientData
    {
        [Header("Identity")]
        public uint   Id;
        public string DisplayName;
        [TextArea] public string Description;

        [Header("Properties")]
        public AttributeType   Attribute;
        public RarityType      Rarity;
        public IngredientState DefaultState;

        [Header("Source")]
        public string SourceMonsterIds;   // 파이프('|') 구분 MON ID 목록

        [Header("Assets — AssetManifest 등록 키")]
        [Tooltip("스프라이트 주소. 형식: sprite/ingredient/{id}")]
        public string SpriteAddress;
    }
}
