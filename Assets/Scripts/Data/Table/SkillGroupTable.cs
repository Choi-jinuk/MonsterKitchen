using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  SkillGroupData — 하나의 스킬 단위 (콤보 체인 컨테이너)
    //
    //  UI에 표시되는 스킬 아이콘·이름·설명을 소유하고,
    //  실제 타격 데이터(SkillData) 체인을 묶는 컨테이너 역할.
    // ====================================================================

    [Serializable]
    public class SkillGroupData
    {
        [Header("Identity")]
        [Tooltip("스킬 그룹 uint ID. DataTable 키. 예: 10001")]
        public uint Id;

        [Tooltip("스킬 그룹 문자열 ID. 예: SGD_001")]
        public string SkillGroupId;

        [Header("Assets — AssetManifest 등록 키")]
        [Tooltip("스킬 아이콘 주소. 형식: sprite/skill/{skillGroupId}")]
        public string SkillIconAddress;

        [Tooltip("UI에 표시되는 스킬 이름.")]
        public string SkillName;

        [Tooltip("스킬 설명 텍스트.")]
        [TextArea(2, 4)]
        public string Description;

        [Header("Chain")]
        [Tooltip("CSV 파이프라인용 스텝 ID 목록 (파이프 구분). 예: 11001|11002|11003\n" +
                 "DataManagerWindow Sync 시 skillChain 으로 변환된다.")]
        public string[] StepIds = Array.Empty<string>();

        [Tooltip("콤보 체인. 리스트 순서대로 1타→2타→3타… 실행된다.\n" +
                 "DataManagerWindow Sync 시 stepIds 를 기반으로 자동 채워진다.")]
        public List<SkillData> SkillChain = new();

        [Header("Cooldown (액티브 스킬·궁극기 전용)")]
        [Tooltip("체인 전체 완료 후 이 스킬이 다시 사용 가능해질 때까지의 시간 (초).\n" +
                 "평타(normalAttackGroup)로 사용될 때는 무시된다.")]
        [Min(0f)]
        public float SkillCooltime = 3f;

        [Header("Weapon Restriction")]
        [Tooltip("장착 가능한 무기 타입 목록.\n" +
                 "비어있으면 모든 무기에서 사용 가능 (공용 스킬).\n" +
                 "값이 있으면 해당 무기 타입을 장착해야만 이 스킬을 슬롯에 등록할 수 있다.")]
        public List<WeaponType> AllowedWeaponTypes = new();

        // ── 편의 메서드 ─────────────────────────────────────────────────

        /// <summary>현재 장착된 무기 타입과 이 스킬이 호환되는지 확인한다.</summary>
        public bool IsCompatibleWith(WeaponType weaponType)
        {
            if (AllowedWeaponTypes == null || AllowedWeaponTypes.Count == 0)
                return true;
            return AllowedWeaponTypes.Contains(weaponType);
        }

        /// <summary>체인에서 지정 인덱스의 SkillData 를 반환한다. 범위 초과 시 마지막 반환.</summary>
        public SkillData GetStep(int index)
        {
            if (SkillChain == null || SkillChain.Count == 0) return null;
            return SkillChain[Mathf.Clamp(index, 0, SkillChain.Count - 1)];
        }

        public int ChainLength => SkillChain?.Count ?? 0;
    }
    
    [Serializable]
    public class SkillGroupTable : DataTable<SkillGroupData>
    {
        // ── 캐시 — 문자열 ID 역방향 조회 ────────────────────────────────
        Dictionary<string, SkillGroupData> m_ByStringId;

        public override void RuntimeSetData()
        {
            m_ByStringId = new Dictionary<string, SkillGroupData>();
            foreach (var sg in All)
                if (!string.IsNullOrEmpty(sg.SkillGroupId))
                    m_ByStringId[sg.SkillGroupId] = sg;
        }

        /// <summary>SkillGroupId 문자열(예: "SGD_001")로 조회. O(1).</summary>
        public SkillGroupData FindByStringId(string skillGroupId)
        {
            if (string.IsNullOrEmpty(skillGroupId) || m_ByStringId == null) return null;
            return m_ByStringId.GetValueOrDefault(skillGroupId);
        }
    }
}
