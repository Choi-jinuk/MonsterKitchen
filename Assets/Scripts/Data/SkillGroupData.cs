using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  SkillGroupData — 하나의 스킬 단위 ScriptableObject
    //
    //  UI에 표시되는 스킬 아이콘·이름·설명을 소유하고,
    //  실제 타격 데이터(SkillData) 체인을 묶는 컨테이너 역할.
    //
    //  ▶ 평타(normalAttackGroup)
    //    WeaponData 가 소유. 플레이어가 직접 선택할 수 없다.
    //    자동으로 체인을 순환하며 발동된다.
    //
    //  ▶ 액티브 스킬 (슬롯 1·2) / 궁극기
    //    플레이어가 슬롯에 직접 장착.
    //    allowedWeaponTypes 가 비어있으면 모든 무기에서 사용 가능(공용 스킬).
    //    값이 있으면 현재 무기 타입이 리스트 안에 있어야 장착 가능.
    //
    //  메뉴: Create → MonsterKitchen → Data → SkillGroupData
    //  파일명 규칙: SGD_001, SGD_002 …
    // ====================================================================

    [CreateAssetMenu(menuName = "MonsterKitchen/Data/SkillGroupData", fileName = "SGD_")]
    public class SkillGroupData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("스킬 그룹 고유 ID. 예: SGD_001")]
        public string skillGroupId;

        [Header("UI")]
        [Tooltip("스킬 슬롯에 표시될 아이콘 스프라이트.")]
        public Sprite skillIcon;

        [Tooltip("UI에 표시되는 스킬 이름.")]
        public string skillName;

        [Tooltip("스킬 설명 텍스트.")]
        [TextArea(2, 4)]
        public string description;

        [Header("Chain")]
        [Tooltip("콤보 체인. 리스트 순서대로 1타→2타→3타… 실행된다.")]
        public List<SkillData> skillChain = new List<SkillData>();

        [Header("Cooldown (액티브 스킬·궁극기 전용)")]
        [Tooltip("체인 전체 완료 후 이 스킬이 다시 사용 가능해질 때까지의 시간 (초).\n" +
                 "평타(normalAttackGroup)로 사용될 때는 무시된다.")]
        [Min(0f)]
        public float skillCooltime = 3f;

        [Header("Weapon Restriction")]
        [Tooltip("장착 가능한 무기 타입 목록.\n" +
                 "비어있으면 모든 무기에서 사용 가능 (공용 스킬).\n" +
                 "값이 있으면 해당 무기 타입을 장착해야만 이 스킬을 슬롯에 등록할 수 있다.")]
        public List<WeaponType> allowedWeaponTypes = new List<WeaponType>();

        // ── 편의 메서드 ─────────────────────────────────────────────────

        /// <summary>현재 장착된 무기 타입과 이 스킬이 호환되는지 확인한다.</summary>
        public bool IsCompatibleWith(WeaponType weaponType)
        {
            if (allowedWeaponTypes == null || allowedWeaponTypes.Count == 0)
                return true;   // 공용 스킬
            return allowedWeaponTypes.Contains(weaponType);
        }

        /// <summary>체인에서 지정 인덱스의 SkillData 를 반환한다. 범위 초과 시 마지막 반환.</summary>
        public SkillData GetStep(int index)
        {
            if (skillChain == null || skillChain.Count == 0) return null;
            return skillChain[Mathf.Clamp(index, 0, skillChain.Count - 1)];
        }

        public int ChainLength => skillChain?.Count ?? 0;
    }
}
