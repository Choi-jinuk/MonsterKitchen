using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  PlayerSpawnData — 플레이어 캐릭터 기본 정의 ScriptableObject
    //
    //  씬마다 플레이어를 직접 배치하지 않고, 이 SO 를 읽어
    //  PlayerManager 가 플레이어를 동적으로 소환·초기화한다.
    //
    //  ▶ 역할 분리
    //    PlayerSpawnData : 캐릭터 고유 기본 스탯 (HP, 이동 속도, 기본 공격력)
    //    WeaponData      : 무기 스탯 기여 (abils) + 평타 체인
    //    SkillGroupData  : 액티브 스킬·궁극기 정의
    //
    //  메뉴: Create → MonsterKitchen → Player Spawn Data
    // ====================================================================

    [CreateAssetMenu(menuName = "MonsterKitchen/Player Spawn Data", fileName = "PlayerSpawnData")]
    public class PlayerSpawnData : ScriptableObject
    {
        [Tooltip("플레이어 프리팹.\n프리팹 루트는 비활성화 상태로 두어야 Init() 전에 Awake 가 실행되지 않는다.")]
        public Player.PlayerController prefab;

        [Header("캐릭터 기본 스탯")]
        [Tooltip("기본 최대 체력. 장비 AbilEntry(MaxHp)가 여기에 합산된다.")]
        [Min(1)] public int   baseMaxHp      = 100;

        [Tooltip("기본 공격력. 장비 AbilEntry(Attack)가 여기에 합산된다.")]
        [Min(1)] public int   baseAttack     = 10;

        [Tooltip("기본 이동 속도. 장비 AbilEntry(Speed)가 여기에 합산된다.")]
        [Min(0.1f)] public float baseMoveSpeed = 5f;

        [Tooltip("기본 방어력. 장비 AbilEntry(Defense)가 여기에 합산된다.")]
        [Min(0)] public int   baseDefense    = 0;

        [Tooltip("이 캐릭터의 기본 속성.")]
        public AttributeType attackAttribute = AttributeType.None;

        [Header("기본 장착")]
        [Tooltip("게임 시작 시 기본으로 장착되는 무기.\nnull 이면 무기 없이 시작한다.")]
        public WeaponData defaultWeapon;

        [Tooltip("기본으로 슬롯 1에 장착되는 스킬 (무기 호환 체크 적용).")]
        public SkillGroupData defaultSkill1;

        [Tooltip("기본으로 슬롯 2에 장착되는 스킬 (무기 호환 체크 적용).")]
        public SkillGroupData defaultSkill2;

        [Tooltip("기본으로 장착되는 궁극기 스킬.")]
        public SkillGroupData defaultUltimate;

        [Header("궁극기 게이지")]
        [Tooltip("궁극기 게이지 최대값.")]
        [Min(1f)] public float maxUltimateGauge = 100f;

        [Tooltip("피격 시 채워지는 게이지량.")]
        [Min(0f)] public float gaugeOnHit = 15f;

        [Tooltip("처치 시 채워지는 게이지량.")]
        [Min(0f)] public float gaugeOnKill = 25f;
    }
}
