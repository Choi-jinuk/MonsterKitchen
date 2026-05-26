using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    [Serializable]
    public class IngredientData
    {
        [Header("Identity")]
        public uint   id;
        public string displayName;
        [TextArea] public string description;

        [Header("Properties")]
        public AttributeType   attribute;
        public RarityType      rarity;
        public IngredientState defaultState;

        [Header("Source")]
        public string sourceMonsterIds;   // 파이프('|') 구분 MON ID 목록

        [Header("Assets — AssetManifest 등록 키")]
        [Tooltip("스프라이트 주소. 형식: sprite/ingredient/{id}")]
        public string spriteAddress;
    }
}
