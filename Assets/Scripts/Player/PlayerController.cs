using MonsterKitchen.AI.BehaviorTree;
using MonsterKitchen.Combat;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Dungeon;
using MonsterKitchen.Navigation;  // NavAgent (정적 유틸 포함)
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterKitchen.Player
{
    // ====================================================================
    //  PlayerController — 이동 · 자동 콤보 공격 · 스킬 · 궁극기 · 대시
    //
    //  ▶ BT 구동 방식
    //    BTRunner 가 매 프레임 PlayerBT 트리를 실행한다.
    //    · BTAction_PlayerMove        → SetBtMoveDir() 로 이동 방향 설정
    //    · BTAction_PlayerAutoAttack  → ExecuteAutoAttack() 호출
    //    · BTAction_PlayerDash        → StartDash() 호출
    //    · BTAction_PlayerSkill       → ExecuteSkillGroup() 호출
    //    입력 이벤트는 블랙보드 플래그("DashRequested" 등)로 변환된다.
    //
    //  ▶ FixedUpdate 는 BT 가 설정한 m_BtMoveDir 을 읽어 물리 이동을 적용한다.
    //    BT 미초기화 시 기존 GetEffectiveMoveDir() 폴백을 사용한다.
    // ====================================================================

    [RequireComponent(typeof(BTRunner))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerController : MonoBehaviour, IBTBlackboardInitializer
    {
        [Header("Layer")]
        [SerializeField] LayerMask m_EnemyLayer;

        [Header("Fallback (무기 미장착 시 사용되는 기본값)")]
        [SerializeField] float m_FallbackSearchRange = 2.5f;
        [SerializeField] float m_FallbackAttackRange = 0.8f;
        [SerializeField] float m_FallbackCooltime    = 0.4f;

        [Header("Move")]
        [Tooltip("공격 타이머가 남아있는 동안 이동 속도 배율. 0.5 = 절반 속도.")]
        [SerializeField] float m_AttackMovePenalty = 0.5f;

        [Header("Auto AI (던전 전용)")]
        [Tooltip("자동 추적 감지 반경.")]
        [SerializeField] float m_AutoChaseRange = 10f;

        [Header("Dash")]
        [SerializeField] float m_DashSpeed    = 18f;
        [SerializeField] float m_DashDuration = 0.18f;
        [SerializeField] float m_DashCooldown = 0.9f;

        [Header("BT")]
        [Tooltip("AssetManifest 에 등록된 PlayerBT 에셋 키. 빈칸이면 BT 를 로드하지 않는다.")]
        [SerializeField] string m_BtAssetAddress = "bt/player/9001";

        // ── 컴포넌트 참조 ─────────────────────────────────────────────
        [SerializeField] Rigidbody2D            m_Rb;
        [SerializeField] Animator               m_Anim;
        [SerializeField] Animator               m_OverlayAnim;
        [SerializeField] WeaponSocketController m_WeaponSocket;
        [SerializeField] SpriteRenderer[]       m_Sprites;
        [SerializeField] Health                 m_Health;
        [SerializeField] PlayerStats            m_Stats;

        // ── 이동 ──────────────────────────────────────────────────────
        Vector2 m_MoveDir;          // HandleMove 로 수신한 원시 입력
        Vector2 m_BtMoveDir;        // BT 가 계산한 최종 이동 방향
        Vector2 m_FacingDir = Vector2.down;
        Vector2 m_CurrentVel;

        // ── 자동 공격 (평타 체인) ──────────────────────────────────────
        int   m_ComboStep;
        float m_ComboWindowTimer;
        float m_AtkTimer;

        // ── 대시 ──────────────────────────────────────────────────────
        float m_DashCooldownTimer;
        bool  m_IsDashing;

        // ── 상태 ──────────────────────────────────────────────────────
        bool m_IsInDungeon;
        bool m_Initialized;
        bool m_IsDead;

        // ── BT 연동 ───────────────────────────────────────────────────
        BTRunner     m_BtRunner;
        BTBlackboard m_Blackboard;          // IBTBlackboardInitializer 콜백에서 캐시

        // ── Gizmo ─────────────────────────────────────────────────────
        float   m_GizmoAttackTime = -1f;
        float   m_GizmoRange;
        Vector2 m_GizmoCenter;

        static readonly int s_HashMoveX     = Animator.StringToHash("MoveX");
        static readonly int s_HashMoveY     = Animator.StringToHash("MoveY");
        static readonly int s_HashSpeed     = Animator.StringToHash("Speed");
        static readonly int s_HashAttack    = Animator.StringToHash("Attack");
        static readonly int s_HashDash      = Animator.StringToHash("Dash");
        static readonly int s_HashComboStep = Animator.StringToHash("ComboStep");

        static readonly WaitForFixedUpdate s_WaitFixed = new WaitForFixedUpdate();

        static readonly Collider2D[] s_OverlapBuffer = new Collider2D[32];

        // ================================================================
        //  공개 프로퍼티 — BT 노드에서 접근
        // ================================================================

        /// <summary>물리 이동 적용 후 발생. 미니맵 도트 갱신 등에 사용.</summary>
        public event System.Action<Vector3> OnMoved;

        public bool             IsInitialized   => m_Initialized;
        public bool             IsDashing        => m_IsDashing;
        public bool             CanAttack        => m_AtkTimer <= 0f;
        public bool             DashCooledDown   => m_DashCooldownTimer <= 0f;
        public Vector2          FacingDir        => m_FacingDir;
        public Rigidbody2D      Rb               => m_Rb;
        public Animator         Anim             => m_Anim;
        public PlayerStats      Stats            => m_Stats;
        public Health           PlayerHealth     => m_Health;
        public LayerMask        EnemyLayer       => m_EnemyLayer;
        public bool             IsInDungeon      => m_IsInDungeon;
        public float            AutoChaseRange   => m_AutoChaseRange;

        // ================================================================
        //  IBTBlackboardInitializer
        // ================================================================

        public void InitializeBlackboard(BTBlackboard bb)
        {
            m_Blackboard = bb;

            bb.Set("MoveInput",          Vector2.zero);
            bb.Set("IsDashing",          false);
            bb.Set("DashRequested",      false);
            bb.Set("Skill1Requested",    false);
            bb.Set("Skill2Requested",    false);
            bb.Set("UltimateRequested",  false);
            bb.Set("IsInDungeon",        m_IsInDungeon);
        }

        // ================================================================
        //  Mono
        // ================================================================

        void OnEnable()
        {
            var im = InputManager.Instance;
            if (im != null)
            {
                im.OnMove     += HandleMove;
                im.OnDash     += HandleDash;
                im.OnSkill1   += HandleSkill1;
                im.OnSkill2   += HandleSkill2;
                im.OnUltimate += HandleUltimate;
            }

            if (m_Health != null)
            {
                m_Health.OnDamaged += OnPlayerDamaged;
                m_Health.OnDeath   += OnPlayerDied;
            }

            if (m_Stats != null)
                m_Stats.OnWeaponChanged += OnWeaponChanged;

            SceneManager.sceneLoaded += OnSceneLoaded;
            UpdateDungeonState(SceneManager.GetActiveScene().name);
        }

        void OnDisable()
        {
            var im = InputManager.Instance;
            if (im != null)
            {
                im.OnMove     -= HandleMove;
                im.OnDash     -= HandleDash;
                im.OnSkill1   -= HandleSkill1;
                im.OnSkill2   -= HandleSkill2;
                im.OnUltimate -= HandleUltimate;
            }

            if (m_Health != null)
            {
                m_Health.OnDamaged -= OnPlayerDamaged;
                m_Health.OnDeath   -= OnPlayerDied;
            }

            if (m_Stats != null)
                m_Stats.OnWeaponChanged -= OnWeaponChanged;

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void Start()
        {
            if (!m_Initialized)
                Init(null);
        }

        // ================================================================
        //  Init — SpawnManager 패턴
        // ================================================================

        public void Init(PlayerCharData data)
        {
            m_Stats.Init(data);

            m_WeaponSocket?.SetWeapon(m_Stats.EquippedWeapon);
            m_WeaponSocket?.SetFacingDirection(m_FacingDir);

            // BTRunner 에 PlayerBT 에셋 설정
            // 우선순위: data.BtAssetAddress (CSV 데이터) → m_BtAssetAddress (Inspector 폴백)
            m_BtRunner = GetComponent<BTRunner>();
            string btKey = (data != null && !string.IsNullOrEmpty(data.BtAssetAddress))
                ? data.BtAssetAddress
                : m_BtAssetAddress;

            if (m_BtRunner != null && !string.IsNullOrEmpty(btKey))
            {
                var btAsset = AssetLoadManager.Instance?.Load<BTAsset>(btKey);
                if (btAsset != null)
                    m_BtRunner.SetAsset(btAsset);
                else
                    DebugUtil.LogWarning(StringUtil.Format("[PlayerController] BT 에셋 로드 실패 — 키: {0}", btKey), this);
            }

            m_Initialized = true;

            DebugUtil.Log("[PlayerController] Init 완료 (BT 구동)");
        }

#if UNITY_EDITOR
        void Reset()
        {
            m_Rb           = GetComponent<Rigidbody2D>();
            m_Anim         = GetComponent<Animator>();
            m_WeaponSocket = GetComponentInChildren<WeaponSocketController>(true);
            m_Health       = GetComponent<Health>();
            m_Stats        = GetComponent<PlayerStats>();

            var weaponSR = m_WeaponSocket != null ? m_WeaponSocket.SR : null;
            var all = GetComponentsInChildren<SpriteRenderer>(true);
            var filtered = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (var sr in all)
                if (sr != weaponSR) filtered.Add(sr);
            m_Sprites = filtered.ToArray();
        }
#endif

        // ================================================================
        //  Update — 타이머만 처리. 행동 결정은 BT 담당.
        // ================================================================

        void Update()
        {
            if (!m_Initialized || m_IsDead) return;

            if (m_AtkTimer > 0f)          m_AtkTimer         -= Time.deltaTime;
            if (m_DashCooldownTimer > 0f) m_DashCooldownTimer -= Time.deltaTime;

            if (m_ComboWindowTimer > 0f)
            {
                m_ComboWindowTimer -= Time.deltaTime;
                if (m_ComboWindowTimer <= 0f)
                    m_ComboStep = 0;
            }
        }

        // ================================================================
        //  FixedUpdate — BT 가 결정한 방향으로 물리 이동 적용
        // ================================================================

        void FixedUpdate()
        {
            if (!m_Initialized || m_IsDashing || m_IsDead) return;

            float speed = m_Stats != null ? m_Stats.FinalMoveSpeed : 5f;
            if (m_AtkTimer > 0f) speed *= m_AttackMovePenalty;

            Vector2 effectiveDir = m_BtMoveDir;

            Vector2 targetPos = ComputeNavPosition(effectiveDir, speed, Time.fixedDeltaTime);
            m_CurrentVel = (targetPos - m_Rb.position) / Time.fixedDeltaTime;
            m_Rb.MovePosition(targetPos);
            if (effectiveDir.sqrMagnitude > 0.01f)
                OnMoved?.Invoke((Vector3)targetPos);

            if (effectiveDir.sqrMagnitude > 0.01f)
                m_FacingDir = effectiveDir.normalized;

            m_Anim.SetFloat(s_HashMoveX, m_FacingDir.x);
            m_Anim.SetFloat(s_HashMoveY, m_FacingDir.y);
            m_Anim.SetFloat(s_HashSpeed, m_CurrentVel.magnitude);

            m_OverlayAnim?.SetFloat(s_HashMoveX, m_FacingDir.x);
            m_OverlayAnim?.SetFloat(s_HashMoveY, m_FacingDir.y);

            m_WeaponSocket?.SetFacingDirection(m_FacingDir);

            if (m_FacingDir.x != 0f && m_Sprites != null)
            {
                bool flip     = m_FacingDir.x > 0f;
                var  weaponSR = m_WeaponSocket != null ? m_WeaponSocket.SR : null;
                foreach (var sr in m_Sprites)
                {
                    if (sr == weaponSR) continue;
                    sr.flipX = flip;
                }
            }
        }

        // ================================================================
        //  BT 노드에서 호출하는 공개 API
        // ================================================================

        /// <summary>BTAction_PlayerMove 가 매 틱 호출해 이동 방향을 설정한다.</summary>
        public void SetBtMoveDir(Vector2 dir) => m_BtMoveDir = dir;

        /// <summary>대시를 시작한다. 쿨타임·중복 체크는 호출 전 확인할 것.</summary>
        public void StartDash() => StartCoroutine(DashCoroutine());

        /// <summary>감지 범위 내 가장 가까운 적을 반환한다.</summary>
        public Transform FindNearestEnemy(float range)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(m_EnemyLayer);
            filter.useTriggers = true;
            int hitCount = Physics2D.OverlapCircle(transform.position, range, filter, s_OverlapBuffer);
            Transform nearest = null;
            float     minDist  = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                float d = Vector2.Distance(transform.position, s_OverlapBuffer[i].transform.position);
                if (d < minDist) { minDist = d; nearest = s_OverlapBuffer[i].transform; }
            }
            return nearest;
        }

        /// <summary>현재 콤보 스텝의 서치 범위를 반환한다.</summary>
        public float GetCurrentSearchRange()
        {
            var group = m_Stats?.NormalAttackGroup;
            if (group == null || group.ChainLength == 0) return m_FallbackSearchRange;
            int  step  = Mathf.Clamp(m_ComboStep, 0, group.ChainLength - 1);
            var  skill = group.GetStep(step);
            return skill != null ? skill.SearchRange : m_FallbackSearchRange;
        }

        /// <summary>현재 콤보 스텝의 공격 범위를 반환한다.</summary>
        public float GetCurrentAttackRange()
        {
            var group = m_Stats?.NormalAttackGroup;
            if (group == null || group.ChainLength == 0) return m_FallbackAttackRange;
            int  step  = Mathf.Clamp(m_ComboStep, 0, group.ChainLength - 1);
            var  skill = group.GetStep(step);
            return skill != null ? skill.AttackRange : m_FallbackAttackRange;
        }

        /// <summary>
        /// 타겟 방향으로 페이싱 갱신 후 콤보 공격을 실행한다.
        /// BTAction_PlayerAutoAttack 에서 호출.
        /// </summary>
        public void ExecuteAutoAttack(Transform target)
        {
            m_FacingDir = ((Vector2)(target.position - transform.position)).normalized;
            m_WeaponSocket?.SetFacingDirection(m_FacingDir);
            DoComboAttack(target);
        }

        /// <summary>인접 채집 노드 수확을 시도한다. BTAction_PlayerAutoAttack 에서 호출.</summary>
        public void DoHarvestNode() => TryHarvestNode();

        /// <summary>스킬 그룹을 즉시 실행한다. BTAction_PlayerSkill 에서 호출.</summary>
        public void ExecuteSkillGroup(SkillGroupData group)
        {
            var skill = group.GetStep(0);
            if (skill == null) return;

            int           dmg  = Mathf.RoundToInt((m_Stats?.FinalAttack ?? 10) * skill.DamageMultiplier);
            AttributeType attr = m_Stats?.AttackAttribute ?? AttributeType.None;

            int triggerHash = string.IsNullOrEmpty(skill.AnimTriggerOverride)
                ? s_HashAttack
                : Animator.StringToHash(skill.AnimTriggerOverride);
            m_Anim.SetTrigger(triggerHash);

            m_OverlayAnim?.SetFloat(s_HashMoveX, m_FacingDir.x);
            m_OverlayAnim?.SetFloat(s_HashMoveY, m_FacingDir.y);
            m_OverlayAnim?.SetTrigger(s_HashAttack);

            m_WeaponSocket?.TriggerWeaponAnim(triggerHash);

            ExecuteSkillStep(skill, dmg, attr);
            m_Stats?.ConsumeWeaponDurabilityOnHit();
        }

        /// <summary>스킬 슬롯 1·2 발동 시도. BTAction_PlayerSkill 에서 호출.</summary>
        public bool TryUseSkill(int slot, out SkillGroupData skill) =>
            m_Stats.TryUseSkill(slot, out skill);

        /// <summary>궁극기 발동 시도. BTAction_PlayerSkill 에서 호출.</summary>
        public bool TryUseUltimate(out SkillGroupData skill) =>
            m_Stats.TryUseUltimate(out skill);

        // ================================================================
        //  InputManager 이벤트 핸들러 — 블랙보드 플래그 기록
        // ================================================================

        void HandleMove(Vector2 dir)
        {
            m_MoveDir = dir;                    // 폴백 이동용
            m_Blackboard?.Set("MoveInput", dir);
        }

        void HandleDash()
        {
            if (!m_Initialized || m_IsDashing || m_DashCooldownTimer > 0f) return;
            if (UI.UIManager.Instance != null && UI.UIManager.Instance.HasOpenPopup) return;
            if (SceneManager.GetActiveScene().name == "KitchenScene") return;

            m_Blackboard?.Set("DashRequested", true);
        }

        void HandleSkill1()
        {
            if (!m_Initialized || m_IsDashing) return;
            m_Blackboard?.Set("Skill1Requested", true);
        }

        void HandleSkill2()
        {
            if (!m_Initialized || m_IsDashing) return;
            m_Blackboard?.Set("Skill2Requested", true);
        }

        void HandleUltimate()
        {
            if (!m_Initialized || m_IsDashing) return;
            m_Blackboard?.Set("UltimateRequested", true);
        }

        // ================================================================
        //  자동 콤보 공격 (평타)
        // ================================================================

        void DoComboAttack(Transform target)
        {
            if (m_IsDashing) return;

            var group = m_Stats?.NormalAttackGroup;

            // 평타 체인 없음 → fallback 즉발 근거리
            if (group == null || group.ChainLength == 0)
            {
                m_AtkTimer = m_FallbackCooltime;
                int           dmg  = m_Stats != null ? m_Stats.FinalAttack : 10;
                AttributeType attr = m_Stats != null ? m_Stats.AttackAttribute : AttributeType.None;
                SimpleMeleeHit(target, dmg, m_FallbackAttackRange, attr);
                m_Stats?.ConsumeWeaponDurabilityOnHit();
                return;
            }

            int step  = Mathf.Clamp(m_ComboStep, 0, group.ChainLength - 1);
            var skill = group.GetStep(step);
            if (skill == null) return;

            int           dmgFinal  = Mathf.RoundToInt((m_Stats?.FinalAttack ?? 10) * skill.DamageMultiplier);
            AttributeType attrFinal = m_Stats?.AttackAttribute ?? AttributeType.None;

            m_AtkTimer = skill.Cooltime;

            bool isLast = (step >= group.ChainLength - 1);
            if (isLast)
            {
                m_ComboStep        = 0;
                m_ComboWindowTimer = 0f;
            }
            else
            {
                m_ComboStep        = step + 1;
                m_ComboWindowTimer = skill.ComboWindow;
            }

            m_Anim.SetInteger(s_HashComboStep, step);
            int triggerHash = string.IsNullOrEmpty(skill.AnimTriggerOverride)
                ? s_HashAttack
                : Animator.StringToHash(skill.AnimTriggerOverride);
            m_Anim.SetTrigger(triggerHash);

            m_OverlayAnim?.SetFloat(s_HashMoveX, m_FacingDir.x);
            m_OverlayAnim?.SetFloat(s_HashMoveY, m_FacingDir.y);
            m_OverlayAnim?.SetTrigger(s_HashAttack);

            m_WeaponSocket?.TriggerWeaponAnim(triggerHash);

            ExecuteSkillStep(skill, dmgFinal, attrFinal);
            m_Stats?.ConsumeWeaponDurabilityOnHit();
        }

        // ================================================================
        //  SkillData 실행
        // ================================================================

        void ExecuteSkillStep(SkillData skill, int dmg, AttributeType attr)
        {
            if (skill.IsProjectile)
            {
                SpawnProjectile(skill, dmg, attr);
            }
            else if (skill.MaxTargets == 1)
            {
                var target = FindNearestEnemy(skill.SearchRange);
                if (target != null)
                    SingleMeleeHit(target, skill.AttackRange, dmg, attr, skill);
            }
            else
            {
                Vector2 center = (Vector2)transform.position + m_FacingDir * (skill.AttackRange * 0.5f);
                AoeMeleeHit(center, skill.AttackRange * 0.5f, skill.MaxTargets, dmg, attr, skill);

                m_GizmoAttackTime = Time.time;
                m_GizmoRange      = skill.AttackRange * 0.5f;
                m_GizmoCenter     = center;
            }
        }

        // ================================================================
        //  근거리 공격
        // ================================================================

        void SimpleMeleeHit(Transform target, int dmg, float range, AttributeType attr)
        {
            float dist = Vector2.Distance(transform.position, target.position);
            if (dist > range) return;

            var hp = target.GetComponent<Health>();
            if (hp == null) return;

            bool wasDead = hp.IsDead;
            hp.TakeDamage(dmg, attr);
            if (!wasDead && hp.IsDead)
            {
                m_Stats?.AddUltimateGaugeOnKill();
                m_Stats?.ConsumeWeaponDurabilityOnKill();
            }
        }

        void SingleMeleeHit(Transform target, float attackRange, int dmg, AttributeType attr, SkillData skill = null)
        {
            float dist = Vector2.Distance(transform.position, target.position);
            if (dist > attackRange) return;

            m_GizmoAttackTime = Time.time;
            m_GizmoRange      = attackRange;
            m_GizmoCenter     = target.position;

            var hp = target.GetComponent<Health>();
            if (hp == null) return;

            bool wasDead = hp.IsDead;
            hp.TakeDamage(dmg, attr);
            if (!wasDead && hp.IsDead)
            {
                m_Stats?.AddUltimateGaugeOnKill();
                m_Stats?.ConsumeWeaponDurabilityOnKill();
            }

            TryApplyCC(skill, target, transform.position);
        }

        void AoeMeleeHit(Vector2 center, float radius, int maxTargets, int dmg, AttributeType attr, SkillData skill = null)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(m_EnemyLayer);
            filter.useTriggers = true;
            int hitCount = Physics2D.OverlapCircle(center, radius, filter, s_OverlapBuffer);
            int count    = 0;

            for (int i = 0; i < hitCount; i++)
            {
                if (count >= maxTargets) break;
                var col = s_OverlapBuffer[i];
                var hp  = col.GetComponent<Health>();
                if (hp == null) continue;

                bool wasDead = hp.IsDead;
                hp.TakeDamage(dmg, attr);
                if (!wasDead && hp.IsDead)
                    m_Stats?.AddUltimateGaugeOnKill();

                TryApplyCC(skill, col.transform, transform.position);
                count++;
            }
        }

        // ================================================================
        //  투사체
        // ================================================================

        void SpawnProjectile(SkillData skill, int dmg, AttributeType attr)
        {
            m_GizmoAttackTime = Time.time;

            var     target  = FindNearestEnemy(skill.SearchRange);
            Vector2 fireDir = target != null
                ? ((Vector2)(target.position - transform.position)).normalized
                : m_FacingDir;

            var go   = new GameObject(skill.IsAoe ? "AoEProjectile" : "Projectile");
            go.transform.position = transform.position;

            var proj = go.AddComponent<Projectile>();
            proj.Init(
                damage:        dmg,
                attr:          attr,
                direction:     fireDir,
                speed:         skill.MissileSpeed,
                maxDistance:   skill.MissileMaxRange,
                targetLayer:   m_EnemyLayer,
                isAoe:         skill.IsAoe,
                aoeRadius:     skill.AttackRange,
                maxTargets:    skill.MaxTargets,
                onKill:        () => m_Stats?.AddUltimateGaugeOnKill(),
                homingTarget:  target,
                ccForce:       skill.CcForce,
                ccDuration:    skill.CcDuration,
                stunDuration:  skill.StunDuration,
                fireSourcePos: transform.position
            );
        }

        // ================================================================
        //  CC 적용
        // ================================================================

        void TryApplyCC(SkillData skill, Transform target, Vector2 sourcePos)
        {
            if (skill == null || target == null) return;
            var cc = target.GetComponent<Combat.CrowdControlComponent>();
            if (cc == null) return;

            if (skill.CcForce > 0f)
            {
                Vector2 dir = ((Vector2)target.position - sourcePos).normalized;
                cc.TryApplyKnockback(dir, skill.CcForce, skill.CcDuration);
            }
            else if (skill.CcForce < 0f)
            {
                cc.TryApplyPullIn(sourcePos, -skill.CcForce, skill.CcDuration);
            }
            else if (skill.StunDuration > 0f)
            {
                cc.TryApplyStun(skill.StunDuration);
            }
        }

        // ================================================================
        //  채집 노드
        // ================================================================

        void TryHarvestNode()
        {
            int nodeLayerIdx = LayerMask.NameToLayer("ResourceNode");
            if (nodeLayerIdx < 0) return;
            LayerMask nodeLayer = 1 << nodeLayerIdx;

            float attackRange = GetCurrentAttackRange();
            var nodeFilter = new ContactFilter2D();
            nodeFilter.SetLayerMask(nodeLayer);
            nodeFilter.useTriggers = true;
            int hitCount = Physics2D.OverlapCircle(transform.position, attackRange, nodeFilter, s_OverlapBuffer);

            ResourceNode nearest = null;
            float        minDist = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                var node = s_OverlapBuffer[i].GetComponent<ResourceNode>();
                if (node == null || node.Depleted) continue;
                float d = Vector2.Distance(transform.position, s_OverlapBuffer[i].transform.position);
                if (d < minDist) { minDist = d; nearest = node; }
            }
            if (nearest == null) return;

            int dmg = m_Stats != null ? m_Stats.FinalAttack : 10;
            m_AtkTimer  = GetCurrentCooltime();
            m_FacingDir = ((Vector2)(nearest.transform.position - transform.position)).normalized;
            m_WeaponSocket?.SetFacingDirection(m_FacingDir);
            m_Anim.SetTrigger(s_HashAttack);
            nearest.TakeHarvestDamage(dmg, m_Stats);
            m_Stats?.ConsumeWeaponDurabilityOnHit();
        }

        float GetCurrentCooltime()
        {
            var group = m_Stats?.NormalAttackGroup;
            if (group == null || group.ChainLength == 0) return m_FallbackCooltime;
            int  step  = Mathf.Clamp(m_ComboStep, 0, group.ChainLength - 1);
            var  skill = group.GetStep(step);
            return skill != null ? skill.Cooltime : m_FallbackCooltime;
        }

        // ================================================================
        //  씬 전환
        // ================================================================

        void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            m_IsDead = false;
            UpdateDungeonState(scene.name);
        }

        void UpdateDungeonState(string sceneName)
        {
            m_IsInDungeon = sceneName == "DungeonScene";
            m_Blackboard?.Set("IsInDungeon", m_IsInDungeon);
        }

        // ================================================================
        //  NavGrid 이동 계산
        // ================================================================

        Vector2 ComputeNavPosition(Vector2 inputDir, float speed, float dt) =>
            NavAgent.ComputeStep(m_Rb.position, inputDir, speed, dt);

        // ================================================================
        //  대시
        // ================================================================

        System.Collections.IEnumerator DashCoroutine()
        {
            m_IsDashing = true;
            if (m_Health != null) m_Health.IsInvincible = true;

            Vector2 dir = m_MoveDir.sqrMagnitude > 0.01f ? m_MoveDir.normalized : m_FacingDir;
            m_Anim.SetTrigger(s_HashDash);

            float elapsed = 0f;
            while (elapsed < m_DashDuration)
            {
                Vector2 nextPos = m_Rb.position + dir * m_DashSpeed * Time.fixedDeltaTime;
                if (!NavAgent.IsValid(nextPos)) break;
                m_Rb.MovePosition(nextPos);
                elapsed += Time.fixedDeltaTime;
                yield return s_WaitFixed;
            }

            m_CurrentVel        = Vector2.zero;
            m_Rb.linearVelocity = Vector2.zero;
            m_IsDashing         = false;
            m_DashCooldownTimer = m_DashCooldown;
            if (m_Health != null) m_Health.IsInvincible = false;
        }

        // ================================================================
        //  Health 이벤트
        // ================================================================

        void OnPlayerDamaged(int amount, AttributeType attr) =>
            DebugUtil.Log(StringUtil.Format("[PlayerController] 피격 -{0}  속성:{1}  HP:{2}/{3}",
                amount, attr, m_Health?.CurrentHp, m_Health?.MaxHp));

        void OnPlayerDied(AttributeType killAttr)
        {
            DebugUtil.Log(StringUtil.Format("[PlayerController] 사망  막타속성:{0}", killAttr));

            if (m_IsInDungeon)
                StartCoroutine(DungeonDeathCoroutine());
        }

        System.Collections.IEnumerator DungeonDeathCoroutine()
        {
            m_IsDead = true;

            // 물리 정지
            if (m_Rb != null)
                m_Rb.linearVelocity = Vector2.zero;

            // 사망 연출 대기 (1.5초)
            yield return new WaitForSeconds(1.5f);

            // 가방 재료 전부 소실
            DungeonBag.Current?.ClearAll();

            // 다음 던전을 위해 체력 완전 회복 (Revive: 사망 상태에서도 작동)
            m_Health?.Revive();

            DebugUtil.Log("[PlayerController] 던전 사망 → ManagementScene 복귀");
            SceneLoader.Instance?.LoadScene("ManagementScene");
        }

        void OnWeaponChanged(Data.WeaponData weapon) => m_WeaponSocket?.SetWeapon(weapon);

        // ================================================================
        //  Gizmo
        // ================================================================

        void OnDrawGizmos()
        {
            float searchRange = Application.isPlaying ? GetCurrentSearchRange() : m_FallbackSearchRange;

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f);
            Gizmos.DrawSphere(transform.position, searchRange);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, searchRange);

            if (Application.isPlaying && (Time.time - m_GizmoAttackTime) < 0.3f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(m_GizmoCenter, m_GizmoRange);
            }
        }
    }
}
