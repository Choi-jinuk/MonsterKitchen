namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>
    /// AND 복합 노드.
    /// 자식을 순서대로 실행하며, 하나라도 Failure 면 즉시 Failure.
    /// 모두 Success 면 Success.
    /// </summary>
    [BTNode("Composite/Sequence")]
    public class BTSequence : BTComposite
    {
        class State { public int current; }

        protected override BTStatus Execute(BTContext ctx)
        {
            var state = ctx.GetOrCreateState<State>(this);

            while (state.current < Children.Count)
            {
                var status = Children[state.current].Tick(ctx);

                if (status == BTStatus.Running) return BTStatus.Running;
                if (status == BTStatus.Failure) { state.current = 0; return BTStatus.Failure; }

                state.current++;
            }

            state.current = 0;
            return BTStatus.Success;
        }

        public override void Abort(BTContext ctx)
        {
            ctx.GetOrCreateState<State>(this).current = 0;
            base.Abort(ctx);
        }
    }
}
