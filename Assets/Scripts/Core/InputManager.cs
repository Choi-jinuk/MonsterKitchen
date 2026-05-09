using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 게임 내 모든 입력을 중앙에서 관리하는 싱글톤.
    /// InputSystem_Actions 인스턴스를 하나만 유지하며 이벤트로 배포한다.
    /// ManagementScene 의 InputManager 오브젝트에 부착, DontDestroyOnLoad.
    ///
    /// ┌─ 플레이어 입력 이벤트 ────────────────────────────────────────────┐
    /// │ OnMove(Vector2)  — WASD/스틱 이동 방향 (정지 = Vector2.zero)     │
    /// │ OnAttack()       — 공격 (마우스 좌클릭 / Z키 / 게임패드)          │
    /// │ OnDash()         — 대시 (Space / 게임패드 RB)                    │
    /// │ OnInteract()     — 상호작용 E키 (Hold 상호작용)                   │
    /// │ OnSkill1()       — 스킬 슬롯 1 (Q키)                             │
    /// │ OnSkill2()       — 스킬 슬롯 2 (R키)                             │
    /// │ OnUltimate()     — 궁극기 (F키)                                  │
    /// └────────────────────────────────────────────────────────────────┘
    ///
    /// ┌─ UI 토글 ────────────────────────────────────────────────────────┐
    /// │ UIPanel.Start() 에서 RegisterUIToggle(panelId, key) 자동 등록.  │
    /// │ ESC 키 → UIManager.PopPopup() (최상위 팝업 닫기)                 │
    /// └────────────────────────────────────────────────────────────────┘
    ///
    /// ┌─ 입력 활성/비활성 ─────────────────────────────────────────────────┐
    /// │ EnablePlayerInput() / DisablePlayerInput()                       │
    /// │ → 다이얼로그·컷씬 등에서 플레이어 입력을 잠글 때 사용            │
    /// └────────────────────────────────────────────────────────────────┘
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        // ── 플레이어 입력 이벤트 ─────────────────────────────────────────
        /// <summary>
        /// 이동 방향. performed / canceled 모두 이 이벤트로 전달된다.
        /// 정지(canceled) 시 Vector2.zero 가 전달된다.
        /// </summary>
        public event Action<Vector2> OnMove;

        /// <summary>공격 입력 (performed). 마우스 좌클릭 · Z키 · 게임패드 서쪽버튼.</summary>
        public event Action OnAttack;

        /// <summary>대시 입력 (performed). Space · 게임패드 RB.</summary>
        public event Action OnDash;

        /// <summary>상호작용 입력 (performed). E키 · 게임패드 북쪽버튼.</summary>
        public event Action OnInteract;

        /// <summary>스킬 슬롯 1 입력 (performed). Q키.</summary>
        public event Action OnSkill1;

        /// <summary>스킬 슬롯 2 입력 (performed). R키.</summary>
        public event Action OnSkill2;

        /// <summary>궁극기 입력 (performed). F키.</summary>
        public event Action OnUltimate;

        // ── UI 토글 맵 (Key → PanelId) ──────────────────────────────────
        readonly Dictionary<Key, string> _uiToggleMap = new();

        InputSystem_Actions _input;

        // ── Unity 생명주기 ────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _input = new InputSystem_Actions();
        }

        void OnEnable()
        {
            _input.Player.Enable();

            _input.Player.Move.performed     += HandleMovePerformed;
            _input.Player.Move.canceled      += HandleMoveCanceled;
            _input.Player.Attack.performed   += HandleAttackPerformed;
            _input.Player.Dash.performed     += HandleDashPerformed;
            _input.Player.Interact.performed += HandleInteractPerformed;
        }

        void OnDisable()
        {
            _input.Player.Move.performed     -= HandleMovePerformed;
            _input.Player.Move.canceled      -= HandleMoveCanceled;
            _input.Player.Attack.performed   -= HandleAttackPerformed;
            _input.Player.Dash.performed     -= HandleDashPerformed;
            _input.Player.Interact.performed -= HandleInteractPerformed;

            _input.Player.Disable();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // 등록된 UI 토글 단축키
            foreach (var kv in _uiToggleMap)
                if (kb[kv.Key].wasPressedThisFrame)
                    UI.UIManager.Instance?.Toggle(kv.Value);

            // ESC → 최상위 팝업 닫기
            if (kb.escapeKey.wasPressedThisFrame)
                UI.UIManager.Instance?.PopPopup();

            // 스킬 / 궁극기 단축키
            if (kb.qKey.wasPressedThisFrame) OnSkill1?.Invoke();
            if (kb.rKey.wasPressedThisFrame) OnSkill2?.Invoke();
            if (kb.fKey.wasPressedThisFrame) OnUltimate?.Invoke();
        }

        // ── 입력 콜백 ─────────────────────────────────────────────────

        void HandleMovePerformed(InputAction.CallbackContext ctx)  => OnMove?.Invoke(ctx.ReadValue<Vector2>());
        void HandleMoveCanceled(InputAction.CallbackContext ctx)   => OnMove?.Invoke(Vector2.zero);
        void HandleAttackPerformed(InputAction.CallbackContext ctx) => OnAttack?.Invoke();
        void HandleDashPerformed(InputAction.CallbackContext ctx)   => OnDash?.Invoke();
        void HandleInteractPerformed(InputAction.CallbackContext ctx) => OnInteract?.Invoke();

        // ── UI 토글 등록 / 해제 ─────────────────────────────────────────

        /// <summary>panelId 패널을 key 로 토글하도록 등록한다. UIPanel.Start() 에서 자동 호출.</summary>
        public void RegisterUIToggle(string panelId, Key key)
        {
            if (key == Key.None || string.IsNullOrEmpty(panelId)) return;

            if (_uiToggleMap.TryGetValue(key, out var existing) && existing != panelId)
                Debug.LogWarning($"[InputManager] Key.{key} 가 '{existing}' 에 이미 등록됨 → '{panelId}' 로 덮어씁니다.");

            _uiToggleMap[key] = panelId;
        }

        /// <summary>패널 ID 로 등록된 UI 토글 키를 모두 해제한다. UIPanel.OnDestroy() 에서 자동 호출.</summary>
        public void UnregisterUIToggle(string panelId)
        {
            var toRemove = new List<Key>();
            foreach (var kv in _uiToggleMap)
                if (kv.Value == panelId) toRemove.Add(kv.Key);
            foreach (var k in toRemove) _uiToggleMap.Remove(k);
        }

        // ── 플레이어 입력 활성화 / 비활성화 ─────────────────────────────

        /// <summary>플레이어 입력을 활성화한다.</summary>
        public void EnablePlayerInput()  => _input.Player.Enable();

        /// <summary>플레이어 입력을 비활성화한다 (다이얼로그·컷씬 등).</summary>
        public void DisablePlayerInput() => _input.Player.Disable();
    }
}
