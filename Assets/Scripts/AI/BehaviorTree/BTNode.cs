using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>
    /// 행동 트리 노드 기반 ScriptableObject.
    /// BTAsset 의 sub-asset 으로 저장되며, 파라미터는 [SerializeField] 필드로 직접 직렬화된다.
    /// 런타임 상태(per-runner)는 BTContext.GetOrCreateState&lt;T&gt; 를 사용한다.
    /// </summary>
    public abstract class BTNode : ScriptableObject
    {
        // ── 에디터 전용 레이아웃 데이터 ─────────────────────────────────
        [HideInInspector] public Vector2 editorPosition;

        // ── 라이프사이클 ─────────────────────────────────────────────────

        /// <summary>
        /// 매 틱 실행. Execute() 를 호출하고 에디터에서 상태를 기록한다.
        /// </summary>
        public BTStatus Tick(BTContext ctx)
        {
            var status = Execute(ctx);
#if UNITY_EDITOR
            ctx.RecordStatus(this, status);
#endif
            return status;
        }

        /// <summary>실제 틱 구현. 서브클래스에서 override 한다.</summary>
        protected abstract BTStatus Execute(BTContext ctx);

        /// <summary>트리가 중단될 때 호출. 자식 노드에 재귀 전파.</summary>
        public virtual void Abort(BTContext ctx) { }

#if UNITY_EDITOR
        /// <summary>에디터에서 사용할 노드 표시 이름.</summary>
        public virtual string DebugLabel => GetType().Name;
#endif
    }
}
