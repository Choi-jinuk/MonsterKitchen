using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree
{
    // ====================================================================
    //  BTService — 주기적 백그라운드 작업 Decorator (MBT 참조)
    //
    //  ▶ 자식이 Running 을 유지하는 동안 interval 간격으로 OnServiceTick 을 호출한다.
    //    블랙보드 갱신, 시야 계산, 경로 재계산 등 비용이 큰 연산을 매 프레임이 아닌
    //    일정 주기로 실행해 성능을 절감한다.
    //
    //  ▶ 사용 예
    //    BTPlayerDetectService → 주기적으로 플레이어 탐색 후 BB 업데이트.
    //
    //  ▶ m_CallOnEnter = true 이면 첫 진입 시 즉시 한 번 OnServiceTick 을 호출한다.
    //
    //  ▶ m_RandomDeviation > 0 이면 interval ± deviation 범위에서 다음 실행 시간을 선택한다.
    //    여러 에이전트의 서비스 실행이 동일 프레임에 몰리는 현상을 방지한다.
    // ====================================================================

    public abstract class BTService : BTDecorator
    {
        [Tooltip("서비스 실행 간격(초).")]
        [SerializeField] protected float m_Interval = 0.25f;

        [Tooltip("interval 에 ±deviation 범위의 랜덤 오프셋을 더한다.\n" +
                 "여러 에이전트의 서비스 실행이 동일 프레임에 몰리지 않게 분산한다.")]
        [SerializeField] float m_RandomDeviation = 0.05f;

        [Tooltip("true 이면 노드 진입 시 즉시 OnServiceTick 을 1회 호출한다.")]
        [SerializeField] bool m_CallOnEnter = true;

        class State { public float Timer; }

        // ── 라이프사이클 ─────────────────────────────────────────────────

        protected override void OnEnter(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (m_CallOnEnter)
            {
                s.Timer = NextInterval();
                OnServiceTick(ctx);
            }
            else
            {
                s.Timer = NextInterval();
            }
        }

        protected override BTStatus Execute(BTContext ctx)
        {
            var s  = ctx.GetOrCreateState<State>(this);
            s.Timer -= ctx.DeltaTime;

            if (s.Timer <= 0f)
            {
                s.Timer = NextInterval();
                OnServiceTick(ctx);
            }

            return Child?.Tick(ctx) ?? BTStatus.Success;
        }

        // ── 서브클래스 구현 ──────────────────────────────────────────────

        /// <summary>주기적으로 호출되는 서비스 로직. 블랙보드 갱신 등을 구현한다.</summary>
        protected abstract void OnServiceTick(BTContext ctx);

        // ── 내부 ─────────────────────────────────────────────────────────

        float NextInterval()
        {
            if (m_RandomDeviation <= 0f) return m_Interval;
            return m_Interval + Random.Range(-m_RandomDeviation, m_RandomDeviation);
        }
    }
}
