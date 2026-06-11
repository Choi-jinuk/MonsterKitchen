// ====================================================================
//  UIManager — 전체 UI 생명주기 싱글톤
//
//  ▶ 패널 등록 방식 (두 가지)
//    A. 씬 직접 배치   : UIPanel.Start() 에서 자동 Register (GameHUD 등)
//    B. 동적 로딩      : UIData.csv → TableData SO → DataRegistry
//                        → Open/Toggle/GetPanel 호출 시 미등록이면 자동 Instantiate
//
//  ▶ 동적 로딩 흐름
//    Open("CookingUI")
//      → _panels 에 없음
//      → DataRegistry.FindUIByPanelId("CookingUI") → UIData.PrefabAddress
//      → AssetLoadManager.Load<GameObject>(address) → Instantiate
//      → Awake 즉시 실행 (Document/Root 설정 완료)
//      → NotifyRegisteredByManager() + Register()
//      → OnOpen() → OnFirstOpen() (첫 열기 시 UXML 쿼리/콜백 등록)
//
//  ▶ 새 패널 추가 방법
//    1. UIData.csv 에 행 추가 (PanelId, PrefabAddress)
//    2. DataManager → Sync SO
//    3. AssetManifest 에 프리팹 키 등록
//
//  ▶ 팝업 스택
//    isPopup=true → push/pop.  PopPopup() = ESC/뒤로가기.
//
//  사용 예:
//    UIManager.Instance.Open("InventoryUI");
//    UIManager.Instance.Close("CookingUI");
//    UIManager.Instance.Toggle("InventoryUI");
//    UIManager.Instance.PopPopup();
//    UIManager.Instance.CloseAll();
// ====================================================================

