namespace MonsterKitchen.AI.BehaviorTree
{
    // ====================================================================
    //  BTInverter — 결과 반전 Decorator (MBT 참조)
    //
    //  Success ↔ Failure 를 반전한다. Running 은 그대로 통과.
    //  "플레이어가 범위 밖일 때" 등 부정 조건 표현에 사용한다.
    // ====================================================================

    [BTNode("Decorator/Inverter")]
    public class BTInverter : BTDecorator
    {
        protected override BTStatus Execute(BTContext ctx)
        {
            if (Child == null) return BTStatus.Failure;

            var status = Child.Tick(ctx);
            return status switch
            {
                BTStatus.Success => BTStatus.Failure,
                BTStatus.Failure => BTStatus.Success,
                _                => status   // Running 통과
            };
        }

#if UNITY_EDITOR
        public override string DebugLabel => "¬ Inverter";
#endif
    }
}
