using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree
{
    // ====================================================================
    //  BTContext — 노드 실행 중 공유되는 런타임 컨텍스트
    //
    //  ▶ BTRunner 가 생성해 Tick() / Abort() 에 전달한다.
    //  ▶ BTNode 는 ScriptableObject 이므로 여러 BTRunner 가 공유한다.
    //    노드별 런타임 상태는 GetOrCreateState<T> 로 컨텍스트에 저장한다.
    //
    //  ▶ DeltaTime (MBT 참조)
    //    BTRunner.Update() 에서 매 틱 주입된다.
    //    노드는 Time.deltaTime 대신 ctx.DeltaTime 을 사용한다.
    //    → 게임 일시정지, 슬로모션, 틱 레이트 변경에 대응 가능.
    //
    //  ▶ IsNodeRunning / SetNodeRunning (MBT 참조)
    //    BTNode.OnEnter / OnExit / Abort 의 올바른 호출 타이밍을 추적한다.
    // ====================================================================

    public class BTContext
    {
        /// <summary>이 트리를 소유한 GameObject.</summary>
        public GameObject Owner { get; }

        /// <summary>에이전트 공유 블랙보드.</summary>
        public BTBlackboard Blackboard { get; }

        /// <summary>
        /// 이번 틱의 경과 시간(초). BTRunner 가 매 틱 주입한다.
        /// 노드에서 Time.deltaTime 대신 이 값을 사용한다.
        /// </summary>
        public float DeltaTime { get; private set; }

        // 노드 참조 → 런타임 상태 (per-runner, per-node)
        readonly Dictionary<BTNode, object> m_NodeStates  = new();

        // 현재 Running 중인 노드 집합 — OnEnter/OnExit 타이밍 추적에 사용
        readonly HashSet<BTNode> m_RunningNodes = new();

#if UNITY_EDITOR
        readonly Dictionary<BTNode, BTStatus> m_LastStatus = new();

        public void RecordStatus(BTNode node, BTStatus status) =>
            m_LastStatus[node] = status;

        public bool TryGetLastStatus(BTNode node, out BTStatus status) =>
            m_LastStatus.TryGetValue(node, out status);

        public System.Collections.Generic.IEnumerable<KeyValuePair<string, object>>
            EditorEntries => Blackboard.EditorEntries;
#endif

        public BTContext(GameObject owner, BTBlackboard blackboard)
        {
            Owner     = owner;
            Blackboard = blackboard;
        }

        // ── DeltaTime 주입 ────────────────────────────────────────────────

        /// <summary>BTRunner 가 매 틱 Tick() 전에 호출해 DeltaTime 을 갱신한다.</summary>
        public void SetDeltaTime(float dt) => DeltaTime = dt;

        // ── Running 노드 추적 ─────────────────────────────────────────────

        public bool IsNodeRunning(BTNode node) => m_RunningNodes.Contains(node);

        public void SetNodeRunning(BTNode node, bool running)
        {
            if (running) m_RunningNodes.Add(node);
            else         m_RunningNodes.Remove(node);
        }

        // ── 노드 상태 저장소 ──────────────────────────────────────────────

        /// <summary>
        /// 이 컨텍스트에서 해당 노드의 런타임 상태를 가져온다.
        /// 최초 호출 시 new T() 로 생성한다.
        /// </summary>
        public T GetOrCreateState<T>(BTNode node) where T : class, new()
        {
            if (m_NodeStates.TryGetValue(node, out var s) && s is T typed)
                return typed;
            var ns = new T();
            m_NodeStates[node] = ns;
            return ns;
        }
    }
}
