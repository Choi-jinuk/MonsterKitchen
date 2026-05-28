using System.Collections.Generic;
using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.UI
{
    /// <summary>
    /// 전체 UI 생명주기를 관리하는 싱글톤.
    /// ManagementScene 의 UIManager 오브젝트에 부착, DontDestroyOnLoad.
    ///
    /// ┌─ 기능 ─────────────────────────────────────────────────────────┐
    /// │ Register / Unregister  : UIPanel 이 자신을 등록/해제            │
    /// │ Open / Close / Toggle  : 패널 열기 · 닫기 · 토글               │
    /// │ 팝업 스택              : isPopup=true 패널은 스택으로 관리      │
    /// │   PopPopup()           : 최상위 팝업 닫기 (뒤로가기 버튼 등)   │
    /// │   CloseAll()           : 모든 패널 & 팝업 닫기                  │
    /// │ 레이어                 : UIPanel.Awake 에서 sortingOrder 자동 설정│
    /// └────────────────────────────────────────────────────────────────┘
    ///
    /// 사용 예:
    ///   UIManager.Instance.Open("InventoryUI");
    ///   UIManager.Instance.Close("InventoryUI");
    ///   UIManager.Instance.Toggle("InventoryUI");
    ///   UIManager.Instance.PopPopup();      // ESC 키 등
    ///   UIManager.Instance.CloseAll();      // 씬 전환 전 등
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }


        // 등록된 모든 패널 (PanelId → UIPanel)
        readonly Dictionary<string, UIPanel> _panels     = new();
        // 팝업 전용 스택 (isPopup=true 인 패널)
        readonly Stack<UIPanel>              _popupStack = new();

        // ── Unity ────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            // AssetLoadManager 경유로 HUD 프리팹을 자식으로 생성.
            // 자식이므로 DontDestroyOnLoad는 UIManager에서 자동 상속.
            var hudPrefab = AssetLoadManager.Instance?.Load<GameObject>(AssetKeys.PREFAB_HUD);
            if (hudPrefab != null)
                Instantiate(hudPrefab, transform);
            else
                Debug.LogWarning("[UIManager] HUD 프리팹을 AssetManifest에서 찾을 수 없습니다. 키: " + AssetKeys.PREFAB_HUD);
        }

        // ── 등록 / 해제 ──────────────────────────────────────────────

        /// <summary>UIPanel.Start() 에서 자동 호출된다.</summary>
        public void Register(UIPanel panel)
        {
            if (panel == null) return;
            if (_panels.ContainsKey(panel.PanelId))
                Debug.LogWarning($"[UIManager] '{panel.PanelId}' 이미 등록됨 — 덮어씁니다.");
            _panels[panel.PanelId] = panel;
        }

        /// <summary>UIPanel.OnDestroy() 에서 자동 호출된다.</summary>
        public void Unregister(UIPanel panel)
        {
            if (panel == null) return;
            _panels.Remove(panel.PanelId);
            RemoveFromPopupStack(panel);
        }

        // ── 열기 / 닫기 / 토글 ───────────────────────────────────────

        /// <summary>패널을 연다. 팝업이면 스택에 push 된다.</summary>
        public void Open(string panelId)
        {
            if (!TryGet(panelId, out var panel)) return;
            if (panel.IsOpen) return;

            if (panel.IsPopup)
            {
                // 기존 최상위 팝업에 blur 알림
                if (_popupStack.Count > 0)
                    _popupStack.Peek().OnBlur();
                _popupStack.Push(panel);
            }

            panel.OnOpen();
        }

        /// <summary>패널을 닫는다. 팝업이면 스택에서 제거된다.</summary>
        public void Close(string panelId)
        {
            if (!TryGet(panelId, out var panel)) return;
            if (!panel.IsOpen) return;

            if (panel.IsPopup)
            {
                RemoveFromPopupStack(panel);
                panel.OnClose();
                // 이전 팝업에 focus 알림
                if (_popupStack.Count > 0)
                    _popupStack.Peek().OnFocus();
            }
            else
            {
                panel.OnClose();
            }
        }

        /// <summary>열려 있으면 닫고, 닫혀 있으면 연다.</summary>
        public void Toggle(string panelId)
        {
            if (!TryGet(panelId, out var panel)) return;
            if (panel.IsOpen) Close(panelId);
            else              Open(panelId);
        }

        // ── 팝업 스택 제어 ────────────────────────────────────────────

        /// <summary>최상위 팝업을 닫는다. (ESC / 뒤로가기 버튼용)</summary>
        /// <returns>닫은 팝업이 있으면 true.</returns>
        public bool PopPopup()
        {
            if (_popupStack.Count == 0) return false;
            var top = _popupStack.Pop();
            top.OnClose();
            if (_popupStack.Count > 0)
                _popupStack.Peek().OnFocus();
            return true;
        }

        /// <summary>열려 있는 팝업이 있는지 여부.</summary>
        public bool HasOpenPopup => _popupStack.Count > 0;

        // ── 전체 닫기 ────────────────────────────────────────────────

        /// <summary>모든 팝업 스택 + 일반 패널을 닫는다.</summary>
        public void CloseAll()
        {
            while (_popupStack.Count > 0)
            {
                var top = _popupStack.Pop();
                top.OnClose();
            }

            foreach (var panel in _panels.Values)
                if (panel.IsOpen && !panel.IsPopup)
                    panel.OnClose();
        }

        // ── 조회 ─────────────────────────────────────────────────────

        public bool IsOpen(string panelId)
            => _panels.TryGetValue(panelId, out var p) && p.IsOpen;

        /// <summary>등록된 패널을 타입으로 가져온다.</summary>
        public T GetPanel<T>(string panelId) where T : UIPanel
            => _panels.TryGetValue(panelId, out var p) ? p as T : null;

        // ── 내부 헬퍼 ────────────────────────────────────────────────

        bool TryGet(string panelId, out UIPanel panel)
        {
            if (_panels.TryGetValue(panelId, out panel)) return true;
            Debug.LogWarning($"[UIManager] 패널 '{panelId}' 를 찾을 수 없습니다. Register 가 됐는지 확인하세요.");
            return false;
        }

        /// <summary>
        /// 팝업 스택 중간에 있는 항목도 제거할 수 있도록
        /// 스택을 재구성한다.
        /// </summary>
        void RemoveFromPopupStack(UIPanel target)
        {
            if (!_popupStack.Contains(target)) return;

            var temp = new List<UIPanel>(_popupStack);
            temp.Remove(target);
            _popupStack.Clear();
            // Stack 은 LIFO 이므로 역순으로 push
            for (int i = temp.Count - 1; i >= 0; i--)
                _popupStack.Push(temp[i]);
        }
    }
}
