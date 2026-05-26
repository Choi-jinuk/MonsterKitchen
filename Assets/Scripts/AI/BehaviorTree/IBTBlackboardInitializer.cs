namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>
    /// BTRunner.Start() 이전에 블랙보드를 채우기 위한 인터페이스.
    /// BTRunner 와 같은 GameObject 의 컴포넌트가 구현한다.
    /// </summary>
    public interface IBTBlackboardInitializer
    {
        /// <summary>블랙보드에 초기 키-값을 세팅한다. BTRunner.Start() 에서 호출된다.</summary>
        void InitializeBlackboard(BTBlackboard blackboard);
    }
}
