using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>자식 노드를 여러 개 가질 수 있는 복합 노드 기반 클래스.</summary>
    public abstract class BTComposite : BTNode
    {
        [SerializeField] List<BTNode> m_Children = new();

        public IReadOnlyList<BTNode> Children => m_Children;

        // ── 에디터 전용 자식 관리 ─────────────────────────────────────
#if UNITY_EDITOR
        public void AddChild(BTNode child)
        {
            if (child != null && !m_Children.Contains(child))
            {
                m_Children.Add(child);
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        public void RemoveChild(BTNode child)
        {
            if (m_Children.Remove(child))
                UnityEditor.EditorUtility.SetDirty(this);
        }

        public void ReorderChild(int from, int to)
        {
            if (from < 0 || to < 0 || from >= m_Children.Count || to >= m_Children.Count) return;
            var node = m_Children[from];
            m_Children.RemoveAt(from);
            m_Children.Insert(to, node);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        public override void Abort(BTContext ctx)
        {
            foreach (var child in m_Children)
                child?.Abort(ctx);
            base.Abort(ctx); // BTNode.Abort → IsNodeRunning 체크 후 OnExit 호출
        }
    }
}
