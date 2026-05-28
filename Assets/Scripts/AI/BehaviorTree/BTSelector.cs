namespace MonsterKitchen.AI.BehaviorTree
{
    // ====================================================================
    //  BTSelector — OR 복합 노드
    //
    //  자식을 우선순위 순(인덱스 오름차순)으로 매 틱 앞에서부터 평가한다.
    //  첫 번째 Failure 가 아닌 결과를 반환한다.
    //
    //  ▶ 높은 우선순위 브랜치 인터럽트 (MBT 참조)
    //    이전 틱에서 i=N 이 Running 이었는데 이번 틱에 i<N 이 Running/Success 가 됐다면
    //    i=N 의 Abort() 를 호출해 정리한다.
    //    예: Patrol 실행 중 PlayerInRange Condition 이 true → Patrol.Abort() 자동 호출.
    // ====================================================================

    [BTNode("Composite/Selector")]
    public class BTSelector : BTComposite
    {
        class State { public int RunningIdx = -1; }

        protected override BTStatus Execute(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);

            for (int i = 0; i < Children.Count; i++)
            {
                var status = Children[i].Tick(ctx);
                if (status == BTStatus.Failure) continue;

                // 이전에 다른 자식이 Running 이었다면 Abort 처리
                if (s.RunningIdx >= 0 && s.RunningIdx != i)
                    Children[s.RunningIdx].Abort(ctx);

                s.RunningIdx = status == BTStatus.Running ? i : -1;
                return status;
            }

            s.RunningIdx = -1;
            return BTStatus.Failure;
        }

        public override void Abort(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            s.RunningIdx = -1;
            base.Abort(ctx);
        }
    }
}
