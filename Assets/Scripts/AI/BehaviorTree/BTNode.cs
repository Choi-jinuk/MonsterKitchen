namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>행동 트리 노드 기반 클래스.</summary>
    public abstract class BTNode
    {
        /// <summary>매 틱 실행. Running / Success / Failure 를 반환한다.</summary>
        public abstract BTStatus Tick();

        /// <summary>트리가 중단될 때 호출. 자식 노드에 재귀 전파.</summary>
        public virtual void Abort() { }
    }
}
