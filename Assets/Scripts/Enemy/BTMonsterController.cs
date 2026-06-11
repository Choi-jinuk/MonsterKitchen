using MonsterKitchen.Core;
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
    public class BTMonsterController : MonsterBase, IBTBlackboardInitializer, IMonsterSeparation
    {
        [Header("Movement")]
        [SerializeField] float m_DetectRangeBonus = 1.5f;

        [Header("Separation Steering")]
        [SerializeField] float     m_SeparationRadius = 0.9f;
        [SerializeField] float     m_SeparationWeight = 2.0f;
        [SerializeField] LayerMask m_MonsterLayer;

        [Header("Projectile (원거리 스킬 전용)")]
        [SerializeField] LayerMask m_PlayerLayer;
        // moveSpeed 는 MonsterData.moveSpeed 에서 읽는다 (인스펙터 아님)
        // 투사체 프리팹은 SkillData.projectilePrefabAddress → AssetLoadManager 에서 로드

        BTRunner             m_BtRunner;
        Rigidbody2D          m_Rb;
        Transform            m_PlayerRef;
        MonsterMovementBase  m_Movement;

        readonly Collider2D[] m_SepBuffer = new Collider2D[12];

        static readonly int s_HashDie = Animator.StringToHash("Die");

        // ── 공개 API ────────────────────────────────────────────────────

        public Rigidbody2D          Rb       => m_Rb;
        public MonsterMovementBase  Movement => m_Movement;

        // ================================================================
        //  Init — SpawnManager 패턴
        // ================================================================

        public override void Init(MonsterData data, Transform player, Vector3 spawnPos)
        {
            m_Rb        = GetComponent<Rigidbody2D>();
            m_BtRunner  = GetComponent<BTRunner>();
            m_PlayerRef = player;

            base.Init(data, player, spawnPos);

            // BTAsset 을 데이터 주소에서 로드해 BTRunner 에 연결
            // BTRunner.Start() 는 SetActive(true) 이후 자동 호출되므로 Init 단계에서 설정해야 함
            if (data != null && !string.IsNullOrEmpty(data.BtAssetAddress))
            {
                var btAsset = MonsterKitchen.Core.AssetLoadManager.Instance?.Load<BTAsset>(data.BtAssetAddress);
                if (m_BtRunner != null && btAsset != null)
                    m_BtRunner.SetAsset(btAsset);
            }

            // 이동 컴포넌트 초기화 (SlimeMovement 등)
            var anim = GetComponent<Animator>();
            m_Movement = GetComponent<MonsterMovementBase>();
            if (m_Movement != null)
            {
                m_Movement.Init(m_Rb, anim, this, Data != null ? Data.MoveSpeed : 2.5f);
                m_Movement.InitNavAgent();
                m_Movement.OnSpawned();
            }
        }

        // ================================================================
        //  IBTBlackboardInitializer
        // ================================================================

        public void InitializeBlackboard(BTBlackboard bb)
        {
            bb.Set("Player",    m_PlayerRef != null ? m_PlayerRef : FindPlayer());
            bb.Set("SpawnPos",  (Vector3)transform.position);
            bb.Set("MoveSpeed", Data != null ? Data.MoveSpeed : 2.5f);

            var groups = Data?.SkillGroups;
            float detectRange   = 5f + m_DetectRangeBonus;
            float preferredDist = 1f;

            if (groups != null && groups.Length > 0)
            {
                float maxSearch   = 0f;
                float minAttack   = float.MaxValue;
                foreach (var g in groups)
                {
                    var s = g?.GetStep(0);
                    if (s == null) continue;
                    if (s.SearchRange  > maxSearch) maxSearch = s.SearchRange;
                    if (s.AttackRange  < minAttack) minAttack = s.AttackRange;
                }
                // DetectRange: 탐지 범위 (SearchRange 기반)
                detectRange   = maxSearch + m_DetectRangeBonus;
                // PreferredDistance: Chase 목표 거리 = AttackRange 이내로 접근
                preferredDist = minAttack == float.MaxValue ? 1f : minAttack;
            }

            bb.Set("DetectRange",       detectRange);
            bb.Set("PreferredDistance", preferredDist);
        }

        // ================================================================
        //  사망
        // ================================================================

        protected override void OnDied(AttributeType killAttr)
        {
            m_Rb.linearVelocity = Vector2.zero;
            m_BtRunner?.AbortTree();

            GetComponent<Animator>()?.SetTrigger(s_HashDie);

            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            Destroy(gameObject, 1.5f);
        }

        // ================================================================
        //  Separation Steering (BTAction 에서 호출)
        // ================================================================

        public Vector2 ApplySeparation(Vector2 desiredDir)
        {
            int count = Physics2D.OverlapCircle(
                transform.position, m_SeparationRadius,
                new ContactFilter2D { layerMask = m_MonsterLayer, useLayerMask = true },
                m_SepBuffer);

            if (count == 0) return desiredDir;

            Vector2 separation = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                if (m_SepBuffer[i] == null || m_SepBuffer[i].gameObject == gameObject) continue;
                Vector2 away = (Vector2)transform.position - (Vector2)m_SepBuffer[i].transform.position;
                float   dist = away.magnitude;
                if (dist < 0.001f) continue;
                separation += away.normalized / (dist * dist);
            }

            if (separation.sqrMagnitude < 0.0001f) return desiredDir;

            Vector2 perp    = new Vector2(-desiredDir.y, desiredDir.x);
            float   slide   = Vector2.Dot(separation.normalized, perp);
            Vector2 blended = desiredDir + perp * (slide * m_SeparationWeight);
            return blended.sqrMagnitude > 0.001f ? blended.normalized : desiredDir;
        }

        // ================================================================
        //  원거리 투사체 발사 (BTAction_Attack 에서 호출)
        // ================================================================

        public void FireProjectile(SkillData skill, int damage, AttributeType attr, Vector2 direction)
        {
            if (string.IsNullOrEmpty(skill.ProjectilePrefabAddress))
            {
                DebugUtil.LogError($"[BTMonsterController] {name}: SkillData({skill.SkillId}).projectilePrefabAddress 가 비어있습니다.", this);
                return;
            }

            var prefab = MonsterKitchen.Core.AssetLoadManager.Instance?.Load<GameObject>(skill.ProjectilePrefabAddress);
            if (prefab == null)
            {
                DebugUtil.LogError($"[BTMonsterController] {name}: 투사체 프리팹 로드 실패 — 키: {skill.ProjectilePrefabAddress}", this);
                return;
            }

            var go   = Instantiate(prefab, transform.position, Quaternion.identity);
            var proj = go.GetComponent<Projectile>();
            if (proj != null)
                proj.Init(damage, attr, direction, skill.MissileSpeed, skill.MissileMaxRange, m_PlayerLayer);
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
