using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>
    /// 노드 실행 중 공유되는 런타임 컨텍스트.
    /// BTRunner 가 생성해 Tick() / Abort() 에 전달한다.
    ///
    /// BTNode 는 ScriptableObject 이므로 여러 BTRunner 가 공유한다.
    /// 노드별 런타임 상태는 GetOrCreateState&lt;T&gt; 를 통해 컨텍스트에 저장한다.
    /// </summary>
    public class BTContext
    {
        /// <summary>이 트리를 소유한 GameObject.</summary>
        public GameObject   Owner      { get; }

        /// <summary>에이전트 공유 블랙보드.</summary>
        public BTBlackboard Blackboard { get; }

        // 노드 참조 → 런타임 상태 (per-runner, per-node)
        readonly Dictionary<BTNode, object> _nodeStates = new();

#if UNITY_EDITOR
        // 에디터 전용: 노드별 마지막 실행 결과 추적
        readonly Dictionary<BTNode, BTStatus> _lastStatus = new();

        public void RecordStatus(BTNode node, BTStatus status) =>
            _lastStatus[node] = status;

        public bool TryGetLastStatus(BTNode node, out BTStatus status) =>
            _lastStatus.TryGetValue(node, out status);
#endif

        public BTContext(GameObject owner, BTBlackboard blackboard)
        {
            Owner      = owner;
            Blackboard = blackboard;
        }

        /// <summary>
        /// 이 컨텍스트에서 해당 노드의 런타임 상태를 가져온다.
        /// 최초 호출 시 new T() 로 생성한다.
        /// </summary>
        public T GetOrCreateState<T>(BTNode node) where T : class, new()
        {
            if (_nodeStates.TryGetValue(node, out var s) && s is T typed)
                return typed;
            var ns = new T();
            _nodeStates[node] = ns;
            return ns;
        }
    }
}
