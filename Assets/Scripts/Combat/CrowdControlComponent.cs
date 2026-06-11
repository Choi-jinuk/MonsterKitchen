using System.Collections;
using MonsterKitchen.Data;
using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    // ====================================================================
    //  CrowdControlComponent — 액터별 이동 제어(CC) 관리자
    //
    //  ▶ 지원 CC 종류 (CCType)
    //    Knockback : 지정 방향으로 밀려남 → duration 동안 감속 후 정지
    //    Stun      : 이동 불가, 속도 = 0
    //    PullIn    : 목표 지점 방향으로 끌려감 → duration 동안 지속
    //
    //  ▶ 중복 차단
    //    IsUnderControl == true 이면 새 CC 신청을 무시한다.
    //    처음 들어온 CC 가 끝난 뒤에만 다음 CC 가 걸린다.
    //
    //  ▶ 면역 체크
    //    같은 GameObject 에 MonsterBase 가 있으면 MonsterData 의
    //    immuneToKnockback / immuneToStun / immuneToPullIn 플래그를 확인한다.
    //    플레이어 등 MonsterBase 가 없는 액터는 면역 없음으로 처리된다.
    //
    //  ▶ 사용 예
    //    // 플레이어가 몬스터를 칠 때:
    //    var cc = monster.GetComponent<CrowdControlComponent>();
    //    cc?.TryApplyKnockback(pushDir, 8f, 0.3f);
    // ====================================================================

    [RequireComponent(typeof(Rigidbody2D))]
    public class CrowdControlComponent : MonoBehaviour
    {
        // ── 프로퍼티 ──────────────────────────────────────────────────────

        /// <summary>현재 CC 가 진행 중이면 true.</summary>
        public bool IsUnderControl => m_CurrentCC != CCType.None;

        /// <summary>현재 걸려 있는 CC 종류. 없으면 CCType.None.</summary>
        public CCType CurrentCC => m_CurrentCC;

        // ── 런타임 상태 ────────────────────────────────────────────────────

        CCType      m_CurrentCC  = CCType.None;
        Coroutine   m_CcRoutine;
        Rigidbody2D m_Rb;

        // ── 초기화 ────────────────────────────────────────────────────────

        void Awake()
        {
            m_Rb = GetComponent<Rigidbody2D>();
        }

        void OnDisable()
        {
            ForceRelease();
        }

        // ================================================================
        //  Public API — CC 신청
        // ================================================================

        /// <summary>
        /// 넉백을 시도한다.
        /// direction: 밀려나는 방향(normalized), force: 초기 속도(units/s), duration: 감속 시간(초).
        /// 이미 CC 중이거나 면역이면 false 반환.
        /// </summary>
        public bool TryApplyKnockback(Vector2 direction, float force, float duration)
        {
            if (IsUnderControl)          return false;
            if (IsImmune(CCType.Knockback)) return false;

            StartCC(CCType.Knockback, KnockbackRoutine(direction, force, duration));
            return true;
        }

        /// <summary>
        /// 스턴을 시도한다.
        /// duration: 정지 유지 시간(초).
        /// </summary>
        public bool TryApplyStun(float duration)
        {
            if (IsUnderControl)       return false;
            if (IsImmune(CCType.Stun)) return false;

            StartCC(CCType.Stun, StunRoutine(duration));
            return true;
        }

        /// <summary>
        /// 풀인을 시도한다.
        /// targetPoint: 끌려갈 목표 위치, force: 이동 속도(units/s), duration: 지속 시간(초).
        /// </summary>
        public bool TryApplyPullIn(Vector2 targetPoint, float force, float duration)
        {
            if (IsUnderControl)          return false;
            if (IsImmune(CCType.PullIn)) return false;

            StartCC(CCType.PullIn, PullInRoutine(targetPoint, force, duration));
            return true;
        }

        /// <summary>현재 CC 를 즉시 해제한다.</summary>
        public void ForceRelease()
        {
            if (m_CcRoutine != null)
            {
                StopCoroutine(m_CcRoutine);
                m_CcRoutine = null;
            }
            m_CurrentCC = CCType.None;
        }

        // ================================================================
        //  코루틴 — 각 CC 동작 정의
        // ================================================================

        IEnumerator KnockbackRoutine(Vector2 direction, float force, float duration)
        {
            float elapsed = 0f;
            Vector2 initialVel = direction.normalized * force;

            while (elapsed < duration)
            {
                // 경과 비율에 따라 선형 감속 (끝에서 속도 = 0)
                float t   = elapsed / duration;
                m_Rb.linearVelocity = Vector2.Lerp(initialVel, Vector2.zero, t);

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            m_Rb.linearVelocity = Vector2.zero;
            EndCC();
        }

        IEnumerator StunRoutine(float duration)
        {
            m_Rb.linearVelocity = Vector2.zero;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                m_Rb.linearVelocity = Vector2.zero;
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            EndCC();
        }

        IEnumerator PullInRoutine(Vector2 targetPoint, float force, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                Vector2 toTarget = targetPoint - (Vector2)transform.position;
                if (toTarget.sqrMagnitude < 0.04f)   // 목표 근처 도달 시 조기 종료
                    break;

                m_Rb.linearVelocity = toTarget.normalized * force;

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            m_Rb.linearVelocity = Vector2.zero;
            EndCC();
        }

        // ================================================================
        //  내부 헬퍼
        // ================================================================

        void StartCC(CCType type, IEnumerator routine)
        {
            m_CurrentCC = type;
            m_CcRoutine = StartCoroutine(routine);
        }

        void EndCC()
        {
            m_CcRoutine = null;
            m_CurrentCC = CCType.None;
        }

        /// <summary>MonsterData 의 면역 플래그를 확인한다. MonsterBase 없으면 항상 false.</summary>
        bool IsImmune(CCType type)
        {
            var mb = GetComponent<MonsterBase>();
            if (mb?.Data == null) return false;

            var data = mb.Data;
            return type switch
            {
                CCType.Knockback => data.ImmuneToKnockback,
                CCType.Stun      => data.ImmuneToStun,
                CCType.PullIn    => data.ImmuneToPullIn,
                CCType.Sleep     => false,
                CCType.Hypnosis  => false,
                _                => false,
            };
        }
    }
}
