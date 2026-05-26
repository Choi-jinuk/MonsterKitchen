using MonsterKitchen.AI.BehaviorTree;
using MonsterKitchen.Combat;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    /// <summary>
    /// MonsterBase + BTRunner 를 연결하는 컨트롤러.
    ///
    /// · MonsterBase 의 Health / DropResolver / MonsterData 관리
    /// · IBTBlackboardInitializer 를 구현해 BTRunner.Start() 전에 블랙보드를 채운다.
    /// · 사망 시 BTRunner.AbortTree() → 애니메이션 → Destroy
    /// </summary>
    [RequireComponent(typeof(BTRunner))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    public class BTMonsterController : MonsterBase, IBTBlackboardInitializer
    {
        [Header("Movement")]
        [SerializeField] float moveSpeed        = 2.5f;
        [SerializeField] float detectRangeBonus = 1.5f;

        [Header("Separation Steering")]
        [SerializeField] float     separationRadius = 0.9f;
        [SerializeField] float     separationWeight = 2.0f;
        [SerializeField] LayerMask monsterLayer;

        [Header("Projectile (원거리 스킬 전용)")]
        [SerializeField] GameObject _projectilePrefab;
        [SerializeField] LayerMask  _playerLayer;

        BTRunner    _btRunner;
        Rigidbody2D _rb;
        Transform   _playerRef;

        readonly Collider2D[] _sepBuffer = new Collider2D[12];

        static readonly int HashDie = Animator.StringToHash("Die");

        // ── 공개 API ────────────────────────────────────────────────────

        public Rigidbody2D Rb => _rb;

        // ================================================================
        //  Init — SpawnManager 패턴
        // ================================================================

        public override void Init(MonsterData data, Transform player, Vector3 spawnPos)
        {
            _rb       = GetComponent<Rigidbody2D>();
            _btRunner = GetComponent<BTRunner>();
            _playerRef = player;

            base.Init(data, player, spawnPos);
            // BTRunner.Start() 는 SetActive(true) 이후 자동 호출된다.
        }

        // ================================================================
        //  IBTBlackboardInitializer
        // ================================================================

        public void InitializeBlackboard(BTBlackboard bb)
        {
            bb.Set("Player",   _playerRef != null ? _playerRef : FindPlayer());
            bb.Set("SpawnPos", (Vector3)transform.position);
            bb.Set("MoveSpeed", moveSpeed);

            var skills = Data?.skills;
            float detectRange  = 5f + detectRangeBonus;
            float preferredDist = 1f;

            if (skills != null && skills.Length > 0)
            {
                float maxSearch = 0f;
                float minSearch = float.MaxValue;
                foreach (var s in skills)
                {
                    if (s == null) continue;
                    if (s.searchRange > maxSearch) maxSearch = s.searchRange;
                    if (s.searchRange < minSearch) minSearch = s.searchRange;
                }
                detectRange  = maxSearch + detectRangeBonus;
                preferredDist = minSearch == float.MaxValue ? 1f : minSearch;
            }

            bb.Set("DetectRange",        detectRange);
            bb.Set("PreferredDistance",  preferredDist);
        }

        // ================================================================
        //  사망
        // ================================================================

        protected override void OnDied(AttributeType killAttr)
        {
            _rb.linearVelocity = Vector2.zero;
            _btRunner?.AbortTree();

            GetComponent<Animator>()?.SetTrigger(HashDie);

            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            MonsterRespawnManager.Instance?.NotifyDeath(this);
            Destroy(gameObject, 1.5f);
        }

        // ================================================================
        //  Separation Steering (BTTask 에서 호출)
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

            Vector2 perp    = new Vector2(-desiredDir.y, desiredDir.x);
            float   slide   = Vector2.Dot(separation.normalized, perp);
            Vector2 blended = desiredDir + perp * (slide * separationWeight);
            return blended.sqrMagnitude > 0.001f ? blended.normalized : desiredDir;
        }

        // ================================================================
        //  원거리 투사체 발사 (BTAction_Attack 에서 호출)
        // ================================================================

        public void FireProjectile(SkillData skill, int damage, AttributeType attr, Vector2 direction)
        {
            if (_projectilePrefab == null)
            {
                Debug.LogWarning($"[BTMonsterController] {name}: ProjectilePrefab 이 연결되지 않았습니다.");
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

        Transform FindPlayer()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            return go != null ? go.transform : null;
        }
    }
}
