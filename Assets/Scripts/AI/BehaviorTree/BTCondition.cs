namespace MonsterKitchen.AI.BehaviorTree
{
    // ====================================================================
    //  BTCondition — 조건부 Decorator 기반 클래스 (MBT 참조)
    //
    //  ▶ 기존 방식과의 차이
    //    이전: Condition 이 Sequence 의 첫 번째 자식으로 들어가는 Leaf 노드.
    //    지금: Condition 이 서브트리 전체를 감싸는 Decorator.
    //
    //    이전 구조:  Selector → [Sequence → [CondLeaf, AttackSubTree], Patrol]
    //    개선 구조:  Selector → [BTCondition → AttackSubTree, Patrol]
    //
    //  ▶ 자동 인터럽트
    //    매 틱 Check() 를 먼저 평가하므로, 플레이어가 사거리를 벗어나는 순간
    //    Child.Abort() 가 자동으로 호출된다. (별도 AbortType 설정 불필요)
    //    BTSelector 의 높은 우선순위 인터럽트와 결합하면 완전한 흐름 전환이 이루어진다.
    //
    //  ▶ 자식 없이 사용
    //    Child 가 null 이면 조건이 true 일 때 Success 를 반환한다.
    //    순수한 가드 체크 용도로 활용 가능.
    // ====================================================================

    public abstract class BTCondition : BTDecorator
    {
        /// <summary>조건 평가. true 이면 자식을 실행, false 이면 Failure 반환.</summary>
        protected abstract bool Check(BTContext ctx);

        protected override BTStatus Execute(BTContext ctx)
        {
            if (!Check(ctx))
            {
                // 자식이 실행 중이었다면 즉시 정리 (Abort 내부에서 OnExit 호출)
                Child?.Abort(ctx);
                return BTStatus.Failure;
            }

            if (Child == null) return BTStatus.Success;
            return Child.Tick(ctx);
        }
    }
}
