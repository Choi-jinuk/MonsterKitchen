using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  PlayerCharData — 플레이어 캐릭터 정의 (순수 CSV 직렬화 클래스)
    //
    //  TableData.Players 에 등록되고 CSV 에서 동기화된다.
    //  (구 이름: PlayerData — 런타임 게임플레이 상태 클래스와 구분하기 위해 변경)
    //
    //  ▶ 프리팹·스킬은 주소/ID 로 간접 참조한다.
    //    prefabAddress   → AssetManifest 키로 런타임에 로드
    //    skillGroupId1/2 → DataRegistry.FindSkillGroupByStringId 로 런타임에 조회
    //
    //  ▶ 3-레이어 구조에서의 위치
    //    TableData(PlayerCharData) — 정적 캐릭터 정의 (이 클래스)    ← 현재
    //    PlayerData                — 런타임 게임플레이 상태
    //    ServerSaveData            — 영속 직렬화
    //
    //  ID 범위: PLR_001 = 9001 ~
    // ====================================================================

    [Serializable]
    public class PlayerCharData
    {
        [Header("Identity")]
        [Tooltip("플레이어 uint ID. DataTable 키. 예: 9001")]
        public uint Id;

        [Tooltip("캐릭터 이름. 예: 기본 플레이어")]
        public string DisplayName;

        [Header("Assets — AssetManifest 등록 키")]
        [Tooltip("플레이어 프리팹 주소. 형식: prefab/player/{id}")]
        public string PrefabAddress;

        [Tooltip("BehaviorTree 에셋 주소. 형식: bt/player/{id}. 빈칸이면 BT 로드 안 함.")]
        public string BtAssetAddress;

        [Header("기본 스탯")]
        [Tooltip("기본 최대 체력. 장비 AbilEntry(MaxHp) 가 합산된다.")]
        public int BaseMaxHp;

        [Tooltip("기본 공격력. 장비 AbilEntry(Attack) 가 합산된다.")]
        public int BaseAttack;

        [Tooltip("기본 이동 속도. 장비 AbilEntry(Speed) 가 합산된다.")]
        public float BaseMoveSpeed;

        [Tooltip("기본 방어력. 장비 AbilEntry(Defense) 가 합산된다.")]
        public int BaseDefense;

        [Tooltip("이 캐릭터의 기본 속성.")]
        public AttributeType AttackAttribute;

        [Header("기본 장착")]
        [Tooltip("게임 시작 시 장착되는 무기 ID (TableData.Weapons 키). 0 이면 무기 없이 시작.")]
        public uint DefaultWeaponId;

        [Header("기본 스킬 — SkillGroups 테이블 참조 (문자열 ID)")]
        [Tooltip("스킬 슬롯 1 SkillGroupData 의 skillGroupId. 예: SGD_010")]
        public string SkillGroupId1;

        [Tooltip("스킬 슬롯 2 SkillGroupData 의 skillGroupId. 예: SGD_011")]
        public string SkillGroupId2;

        [Tooltip("궁극기 슬롯 SkillGroupData 의 skillGroupId. 예: SGD_020")]
        public string UltimateSkillGroupId;

        [Header("궁극기 게이지")]
        [Tooltip("궁극기 게이지 최대값.")]
        public float MaxUltimateGauge;

        [Tooltip("피격 시 채워지는 게이지량.")]
        public float GaugeOnHit;

        [Tooltip("처치 시 채워지는 게이지량.")]
        public float GaugeOnKill;

        [Header("클래스/희귀도")]
        [Tooltip("클래스 = 장착 가능 무기 타입 (1:1). 이 타입 무기만 장착 가능.")]
        public WeaponType CharClass;

        [Tooltip("태생 성급 1~3. 뽑기 풀 배정·획득 시 초기 성급.")]
        [Range(1, 3)]
        public int NatalStars = 1;

        [Header("작업 스탯 — 근무지 배치 모듈(후속)에서 적용")]
        [Tooltip("주방 배치 시 요리 속도 배율 기준 (1.0 = 표준).")]
        public float CookSpeed = 1f;

        [Tooltip("식당 배치 시 서빙/보조 속도 배율 기준 (1.0 = 표준).")]
        public float ServeSpeed = 1f;
    }

    [Serializable]
    public class PlayerCharTable : DataTable<PlayerCharData> { }
}
