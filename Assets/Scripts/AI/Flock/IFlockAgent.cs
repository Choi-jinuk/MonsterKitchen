using UnityEngine;

namespace MonsterKitchen.AI
{
    /// <summary>
    /// 군집(FlockManager)에 등록되는 에이전트. 위치 질의용 최소 인터페이스.
    /// 몬스터·동료(컴패니언) 등 겹침 회피가 필요한 모든 AI 가 구현한다.
    /// </summary>
    public interface IFlockAgent
    {
        Vector2   FlockPosition  { get; }
        Transform FlockTransform { get; }  // self 제외 식별용
    }
}
