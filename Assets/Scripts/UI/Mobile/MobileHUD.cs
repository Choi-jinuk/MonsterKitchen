using UnityEngine;

namespace MonsterKitchen.UI.Mobile
{
    // ====================================================================
    //  MobileHUD — 모바일 입력 UI 총괄 싱글톤
    //
    //  ▶ DontDestroyOnLoad — GlobalController 가 Awake 에서 생성
    //  ▶ SetContext(MobileContext) — 버튼 슬롯 액션 + 활성화 재구성
    //  ▶ m_ForceShow = true 로 에디터에서 강제 표시 가능 (테스트용)
    //
    //  컨텍스트 기본값:
    //    Dungeon         → [Dash][Skill1][Skill2]
    //    DungeonInteract → [Dash][Skill1][Interact]
    //    Exploration     → [Dash][Interact]
    // ====================================================================

    public class MobileHUD : MonoBehaviour
    {
        // ── 직렬화 타입 ──────────────────────────────────────────────

        [System.Serializable]
        public struct ContextConfig
        {
            public MobileContext  Context;
            public MobileAction[] SlotActions;
        }

        // ── Inspector 필드 ───────────────────────────────────────────

        [SerializeField] bool            m_ForceShow;
        [SerializeField] MobileButton[]  m_Slots;
        [SerializeField] ContextConfig[] m_Configs;

        // ── 싱글톤 ───────────────────────────────────────────────────

        public static MobileHUD Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 항상 숨김 상태로 시작 — SetContext() 호출 시 인게임 씬에서만 표시
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 공개 API ─────────────────────────────────────────────────

        /// <summary>
        /// 컨텍스트에 맞게 버튼 슬롯 액션 및 활성화 상태를 갱신한다.
        /// 매칭 Config 가 없으면 아무 동작도 하지 않는다.
        /// </summary>
        /// <summary>HUD 전체를 숨긴다 (로딩씬·시작씬 전환 시 호출).</summary>
        public void Hide() => gameObject.SetActive(false);

        public void SetContext(MobileContext ctx)
        {
            // 모바일 또는 에디터 ForceShow 일 때만 표시
            bool canShow = Application.isMobilePlatform || m_ForceShow;
            if (!canShow) return;

            gameObject.SetActive(true);

            if (m_Slots == null || m_Configs == null) return;

            ContextConfig? match = null;
            foreach (var cfg in m_Configs)
            {
                if (cfg.Context == ctx) { match = cfg; break; }
            }

            if (match == null) return;

            MobileAction[] actions = match.Value.SlotActions ?? System.Array.Empty<MobileAction>();

            for (int i = 0; i < m_Slots.Length; i++)
            {
                if (m_Slots[i] == null) continue;

                if (i < actions.Length)
                {
                    m_Slots[i].SetAction(actions[i]);
                    m_Slots[i].gameObject.SetActive(true);
                }
                else
                {
                    m_Slots[i].gameObject.SetActive(false);
                }
            }
        }

        // ── 테스트 전용 ──────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>EditMode 테스트에서 슬롯을 직접 주입할 때 사용.</summary>
        public void SetSlotsForTest(MobileButton[] slots) => m_Slots = slots;

        /// <summary>EditMode 테스트에서 컨텍스트 설정을 직접 주입할 때 사용.</summary>
        public void SetConfigsForTest(ContextConfig[] configs) => m_Configs = configs;
#endif
    }
}
