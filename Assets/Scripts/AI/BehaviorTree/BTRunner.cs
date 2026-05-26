using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>
    /// BTAsset(ScriptableObject) 의 루트 노드를 매 프레임 실행한다.
    /// 노드 SO 를 직접 참조하므로 런타임 빌드 단계가 없다.
    ///
    /// 사용법:
    ///   1. BTRunner 를 GameObject 에 추가한다.
    ///   2. Inspector 에서 BTAsset 을 연결한다.
    ///   3. 같은 GameObject 에 IBTBlackboardInitializer 구현체를 추가한다.
    /// </summary>
    public class BTRunner : MonoBehaviour
    {
        [SerializeField] BTAsset _asset;

        BTNode    _root;
        BTContext _ctx;
        bool      _aborted;

        // ================================================================
        //  Unity lifecycle
        // ================================================================

        void Start()
        {
            if (_asset == null || _asset.root == null)
            {
                Debug.LogWarning($"[BTRunner] {name}: BTAsset 또는 루트 노드가 없습니다.");
                return;
            }

            var bb = new BTBlackboard();
            _ctx  = new BTContext(gameObject, bb);

            // BTAsset 의 blackboardDefaults 를 먼저 적용 (코드에서 덮어쓸 수 있음)
            if (_asset.blackboardDefaults != null)
                foreach (var entry in _asset.blackboardDefaults)
                    ApplyBBEntry(bb, entry);

            foreach (var init in GetComponents<IBTBlackboardInitializer>())
                init.InitializeBlackboard(bb);

            _root    = _asset.root;
            _aborted = false;
        }

        static void ApplyBBEntry(BTBlackboard bb, BBEntry e)
        {
            if (string.IsNullOrEmpty(e.key)) return;
            switch (e.valueType)
            {
                case BBValueType.Float:   bb.Set(e.key, e.floatValue);   break;
                case BBValueType.Int:     bb.Set(e.key, e.intValue);     break;
                case BBValueType.Bool:    bb.Set(e.key, e.boolValue);    break;
                case BBValueType.String:  bb.Set(e.key, e.stringValue);  break;
                case BBValueType.Vector2: bb.Set(e.key, e.vector2Value); break;
                case BBValueType.Vector3: bb.Set(e.key, e.vector3Value); break;
            }
        }

        void Update()
        {
            if (_aborted || _root == null) return;
            _root.Tick(_ctx);
        }

        // ================================================================
        //  공개 API
        // ================================================================

        public void AbortTree()
        {
            _aborted = true;
            _root?.Abort(_ctx);
        }

        // ================================================================
        //  에디터 전용 접근자
        // ================================================================
#if UNITY_EDITOR
        public BTNode    EditorRoot    => _root;
        public bool      EditorAborted => _aborted;
        public BTAsset   EditorAsset   => _asset;
        public BTContext EditorContext  => _ctx;
#endif
    }
}
