using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree
{
    // ====================================================================
    //  BTDecorator — 단일 자식을 감싸는 장식자 노드 기반 클래스 (MBT 참조)
    //
    //  ▶ BTCondition, BTService, BTInverter 등의 공통 기반.
    //  ▶ 자식은 하나만 가진다. 에디터에서 연결선으로 지정한다.
    //  ▶ Abort 시 자식을 먼저 정리한 뒤 자신의 OnExit 를 호출한다.
    // ====================================================================

    public abstract class BTDecorator : BTNode
    {
        [SerializeField] BTNode m_Child;

        /// <summary>감싸고 있는 자식 노드.</summary>
        public BTNode Child => m_Child;

        // ── 에디터 전용 자식 관리 ─────────────────────────────────────
#if UNITY_EDITOR
        public void SetChild(BTNode child)
        {
            m_Child = child;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void ClearChild()
        {
            m_Child = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        public override void Abort(BTContext ctx)
        {
            m_Child?.Abort(ctx);
            base.Abort(ctx); // BTNode.Abort → IsNodeRunning 체크 후 OnExit 호출
        }
    }
}
