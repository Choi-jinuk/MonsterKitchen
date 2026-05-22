namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>
    /// AND 복합 노드.
    /// 자식을 순서대로 실행하며, 하나라도 Failure 면 즉시 Failure.
    /// 모두 Success 면 Success.
    /// </summary>
    public class BTSequence : BTComposite
    {
        int _current;

        public override BTStatus Tick()
        {
            while (_current < Children.Count)
            {
                var status = Children[_current].Tick();

                if (status == BTStatus.Running)  return BTStatus.Running;
                if (status == BTStatus.Failure)  { _current = 0; return BTStatus.Failure; }

                _current++;
            }

            _current = 0;
            return BTStatus.Success;
        }

        public override void Abort()
        {
            _current = 0;
            base.Abort();
        }
    }
}
