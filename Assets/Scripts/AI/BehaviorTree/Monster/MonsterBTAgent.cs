using MonsterKitchen.Combat;
using MonsterKitchen.Data;
using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Monster
{
    /// <summary>
    /// 몬스터 행동 트리 에이전트.
    /// BTMonsterController 와 같은 GameObject 에 배치한다.
    ///
    /// 트리 구조 (MonsterAI FSM 과 동일한 우선순위):
    ///   Selector (Root)
    ///   ├─ Sequence [Attack]  : PlayerInRange(preferredDist) → Attack
    ///   ├─ Sequence [Chase]   : PlayerInRange(detectRange)   → MoveToPlayer
    ///   └─ Patrol (fallback)
    /// </summary>
    public class MonsterBTAgent : BTAgent
    {
        [Header("AI")]
        [SerializeField] float moveSpeed        = 2.5f;
        [SerializeField] float detectRangeBonus = 1.5f;

        [Header("Patrol")]
        [SerializeField] float patrolRadius = 3f;
        [SerializeField] float patrolWait   = 1.5f;

        [Header("Projectile (원거리 스킬 전용)")]
        [SerializeField] GameObject _projectilePrefab;
        [SerializeField] LayerMask  _playerLayer;

        // ── 런타임 상태 ─────────────────────────────────────────────────
        BTMonsterController _ctrl;
        Animator            _anim;
        Transform           _player;
        Vector3             _spawnPos;
        float[]             _skillTimers;

        // ── 공개 프로퍼티 (BTTask 에서 접근) ────────────────────────────

        public BTMonsterController Controller      => _ctrl;
        public Animator            Anim            => _anim;
        public Transform           PlayerTransform  => GetPlayer();
        public Vector3             SpawnPos        => _spawnPos;
        public float               MoveSpeed       => moveSpeed;
        public float               PatrolRadius    => patrolRadius;
        public float               PatrolWait      => patrolWait;
        public float[]             SkillTimers     => _skillTimers;

        /// <summary>감지 범위 = 가장 긴 스킬 searchRange + 여유 거리.</summary>
        public float DetectRange
        {
            get
            {
                var skills = _ctrl?.Data?.skills;
                if (skills == null || skills.Length == 0) return 5f + detectRangeBonus;
                float max = 0f;
                foreach (var s in skills)
                    if (s != null && s.searchRange > max) max = s.searchRange;
                return max + detectRangeBonus;
            }
        }

        /// <summary>선호 거리 = 가장 짧은 스킬 searchRange. Attack 상태 진입 기준.</summary>
        public float PreferredDistance
        {
            get
            {
                var skills = _ctrl?.Data?.skills;
                if (skills == null || skills.Length == 0) return 1.0f;
                float min = float.MaxValue;
                foreach (var s in skills)
                    if (s != null && s.searchRange < min) min = s.searchRange;
                return min == float.MaxValue ? 1.0f : min;
            }
        }

        // ================================================================
        //  Unity lifecycle
        // ================================================================

        protected override void Start()
        {
            _ctrl     = GetComponent<BTMonsterController>();
            _anim     = GetComponent<Animator>();
            _spawnPos = transform.position;

            // 스킬 타이머 초기화
            var skills = _ctrl?.Data?.skills;
            int count  = skills != null ? Mathf.Min(skills.Length, 3) : 0;
            _skillTimers = new float[count];

            base.Start(); // → BuildTree()
        }

        // ================================================================
        //  트리 구성
        // ================================================================

        protected override BTNode BuildTree()
        {
            return new BTSelector()
                .Add(new BTSequence()
                    .Add(new BTCondition_PlayerInRange(this, PreferredDistance))
                    .Add(new BTAction_Attack(this)))
                .Add(new BTSequence()
                    .Add(new BTCondition_PlayerInRange(this, DetectRange))
                    .Add(new BTAction_MoveToPlayer(this)))
                .Add(new BTAction_Patrol(this));
        }

        // ================================================================
        //  공개 유틸리티 (BTTask 에서 호출)
        // ================================================================

        /// <summary>스킬 쿨타임 전체 감소. BTAction_Attack 에서 매 틱 호출.</summary>
        public void TickSkillTimers(float dt)
        {
            if (_skillTimers == null) return;
            for (int i = 0; i < _skillTimers.Length; i++)
                if (_skillTimers[i] > 0f) _skillTimers[i] -= dt;
        }

        /// <summary>원거리 투사체 발사. BTAction_Attack 에서 호출.</summary>
        public void FireProjectile(SkillData skill, int damage, AttributeType attr, Vector2 direction)
        {
            if (_projectilePrefab == null)
            {
                Debug.LogWarning($"[MonsterBTAgent] {name}: 원거리 스킬이지만 ProjectilePrefab 이 연결되지 않았습니다.");
                return;
            }

            var go   = Instantiate(_projectilePrefab, transform.position, Quaternion.identity);
            var proj = go.GetComponent<Projectile>();
            if (proj != null)
                proj.Init(damage, attr, direction, skill.missileSpeed, skill.missileMaxRange, _playerLayer);
        }

        // ================================================================
        //  내부
        // ================================================================

        Transform GetPlayer()
        {
            if (_player != null) return _player;
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) _player = go.transform;
            return _player;
        }
    }
}
