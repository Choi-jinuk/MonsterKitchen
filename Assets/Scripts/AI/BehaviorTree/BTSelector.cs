namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>
    /// OR 복합 노드.
    /// 자식을 순서대로 실행하며, 첫 번째 Success(또는 Running) 에서 즉시 반환.
    /// 모두 Failure 면 Failure.
    /// </summary>
    [BTNode("Composite/Selector")]
    public class BTSelector : BTComposite
    {
        protected override BTStatus Execute(BTContext ctx)
        {
            foreach (var child in Children)
            {
                var status = child.Tick(ctx);
                if (status != BTStatus.Failure) return status;
            }
            return BTStatus.Failure;
        }
    }
}
