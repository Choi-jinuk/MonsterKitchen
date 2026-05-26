using System.Collections;
using MonsterKitchen.Combat;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Navigation;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  MonsterAI — FSM (Idle / Patrol / Chase / Attack / Die)
    //
    //  ▶ 공통 역할: 감지 · 상태 전환 · 스킬 공격 · 사망 · Separation Steering
    //
    //  ▶ 스킬 시스템
    //    MonsterData.skills[] (최대 3개) 를 읽어 동작한다.
    //    · DetectRange    = max(skill.searchRange) + detectRangeBonus
    //    · PreferredDist  = min(skill.searchRange)  → 이 거리 안으로 들어오면 Attack 상태 진입
    //    · Attack 상태에서 각 스킬을 우선순위(인덱스 0↑) 순으로 탐색하여
    //      거리 & 쿨타임 조건을 만족하는 첫 번째 스킬을 발동한다.
    //    · missileSpeed > 0 → MonsterProjectile 발사
    //    · missileSpeed = 0 → OverlapCircle 근거리 데미지
    //
    //  ▶ 이동 위임 (컴포넌트 패턴)
    //    [SerializeField] MonsterMovementBase _movement 슬롯에
    //    몬스터 전용 이동 컴포넌트(예: SlimeMovement)를 연결한다.
    //    null 이면 단순 직선 이동으로 fallback.
    //
    //  ▶ Separation Steering
    //    물리 충돌은 SpawnManager.Awake 에서 꺼놓는다.
    //    ApplySeparation() 이 public 이므로 이동 컴포넌트에서 호출 가능.
    // ====================================================================

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CrowdControlComponent))]
    public class MonsterAI : MonsterBase
    {
        static readonly Collider2D[] _overlapBuffer = new Collider2D[16];

        [Header("AI")]
        [SerializeField] float moveSpeed = 2.5f;
        [Tooltip("가장 긴 스킬 감지 범위에 더하는 여유 거리.\n이 범위 안에 들어오면 추적을 시작한다.")]
        [SerializeField] float _detectRangeBonus = 1.5f;

        [Header("Patrol")]
        [SerializeField] float patrolRadius = 3f;
        [SerializeField] float patrolWait   = 1.5f;

        [Header("Movement")]
        [Tooltip("이동 패턴 컴포넌트. null 이면 단순 직선 이동(fallback).")]
        [SerializeField] MonsterMovementBase _movement;

        [Header("Projectile (원거리 스킬 전용)")]
        [Tooltip("missileSpeed > 0 인 스킬이 있을 때 발사할 투사체 프리팹.")]
        [SerializeField] GameObject _projectilePrefab;
        [Tooltip("플레이어 레이어 마스크 — 투사체 충돌 판정용.")]
        [SerializeField] LayerMask  _playerLayer;

        [Header("Separation Steering")]
        [SerializeField] float     separationRadius = 0.9f;
        [SerializeField] float     separationWeight = 2.0f;
        [Tooltip("Enemy 레이어 마스크 — 동료 몬스터 감지용")]
        [SerializeField] LayerMask monsterLayer;

        // ── 런타임 상태 ─────────────────────────────────────────────────

        enum State { Idle, Patrol, Chase, Attack, Die }

        State       _state = State.Idle;
        Transform   _player;
        Rigidbody2D _rb;
        Animator    _anim;
        Coroutine   _aiCoroutine;

        Vector2 _patrolTarget;
        float   _patrolWaitTimer;
        float[] _skillTimers;   // 스킬별 남은 쿨타임
        Vector3 _spawnPos;
        float   _attackGizmoTime = -1f;

        readonly Collider2D[] _sepBuffer = new Collider2D[16];

        // ── CC 컴포넌트 ──────────────────────────────────────────────────
        CrowdControlComponent _cc;
        bool _prevCCActive;

        // ── Nav 폴백 (_movement 없을 때 MonsterAI 자체 경로 탐색) ────
        NavAgent _navAgent;

        static readonly WaitForFixedUpdate WaitFixed = new WaitForFixedUpdate();

        static readonly int HashMoveX  = Animator.StringToHash("MoveX");
        static readonly int HashMoveY  = Animator.StringToHash("MoveY");
        static readonly int HashSpeed  = Animator.StringToHash("Speed");
        static readonly int HashAttack = Animator.StringToHash("Attack");
        static readonly int HashDie    = Animator.StringToHash("Die");

        // ── 스킬 데이터 접근 ────────────────────────────────────────────

        SkillData[] Skills => monsterData?.skills;

        /// <summary>감지 범위 = 가장 긴 스킬의 searchRange + 여유 거리.</summary>
        float DetectRange
        {
            get
            {
                var skills = Skills;
                if (skills == null || skills.Length == 0) return 5f + _detectRangeBonus;
                float max = 0f;
                foreach (var s in skills)
                    if (s != null && s.searchRange > max) max = s.searchRange;
                return max + _detectRangeBonus;
            }
        }

        /// <summary>선호 거리 = 가장 짧은 스킬의 searchRange. Attack 상태 진입 기준.</summary>
        float PreferredDistance
        {
            get
            {
                var skills = Skills;
                if (skills == null || skills.Length == 0) return 1.0f;
                float min = float.MaxValue;
                foreach (var s in skills)
                    if (s != null && s.searchRange < min) min = s.searchRange;
                return min == float.MaxValue ? 1.0f : min;
            }
        }

        // ================================================================
        //  Init — SpawnManager 가 SetActive(true) 전에 호출
        // ================================================================

        public override void Init(MonsterData data, Transform player, Vector3 spawnPos)
        {
            _rb   = GetComponent<Rigidbody2D>();
            _anim = GetComponent<Animator>();
            _cc   = GetComponent<CrowdControlComponent>();

            base.Init(data, player, spawnPos);

            _player       = player;
            _spawnPos     = spawnPos;
            _patrolTarget = (Vector2)spawnPos;

            // 스킬 타이머 초기화 (최대 3개)
            var skills = Skills;
            int count  = skills != null ? Mathf.Min(skills.Length, 3) : 0;
            _skillTimers = new float[count];

            // _movement 없을 때 폴백용 nav agent (NavGrid 있으면 생성)
            if (NavGrid.Instance != null)
                _navAgent = new NavAgent(transform);

            // 이동 컴포넌트 초기화 및 스폰 콜백
            if (_movement != null)
            {
                _movement.Init(_rb, _anim, this);
                _movement.InitNavAgent();      // NavGrid 유무를 스스로 판단
                _movement.OnSpawned();
            }
        }

        // ================================================================
        //  Mono — OnEnable / OnDisable
        // ================================================================

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_initialized && _state != State.Die)
                StartAiCoroutine();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopAiCoroutine();
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            _navAgent?.Stop();   // 폴백 nav agent 초기화
            _cc?.ForceRelease(); // CC 진행 중이면 강제 해제
        }

        // ================================================================
        //  코루틴 AI 루프
        // ================================================================

        void StartAiCoroutine()
        {
            if (_aiCoroutine != null) return;
            _aiCoroutine = StartCoroutine(AiLoop());
        }

        void StopAiCoroutine()
        {
            if (_aiCoroutine == null) return;
            StopCoroutine(_aiCoroutine);
            _aiCoroutine = null;
        }

        IEnumerator AiLoop()
        {
            while (true)
            {
                Tick(Time.fixedDeltaTime);
                yield return WaitFixed;
            }
        }

        // ================================================================
        //  FSM 틱
        // ================================================================

        void Tick(float dt)
        {
            if (_state == State.Die) return;

            // 모든 스킬 쿨타임 감소 (CC 중에도 계속 감소)
            if (_skillTimers != null)
                for (int i = 0; i < _skillTimers.Length; i++)
                    if (_skillTimers[i] > 0f) _skillTimers[i] -= dt;

            // CC 진행 중: FSM 일시 정지 (CrowdControlComponent 가 속도를 직접 제어)
            bool ccActive = _cc != null && _cc.IsUnderControl;
            if (ccActive)
            {
                _prevCCActive = true;
                return;
            }

            // CC 가 방금 끝난 직후: 이동 컴포넌트 상태를 초기화해 즉시 재개
            if (_prevCCActive)
            {
                _prevCCActive          = false;
                _rb.linearVelocity     = Vector2.zero;
                _movement?.ResetMovement();
            }

            switch (_state)
            {
                case State.Idle:   TickIdle(dt);   break;
                case State.Patrol: TickPatrol(dt);  break;
                case State.Chase:  TickChase(dt);   break;
                case State.Attack: TickAttack();    break;
            }
        }

        void TickIdle(float dt)
        {
            _rb.linearVelocity = Vector2.zero;
            _patrolWaitTimer  -= dt;

            if (_patrolWaitTimer <= 0f)
            {
                PickPatrolTarget();
                _state = State.Patrol;
            }
            else if (PlayerInRange(DetectRange))
            {
                _state = State.Chase;
            }
        }

        void TickPatrol(float dt)
        {
            if (PlayerInRange(DetectRange))
            {
                _movement?.ResetMovement();
                _state = State.Chase;
                return;
            }

            Vector2 toPatrol = (Vector2)_patrolTarget - (Vector2)transform.position;
            if (toPatrol.magnitude < 0.2f)
            {
                _movement?.ResetMovement();
                _rb.linearVelocity = Vector2.zero;
                SetMoveAnim(Vector2.zero);
                _patrolWaitTimer = patrolWait;
                _state           = State.Idle;
                return;
            }

            if (_movement != null)
                _movement.TickPatrol(dt, _patrolTarget);
            else
            {
                var navDir = GetFallbackNavDirection(_patrolTarget);
                Move(ApplySeparation(navDir) * (moveSpeed * 0.6f));
            }
        }

        void TickChase(float dt)
        {
            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                _player = go != null ? go.transform : null;
                if (_player == null) { _state = State.Idle; return; }
            }

            float dist      = Vector2.Distance(transform.position, _player.position);
            float preferred = PreferredDistance;

            if (dist > DetectRange * 1.5f)
            {
                _movement?.ResetMovement();
                _state = State.Patrol;
                return;
            }
            if (dist <= preferred)
            {
                _movement?.ResetMovement();
                _state = State.Attack;
                return;
            }

            if (_movement != null)
                _movement.TickChase(dt, _player.position, preferred);
            else
            {
                var navDir = GetFallbackNavDirection(_player.position);
                Move(ApplySeparation(navDir) * moveSpeed);
            }
        }

        void TickAttack()
        {
            if (_player == null) { _state = State.Idle; return; }

            float   dist      = Vector2.Distance(transform.position, _player.position);
            float   preferred = PreferredDistance;
            Vector2 toPlayer  = ((Vector2)_player.position - (Vector2)transform.position).normalized;

            // 적정 거리(preferred)를 벗어나면 즉시 Chase로 전환
            // — 넉백 등으로 밀려난 경우 슬라임 고유 대시 사이클(윈드업→대시)로 재접근
            if (dist > preferred) { _state = State.Chase; return; }

            // 너무 가까우면 살짝 후퇴, 적정 거리 내면 정지하고 공격
            if (dist < preferred * 0.5f)
                Move(ApplySeparation(-toPlayer) * (moveSpeed * 0.5f));
            else
            {
                _rb.linearVelocity = Vector2.zero;
                SetMoveAnim(Vector2.zero);
            }

            // 스킬 발동 — 우선순위(인덱스 0↑) 순, 가능한 첫 번째 스킬 사용
            var skills = Skills;
            if (skills == null) return;

            int limit = Mathf.Min(skills.Length, _skillTimers?.Length ?? 0, 3);
            for (int i = 0; i < limit; i++)
            {
                var skill = skills[i];
                if (skill == null) continue;
                if (_skillTimers[i] > 0f) continue;     // 쿨타임 중
                if (dist > skill.searchRange) continue;  // 사거리 밖

                ExecuteSkill(i, skill, toPlayer);
                break;  // 틱당 하나만 발동
            }
        }

        // ================================================================
        //  스킬 실행
        // ================================================================

        void ExecuteSkill(int index, SkillData skill, Vector2 toPlayer)
        {
            _skillTimers[index] = skill.cooltime;
            _attackGizmoTime    = Time.time;

            // 애니메이션 트리거
            int triggerHash = string.IsNullOrEmpty(skill.animTriggerOverride)
                ? HashAttack
                : Animator.StringToHash(skill.animTriggerOverride);
            _anim.SetTrigger(triggerHash);

            // 데미지 계산: 몬스터 기본 공격력 × 스킬 배율
            int           baseDmg = monsterData != null ? monsterData.attack : 5;
            int           damage  = Mathf.Max(1, Mathf.RoundToInt(baseDmg * skill.damageMultiplier));
            AttributeType attr    = monsterData != null ? monsterData.attribute : AttributeType.None;

            if (skill.IsProjectile)
                FireProjectile(skill, damage, attr, toPlayer);
            else
                ApplyMeleeDamage(skill, damage, attr);
        }

        void ApplyMeleeDamage(SkillData skill, int damage, AttributeType attr)
        {
            if (_player == null) return;

            // 단일 대상이거나 playerLayer 미설정: 플레이어 직접 참조
            if (skill.maxTargets <= 1 || _playerLayer.value == 0)
            {
                if (Vector2.Distance(transform.position, _player.position) <= skill.attackRange)
                {
                    var hp = _player.GetComponent<Health>();
                    hp?.TakeDamage(damage, attr);
                }
                return;
            }

            // 다중 대상: OverlapCircle 로 attackRange 내 최대 maxTargets 명
            var attackFilter = new ContactFilter2D();
            attackFilter.SetLayerMask(_playerLayer);
            attackFilter.useTriggers = true;
            int hitCount = Physics2D.OverlapCircle(transform.position, skill.attackRange, attackFilter, _overlapBuffer);
            int count = 0;
            for (int i = 0; i < hitCount; i++)
            {
                if (count >= skill.maxTargets) break;
                var hp = _overlapBuffer[i].GetComponent<Health>();
                if (hp == null) continue;
                hp.TakeDamage(damage, attr);
                count++;
            }
        }

        void FireProjectile(SkillData skill, int damage, AttributeType attr, Vector2 direction)
        {
            if (_projectilePrefab == null)
            {
                Debug.LogWarning($"[MonsterAI] {name}: 원거리 스킬이지만 ProjectilePrefab 이 연결되지 않았습니다.");
                return;
            }

            var go   = Instantiate(_projectilePrefab, transform.position, Quaternion.identity);
            var proj = go.GetComponent<Projectile>();
            if (proj != null)
                proj.Init(damage, attr, direction, skill.missileSpeed, skill.missileMaxRange, _playerLayer);
        }

        void EnterDie()
        {
            _state             = State.Die;
            _rb.linearVelocity = Vector2.zero;
            _anim.SetTrigger(HashDie);

            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            StopAiCoroutine();

            // 리스폰 매니저에 사망 통보 (등록된 경우에만 처리)
            MonsterRespawnManager.Instance?.NotifyDeath((MonsterBase)this);

            Destroy(gameObject, 1.5f);
        }

        protected override void OnDied(AttributeType killAttr) => EnterDie();

        // ================================================================
        //  Separation Steering — public: 이동 컴포넌트에서 호출 가능
        // ================================================================

        public Vector2 ApplySeparation(Vector2 desiredDir)
        {
            int count = Physics2D.OverlapCircle(
                transform.position, separationRadius,
                new ContactFilter2D { layerMask = monsterLayer, useLayerMask = true },
                _sepBuffer);

            if (count == 0) return desiredDir;

            Vector2 separation = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                if (_sepBuffer[i] == null || _sepBuffer[i].gameObject == gameObject) continue;

                Vector2 away = (Vector2)transform.position - (Vector2)_sepBuffer[i].transform.position;
                float   dist = away.magnitude;
                if (dist < 0.001f) continue;

                separation += away.normalized / (dist * dist);
            }

            if (separation.sqrMagnitude < 0.0001f) return desiredDir;

            Vector2 perpendicular = new Vector2(-desiredDir.y, desiredDir.x);
            float   slideAmount   = Vector2.Dot(separation.normalized, perpendicular);
            Vector2 blended       = desiredDir + perpendicular * (slideAmount * separationWeight);

            return blended.sqrMagnitude > 0.001f ? blended.normalized : desiredDir;
        }

        // ================================================================
        //  Helpers
        // ================================================================

        /// <summary>
        /// _movement 컴포넌트가 없을 때 MonsterAI 자체 폴백용 경로 방향.
        /// NavGrid 가 있으면 A* 다음 경유지 방향, 없으면 직선 방향을 반환한다.
        /// </summary>
        Vector2 GetFallbackNavDirection(Vector2 target)
        {
            Vector2 straight = (target - (Vector2)transform.position).normalized;

            if (_navAgent == null) return straight;

            _navAgent.SetDestination(target);
            var dir = _navAgent.GetDirection();
            return dir == Vector2.zero ? straight : dir;
        }

        void Move(Vector2 velocity)
        {
            _rb.linearVelocity = velocity;
            SetMoveAnim(velocity);
        }

        void SetMoveAnim(Vector2 velocity)
        {
            _anim.SetFloat(HashMoveX, velocity.x);
            _anim.SetFloat(HashMoveY, velocity.y);
            _anim.SetFloat(HashSpeed, velocity.magnitude);
        }

        bool PlayerInRange(float range)
        {
            if (_player == null) return false;
            return Vector2.Distance(transform.position, _player.position) <= range;
        }

        void PickPatrolTarget()
        {
            Vector2 offset = RandomUtil.InCircle(patrolRadius);
            _patrolTarget  = (Vector2)_spawnPos + offset;
        }

        // ================================================================
        //  Gizmo
        // ================================================================

        void OnDrawGizmos()
        {
            bool flashing = Application.isPlaying && (Time.time - _attackGizmoTime) < 0.3f;

            float detectR = DetectRange;

            // 감지 범위 (노랑)
            Gizmos.color = new Color(1f, 1f, 0f, 0.18f);
            Gizmos.DrawSphere(transform.position, detectR);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectR);

            // 분리 반경 (청록)
            Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
            Gizmos.DrawSphere(transform.position, separationRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, separationRadius);

            // 스킬별 범위 (인덱스 0 = 밝은색, 순서대로 어두워짐)
            var skills = Application.isPlaying ? Skills : null;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                skills = monsterData != null ? monsterData.skills : null;
