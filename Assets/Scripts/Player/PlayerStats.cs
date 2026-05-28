using System;
using MonsterKitchen.Combat;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Player
{
    // ====================================================================
    //  PlayerStats — 플레이어 스탯·장착·궁극기 게이지 관리 컴포넌트
    //
    //  ▶ 스탯 계산
    //    FinalStat = 캐릭터 기본 스탯 + 장착 무기 abils 합산
    //    (확정 보류: 최종 계산식은 모듈 합산 단계에서 재검토)
    //
    //  ▶ 장착 슬롯
    //    무기 슬롯  : WeaponData → normalAttackGroup 평타 자동 적용
    //    스킬 슬롯1 : SkillGroupData (무기 타입 호환 체크)
    //    스킬 슬롯2 : SkillGroupData (무기 타입 호환 체크)
    //    궁극기 슬롯: SkillGroupData (무기 타입 호환 체크)
    //
    //  ▶ 궁극기 게이지
    //    피격(OnDamaged) 시 gaugeOnHit, 처치(AddUltimateGaugeOnKill) 시 gaugeOnKill 적립.
    //    MAX 도달 → OnUltimateReady 이벤트 → PlayerController 가 발동.
    // ====================================================================

    public class PlayerStats : MonoBehaviour
    {
        // ── 기본 스탯 (PlayerData 에서 초기화) ───────────────────────
        public int   BaseMaxHp      { get; private set; }
        public int   BaseAttack     { get; private set; }
        public float BaseMoveSpeed  { get; private set; }
        public int   BaseDefense    { get; private set; }
        public AttributeType AttackAttribute { get; private set; }

        // ── 최종 스탯 (기본 + 무기 abils 합산) ───────────────────────
        public int   FinalMaxHp      { get; private set; }
        public int   FinalAttack     { get; private set; }
        public float FinalMoveSpeed  { get; private set; }
        public int   FinalDefense    { get; private set; }
        public float FinalCritRate   { get; private set; }
        public float FinalCritDamage { get; private set; }
        public float FinalAttackSpeed{ get; private set; }  // 0.0 기준, + 가 빠름

        // ── 장착 슬롯 ─────────────────────────────────────────────────
        public WeaponData     EquippedWeapon { get; private set; }
        public SkillGroupData SkillSlot1     { get; private set; }
        public SkillGroupData SkillSlot2     { get; private set; }
        public SkillGroupData UltimateSlot   { get; private set; }

        /// <summary>현재 무기의 평타 체인. 무기 미장착 시 null.</summary>
        public SkillGroupData NormalAttackGroup => EquippedWeapon?.NormalAttackGroup;

        // ── 채집 도구 슬롯 ────────────────────────────────────────────
        [SerializeField] GatheringToolData m_GatheringTool;

        // ── 내구도 (런타임) ───────────────────────────────────────────
        int m_WeaponDurability;
        int m_GatheringToolDurability;

        public GatheringToolData GatheringTool           => m_GatheringTool;
        public int               WeaponDurability        => m_WeaponDurability;
        public int               GatheringToolDurability => m_GatheringToolDurability;

        // ── 스킬 쿨타임 ───────────────────────────────────────────────
        public float Skill1CoolRemaining { get; private set; }
        public float Skill2CoolRemaining { get; private set; }
        float m_Skill1CoolTotal;
        float m_Skill2CoolTotal;

        // ── 궁극기 게이지 ─────────────────────────────────────────────
        public float UltimateGauge    { get; private set; }
        public float MaxUltimateGauge { get; private set; }
        float m_GaugeOnHit;
        float m_GaugeOnKill;
        bool  m_UltimateReady;

        // ── 이벤트 ────────────────────────────────────────────────────
        /// <summary>무기 장착/해제 시. null 이면 해제.</summary>
        public event Action<WeaponData>          OnWeaponChanged;

        /// <summary>스킬 슬롯 변경 시. slot: 1·2·3(궁극기). null 이면 해제.</summary>
        public event Action<int, SkillGroupData> OnSkillChanged;

        /// <summary>스킬 쿨타임 변경 시. slot: 1·2. remaining, total.</summary>
        public event Action<int, float, float>   OnCooldownChanged;

        /// <summary>궁극기 게이지 변경 시. current, max.</summary>
        public event Action<float, float>        OnUltimateGaugeChanged;

        /// <summary>궁극기 게이지가 MAX 에 처음 도달했을 때.</summary>
        public event Action                      OnUltimateReady;

        [SerializeField] Health m_Health;

        // ================================================================
        //  Mono
        // ================================================================

        void OnEnable()
        {
            if (m_Health != null)
            {
                m_Health.OnDamaged += HandlePlayerDamaged;
                m_Health.OnDeath   += HandlePlayerDied;
            }
        }

        void OnDisable()
        {
            if (m_Health != null)
            {
                m_Health.OnDamaged -= HandlePlayerDamaged;
                m_Health.OnDeath   -= HandlePlayerDied;
            }
        }

        void Update()
        {
            TickCooldowns();
        }

        // ================================================================
        //  Init
        // ================================================================

        public void Init(PlayerCharData data)
        {
            if (data == null) return;

            BaseMaxHp       = data.BaseMaxHp;
            BaseAttack      = data.BaseAttack;
            BaseMoveSpeed   = data.BaseMoveSpeed;
            BaseDefense     = data.BaseDefense;
            AttackAttribute = data.AttackAttribute;

            MaxUltimateGauge = data.MaxUltimateGauge;
            m_GaugeOnHit     = data.GaugeOnHit;
            m_GaugeOnKill    = data.GaugeOnKill;
            UltimateGauge    = 0f;

            RecalculateStats();

            // 기본 장착 — 무기·스킬 모두 DataRegistry 를 통해 ID/문자열로 조회
            if (data.DefaultWeaponId != 0)
            {
                var weapon = DataRegistry.Instance?.GetWeapon(data.DefaultWeaponId);
                if (weapon != null) EquipWeapon(weapon);
                else Debug.LogWarning($"[PlayerStats] defaultWeaponId={data.DefaultWeaponId} 를 TableData 에서 찾지 못했습니다.");
            }

            var dr = DataRegistry.Instance;
            if (!string.IsNullOrEmpty(data.SkillGroupId1))
            {
                var sg = dr?.FindSkillGroupByStringId(data.SkillGroupId1);
                if (sg != null) EquipSkill(1, sg);
                else Debug.LogWarning($"[PlayerStats] skillGroupId1='{data.SkillGroupId1}' 를 SkillGroups 에서 찾지 못했습니다.");
            }
            if (!string.IsNullOrEmpty(data.SkillGroupId2))
            {
                var sg = dr?.FindSkillGroupByStringId(data.SkillGroupId2);
                if (sg != null) EquipSkill(2, sg);
                else Debug.LogWarning($"[PlayerStats] skillGroupId2='{data.SkillGroupId2}' 를 SkillGroups 에서 찾지 못했습니다.");
            }
            if (!string.IsNullOrEmpty(data.UltimateSkillGroupId))
            {
                var sg = dr?.FindSkillGroupByStringId(data.UltimateSkillGroupId);
                if (sg != null) EquipSkill(3, sg);
                else Debug.LogWarning($"[PlayerStats] ultimateSkillGroupId='{data.UltimateSkillGroupId}' 를 SkillGroups 에서 찾지 못했습니다.");
            }
        }

        // ================================================================
        //  장착
        // ================================================================

        /// <summary>무기를 장착한다. null 을 넣으면 해제.</summary>
        public void EquipWeapon(WeaponData weapon)
        {
            EquippedWeapon    = weapon;
            m_WeaponDurability = weapon != null ? weapon.MaxDurability : 0;
            RecalculateStats();
            OnWeaponChanged?.Invoke(weapon);
        }

        /// <summary>채집 도구를 장착한다. null 을 넣으면 해제.</summary>
        public void EquipGatheringTool(GatheringToolData tool)
        {
            m_GatheringTool           = tool;
            m_GatheringToolDurability = tool != null ? tool.MaxDurability : 0;
        }

        /// <summary>
        /// 스킬을 슬롯에 장착한다.
        /// slot: 1=스킬1, 2=스킬2, 3=궁극기.
        /// 무기 타입과 호환되지 않으면 false 반환(장착 거부).
        /// null 을 넣으면 해제(항상 성공).
        /// </summary>
        public bool EquipSkill(int slot, SkillGroupData skill)
        {
            // 호환 체크 (해제 시 생략)
            if (skill != null && EquippedWeapon != null)
            {
                if (!skill.IsCompatibleWith(EquippedWeapon.WeaponType))
                {
                    Debug.LogWarning($"[PlayerStats] 슬롯{slot}: '{skill.SkillName}' 은 " +
                                     $"{EquippedWeapon.WeaponType} 무기와 호환되지 않아 장착 거부.");
                    return false;
                }
            }

            switch (slot)
            {
                case 1: SkillSlot1   = skill; break;
                case 2: SkillSlot2   = skill; break;
                case 3: UltimateSlot = skill; break;
                default:
                    Debug.LogWarning($"[PlayerStats] 잘못된 슬롯 번호: {slot}");
                    return false;
            }

            OnSkillChanged?.Invoke(slot, skill);
            return true;
        }

        // ================================================================
        //  스킬 발동
        // ================================================================

        /// <summary>
        /// 스킬 슬롯 1 또는 2 발동 시도.
        /// 쿨타임 중이면 false. 성공 시 쿨타임 시작 후 true.
        /// out skill 에 해당 SkillGroupData 가 반환된다.
        /// </summary>
        public bool TryUseSkill(int slot, out SkillGroupData skill)
        {
            skill = slot == 1 ? SkillSlot1 : SkillSlot2;
            if (skill == null) return false;

            float remaining = slot == 1 ? Skill1CoolRemaining : Skill2CoolRemaining;
            if (remaining > 0f) return false;

            StartSkillCooldown(slot, skill.SkillCooltime);
            return true;
        }

        /// <summary>
        /// 궁극기 발동 시도. 게이지가 MAX 이어야 성공.
        /// 성공 시 게이지 초기화 후 true.
        /// </summary>
        public bool TryUseUltimate(out SkillGroupData skill)
        {
            skill = UltimateSlot;
            if (skill == null || !m_UltimateReady) return false;

            UltimateGauge = 0f;
            m_UltimateReady = false;
            OnUltimateGaugeChanged?.Invoke(UltimateGauge, MaxUltimateGauge);
            return true;
        }

        // ================================================================
        //  궁극기 게이지
        // ================================================================

        /// <summary>처치 시 PlayerController 에서 호출.</summary>
        public void AddUltimateGaugeOnKill() => AddGauge(m_GaugeOnKill);

        void HandlePlayerDamaged(int amount, AttributeType _) => AddGauge(m_GaugeOnHit);
        void HandlePlayerDied(AttributeType _) { }

        void AddGauge(float amount)
        {
            if (m_UltimateReady) return;   // 이미 MAX

            UltimateGauge = Mathf.Min(UltimateGauge + amount, MaxUltimateGauge);
            OnUltimateGaugeChanged?.Invoke(UltimateGauge, MaxUltimateGauge);

            if (!m_UltimateReady && UltimateGauge >= MaxUltimateGauge)
            {
                m_UltimateReady = true;
                OnUltimateReady?.Invoke();
                Debug.Log("[PlayerStats] 궁극기 게이지 MAX — 궁극기 사용 가능");
            }
        }

        // ================================================================
        //  내구도
        // ================================================================

        /// <summary>공격 1회 시 PlayerController 에서 호출. PerHit 무기만 소모.</summary>
        public void ConsumeWeaponDurabilityOnHit()
        {
            if (EquippedWeapon == null || EquippedWeapon.MaxDurability == 0) return;
            if (EquippedWeapon.DecayMode != DurabilityDecayMode.PerHit) return;
            m_WeaponDurability = Mathf.Max(0, m_WeaponDurability - 1);
            if (m_WeaponDurability == 0)
                Debug.Log($"[PlayerStats] 무기 '{EquippedWeapon.WeaponName}' 내구도 소진");
        }

        /// <summary>처치 시 PlayerController 에서 호출. PerKill 무기만 소모.</summary>
        public void ConsumeWeaponDurabilityOnKill()
        {
            if (EquippedWeapon == null || EquippedWeapon.MaxDurability == 0) return;
            if (EquippedWeapon.DecayMode != DurabilityDecayMode.PerKill) return;
            m_WeaponDurability = Mathf.Max(0, m_WeaponDurability - 1);
            if (m_WeaponDurability == 0)
                Debug.Log($"[PlayerStats] 무기 '{EquippedWeapon.WeaponName}' 내구도 소진");
        }

        /// <summary>채집 성공 시 ResourceNode 에서 호출.</summary>
        public void ConsumeGatheringToolDurability()
        {
            if (m_GatheringTool == null || m_GatheringTool.MaxDurability == 0) return;
            m_GatheringToolDurability = Mathf.Max(0, m_GatheringToolDurability - 1);
            if (m_GatheringToolDurability == 0)
                Debug.Log($"[PlayerStats] 채집 도구 '{m_GatheringTool.ToolName}' 내구도 소진");
        }

        // ================================================================
        //  스탯 계산
        // ================================================================

        /// <summary>무기 Abil 을 합산해 Final 스탯을 갱신한다.</summary>
        void RecalculateStats()
        {
            // 기본값 복사
            FinalMaxHp      = BaseMaxHp;
            FinalAttack     = BaseAttack;
            FinalMoveSpeed  = BaseMoveSpeed;
            FinalDefense    = BaseDefense;
            FinalCritRate   = 0f;
            FinalCritDamage = 0f;
            FinalAttackSpeed= 0f;

            // 무기 abils 합산
            if (EquippedWeapon != null)
            {
                foreach (var abil in EquippedWeapon.Abils)
                {
                    switch (abil.AbilType)
                    {
                        case AbilType.Attack:      FinalAttack      += Mathf.RoundToInt(abil.Value); break;
                        case AbilType.Defense:     FinalDefense     += Mathf.RoundToInt(abil.Value); break;
                        case AbilType.MaxHp:       FinalMaxHp       += Mathf.RoundToInt(abil.Value); break;
                        case AbilType.Speed:       FinalMoveSpeed   += abil.Value; break;
                        case AbilType.AttackSpeed: FinalAttackSpeed += abil.Value; break;
                        case AbilType.CritRate:    FinalCritRate    += abil.Value; break;
                        case AbilType.CritDamage:  FinalCritDamage  += abil.Value; break;
                    }
                }
            }

            // Health 컴포넌트 MaxHp 동기화
            if (m_Health != null)
                m_Health.SetMaxHp(FinalMaxHp);
        }

        // ================================================================
        //  쿨타임 틱
        // ================================================================

        void TickCooldowns()
        {
            if (Skill1CoolRemaining > 0f)
            {
                Skill1CoolRemaining = Mathf.Max(0f, Skill1CoolRemaining - Time.deltaTime);
                OnCooldownChanged?.Invoke(1, Skill1CoolRemaining, m_Skill1CoolTotal);
            }
            if (Skill2CoolRemaining > 0f)
            {
                Skill2CoolRemaining = Mathf.Max(0f, Skill2CoolRemaining - Time.deltaTime);
                OnCooldownChanged?.Invoke(2, Skill2CoolRemaining, m_Skill2CoolTotal);
            }
        }

        void StartSkillCooldown(int slot, float duration)
        {
            if (slot == 1) { Skill1CoolRemaining = duration; m_Skill1CoolTotal = duration; }
            else           { Skill2CoolRemaining = duration; m_Skill2CoolTotal = duration; }
            OnCooldownChanged?.Invoke(slot, duration, duration);
        }

#if UNITY_EDITOR
        void Reset()
        {
            m_Health = GetComponent<Health>();
        }
#endif
    }
}
