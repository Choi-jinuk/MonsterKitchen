using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>
    /// 행동 트리를 소유하고 매 프레임 실행하는 MonoBehaviour 에이전트.
    ///
    /// 사용법:
    ///   1. BTAgent 를 상속한 서브클래스를 만들고 BuildTree() 를 재정의한다.
    ///   2. GameObject 에 서브클래스를 추가한다.
    ///   3. BTMonsterController.Init() 이후 SetActive(true) 시 Start() 가 자동 호출된다.
    /// </summary>
    public class BTAgent : MonoBehaviour
    {
        BTNode _root;
        bool   _aborted;

        // ================================================================
        //  Unity lifecycle
        // ================================================================

        protected virtual void Start()
        {
            _root    = BuildTree();
            _aborted = false;
        }

        void Update()
        {
            if (_aborted || _root == null) return;
            _root.Tick();
        }

        // ================================================================
        //  공개 API
        // ================================================================

        /// <summary>트리 실행을 즉시 중단한다. OnDied 에서 호출.</summary>
        public void AbortTree()
        {
            _aborted = true;
            _root?.Abort();
        }

        // ================================================================
        //  재정의 포인트
        // ================================================================

        /// <summary>
        /// 루트 BTNode 를 생성해 반환한다.
        /// 서브클래스에서 반드시 재정의할 것.
        /// </summary>
        protected virtual BTNode BuildTree() => null;
    }
}
