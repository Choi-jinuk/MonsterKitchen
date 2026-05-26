using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>자식 노드를 여러 개 가질 수 있는 복합 노드 기반 클래스.</summary>
    public abstract class BTComposite : BTNode
    {
        [SerializeField] List<BTNode> _children = new();

        public IReadOnlyList<BTNode> Children => _children;

        // ── 에디터 전용 자식 관리 ─────────────────────────────────────
#if UNITY_EDITOR
        public void AddChild(BTNode child)
        {
            if (child != null && !_children.Contains(child))
            {
                _children.Add(child);
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        public void RemoveChild(BTNode child)
        {
            if (_children.Remove(child))
                UnityEditor.EditorUtility.SetDirty(this);
        }

        public void ReorderChild(int from, int to)
        {
            if (from < 0 || to < 0 || from >= _children.Count || to >= _children.Count) return;
            var node = _children[from];
            _children.RemoveAt(from);
            _children.Insert(to, node);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        public override void Abort(BTContext ctx)
        {
            foreach (var child in _children)
                child.Abort(ctx);
        }

#if UNITY_EDITOR
        public IReadOnlyList<BTNode> DebugChildren => _children;
#endif
    }
}
