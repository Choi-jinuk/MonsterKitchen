using UnityEngine;

namespace MonsterKitchen.Enemy
{
    /// <summary>
    /// 군집 매니저에 등록되는 에이전트. 위치 질의용 최소 인터페이스.
    /// </summary>
    public interface IMonsterFlockAgent
    {
        Vector2   FlockPosition  { get; }
        Transform FlockTransform { get; }  // self 제외 식별용
    }
}
