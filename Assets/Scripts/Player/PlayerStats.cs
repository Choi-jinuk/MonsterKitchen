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
        // ── 기본 스탯 (PlayerSpawnData 에서 초기화) ───────────────────
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
        public SkillGroupData NormalAttackGroup => EquippedWeapon?.normalAttackGroup;

        // ── 스킬 쿨타임 ───────────────────────────────────────────────
        public float Skill1CoolRemaining { get; private set; }
        public float Skill2CoolRemaining { get; private set; }
        float _skill1CoolTotal;
        float _skill2CoolTotal;

        // ── 궁극기 게이지 ─────────────────────────────────────────────
        public float UltimateGauge    { get; private set; }
        public float MaxUltimateGauge { get; private set; }
        float _gaugeOnHit;
        float _gaugeOnKill;
        bool  _ultimateReady;

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

        [SerializeField] Health _health;

        // ================================================================
        //  Mono
        // ================================================================

        void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDamaged += HandlePlayerDamaged;
                _health.OnDeath   += HandlePlayerDied;
            }
        }

        void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamaged -= HandlePlayerDamaged;
                _health.OnDeath   -= HandlePlayerDied;
            }
        }

        void Update()
        {
            TickCooldowns();
        }

        // ================================================================
        //  Init
        // ================================================================

        public void Init(PlayerSpawnData data)
        {
            if (data == null) return;

            BaseMaxHp      = data.baseMaxHp;
            BaseAttack     = data.baseAttack;
            BaseMoveSpeed  = data.baseMoveSpeed;
            BaseDefense    = data.baseDefense;
            AttackAttribute = data.attackAttribute;

            MaxUltimateGauge = data.maxUltimateGauge;
            _gaugeOnHit      = data.gaugeOnHit;
            _gaugeOnKill     = data.gaugeOnKill;
            UltimateGauge    = 0f;

            RecalculateStats();

            // 기본 장착
            if (data.defaultWeapon != null)   EquipWeapon(data.defaultWeapon);
            if (data.defaultSkill1 != null)   EquipSkill(1, data.defaultSkill1);
            if (data.defaultSkill2 != null)   EquipSkill(2, data.defaultSkill2);
            if (data.defaultUltimate != null) EquipSkill(3, data.defaultUltimate);
        }

        // ================================================================
        //  장착
        // ================================================================

        /// <summary>무기를 장착한다. null 을 넣으면 해제.</summary>
        public void EquipWeapon(WeaponData weapon)
        {
            EquippedWeapon = weapon;
            RecalculateStats();
            OnWeaponChanged?.Invoke(weapon);
            Debug.Log(weapon != null
                ? $"[PlayerStats] 무기 장착: {weapon.weaponName} ({weapon.weaponType})"
                : "[PlayerStats] 무기 해제");
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
                if (!skill.IsCompatibleWith(EquippedWeapon.weaponType))
                {
                    Debug.LogWarning($"[PlayerStats] 슬롯{slot}: '{skill.skillName}' 은 " +
                                     $"{EquippedWeapon.weaponType} 무기와 호환되지 않아 장착 거부.");
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
            Debug.Log(skill != null
                ? $"[PlayerStats] 슬롯{slot} 장착: {skill.skillName}"
                : $"[PlayerStats] 슬롯{slot} 해제");
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

            StartSkillCooldown(slot, skill.skillCooltime);
            return true;
        }

        /// <summary>
        /// 궁극기 발동 시도. 게이지가 MAX 이어야 성공.
        /// 성공 시 게이지 초기화 후 true.
        /// </summary>
        public bool TryUseUltimate(out SkillGroupData skill)
        {
            skill = UltimateSlot;
            if (skill == null || !_ultimateReady) return false;

            UltimateGauge = 0f;
            _ultimateReady = false;
            OnUltimateGaugeChanged?.Invoke(UltimateGauge, MaxUltimateGauge);
            return true;
        }

        // ================================================================
        //  궁극기 게이지
        // ================================================================

        /// <summary>처치 시 PlayerController 에서 호출.</summary>
        public void AddUltimateGaugeOnKill() => AddGauge(_gaugeOnKill);

        void HandlePlayerDamaged(int amount, AttributeType _) => AddGauge(_gaugeOnHit);
        void HandlePlayerDied(AttributeType _) { }

        void AddGauge(float amount)
        {
            if (_ultimateReady) return;   // 이미 MAX

            UltimateGauge = Mathf.Min(UltimateGauge + amount, MaxUltimateGauge);
            OnUltimateGaugeChanged?.Invoke(UltimateGauge, MaxUltimateGauge);

            if (!_ultimateReady && UltimateGauge >= MaxUltimateGauge)
            {
                _ultimateReady = true;
                OnUltimateReady?.Invoke();
                Debug.Log("[PlayerStats] 궁극기 게이지 MAX — 궁극기 사용 가능");
            }
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
                foreach (var abil in EquippedWeapon.abils)
                {
                    switch (abil.abilType)
                    {
                        case AbilType.Attack:      FinalAttack      += Mathf.RoundToInt(abil.value); break;
                        case AbilType.Defense:     FinalDefense     += Mathf.RoundToInt(abil.value); break;
                        case AbilType.MaxHp:       FinalMaxHp       += Mathf.RoundToInt(abil.value); break;
                        case AbilType.Speed:       FinalMoveSpeed   += abil.value; break;
                        case AbilType.AttackSpeed: FinalAttackSpeed += abil.value; break;
                        case AbilType.CritRate:    FinalCritRate    += abil.value; break;
                        case AbilType.CritDamage:  FinalCritDamage  += abil.value; break;
                    }
                }
            }

            // Health 컴포넌트 MaxHp 동기화
            if (_health != null)
                _health.SetMaxHp(FinalMaxHp);
        }

        // ================================================================
        //  쿨타임 틱
        // ================================================================

        void TickCooldowns()
        {
            if (Skill1CoolRemaining > 0f)
            {
                Skill1CoolRemaining = Mathf.Max(0f, Skill1CoolRemaining - Time.deltaTime);
                OnCooldownChanged?.Invoke(1, Skill1CoolRemaining, _skill1CoolTotal);
            }
            if (Skill2CoolRemaining > 0f)
            {
                Skill2CoolRemaining = Mathf.Max(0f, Skill2CoolRemaining - Time.deltaTime);
                OnCooldownChanged?.Invoke(2, Skill2CoolRemaining, _skill2CoolTotal);
            }
        }

        void StartSkillCooldown(int slot, float duration)
        {
            if (slot == 1) { Skill1CoolRemaining = duration; _skill1CoolTotal = duration; }
            else           { Skill2CoolRemaining = duration; _skill2CoolTotal = duration; }
            OnCooldownChanged?.Invoke(slot, duration, duration);
        }

#if UNITY_EDITOR
        void Reset()
        {
            _health = GetComponent<Health>();
        }
#endif
    }
}
