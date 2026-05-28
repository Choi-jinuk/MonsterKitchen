namespace MonsterKitchen.AI.BehaviorTree
{
    // ====================================================================
    //  BTSequence — AND 복합 노드
    //
    //  자식을 순서대로 실행하며, 하나라도 Failure 면 즉시 Failure.
    //  모두 Success 면 Success.
    // ====================================================================

    [BTNode("Composite/Sequence")]
    public class BTSequence : BTComposite
    {
        class State { public int Current; }

        protected override BTStatus Execute(BTContext ctx)
        {
            var state = ctx.GetOrCreateState<State>(this);

            while (state.Current < Children.Count)
            {
                var status = Children[state.Current].Tick(ctx);

                if (status == BTStatus.Running) return BTStatus.Running;
                if (status == BTStatus.Failure)
                {
                    state.Current = 0;
                    return BTStatus.Failure;
                }

                state.Current++;
            }

            state.Current = 0;
            return BTStatus.Success;
        }

        public override void Abort(BTContext ctx)
        {
            ctx.GetOrCreateState<State>(this).Current = 0;
            base.Abort(ctx);
        }
    }
}
