using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree
{
    // ====================================================================
    //  BTParallel — 매 틱 모든 자식을 순서대로 실행하는 복합 노드
    //
    //  ▶ Selector/Sequence 와 달리 중간에 중단하지 않는다.
    //  ▶ 반환값:
    //    · Failure : 하나라도 Failure 반환 시
    //    · Running : 하나 이상이 Running 이고 Failure 없음
    //    · Success : 모두 Success 반환 시
    //  ▶ 플레이어처럼 이동·공격·스킬이 동시에 동작해야 할 때 사용.
    // ====================================================================

    [BTNode("Composite/Parallel")]
    public class BTParallel : BTComposite
    {
        protected override BTStatus Execute(BTContext ctx)
        {
            bool anyRunning = false;

            foreach (var child in Children)
            {
                if (child == null) continue;

                var status = child.Tick(ctx);

                if (status == BTStatus.Failure) return BTStatus.Failure;
                if (status == BTStatus.Running)  anyRunning = true;
            }

            return anyRunning ? BTStatus.Running : BTStatus.Success;
        }

#if UNITY_EDITOR
        public override string DebugLabel => "∥ Parallel";
#endif
    }
}