#endif
            if (skills == null) return;

            int limit = Mathf.Min(skills.Length, 3);
            for (int i = 0; i < limit; i++)
            {
                var skill = skills[i];
                if (skill == null) continue;

                float brightness = 1f - i * 0.25f;

                // searchRange — 초록 계열 (스킬 발동 거리)
                Gizmos.color = new Color(0f, brightness, 0f, 0.08f);
                Gizmos.DrawSphere(transform.position, skill.searchRange);
                Gizmos.color = new Color(0f, brightness, 0f, 0.6f);
                Gizmos.DrawWireSphere(transform.position, skill.searchRange);

                // attackRange — 빨강 계열 (근거리 히트박스 / AoE 반경)
                bool skillFlash = flashing && i == 0; // 마지막 발동 스킬(0번 우선) 플래시
                Gizmos.color = skillFlash
                    ? new Color(brightness, 0f, 0f, 0.5f)
                    : new Color(brightness, 0.15f, 0.15f, 0.08f);
                Gizmos.DrawSphere(transform.position, skill.attackRange);
                Gizmos.color = skillFlash
                    ? new Color(1f, 0f, 0f, 0.9f)
                    : new Color(brightness, 0.3f, 0.3f, 0.5f);
                Gizmos.DrawWireSphere(transform.position, skill.attackRange);
            }
        }
    }
}
