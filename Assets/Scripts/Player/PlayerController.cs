using MonsterKitchen.Combat;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

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
    // ====================================================================

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Layer")]
        [SerializeField] LayerMask enemyLayer;

        [Header("Fallback (무기 미장착 시 사용되는 기본값)")]
        [SerializeField] float fallbackSearchRange = 2.5f;
        [SerializeField] float fallbackAttackRange = 0.8f;
        [SerializeField] float fallbackCooltime    = 0.4f;

        [Header("Move")]
        [Tooltip("이동 가속도 (units/s²). 클수록 반응이 빠르고, 작을수록 미끄러지는 느낌.")]
        [SerializeField] float moveAcceleration = 30f;
        [Tooltip("공격 타이머가 남아있는 동안 이동 속도 배율. 0.5 = 절반 속도.")]
        [SerializeField] float attackMovePenalty = 0.5f;

        [Header("Dash")]
        [SerializeField] float dashSpeed    = 18f;
        [SerializeField] float dashDuration = 0.18f;
        [SerializeField] float dashCooldown = 0.9f;

        // ── 컴포넌트 참조 ─────────────────────────────────────────────
        [SerializeField] Rigidbody2D      _rb;
        [SerializeField] Animator         _anim;
        [SerializeField] Animator         _overlayAnim;  // Overlay 자식 Animator
        [SerializeField] SpriteRenderer[] _sprites;   // 자신 + 모든 자식 SpriteRenderer
        [SerializeField] Health           _health;
        [SerializeField] PlayerStats    _stats;

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
            _initialized = true;

            Debug.Log("[PlayerController] Init 완료");
        }

#if UNITY_EDITOR
        // 에디터에서 컴포넌트 추가 또는 Reset 시 자동 배선.
        // 빌드에 포함되지 않으므로 런타임 비용 없음.
        void Reset()
        {
            _rb     = GetComponent<Rigidbody2D>();
            _anim   = GetComponent<Animator>();
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
            _health = GetComponent<Health>();
            _stats  = GetComponent<PlayerStats>();
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

            // 자동 공격: 쿨타임 종료 + 감지 범위 내 적 존재 시
            if (_atkTimer <= 0f)
            {
                float searchRange = GetCurrentSearchRange();
                var   target      = FindNearestEnemy(searchRange);
                if (target != null)
                {
                    _facingDir = ((Vector2)(target.position - transform.position)).normalized;
                    DoComboAttack(target);
                }
            }
        }

        void FixedUpdate()
        {
            if (!_initialized || _isDashing) return;

            float speed = _stats != null ? _stats.FinalMoveSpeed : 5f;

            // 공격 타이머가 남아있으면 이동 속도 패널티 (공격 모션 중 느려짐)
            if (_atkTimer > 0f) speed *= attackMovePenalty;

            // MoveTowards 로 가속/감속 — 관성감 있는 이동
            Vector2 targetVel = _moveDir.normalized * speed;
            _currentVel = Vector2.MoveTowards(_currentVel, targetVel, moveAcceleration * Time.fixedDeltaTime);
            _rb.linearVelocity = _currentVel;

            if (_moveDir.sqrMagnitude > 0.01f)
                _facingDir = _moveDir.normalized;

            _anim.SetFloat(HashMoveX, _facingDir.x);
            _anim.SetFloat(HashMoveY, _facingDir.y);
            _anim.SetFloat(HashSpeed, _currentVel.magnitude);

            _overlayAnim?.SetFloat(HashMoveX, _facingDir.x);
            _overlayAnim?.SetFloat(HashMoveY, _facingDir.y);

            if (_facingDir.x != 0f && _sprites != null)
            {
                bool flip = _facingDir.x > 0f;
                foreach (var sr in _sprites) sr.flipX = flip;
            }
        }

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
            _overlayAnim?.SetTrigger(HashAttack);

            ExecuteSkillStep(skill, dmgFinal, attrFinal);

            Debug.Log($"[PlayerController] 평타 {step + 1}타 [{skill.skillName}]  " +
                      $"dmg×{skill.damageMultiplier:F2}→{dmgFinal}  " +
                      (isLast ? "(체인 종료)" : $"창:{skill.comboWindow:F1}s"));
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
            _overlayAnim?.SetTrigger(HashAttack);

            ExecuteSkillStep(skill, dmg, attr);
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
                    SingleMeleeHit(target, skill.attackRange, dmg, attr);
            }
            else
            {
                // 범위 근거리 — OverlapCircle
                Vector2 center = (Vector2)transform.position + _facingDir * (skill.attackRange * 0.5f);
                AoeMeleeHit(center, skill.attackRange * 0.5f, skill.maxTargets, dmg, attr);

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
            if (!wasDead && hp.IsDead) _stats?.AddUltimateGaugeOnKill();

            Debug.Log($"[PlayerController][Melee] → {target.name} -{dmg}");
        }

        /// <summary>단일 타겟 근거리 (SkillData.maxTargets == 1).</summary>
        void SingleMeleeHit(Transform target, float attackRange, int dmg, AttributeType attr)
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
            if (!wasDead && hp.IsDead) _stats?.AddUltimateGaugeOnKill();

            Debug.Log($"[PlayerController][MeleeTarget] → {target.name} -{dmg}");
        }

        /// <summary>범위 근거리 — OverlapCircle.</summary>
        void AoeMeleeHit(Vector2 center, float radius, int maxTargets, int dmg, AttributeType attr)
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
                if (!wasDead && hp.IsDead) _stats?.AddUltimateGaugeOnKill();

                count++;
            }

            Debug.Log($"[PlayerController][MeleeRange] center:{center} r:{radius:F2} 적중:{count}명");
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
                damage:         dmg,
                attr:           attr,
                direction:      fireDir,
                speed:          skill.missileSpeed,
                maxDistance:    skill.missileMaxRange,
                targetLayer:    enemyLayer,
                isAoe:          skill.IsAoe,
                aoeRadius:      skill.attackRange,
                maxTargets:     skill.maxTargets,
                onKill:         () => _stats?.AddUltimateGaugeOnKill(),
                homingTarget:   target
            );

            Debug.Log($"[PlayerController][Projectile] 발사  타겟:{target?.name ?? "없음"}  " +
                      $"속도:{skill.missileSpeed}  사거리:{skill.missileMaxRange:F1}  AoE:{skill.IsAoe}");
        }

        // ================================================================
        //  내부 유틸
        // ================================================================

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

        // ================================================================
        //  Health 이벤트
        // ================================================================

        void OnPlayerDamaged(int amount, AttributeType attr) =>
            Debug.Log($"[PlayerController] 피격 -{amount}  속성:{attr}  HP:{_health?.CurrentHp}/{_health?.MaxHp}");

        void OnPlayerDied(AttributeType killAttr) =>
            Debug.Log($"[PlayerController] 사망  막타속성:{killAttr}");

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
