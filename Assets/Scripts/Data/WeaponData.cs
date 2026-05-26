using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  WeaponData — 무기 정의
    //
    //  ▶ abils
    //    장착 시 플레이어 기본 스탯에 더해지는 수치 목록.
    //
    //  ▶ normalAttackGroup
    //    이 무기의 평타 체인. 장착 시 자동으로 평타로 사용된다.
    // ====================================================================

    [Serializable]
    public class WeaponData
    {
        [Header("Identity")]
        [Tooltip("무기 고유 ID")]
        public uint   weaponId;

        [Tooltip("표시 이름. 예: 낡은 검")]
        public string weaponName;

        [Tooltip("무기 타입. 스킬 호환 체크에 사용된다.")]
        public WeaponType weaponType;

        [Header("Stats (AbilEntry)")]
        [Tooltip("이 무기 장착 시 플레이어 스탯에 더해지는 수치 목록.")]
        public List<AbilEntry> abils = new();

        [Header("Durability")]
        [Tooltip("최대 내구도. 0이면 파괴되지 않는 영구 무기.")]
        [Min(0)]
        public int maxDurability = 0;

        [Tooltip("내구도 소모 방식.")]
        public DurabilityDecayMode decayMode = DurabilityDecayMode.None;

        [Header("Normal Attack")]
        [Tooltip("이 무기의 평타 체인 SkillGroupData.")]
        public SkillGroupData normalAttackGroup;

        [Header("Assets — AssetManifest 등록 키")]
        [Tooltip("무기 스프라이트 주소. 형식: sprite/weapon/{id}")]
        public string weaponSpriteAddress;

        [Tooltip("무기 AnimatorController 주소. 형식: anim/weapon/{id}")]
        public string weaponAnimAddress;
    }

    // ----------------------------------------------------------------
    //  DurabilityDecayMode — 내구도 소모 방식
    // ----------------------------------------------------------------
    public enum DurabilityDecayMode
    {
        None    = 0,
        PerHit  = 1,
        PerKill = 2,
    }
}
