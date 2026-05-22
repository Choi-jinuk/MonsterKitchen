using System.Collections.Generic;

namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>자식 노드를 여러 개 가질 수 있는 복합 노드 기반 클래스.</summary>
    public abstract class BTComposite : BTNode
    {
        protected readonly List<BTNode> Children = new List<BTNode>();

        public BTComposite Add(BTNode child)
        {
            Children.Add(child);
            return this;
        }

        public override void Abort()
        {
            foreach (var child in Children)
                child.Abort();
        }
    }
}
