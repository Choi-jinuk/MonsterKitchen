using MonsterKitchen.AI.BehaviorTree;
using MonsterKitchen.Combat;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    /// <summary>
    /// MonsterBase + BTAgent 를 연결하는 컨트롤러.
    ///
    /// MonsterAI(FSM) 를 대체하는 행동 트리 기반 몬스터용.
    ///
    /// · MonsterBase 의 Health / DropResolver / MonsterData 관리
    /// · 같은 GameObject 의 BTAgent 가 트리를 실행한다.
    /// · 사망 시 BTAgent.AbortTree() → 애니메이션 → 리스폰 알림 → Destroy
    /// </summary>
    [RequireComponent(typeof(BTAgent))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    public class BTMonsterController : MonsterBase
    {
        [Header("Separation Steering")]
        [SerializeField] float     separationRadius = 0.9f;
        [SerializeField] float     separationWeight = 2.0f;
        [SerializeField] LayerMask monsterLayer;

        BTAgent     _btAgent;
        Rigidbody2D _rb;
        Animator    _anim;

        readonly Collider2D[] _sepBuffer = new Collider2D[12];

        static readonly int HashDie = Animator.StringToHash("Die");

        // ── 공개 API (BTTask 등에서 접근) ──────────────────────────────

        public Rigidbody2D Rb => _rb;

        // ================================================================
        //  Init — SpawnManager 패턴
        // ================================================================

        public override void Init(MonsterData data, Transform player, Vector3 spawnPos)
        {
            _rb      = GetComponent<Rigidbody2D>();
            _anim    = GetComponent<Animator>();
            _btAgent = GetComponent<BTAgent>();

            base.Init(data, player, spawnPos);
            // BTAgent.Start() 는 SetActive(true) 이후 자동 호출된다.
        }

        // ================================================================
        //  사망
        // ================================================================

        protected override void OnDied(AttributeType killAttr)
        {
            _rb.linearVelocity = Vector2.zero;
            _btAgent.AbortTree();

            _anim.SetTrigger(HashDie);

            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            // BTMonsterController 는 MonsterAI 와 별도 타입이므로
            // MonsterRespawnManager(MonsterAI 전용) 에는 등록되지 않음.
            // BT 몬스터 리스폰이 필요하면 별도 BT용 리스폰 매니저를 구현한다.
            Destroy(gameObject, 1.5f);
        }

        // ================================================================
        //  Separation Steering (BTTask 에서 호출 가능)
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
                float dist = away.magnitude;
                if (dist < 0.001f) continue;
                separation += away.normalized / (dist * dist);
            }

            if (separation.sqrMagnitude < 0.0001f) return desiredDir;

            Vector2 perp      = new Vector2(-desiredDir.y, desiredDir.x);
            float   slide     = Vector2.Dot(separation.normalized, perp);
            Vector2 blended   = desiredDir + perp * (slide * separationWeight);
            return blended.sqrMagnitude > 0.001f ? blended.normalized : desiredDir;
        }
    }
}
