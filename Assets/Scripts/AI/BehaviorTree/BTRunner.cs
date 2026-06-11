using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree
{
    // ====================================================================
    //  BTRunner — BTAsset 의 루트 노드를 매 프레임 실행한다.
    //
    //  ▶ DeltaTime 주입 (MBT 참조)
    //    매 틱 ctx.SetDeltaTime(Time.deltaTime) 을 호출해 DeltaTime 을 갱신한다.
    //    노드는 Time.deltaTime 대신 ctx.DeltaTime 을 사용하므로
    //    게임 일시정지·슬로모션·틱 레이트 변경에 대응 가능하다.
    // ====================================================================

    public class BTRunner : MonoBehaviour
    {
        [SerializeField] BTAsset m_Asset;

        [Tooltip("BT 틱 최소 간격(초). 0 = 매 프레임. 0.05~0.1 권장 (20~10fps 틱).")]
        [SerializeField] float m_TickInterval = 0f;

        BTNode    m_Root;
        BTContext m_Ctx;
        bool      m_Aborted;
        float     m_TickTimer;

        // ================================================================
        //  Unity lifecycle
        // ================================================================

        void Start()
        {
            if (m_Asset == null || m_Asset.Root == null)
            {
                DebugUtil.LogWarning($"[BTRunner] {name}: BTAsset 또는 루트 노드가 없습니다.");
                return;
            }

            var bb = new BTBlackboard();
            m_Ctx  = new BTContext(gameObject, bb);

            if (m_Asset.BlackboardDefaults != null)
                foreach (var entry in m_Asset.BlackboardDefaults)
                    ApplyBBEntry(bb, entry);

            foreach (var init in GetComponents<IBTBlackboardInitializer>())
                init.InitializeBlackboard(bb);

            m_Root    = m_Asset.Root;
            m_Aborted = false;
        }

        static void ApplyBBEntry(BTBlackboard bb, BBEntry e)
        {
            if (string.IsNullOrEmpty(e.Key)) return;
            switch (e.ValueType)
            {
                case BBValueType.Float:   bb.Set(e.Key, e.FloatValue);   break;
                case BBValueType.Int:     bb.Set(e.Key, e.IntValue);     break;
                case BBValueType.Bool:    bb.Set(e.Key, e.BoolValue);    break;
                case BBValueType.String:  bb.Set(e.Key, e.StringValue);  break;
                case BBValueType.Vector2: bb.Set(e.Key, e.Vector2Value); break;
                case BBValueType.Vector3: bb.Set(e.Key, e.Vector3Value); break;
            }
        }

        void Update()
        {
            if (m_Aborted || m_Root == null) return;

            float dt = Time.deltaTime;
            if (m_TickInterval > 0f)
            {
                m_TickTimer -= dt;
                if (m_TickTimer > 0f) return;
                m_TickTimer = m_TickInterval;
            }

            m_Ctx.SetDeltaTime(dt);
            m_Root.Tick(m_Ctx);
        }

        // ================================================================
        //  공개 API
        // ================================================================

        public void AbortTree()
        {
            m_Aborted = true;
            m_Root?.Abort(m_Ctx);
        }

        /// <summary>
        /// BTAsset 을 런타임에 설정한다.
        /// SetActive(true) 전(Init 단계)에 호출하면 Start() 가 새 에셋으로 초기화된다.
        /// 이미 실행 중이라면 트리를 재시작한다.
        /// </summary>
        public void SetAsset(BTAsset asset)
        {
            m_Asset   = asset;
            m_Root    = null;
            m_Aborted = false;

            // 이미 Start() 가 실행된 뒤라면 즉시 재초기화
            if (m_Ctx != null && m_Asset != null && m_Asset.Root != null)
            {
                var bb = new BTBlackboard();
                m_Ctx = new BTContext(gameObject, bb);

                if (m_Asset.BlackboardDefaults != null)
                    foreach (var entry in m_Asset.BlackboardDefaults)
                        ApplyBBEntry(bb, entry);

                foreach (var init in GetComponents<IBTBlackboardInitializer>())
                    init.InitializeBlackboard(bb);

                m_Root = m_Asset.Root;
            }
        }

        // ================================================================
        //  에디터 전용 접근자
        // ================================================================
#if UNITY_EDITOR
        public BTNode    EditorRoot    => m_Root;
        public bool      EditorAborted => m_Aborted;
        public BTAsset   EditorAsset   => m_Asset;
        public BTContext EditorContext  => m_Ctx;
#endif
    }
}
