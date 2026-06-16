using System;
using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.Localization;

namespace MonsterKitchen.Data
{
    [Serializable]
    public class IngredientData
    {
        [Header("Identity")]
        public uint   Id;
        [Tooltip("로컬라이제이션 키. 예: ING_001_NAME → StringData 조회")]
        public string NameKey;
        [Tooltip("로컬라이제이션 키. 예: ING_001_DESC → StringData 조회")]
        public string DescKey;

        [NonSerialized] LocalizedString m_NameLs;
        [NonSerialized] LocalizedString m_DescLs;

        /// <summary>현재 언어에 맞는 이름. 미등록 키 → NameKey 반환.</summary>
        public string DisplayName => Loc.Get(ref m_NameLs, NameKey);
        /// <summary>현재 언어에 맞는 설명. 미등록 키 → DescKey 반환.</summary>
        public string Description => Loc.Get(ref m_DescLs, DescKey);

        [Header("Properties")]
        public AttributeType   Attribute;
        public RarityType      Rarity;
        public IngredientState DefaultState;

        [Header("Source")]
        public string SourceMonsterIds;   // 파이프('|') 구분 MON ID 목록

        [Header("Assets — AssetManifest 등록 키")]
        [Tooltip("스프라이트 주소. 형식: sprite/ingredient/{id}")]
        public string SpriteAddress;

        [Header("Dungeon Bag")]
        [Tooltip("던전 가방 무게. 기본값 1.")]
        public int Weight = 1;
    }

    [Serializable]
    public class IngredientTable : DataTable<IngredientData> { }
}
