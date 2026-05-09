using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  WeaponData — 무기 정의 ScriptableObject
    //
    //  ▶ 역할 분리
    //    PlayerSpawnData : 캐릭터 고유 수치 (기본 HP, 이동 속도 등)
    //    WeaponData      : 무기 고유 수치 (스탯 기여 + 평타 체인)
    //
    //  ▶ abils
    //    장착 시 플레이어 기본 스탯에 더해지는 수치 목록.
    //    무기라도 방어력 스탯을 가질 수 있다.
    //    예) [(Attack, +15), (Defense, +3), (Speed, +0.2)]
    //
    //  ▶ normalAttackGroup
    //    이 무기의 평타 체인. 장착 시 자동으로 평타로 사용된다.
    //    플레이어가 별도로 선택할 수 없다.
    //
    //  메뉴: Create → MonsterKitchen → Data → WeaponData
    //  파일명 규칙: WPN_001, WPN_002 …
    // ====================================================================

    [CreateAssetMenu(menuName = "MonsterKitchen/Data/WeaponData", fileName = "WPN_")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("무기 고유 ID. 예: WPN_001")]
        public string weaponId;

        [Tooltip("표시 이름. 예: 낡은 검")]
        public string weaponName;

        [Tooltip("무기 타입. 스킬 호환 체크에 사용된다.")]
        public WeaponType weaponType;

        [Header("Stats (AbilEntry)")]
        [Tooltip("이 무기 장착 시 플레이어 스탯에 더해지는 수치 목록.\n" +
                 "공격력뿐 아니라 방어력·속도 등 어떤 AbilType 이든 가능하다.")]
        public List<AbilEntry> abils = new List<AbilEntry>();

        [Header("Normal Attack")]
        [Tooltip("이 무기의 평타 체인 SkillGroupData.\n" +
                 "무기 장착 시 자동으로 평타로 사용된다.")]
        public SkillGroupData normalAttackGroup;
    }
}
