using MonsterKitchen.Combat;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Dungeon;
using MonsterKitchen.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterKitchen.Player
{
    // ====================================================================
    //  PlayerController — 이동 · 자동 콤보 공격 · 스킬 · 궁극기 · 대시
    //
    //  ▶ 공격 방식 (데이터 기반, AttackPattern enum 없음)
    //    SkillData.missileSpeed == 0 → 즉발(근거리)
    //      maxTargets == 1 → 가장 가까운 적 1명 (MeleeTarget)
    //      maxTargets  > 1 → OverlapCircle 범위 (MeleeRange)
    //    SkillData.missileSpeed  > 0 → 투사체 발사
    //      IsAoe == true  → 적중 위치 폭발 (RangedAoE)
    //      IsAoe == false → 단일 타겟 (RangedTarget)
    //
    //  ▶ 자동 공격 (평타 체인)
    //    PlayerStats.NormalAttackGroup 의 skillChain 을 순서대로 실행.
    //    searchRange 내 적이 있을 때만 실행 (자동 조준).
    //    comboWindow 내 다음 타격이 없으면 체인 초기화.
    //
    //  ▶ 스킬 / 궁극기 (Q · R · F 키)
    //    PlayerStats.TryUseSkill / TryUseUltimate 호출 후 SkillGroupData 실행.
    //    스킬의 체인 첫 번째 SkillData 를 즉시 적용한다.
    //
    //  ▶ 궁극기 게이지
    //    적에게 데미지를 줘 처치 시 PlayerStats.AddUltimateGaugeOnKill 호출.
    //    피격 시 게이지 적립은 PlayerStats 가 Health.OnDamaged 를 직접 구독.
    //
    //  ▶ 자동 AI (던전 전용)
    //    DungeonScene 에서 유저 이동 입력이 없으면 자동으로 가장 가까운 적을
    //    추적한다 (_autoChaseRange 내 탐색). 공격 범위 진입 시 정지.
    //    WASD 입력이 들어오면 즉시 수동 이동으로 전환.
    // ====================================================================

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Layer")]
        [SerializeField] LayerMask enemyLayer;
        // resourceNodeLayer 는 Inspector 설정 없이 런타임 자동 계산 ("ResourceNode" 레이어)

        [Header("Fallback (무기 미장착 시 사용되는 기본값)")]
        [SerializeField] float fallbackSearchRange = 2.5f;
        [SerializeField] float fallbackAttackRange = 0.8f;
        [SerializeField] float fallbackCooltime    = 0.4f;

        [Header("Move")]
        [Tooltip("이동 가속도 (units/s²). 클수록 반응이 빠르고, 작을수록 미끄러지는 느낌.")]
        [SerializeField] float moveAcceleration = 30f;
        [Tooltip("공격 타이머가 남아있는 동안 이동 속도 배율. 0.5 = 절반 속도.")]
        [SerializeField] float attackMovePenalty = 0.5f;

        [Header("Auto AI (던전 전용)")]
        [Tooltip("자동 추적 감지 반경. 이 범위 내 적이 있으면 자동으로 접근한다.")]
        [SerializeField] float _autoChaseRange = 6f;
        [Tooltip("자동 추적 시 공격 범위 앞에서 정지하는 비율 (0~1). 0.85 = 공격 범위의 85% 지점에서 멈춤.")]
        [SerializeField] float _autoStopRatio  = 0.85f;

        [Header("Dash")]
        [SerializeField] float dashSpeed    = 18f;
        [SerializeField] float dashDuration = 0.18f;
        [SerializeField] float dashCooldown = 0.9f;

        // ── 컴포넌트 참조 ─────────────────────────────────────────────
        [SerializeField] Rigidbody2D           _rb;
        [SerializeField] Animator              _anim;
        [SerializeField] Animator              _overlayAnim;   // Overlay 자식 Animator
        [SerializeField] WeaponSocketController _weaponSocket; // 무기 소켓 (WeaponSocket 자식 GO)
        [SerializeField] SpriteRenderer[]      _sprites;      // 자신 + 자식 SpriteRenderer (WeaponSocket 제외)
        [SerializeField] Health                _health;
        [SerializeField] PlayerStats           _stats;

        // ── 이동 ──────────────────────────────────────────────────────
        Vector2 _moveDir;
        Vector2 _facingDir  = Vector2.down;
        Vector2 _currentVel;          // 관성용 현재 속도

        // ── 자동 공격 (평타 체인) ──────────────────────────────────────
        int   _comboStep;
        float _comboWindowTimer;
        float _atkTimer;

        // ── 대시 ──────────────────────────────────────────────────────
        float _dashCooldownTimer;
        bool  _isDashing;

        // ── 자동 AI ───────────────────────────────────────────────────
        bool _isInDungeon;

        // ── Gizmo ─────────────────────────────────────────────────────
        float   _gizmoAttackTime = -1f;
        float   _gizmoRange;
        Vector2 _gizmoCenter;

        bool _initialized;

        static readonly int HashMoveX     = Animator.StringToHash("MoveX");
        static readonly int HashMoveY     = Animator.StringToHash("MoveY");
        static readonly int HashSpeed     = Animator.StringToHash("Speed");
        static readonly int HashAttack    = Animator.StringToHash("Attack");
        static readonly int HashDash      = Animator.StringToHash("Dash");
        static readonly int HashComboStep = Animator.StringToHash("ComboStep");

        static readonly WaitForFixedUpdate _waitFixed = new WaitForFixedUpdate();

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

            if (_health != null)
            {
                _health.OnDamaged += OnPlayerDamaged;
                _health.OnDeath   += OnPlayerDied;
            }

            if (_stats != null)
                _stats.OnWeaponChanged += OnWeaponChanged;

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

            if (_health != null)
            {
                _health.OnDamaged -= OnPlayerDamaged;
                _health.OnDeath   -= OnPlayerDied;
            }

            if (_stats != null)
                _stats.OnWeaponChanged -= OnWeaponChanged;

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // 씬에 직접 배치된 경우 PlayerManager 없이도 Start 에서 자동 Init
        void Start()
        {
            if (!_initialized)
                Init(null);
        }

        // ================================================================
        //  Init — GetComponent + 스탯 초기화. SetActive(true) 전에 호출된다.
        // ================================================================

        public void Init(PlayerSpawnData data)
        {
            _stats.Init(data);

            // SpawnManager 패턴: Init()은 OnEnable() 전에 호출되므로
            // OnWeaponChanged 이벤트가 아직 구독 전일 수 있다.
            // 초기 무기를 WeaponSocket 에 직접 동기화.
            _weaponSocket?.SetWeapon(_stats.EquippedWeapon);
            _weaponSocket?.SetFacingDirection(_facingDir);

            _initialized = true;

            Debug.Log("[PlayerController] Init 완료");
        }

#if UNITY_EDITOR
        // 에디터에서 컴포넌트 추가 또는 Reset 시 자동 배선.
        // 빌드에 포함되지 않으므로 런타임 비용 없음.
        void Reset()
        {
            _rb           = GetComponent<Rigidbody2D>();
            _anim         = GetComponent<Animator>();
            _weaponSocket = GetComponentInChildren<WeaponSocketController>(true);
            _health       = GetComponent<Health>();
            _stats        = GetComponent<PlayerStats>();

            // WeaponSocket SR 제외하고 나머지 SpriteRenderer 수집
            var weaponSR = _weaponSocket != null ? _weaponSocket.SR : null;
            var all = GetComponentsInChildren<SpriteRenderer>(true);
            var filtered = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (var sr in all)
                if (sr != weaponSR) filtered.Add(sr);
            _sprites = filtered.ToArray();
        }
#endif

        // ================================================================
        //  Update / FixedUpdate
        // ================================================================

        void Update()
        {
            if (!_initialized) return;

            if (_atkTimer > 0f)          _atkTimer         -= Time.deltaTime;
            if (_dashCooldownTimer > 0f) _dashCooldownTimer -= Time.deltaTime;

            // 콤보 창 카운트다운
            if (_comboWindowTimer > 0f)
            {
                _comboWindowTimer -= Time.deltaTime;
                if (_comboWindowTimer <= 0f)
                {
                    _comboStep = 0;
                    Debug.Log("[PlayerController] 콤보 창 만료 → 초기화");
                }
            }

            // 자동 공격: 쿨타임 종료 + 감지 범위 내 적 존재 시. 적 없으면 채집 노드 공격.
            if (_atkTimer <= 0f)
            {
                float searchRange = GetCurrentSearchRange();
                var   target      = FindNearestEnemy(searchRange);
                if (target != null)
                {
                    _facingDir = ((Vector2)(target.position - transform.position)).normalized;
                    _weaponSocket?.SetFacingDirection(_facingDir); // 공격 전 소켓 방향 선반영
                    DoComboAttack(target);
                }
                else
                {
                    TryHarvestNode();
                }
            }
        }

        void FixedUpdate()
        {
            if (!_initialized || _isDashing) return;

            float speed = _stats != null ? _stats.FinalMoveSpeed : 5f;

            // 공격 타이머가 남아있으면 이동 속도 패널티 (공격 모션 중 느려짐)
            if (_atkTimer > 0f) speed *= attackMovePenalty;

            // 유저 입력 없고 던전 씬이면 자동 AI 방향 사용
            Vector2 effectiveDir = GetEffectiveMoveDir();

            // MoveTowards 로 가속/감속 — nav 맵이 있으면 벽 슬라이딩 적용
            Vector2 targetVel = ComputeNavVelocity(effectiveDir, speed);
            _currentVel = Vector2.MoveTowards(_currentVel, targetVel, moveAcceleration * Time.fixedDeltaTime);
            _rb.linearVelocity = _currentVel;

            if (effectiveDir.sqrMagnitude > 0.01f)
                _facingDir = effectiveDir.normalized;

            _anim.SetFloat(HashMoveX, _facingDir.x);
            _anim.SetFloat(HashMoveY, _facingDir.y);
            _anim.SetFloat(HashSpeed, _currentVel.magnitude);

            _overlayAnim?.SetFloat(HashMoveX, _facingDir.x);
            _overlayAnim?.SetFloat(HashMoveY, _facingDir.y);

            // 무기 소켓 방향 갱신 (360도 world space 회전)
            _weaponSocket?.SetFacingDirection(_facingDir);

            // WeaponSocket SR 은 자체적으로 방향을 처리하므로 flipX 루프에서 제외
            if (_facingDir.x != 0f && _sprites != null)
            {
                bool flip = _facingDir.x > 0f;
                var  weaponSR = _weaponSocket != null ? _weaponSocket.SR : null;
                foreach (var sr in _sprites)
                {
                    if (sr == weaponSR) continue;
                    sr.flipX = flip;
                }
            }
        }

        // ================================================================
        //  자동 AI 헬퍼
        // ================================================================

        /// <summary>
        /// 이동에 사용할 실제 방향을 반환한다.
        /// 유저 입력(WASD)이 있으면 수동 우선, 없고 DungeonScene이면 자동 추적.
        /// </summary>
        Vector2 GetEffectiveMoveDir()
        {
            // 유저 입력 최우선
            if (_moveDir.sqrMagnitude > 0.01f) return _moveDir;

            // 던전 외 씬이거나 대시 중에는 자동 없음
            if (!_isInDungeon || _isDashing) return Vector2.zero;

            // 가장 가까운 적 탐색
            var target = FindNearestEnemy(_autoChaseRange);
            if (target == null) return Vector2.zero;

            float dist     = Vector2.Distance(transform.position, target.position);
            float stopDist = GetCurrentSearchRange() * _autoStopRatio;

            // 이미 공격 범위 근처에 있으면 정지 (자동 공격이 처리)
            if (dist <= stopDist) return Vector2.zero;

            return ((Vector2)(target.position - transform.position)).normalized;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode _) => UpdateDungeonState(scene.name);

        void UpdateDungeonState(string sceneName) => _isInDungeon = sceneName == "DungeonScene";

        // ================================================================
        //  InputManager 이벤트 핸들러
        // ================================================================

        void HandleMove(Vector2 dir) => _moveDir = dir;

        void HandleDash()
        {
            if (!_initialized || _isDashing || _dashCooldownTimer > 0f) return;
            if (UI.UIManager.Instance != null && UI.UIManager.Instance.HasOpenPopup) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "KitchenScene") return;

            StartCoroutine(DashCoroutine());
        }

        void HandleSkill1()
        {
            if (!_initialized || _isDashing) return;
            if (!_stats.TryUseSkill(1, out var skill)) return;

            Debug.Log($"[PlayerController] 스킬1 발동: {skill.skillName}");
            ExecuteSkillGroup(skill);
        }

        void HandleSkill2()
        {
            if (!_initialized || _isDashing) return;
            if (!_stats.TryUseSkill(2, out var skill)) return;

            Debug.Log($"[PlayerController] 스킬2 발동: {skill.skillName}");
            ExecuteSkillGroup(skill);
        }

        void HandleUltimate()
        {
            if (!_initialized || _isDashing) return;
            if (!_stats.TryUseUltimate(out var skill)) return;

            Debug.Log($"[PlayerController] 궁극기 발동: {skill.skillName}");
            ExecuteSkillGroup(skill);
        }

        // ================================================================
        //  자동 콤보 공격 (평타)
        // ================================================================

        void DoComboAttack(Transform target)
        {
            if (_isDashing) return;

            var group = _stats?.NormalAttackGroup;

            // 평타 체인 없음 → 기본 즉발 근거리 1타
            if (group == null || group.ChainLength == 0)
            {
                _atkTimer = fallbackCooltime;
                int   dmg    = _stats != null ? _stats.FinalAttack : 10;
                AttributeType attr = _stats != null ? _stats.AttackAttribute : AttributeType.None;
                SimpleMeleeHit(target, dmg, fallbackAttackRange, attr);
                _stats?.ConsumeWeaponDurabilityOnHit();
                return;
            }

            int step  = Mathf.Clamp(_comboStep, 0, group.ChainLength - 1);
            var skill = group.GetStep(step);
            if (skill == null) return;

            int dmgFinal = Mathf.RoundToInt((_stats?.FinalAttack ?? 10) * skill.damageMultiplier);
            AttributeType attrFinal = _stats?.AttackAttribute ?? AttributeType.None;

            _atkTimer = skill.cooltime;

            bool isLast = (step >= group.ChainLength - 1);
            if (isLast)
            {
                _comboStep        = 0;
                _comboWindowTimer = 0f;
            }
            else
            {
                _comboStep        = step + 1;
                _comboWindowTimer = skill.comboWindow;
            }

            // 애니메이션
            _anim.SetInteger(HashComboStep, step);
            int triggerHash = string.IsNullOrEmpty(skill.animTriggerOverride)
                ? HashAttack
                : Animator.StringToHash(skill.animTriggerOverride);
            _anim.SetTrigger(triggerHash);

            // Overlay: 모든 방향에서 발동 (_facingDir.y < 0.5f 핵 제거)
            // 트리거 발동 직전에 파라미터 동기화 (FixedUpdate 딜레이 방지)
            _overlayAnim?.SetFloat(HashMoveX, _facingDir.x);
            _overlayAnim?.SetFloat(HashMoveY, _facingDir.y);
            _overlayAnim?.SetTrigger(HashAttack);

            // 무기 소켓 애니메이션 (활 시위 등 무기별 모션)
            _weaponSocket?.TriggerWeaponAnim(triggerHash);

            ExecuteSkillStep(skill, dmgFinal, attrFinal);
            _stats?.ConsumeWeaponDurabilityOnHit();

            Debug.Log($"[PlayerController] 평타 {step + 1}타");
        }

        // ================================================================
        //  스킬 그룹 실행 (스킬1·2·궁극기)
        // ================================================================

        /// <summary>SkillGroupData 의 첫 번째 SkillData 를 즉시 실행한다.</summary>
        void ExecuteSkillGroup(SkillGroupData group)
        {
            var skill = group.GetStep(0);
            if (skill == null) return;

            int           dmg  = Mathf.RoundToInt((_stats?.FinalAttack ?? 10) * skill.damageMultiplier);
            AttributeType attr = _stats?.AttackAttribute ?? AttributeType.None;

            // 애니메이션
            int triggerHash = string.IsNullOrEmpty(skill.animTriggerOverride)
                ? HashAttack
                : Animator.StringToHash(skill.animTriggerOverride);
            _anim.SetTrigger(triggerHash);

            // Overlay: 모든 방향에서 발동
            _overlayAnim?.SetFloat(HashMoveX, _facingDir.x);
            _overlayAnim?.SetFloat(HashMoveY, _facingDir.y);
            _overlayAnim?.SetTrigger(HashAttack);

            // 무기 소켓 애니메이션
            _weaponSocket?.TriggerWeaponAnim(triggerHash);

            ExecuteSkillStep(skill, dmg, attr);
            _stats?.ConsumeWeaponDurabilityOnHit();
        }

        // ================================================================
        //  SkillData 실행 — 데이터 필드로 공격 방식 결정
        // ================================================================

        /// <summary>SkillData 의 필드 값에 따라 근거리/투사체 분기 실행.</summary>
        void ExecuteSkillStep(SkillData skill, int dmg, AttributeType attr)
        {
            if (skill.IsProjectile)
            {
                SpawnProjectile(skill, dmg, attr);
            }
            else if (skill.maxTargets == 1)
            {
                // 단일 근거리
                var target = FindNearestEnemy(skill.searchRange);
                if (target != null)
                    SingleMeleeHit(target, skill.attackRange, dmg, attr, skill);
            }
            else
            {
                // 범위 근거리 — OverlapCircle
                Vector2 center = (Vector2)transform.position + _facingDir * (skill.attackRange * 0.5f);
                AoeMeleeHit(center, skill.attackRange * 0.5f, skill.maxTargets, dmg, attr, skill);

                _gizmoAttackTime = Time.time;
                _gizmoRange      = skill.attackRange * 0.5f;
                _gizmoCenter     = center;
            }
        }

        // ================================================================
        //  근거리 공격
        // ================================================================

        /// <summary>가장 가까운 적 1명에게 즉시 데미지 (단일 타겟 fallback).</summary>
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
                _stats?.AddUltimateGaugeOnKill();
                _stats?.ConsumeWeaponDurabilityOnKill();
                Debug.Log("[PlayerController] 처치 게이지 적립");
            }
            // fallback 공격은 SkillData 없음 → CC 없음
        }

        /// <summary>단일 타겟 근거리 (SkillData.maxTargets == 1).</summary>
        void SingleMeleeHit(Transform target, float attackRange, int dmg, AttributeType attr, Data.SkillData skill = null)
        {
            float dist = Vector2.Distance(transform.position, target.position);
            if (dist > attackRange) return;

            _gizmoAttackTime = Time.time;
            _gizmoRange      = attackRange;
            _gizmoCenter     = target.position;

            var hp = target.GetComponent<Health>();
            if (hp == null) return;

            bool wasDead = hp.IsDead;
            hp.TakeDamage(dmg, attr);
            if (!wasDead && hp.IsDead)
            {
                _stats?.AddUltimateGaugeOnKill();
                _stats?.ConsumeWeaponDurabilityOnKill();
                Debug.Log("[PlayerController] 처치 게이지 적립");
            }

            TryApplyCC(skill, target, transform.position);
        }

        /// <summary>범위 근거리 — OverlapCircle.</summary>
        void AoeMeleeHit(Vector2 center, float radius, int maxTargets, int dmg, AttributeType attr, Data.SkillData skill = null)
        {
            var hits = Physics2D.OverlapCircleAll(center, radius, enemyLayer);
            int count = 0;

            foreach (var col in hits)
            {
                if (count >= maxTargets) break;

                var hp = col.GetComponent<Health>();
                if (hp == null) continue;

                bool wasDead = hp.IsDead;
                hp.TakeDamage(dmg, attr);
                if (!wasDead && hp.IsDead)
                {
                    _stats?.AddUltimateGaugeOnKill();
                    Debug.Log("[PlayerController] 처치 게이지 적립");
                }

                TryApplyCC(skill, col.transform, transform.position);
                count++;
            }
        }

        // ================================================================
        //  투사체 공격
        // ================================================================

        void SpawnProjectile(SkillData skill, int dmg, AttributeType attr)
        {
            _gizmoAttackTime = Time.time;

            // 가장 가까운 적을 타겟으로 지정 — 없으면 페이싱 방향으로 직진
            var    target  = FindNearestEnemy(skill.searchRange);
            Vector2 fireDir = target != null
                ? ((Vector2)(target.position - transform.position)).normalized
                : _facingDir;

            var go = new GameObject(skill.IsAoe ? "AoEProjectile" : "Projectile");
            go.transform.position = transform.position;

            var proj = go.AddComponent<Projectile>();
            proj.Init(
                damage:            dmg,
                attr:              attr,
                direction:         fireDir,
                speed:             skill.missileSpeed,
                maxDistance:       skill.missileMaxRange,
                targetLayer:       enemyLayer,
                isAoe:             skill.IsAoe,
                aoeRadius:         skill.attackRange,
                maxTargets:        skill.maxTargets,
                onKill:            () =>
                {
                    _stats?.AddUltimateGaugeOnKill();
                    Debug.Log("[PlayerController] 처치 게이지 적립");
                },
                homingTarget:      target,
                ccForce:       skill.ccForce,
                ccDuration:    skill.ccDuration,
                stunDuration:  skill.stunDuration,
                fireSourcePos: transform.position
            );
        }

        // ================================================================
        //  CC 적용 헬퍼
        // ================================================================

        /// <summary>
        /// SkillData 의 CC 파라미터를 읽어 대상에게 적용한다.
        /// 우선순위: knockbackForce > stunDuration > pullInForce.
        /// skill 이 null 이거나 모든 CC 값이 0 이면 아무것도 하지 않는다.
        /// </summary>
        void TryApplyCC(SkillData skill, Transform target, Vector2 sourcePos)
        {
            if (skill == null || target == null) return;

            var cc = target.GetComponent<Combat.CrowdControlComponent>();
            if (cc == null) return;

            if (skill.ccForce > 0f)
            {
                // 양수 → 넉백 (시전자 반대 방향)
                Vector2 dir = ((Vector2)target.position - sourcePos).normalized;
                cc.TryApplyKnockback(dir, skill.ccForce, skill.ccDuration);
            }
            else if (skill.ccForce < 0f)
            {
                // 음수 → 풀인 (시전자 방향으로, 절댓값을 힘으로 사용)
                cc.TryApplyPullIn(sourcePos, -skill.ccForce, skill.ccDuration);
            }
            else if (skill.stunDuration > 0f)
            {
                cc.TryApplyStun(skill.stunDuration);
            }
        }

        // ================================================================
        //  내부 유틸
        // ================================================================

        /// <summary>
        /// NavMapProvider 가 있으면 벽 슬라이딩을 적용한 목표 속도를 반환한다.
        /// 맵이 없으면 입력 방향 × 속도를 그대로 반환 (기존 동작 유지).
        ///
        /// 슬라이딩 우선순위:
        ///   1. 원하는 방향 전체 이동 가능 → 그대로
        ///   2. X 축만 이동 가능           → 수평 슬라이드
        ///   3. Y 축만 이동 가능           → 수직 슬라이드
        ///   4. 둘 다 불가                 → 정지
        /// </summary>
        Vector2 ComputeNavVelocity(Vector2 inputDir, float speed)
        {
            if (inputDir.sqrMagnitude < 0.01f)
                return Vector2.zero;

            var grid = NavGrid.Instance;
            if (grid == null)
                return inputDir.normalized * speed; // NavGrid 없는 씬 → 직선 이동

            float   dt      = Time.fixedDeltaTime;
            Vector2 pos     = transform.position;
            Vector2 normDir = inputDir.normalized;
            Vector2 desired = pos + normDir * speed * dt;

            // 원하는 방향으로 이동 가능
            if (grid.IsWalkable(desired))
                return normDir * speed;

            // 수평 슬라이드 (X 방향만)
            if (Mathf.Abs(normDir.x) > 0.01f)
            {
                Vector2 hPos = new Vector2(desired.x, pos.y);
                if (grid.IsWalkable(hPos))
                    return new Vector2(normDir.x, 0f) * speed;
            }

            // 수직 슬라이드 (Y 방향만)
            if (Mathf.Abs(normDir.y) > 0.01f)
            {
                Vector2 vPos = new Vector2(pos.x, desired.y);
                if (grid.IsWalkable(vPos))
                    return new Vector2(0f, normDir.y) * speed;
            }

            return Vector2.zero;
        }

        float GetCurrentSearchRange()
        {
            var group = _stats?.NormalAttackGroup;
            if (group == null || group.ChainLength == 0) return fallbackSearchRange;

            int  step  = Mathf.Clamp(_comboStep, 0, group.ChainLength - 1);
            var  skill = group.GetStep(step);
            return skill != null ? skill.searchRange : fallbackSearchRange;
        }

        Transform FindNearestEnemy(float range)
        {
            var hits    = Physics2D.OverlapCircleAll(transform.position, range, enemyLayer);
            Transform nearest = null;
            float     minDist = float.MaxValue;

            foreach (var hit in hits)
            {
                float d = Vector2.Distance(transform.position, hit.transform.position);
                if (d < minDist) { minDist = d; nearest = hit.transform; }
            }
            return nearest;
        }

        /// <summary>
        /// 적 없을 때 인접한 채집 노드를 공격한다.
        /// attackRange 내 가장 가까운 미소진 ResourceNode 에 무기 데미지를 전달.
        /// "ResourceNode" 레이어는 런타임에 자동 계산 — Inspector 연결 불필요.
        /// </summary>
        void TryHarvestNode()
        {
            int nodeLayerIdx = LayerMask.NameToLayer("ResourceNode");
            if (nodeLayerIdx < 0) return;          // 레이어 미등록 시 채집 생략
            LayerMask nodeLayer = 1 << nodeLayerIdx;

            float attackRange = GetCurrentAttackRange();
            var hits = Physics2D.OverlapCircleAll(transform.position, attackRange, nodeLayer);

            ResourceNode nearest  = null;
            float        minDist  = float.MaxValue;
            foreach (var h in hits)
            {
                var node = h.GetComponent<ResourceNode>();
                if (node == null || node.Depleted) continue;
                float d = Vector2.Distance(transform.position, h.transform.position);
                if (d < minDist) { minDist = d; nearest = node; }
            }
            if (nearest == null) return;

            int dmg = _stats != null ? _stats.FinalAttack : 10;
            _atkTimer  = GetCurrentCooltime();
            _facingDir = ((Vector2)(nearest.transform.position - transform.position)).normalized;
            _weaponSocket?.SetFacingDirection(_facingDir);
            _anim.SetTrigger(HashAttack);

            nearest.TakeHarvestDamage(dmg, _stats);
            _stats?.ConsumeWeaponDurabilityOnHit();
        }

        float GetCurrentAttackRange()
        {
            var group = _stats?.NormalAttackGroup;
            if (group == null || group.ChainLength == 0) return fallbackAttackRange;
            int  step  = Mathf.Clamp(_comboStep, 0, group.ChainLength - 1);
            var  skill = group.GetStep(step);
            return skill != null ? skill.attackRange : fallbackAttackRange;
        }

        float GetCurrentCooltime()
        {
            var group = _stats?.NormalAttackGroup;
            if (group == null || group.ChainLength == 0) return fallbackCooltime;
            int  step  = Mathf.Clamp(_comboStep, 0, group.ChainLength - 1);
            var  skill = group.GetStep(step);
            return skill != null ? skill.cooltime : fallbackCooltime;
        }

        // ================================================================
        //  Health 이벤트
        // ================================================================

        void OnPlayerDamaged(int amount, AttributeType attr) =>
            Debug.Log($"[PlayerController] 피격 -{amount}  속성:{attr}  HP:{_health?.CurrentHp}/{_health?.MaxHp}");

        void OnPlayerDied(AttributeType killAttr) =>
            Debug.Log($"[PlayerController] 사망  막타속성:{killAttr}");

        /// <summary>
        /// 무기 교체 이벤트 핸들러.
        /// PlayerStats.EquipWeapon() 호출 시 발동 — WeaponSocket 시각 갱신.
        /// </summary>
        void OnWeaponChanged(Data.WeaponData weapon) => _weaponSocket?.SetWeapon(weapon);

        // ================================================================
        //  대시
        // ================================================================

        System.Collections.IEnumerator DashCoroutine()
        {
            _isDashing = true;
            if (_health != null) _health.IsInvincible = true;

            Vector2 dir = _moveDir.sqrMagnitude > 0.01f ? _moveDir.normalized : _facingDir;
            _anim.SetTrigger(HashDash);

            float elapsed = 0f;
            while (elapsed < dashDuration)
            {
                _rb.linearVelocity = dir * dashSpeed;
                elapsed += Time.fixedDeltaTime;
                yield return _waitFixed;
            }

            // 대시 종료 — 관성 초기화 (대시 속도가 그대로 남지 않도록)
            _currentVel        = Vector2.zero;
            _rb.linearVelocity = Vector2.zero;
            _isDashing         = false;
            _dashCooldownTimer = dashCooldown;
            if (_health != null) _health.IsInvincible = false;
        }

        // ================================================================
        //  Gizmo
        // ================================================================

        void OnDrawGizmos()
        {
            float searchRange = Application.isPlaying ? GetCurrentSearchRange() : fallbackSearchRange;

            // 자동 감지 범위 (파란색)
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f);
            Gizmos.DrawSphere(transform.position, searchRange);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, searchRange);

            // 마지막 공격 히트박스 (0.3초간 플래시)
            if (Application.isPlaying && (Time.time - _gizmoAttackTime) < 0.3f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_gizmoCenter, _gizmoRange);
            }
        }
    }
}
