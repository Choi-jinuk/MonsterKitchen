using UnityEngine;

namespace MonsterKitchen.Enemy
{
    /// <summary>
    /// 이동 컴포넌트가 Separation Steering 을 요청하는 인터페이스.
    /// MonsterAI (FSM) 와 BTMonsterController (BT) 가 구현한다.
    /// </summary>
    public interface IMonsterSeparation
    {
        Vector2 ApplySeparation(Vector2 desiredDir);
    }
}
