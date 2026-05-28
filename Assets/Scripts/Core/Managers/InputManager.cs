using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 게임 내 모든 입력을 중앙에서 관리하는 싱글톤.
    /// GlobalController 가 new 로 생성하고 Init() 를 호출한다.
    /// GlobalController 가 OnEnable / OnDisable / Update 를 위임 호출한다.
    ///
    /// ┌─ 플레이어 입력 이벤트 ────────────────────────────────────────────┐
    /// │ OnMove(Vector2)  — WASD/스틱 이동 방향 (정지 = Vector2.zero)     │
    /// │ OnAttack()       — 공격 (마우스 좌클릭 / Z키 / 게임패드)          │
    /// │ OnDash()         — 대시 (Space / 게임패드 RB)                    │
    /// │ OnInteract()     — 상호작용 E키                                   │
    /// │ OnSkill1()       — 스킬 슬롯 1 (Q키)                             │
    /// │ OnSkill2()       — 스킬 슬롯 2 (R키)                             │
    /// │ OnUltimate()     — 궁극기 (F키)                                  │
    /// └────────────────────────────────────────────────────────────────┘
    /// </summary>
    public class InputManager
    {
        public static InputManager Instance { get; private set; }

        public event Action<Vector2> OnMove;
        public event Action OnAttack;
        public event Action OnDash;
        public event Action OnInteract;
        public event Action OnSkill1;
        public event Action OnSkill2;
        public event Action OnUltimate;

        readonly Dictionary<Key, string> m_UiToggleMap = new();

        InputSystem_Actions m_Input;

        public void Init()
        {
            Instance = this;
            m_Input  = new InputSystem_Actions();
        }

        // ── GlobalController 에서 위임 호출 ──────────────────────────────

        public void OnEnable()
        {
            m_Input.Player.Enable();

            m_Input.Player.Move.performed     += HandleMovePerformed;
            m_Input.Player.Move.canceled      += HandleMoveCanceled;
            m_Input.Player.Attack.performed   += HandleAttackPerformed;
            m_Input.Player.Dash.performed     += HandleDashPerformed;
            m_Input.Player.Interact.performed += HandleInteractPerformed;
        }

        public void OnDisable()
        {
            m_Input.Player.Move.performed     -= HandleMovePerformed;
            m_Input.Player.Move.canceled      -= HandleMoveCanceled;
            m_Input.Player.Attack.performed   -= HandleAttackPerformed;
            m_Input.Player.Dash.performed     -= HandleDashPerformed;
            m_Input.Player.Interact.performed -= HandleInteractPerformed;

            m_Input.Player.Disable();
        }

        public void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            foreach (var kv in m_UiToggleMap)
                if (kb[kv.Key].wasPressedThisFrame)
                    UI.UIManager.Instance?.Toggle(kv.Value);

            if (kb.escapeKey.wasPressedThisFrame)
                UI.UIManager.Instance?.PopPopup();

            if (kb.qKey.wasPressedThisFrame) OnSkill1?.Invoke();
            if (kb.rKey.wasPressedThisFrame) OnSkill2?.Invoke();
            if (kb.fKey.wasPressedThisFrame) OnUltimate?.Invoke();
        }

        // ── 입력 콜백 ─────────────────────────────────────────────────

        void HandleMovePerformed(InputAction.CallbackContext ctx)    => OnMove?.Invoke(ctx.ReadValue<Vector2>());
        void HandleMoveCanceled(InputAction.CallbackContext ctx)     => OnMove?.Invoke(Vector2.zero);
        void HandleAttackPerformed(InputAction.CallbackContext ctx)  => OnAttack?.Invoke();
        void HandleDashPerformed(InputAction.CallbackContext ctx)    => OnDash?.Invoke();
        void HandleInteractPerformed(InputAction.CallbackContext ctx) => OnInteract?.Invoke();

        // ── UI 토글 등록 / 해제 ─────────────────────────────────────────

        public void RegisterUIToggle(string panelId, Key key)
        {
            if (key == Key.None || string.IsNullOrEmpty(panelId)) return;

            if (m_UiToggleMap.TryGetValue(key, out var existing) && existing != panelId)
                Debug.LogWarning($"[InputManager] Key.{key} 가 '{existing}' 에 이미 등록됨 → '{panelId}' 로 덮어씁니다.");

            m_UiToggleMap[key] = panelId;
        }

        public void UnregisterUIToggle(string panelId)
        {
            var toRemove = new List<Key>();
            foreach (var kv in m_UiToggleMap)
                if (kv.Value == panelId) toRemove.Add(kv.Key);
            foreach (var k in toRemove) m_UiToggleMap.Remove(k);
        }

        public void EnablePlayerInput()  => m_Input.Player.Enable();
        public void DisablePlayerInput() => m_Input.Player.Disable();

        // 바인딩 정보를 외부에서 읽을 수 있도록 액션 참조를 노출한다.
        // Init() 이후에만 호출할 것 (Instance != null 보장 이후).
        public InputAction InteractAction => m_Input.Player.Interact;
    }
}
