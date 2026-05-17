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
        // ── 런타임 상태 ─────────────────────────────────────────────────
        int           _damage;
        AttributeType _attr;
        float         _speed;
        float         _maxDistance;
        Vector3       _startPos;
        LayerMask     _targetLayer;
        bool          _isAoe;
        float         _aoeRadius;
        int           _maxTargets;
        Action        _onKill;
        bool          _hit;

        Transform     _homingTarget;
        float         _homingTurnSpeed;

        // ── CC 파라미터 ──────────────────────────────────────────────────
        float   _ccForce;         // 양수=넉백, 음수=풀인, 0=없음
        float   _ccDuration;
        float   _stunDuration;
        Vector3 _fireSourcePos;   // 시전자 위치 (풀인 방향 계산용)

        Rigidbody2D   _rb;

        static Sprite _sharedSprite;

        // ================================================================
        //  Awake
        // ================================================================

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale           = 0f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.constraints            = RigidbodyConstraints2D.FreezeRotation;

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
            _damage          = damage;
            _attr            = attr;
            _speed           = speed;
            _maxDistance     = maxDistance;
            _targetLayer     = targetLayer;
            _isAoe           = isAoe;
            _aoeRadius       = aoeRadius;
            _maxTargets      = Mathf.Max(1, maxTargets);
            _onKill          = onKill;
            _homingTarget    = homingTarget;
            _homingTurnSpeed = homingTurnSpeed;
            _startPos        = transform.position;
            _hit             = false;

            _ccForce       = ccForce;
            _ccDuration    = ccDuration;
            _stunDuration  = stunDuration;
            _fireSourcePos = fireSourcePos == default ? transform.position : fireSourcePos;

            // 색상: AoE(보라) / 유도(노랑) / 직선(주황)
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.color = isAoe
                    ? new Color(0.8f, 0.3f, 1f)   // 보라 — AoE
                    : homingTarget != null
                        ? new Color(1f, 0.85f, 0.1f)  // 노랑 — 유도
                        : new Color(1f, 0.4f, 0.1f);  // 주황 — 직선

            _rb.linearVelocity = direction.normalized * speed;
        }

        // ================================================================
        //  Update
        // ================================================================

        void Update()
        {
            if (_hit) return;

            // 사거리 초과 소멸
            if (Vector3.Distance(transform.position, _startPos) >= _maxDistance)
            {
                Destroy(gameObject);
                return;
            }

            // 유도 선회
            if (_homingTarget != null)
            {
                var hp = _homingTarget.GetComponent<Health>();
                if (hp == null || hp.IsDead)
                {
                    _homingTarget = null;   // 타겟 소멸 → 직진 유지
                }
                else
                {
                    Vector2 toTarget     = (Vector2)(_homingTarget.position - transform.position);
                    float   currentAngle = Mathf.Atan2(_rb.linearVelocity.y, _rb.linearVelocity.x) * Mathf.Rad2Deg;
                    float   targetAngle  = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                    float   newAngle     = Mathf.MoveTowardsAngle(currentAngle, targetAngle, _homingTurnSpeed * Time.deltaTime);
                    _rb.linearVelocity   = new Vector2(
                        Mathf.Cos(newAngle * Mathf.Deg2Rad),
                        Mathf.Sin(newAngle * Mathf.Deg2Rad)) * _speed;
                }
            }
        }

        // ================================================================
        //  충돌 처리
        // ================================================================

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_hit) return;
            if (((1 << other.gameObject.layer) & _targetLayer.value) == 0) return;

            _hit = true;

            if (_isAoe)
            {
                var hits  = Physics2D.OverlapCircleAll(transform.position, _aoeRadius, _targetLayer);
                int count = 0;

                foreach (var col in hits)
                {
                    if (count >= _maxTargets) break;
                    var hp = col.GetComponent<Health>();
                    if (hp == null) continue;

                    bool wasDead = hp.IsDead;
                    hp.TakeDamage(_damage, _attr);
                    if (!wasDead && hp.IsDead) _onKill?.Invoke();

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
                    hp.TakeDamage(_damage, _attr);
                    if (!wasDead && hp.IsDead) _onKill?.Invoke();
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

            if (_ccForce > 0f)
            {
                // 양수 → 넉백: 투사체 진행 방향으로 밀어냄
                Vector2 dir = _rb.linearVelocity.sqrMagnitude > 0.01f
                    ? _rb.linearVelocity.normalized
                    : ((Vector2)(target.position - _fireSourcePos)).normalized;
                cc.TryApplyKnockback(dir, _ccForce, _ccDuration);
            }
            else if (_ccForce < 0f)
            {
                // 음수 → 풀인: 시전자 방향으로 끌어당김
                cc.TryApplyPullIn((Vector2)_fireSourcePos, -_ccForce, _ccDuration);
            }
            else if (_stunDuration > 0f)
            {
                cc.TryApplyStun(_stunDuration);
            }
        }

        // ================================================================
        //  원형 스프라이트 절차적 생성 (최초 1회)
        // ================================================================

        static Sprite GetOrCreateSprite()
        {
            if (_sharedSprite != null) return _sharedSprite;

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

            _sharedSprite = Sprite.Create(
                tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _sharedSprite;
        }
    }
}
