using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree
{
    // ====================================================================
    //  BTNode — 행동 트리 노드 기반 ScriptableObject
    //
    //  ▶ 라이프사이클 (MBT 참조)
    //    OnEnter  → 첫 실행 진입 시 1회 호출
    //    Execute  → 매 틱 호출, BTStatus 반환
    //    OnExit   → Running 이 아닌 값을 반환하거나 Abort 될 때 1회 호출
    //
    //  ▶ "진입 중" 추적은 BTContext.IsNodeRunning() 으로 관리한다.
    //    BTNode 자체는 ScriptableObject 이므로 여러 BTRunner 가 공유한다.
    //    인스턴스 필드에 런타임 상태를 저장하면 안 된다.
    // ====================================================================

    public abstract class BTNode : ScriptableObject
    {
        // ── 에디터 전용 레이아웃 데이터 ─────────────────────────────────
        [HideInInspector] public Vector2 EditorPosition;

        // ── 라이프사이클 ─────────────────────────────────────────────────

        /// <summary>
        /// 매 틱 진입점. OnEnter / Execute / OnExit 를 올바른 순서로 호출한다.
        /// </summary>
        public BTStatus Tick(BTContext ctx)
        {
            // 이 노드가 이번 틱 처음 실행되는 경우 OnEnter
            if (!ctx.IsNodeRunning(this))
                OnEnter(ctx);

            var status = Execute(ctx);

#if UNITY_EDITOR
            ctx.RecordStatus(this, status);
#endif

            if (status != BTStatus.Running)
            {
                ctx.SetNodeRunning(this, false);
                OnExit(ctx);
            }
            else
            {
                ctx.SetNodeRunning(this, true);
            }

            return status;
        }

        /// <summary>실제 틱 구현. 서브클래스에서 override 한다.</summary>
        protected abstract BTStatus Execute(BTContext ctx);

        /// <summary>이 노드가 처음 Running 에 진입할 때 1회 호출.</summary>
        protected virtual void OnEnter(BTContext ctx) { }

        /// <summary>
        /// Running 이 아닌 값을 반환하거나 Abort 됐을 때 1회 호출.
        /// 이동 중지, 애니메이션 리셋 등 정리 작업에 사용한다.
        /// </summary>
        protected virtual void OnExit(BTContext ctx) { }

        /// <summary>
        /// 트리가 중단될 때 호출. OnExit 를 실행하고 자식에 재귀 전파한다.
        /// Running 상태가 아닌 노드는 아무것도 하지 않는다.
        /// </summary>
        public virtual void Abort(BTContext ctx)
        {
            if (!ctx.IsNodeRunning(this)) return;
            ctx.SetNodeRunning(this, false);
            OnExit(ctx);
        }

#if UNITY_EDITOR
        /// <summary>에디터에서 사용할 노드 표시 이름.</summary>
        public virtual string DebugLabel => GetType().Name;
#endif
    }
}
