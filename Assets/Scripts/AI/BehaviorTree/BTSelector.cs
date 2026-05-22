namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>
    /// OR 복합 노드.
    /// 자식을 순서대로 실행하며, 첫 번째 Success(또는 Running)에서 즉시 반환.
    /// 모두 Failure 면 Failure.
    /// </summary>
    public class BTSelector : BTComposite
    {
        public override BTStatus Tick()
        {
            foreach (var child in Children)
            {
                var status = child.Tick();
                if (status != BTStatus.Failure) return status;
            }
            return BTStatus.Failure;
        }
    }
}
