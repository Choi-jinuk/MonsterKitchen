using System;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    // ====================================================================
    //  Projectile — 플레이어·몬스터 공용 투사체
    //
    //  ▶ Init() 파라미터로 동작을 완전히 제어한다.
    //
    //  ▶ 단일 타겟 (isAoe = false)
    //    targetLayer 에 닿으면 데미지 후 소멸.
    //
    //  ▶ 범위 폭발 (isAoe = true)
    //    적중 위치에서 OverlapCircle(aoeRadius) — 최대 maxTargets 까지 데미지.
    //
    //  ▶ 유도 (homingTarget != null)
    //    매 프레임 homingTurnSpeed(도/초)로 타겟 방향 선회.
    //    타겟이 사망하면 직진으로 전환.
    //
    //  ▶ 사거리 초과 시 자동 소멸 (maxDistance).
    //
    //  사용 예
    //    // 플레이어 원거리 유도 투사체
    //    proj.Init(dmg, attr, dir, speed, range, enemyLayer,
    //              homingTarget: target, onKill: () => stats.AddGauge());
    //
    //    // 몬스터 직선 투사체
    //    proj.Init(dmg, attr, dir, speed, range, playerLayer);
    // ====================================================================

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Projectile : MonoBehaviour
    {
        static readonly Collider2D[] s_OverlapBuffer = new Collider2D[16];

        // ── 런타임 상태 ─────────────────────────────────────────────────
        int           m_Damage;
        AttributeType m_Attr;
        float         m_Speed;
        float         m_MaxDistance;
        Vector3       m_StartPos;
        LayerMask     m_TargetLayer;
        bool          m_IsAoe;
        float         m_AoeRadius;
        int           m_MaxTargets;
        Action        m_OnKill;
        bool          m_Hit;

        Transform     m_HomingTarget;
        float         m_HomingTurnSpeed;

        // ── CC 파라미터 ──────────────────────────────────────────────────
        float   m_CcForce;         // 양수=넉백, 음수=풀인, 0=없음
        float   m_CcDuration;
        float   m_StunDuration;
        Vector3 m_FireSourcePos;   // 시전자 위치 (풀인 방향 계산용)

        Rigidbody2D m_Rb;

        static Sprite s_SharedSprite;

        // ================================================================
        //  Awake
        // ================================================================

        void Awake()
        {
            m_Rb = GetComponent<Rigidbody2D>();
            m_Rb.gravityScale           = 0f;
            m_Rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            m_Rb.constraints            = RigidbodyConstraints2D.FreezeRotation;

            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius    = 0.15f;

            if (GetComponent<SpriteRenderer>() == null)
            {
                var sr       = gameObject.AddComponent<SpriteRenderer>();
                sr.sprite       = GetOrCreateSprite();
                sr.sortingOrder = 10;
                transform.localScale = Vector3.one * 0.3f;
            }
        }

        // ================================================================
        //  Init
        // ================================================================

        /// <param name="damage">적용 데미지</param>
        /// <param name="attr">속성</param>
        /// <param name="direction">발사 방향 (normalized)</param>
        /// <param name="speed">이동 속도 (units/s)</param>
        /// <param name="maxDistance">최대 사거리</param>
        /// <param name="targetLayer">충돌 판정할 레이어 마스크</param>
        /// <param name="isAoe">true 면 범위 폭발 모드</param>
        /// <param name="aoeRadius">폭발 반지름 (isAoe=true 전용)</param>
        /// <param name="maxTargets">최대 피격 대상 수</param>
        /// <param name="onKill">처치마다 호출되는 콜백 (null 허용)</param>
        /// <param name="homingTarget">유도 타겟. null 이면 직진.</param>
        /// <param name="homingTurnSpeed">유도 선회 속도 (도/초)</param>
        /// <param name="ccForce">양수=넉백(units/s), 음수=풀인(units/s), 0=CC없음</param>
        /// <param name="ccDuration">CC 지속 시간(초)</param>
        /// <param name="stunDuration">0 초과이면 명중 대상에게 스턴 적용 (초)</param>
        /// <param name="fireSourcePos">시전자 위치. 풀인 방향 계산에 사용된다.</param>
        public void Init(int damage, AttributeType attr,
                         Vector2 direction, float speed, float maxDistance,
                         LayerMask targetLayer,
                         bool isAoe = false, float aoeRadius = 0f,
                         int maxTargets = 1, Action onKill = null,
                         Transform homingTarget = null, float homingTurnSpeed = 240f,
                         float ccForce = 0f, float ccDuration = 0.25f,
                         float stunDuration = 0f,
                         Vector3 fireSourcePos = default)
        {
            m_Damage          = damage;
            m_Attr            = attr;
            m_Speed           = speed;
            m_MaxDistance     = maxDistance;
            m_TargetLayer     = targetLayer;
            m_IsAoe           = isAoe;
            m_AoeRadius       = aoeRadius;
            m_MaxTargets      = Mathf.Max(1, maxTargets);
            m_OnKill          = onKill;
            m_HomingTarget    = homingTarget;
            m_HomingTurnSpeed = homingTurnSpeed;
            m_StartPos        = transform.position;
            m_Hit             = false;

            m_CcForce       = ccForce;
            m_CcDuration    = ccDuration;
            m_StunDuration  = stunDuration;
            m_FireSourcePos = fireSourcePos == default ? transform.position : fireSourcePos;

            // 색상: AoE(보라) / 유도(노랑) / 직선(주황)
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.color = isAoe
                    ? new Color(0.8f, 0.3f, 1f)   // 보라 — AoE
                    : homingTarget != null
                        ? new Color(1f, 0.85f, 0.1f)  // 노랑 — 유도
                        : new Color(1f, 0.4f, 0.1f);  // 주황 — 직선

            m_Rb.linearVelocity = direction.normalized * speed;
        }

        // ================================================================
        //  Update
        // ================================================================

        void Update()
        {
            if (m_Hit) return;

            // 사거리 초과 소멸
            if (Vector3.Distance(transform.position, m_StartPos) >= m_MaxDistance)
            {
                Destroy(gameObject);
                return;
            }

            // 유도 선회
            if (m_HomingTarget != null)
            {
                var hp = m_HomingTarget.GetComponent<Health>();
                if (hp == null || hp.IsDead)
                {
                    m_HomingTarget = null;   // 타겟 소멸 → 직진 유지
                }
                else
                {
                    Vector2 toTarget     = (Vector2)(m_HomingTarget.position - transform.position);
                    float   currentAngle = Mathf.Atan2(m_Rb.linearVelocity.y, m_Rb.linearVelocity.x) * Mathf.Rad2Deg;
                    float   targetAngle  = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                    float   newAngle     = Mathf.MoveTowardsAngle(currentAngle, targetAngle, m_HomingTurnSpeed * Time.deltaTime);
                    m_Rb.linearVelocity  = new Vector2(
                        Mathf.Cos(newAngle * Mathf.Deg2Rad),
                        Mathf.Sin(newAngle * Mathf.Deg2Rad)) * m_Speed;
                }
            }
        }

        // ================================================================
        //  충돌 처리
        // ================================================================

        void OnTriggerEnter2D(Collider2D other)
        {
            if (m_Hit) return;
            if (((1 << other.gameObject.layer) & m_TargetLayer.value) == 0) return;

            m_Hit = true;

            if (m_IsAoe)
            {
                var aoeFilter = new ContactFilter2D();
                aoeFilter.SetLayerMask(m_TargetLayer);
                aoeFilter.useTriggers = true;
                int hitCount = Physics2D.OverlapCircle(transform.position, m_AoeRadius, aoeFilter, s_OverlapBuffer);
                int count = 0;

                for (int i = 0; i < hitCount; i++)
                {
                    var col = s_OverlapBuffer[i];
                    if (count >= m_MaxTargets) break;
                    var hp = col.GetComponent<Health>();
                    if (hp == null) continue;

                    bool wasDead = hp.IsDead;
                    hp.TakeDamage(m_Damage, m_Attr);
                    if (!wasDead && hp.IsDead) m_OnKill?.Invoke();

                    ApplyProjectileCC(col.transform);
                    count++;
                }

            }
            else
            {
                var hp = other.GetComponent<Health>();
                if (hp != null)
                {
                    bool wasDead = hp.IsDead;
                    hp.TakeDamage(m_Damage, m_Attr);
                    if (!wasDead && hp.IsDead) m_OnKill?.Invoke();
                }
                ApplyProjectileCC(other.transform);
            }

            Destroy(gameObject);
        }

        // ================================================================
        //  CC 적용 헬퍼
        // ================================================================

        void ApplyProjectileCC(Transform target)
        {
            if (target == null) return;

            var cc = target.GetComponent<CrowdControlComponent>();
            if (cc == null) return;

            if (m_CcForce > 0f)
            {
                // 양수 → 넉백: 투사체 진행 방향으로 밀어냄
                Vector2 dir = m_Rb.linearVelocity.sqrMagnitude > 0.01f
                    ? m_Rb.linearVelocity.normalized
                    : ((Vector2)(target.position - m_FireSourcePos)).normalized;
                cc.TryApplyKnockback(dir, m_CcForce, m_CcDuration);
            }
            else if (m_CcForce < 0f)
            {
                // 음수 → 풀인: 시전자 방향으로 끌어당김
                cc.TryApplyPullIn((Vector2)m_FireSourcePos, -m_CcForce, m_CcDuration);
            }
            else if (m_StunDuration > 0f)
            {
                cc.TryApplyStun(m_StunDuration);
            }
        }

        // ================================================================
        //  원형 스프라이트 절차적 생성 (최초 1회)
        // ================================================================

        static Sprite GetOrCreateSprite()
        {
            if (s_SharedSprite != null) return s_SharedSprite;

            const int size = 64;
            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float cx = size * 0.5f, cy = size * 0.5f, r = size * 0.5f - 1f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx   = x + 0.5f - cx, dy = y + 0.5f - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r - dist + 1f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            tex.SetPixels(pixels);
            tex.Apply();

            s_SharedSprite = Sprite.Create(
                tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return s_SharedSprite;
        }
    }
}