using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────
        [Header("항상 표시되는 HUD (씬 독립)")]
        [SerializeField] GameObject m_HUDPrefab;


        // ── 런타임 상태 ───────────────────────────────────────────────
        readonly Dictionary<string, UIPanel> m_Panels     = new();
        readonly Stack<UIPanel>              m_PopupStack = new();

        // ── Unity ────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            // HUD 는 항상 즉시 생성 (DontDestroyOnLoad 자동 상속).
            if (m_HUDPrefab != null)
                Instantiate(m_HUDPrefab, transform);
            else
                DebugUtil.LogWarning("[UIManager] m_HUDPrefab 이 비어 있습니다. Inspector 에서 설정하세요.");

            // StartScene 에서 DataLoad 가 완료된 후 ManagementScene 이 로드되므로
            // 이 시점에 DataRegistry 는 반드시 준비 상태 — 직접 호출.
            RegisterUIDataToggleKeys();
        }

        void RegisterUIDataToggleKeys()
        {
            var uiTable = DataRegistry.Instance?.UIPanels;
            if (uiTable == null) return;

            foreach (var data in uiTable.All)
            {
                if (string.IsNullOrEmpty(data.ToggleKey)) continue;
                if (!System.Enum.TryParse<UnityEngine.InputSystem.Key>(data.ToggleKey, true, out var key)) continue;
                if (key == UnityEngine.InputSystem.Key.None) continue;

                Core.InputManager.Instance?.RegisterUIToggle(data.PanelId, key);
            }
        }

        // ── 등록 / 해제 ──────────────────────────────────────────────

        /// <summary>UIPanel.Start() 또는 UIManager 내부에서 자동 호출.</summary>
        public void Register(UIPanel panel)
        {
            if (panel == null) return;
            m_Panels[panel.PanelId] = panel;
        }

        /// <summary>UIPanel.OnDestroy() 에서 자동 호출.</summary>
        public void Unregister(UIPanel panel)
        {
            if (panel == null) return;
            m_Panels.Remove(panel.PanelId);
            RemoveFromPopupStack(panel);
        }

        // ── 열기 / 닫기 / 토글 ───────────────────────────────────────

        /// <summary>
        /// 패널을 연다.
        /// 미등록이면 UIData.csv → DataRegistry → AssetManifest 경유 자동 Instantiate.
        /// </summary>
        public void Open(string panelId)
        {
            var panel = GetOrLoad(panelId);
            if (panel == null) return;
            if (panel.IsOpen) return;

            if (panel.IsPopup)
            {
                if (m_PopupStack.Count > 0) m_PopupStack.Peek().OnBlur();
                m_PopupStack.Push(panel);
            }

            panel.OnOpen();
        }

        /// <summary>패널을 닫는다.</summary>
        public void Close(string panelId)
        {
            if (!m_Panels.TryGetValue(panelId, out var panel)) return;
            if (!panel.IsOpen) return;

            if (panel.IsPopup)
            {
                RemoveFromPopupStack(panel);
                panel.OnClose();
                if (m_PopupStack.Count > 0) m_PopupStack.Peek().OnFocus();
            }
            else
            {
                panel.OnClose();
            }
        }

        /// <summary>열려 있으면 닫고, 닫혀 있으면 연다. 미등록이면 자동 로드 후 열기.</summary>
        public void Toggle(string panelId)
        {
            var panel = GetOrLoad(panelId);
            if (panel == null) return;
            if (panel.IsOpen) Close(panelId);
            else              Open(panelId);
        }

        // ── 팝업 스택 제어 ────────────────────────────────────────────

        /// <summary>최상위 팝업을 닫는다. ESC / 뒤로가기 버튼용.</summary>
        /// <returns>닫은 팝업이 있으면 true.</returns>
        public bool PopPopup()
        {
            if (m_PopupStack.Count == 0) return false;
            var top = m_PopupStack.Pop();
            top.OnClose();
            if (m_PopupStack.Count > 0) m_PopupStack.Peek().OnFocus();
            return true;
        }

        public bool HasOpenPopup => m_PopupStack.Count > 0;

        // ── 전체 닫기 ────────────────────────────────────────────────

        public void CloseAll()
        {
            while (m_PopupStack.Count > 0)
                m_PopupStack.Pop().OnClose();

            foreach (var panel in m_Panels.Values)
                if (panel.IsOpen && !panel.IsPopup)
                    panel.OnClose();
        }

        // ── 조회 ─────────────────────────────────────────────────────

        public bool IsOpen(string panelId)
            => m_Panels.TryGetValue(panelId, out var p) && p.IsOpen;

        /// <summary>패널을 타입으로 가져온다. 미등록이면 자동 로드 시도.</summary>
        public T GetPanel<T>(string panelId) where T : UIPanel
            => GetOrLoad(panelId) as T;

        // ── 동적 로딩 ────────────────────────────────────────────────

        /// <summary>
        /// 등록된 패널 반환.
        /// 없으면 DataRegistry.FindUIByPanelId → AssetManifest → Instantiate 후 반환.
        /// </summary>
        UIPanel GetOrLoad(string panelId)
        {
            if (m_Panels.TryGetValue(panelId, out var panel)) return panel;

            // UIData.csv 에서 패널 정의 조회
            var uiData = DataRegistry.Instance?.UIPanels?.FindUIByPanelId(panelId);
            if (uiData == null)
            {
                DebugUtil.LogWarning($"[UIManager] '{panelId}' UIData 없음 — UIData.csv 에 등록되고 Sync SO 됐는지 확인하세요.");
                return null;
            }

            // AssetManifest 에서 프리팹 로드
            var prefab = AssetLoadManager.Instance?.Load<GameObject>(uiData.PrefabAddress);
            if (prefab == null)
            {
                DebugUtil.LogError($"[UIManager] '{panelId}' 프리팹 로드 실패 — AssetManifest 키: '{uiData.PrefabAddress}' 등록 여부 확인.");
                return null;
            }

            // Instantiate → Awake 즉시 실행 (Document/Root 설정 완료)
            var go = Instantiate(prefab, transform);
            go.name = panelId; // GO 이름 = PanelId 보장

            panel = go.GetComponent<UIPanel>();
            if (panel == null)
            {
                DebugUtil.LogError($"[UIManager] '{panelId}' 프리팹 루트에 UIPanel 컴포넌트가 없습니다.");
                Destroy(go);
                return null;
            }

            // Start 보다 먼저 Register — Start 에서 중복 등록 방지
            panel.NotifyRegisteredByManager();
            Register(panel);
            return panel;
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────

        void RemoveFromPopupStack(UIPanel target)
        {
            if (!m_PopupStack.Contains(target)) return;

            var temp = new List<UIPanel>(m_PopupStack);
            temp.Remove(target);
            m_PopupStack.Clear();
            for (int i = temp.Count - 1; i >= 0; i--)
                m_PopupStack.Push(temp[i]);
        }
    }
}
