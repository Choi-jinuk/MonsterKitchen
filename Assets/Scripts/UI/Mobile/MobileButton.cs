using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace MonsterKitchen.UI.Mobile
{
    // ====================================================================
    //  MobileButton — 모바일 액션 버튼 1개
    //
    //  ▶ New Input System (Mouse.current / Touchscreen.current) 직접 폴링
    //    — EventSystem / InputSystemUIInputModule 불필요
    //  ▶ RectangleContainsScreenPoint 로 버튼 영역 히트테스트
    //  ▶ SetAction(MobileAction) 으로 런타임 교체 가능 (MobileHUD 가 사용)
    //  ▶ Press 시각 피드백: scale 0.9 → Release 시 1.0 복귀
    // ====================================================================

    public class MobileButton : MonoBehaviour
    {
        [SerializeField] MobileAction m_Action = MobileAction.Dash;

        public MobileAction Action => m_Action;

        bool   m_Pressed;
        int    m_TouchId = int.MinValue;
        Canvas m_RootCanvas;

        // ── Unity ────────────────────────────────────────────────────

        void Awake()
        {
            m_RootCanvas = GetComponentInParent<Canvas>();
        }

        void Update()
        {
            HandleMouse();
            HandleTouch();
        }

        // ── Mouse (PC / 에디터 테스트) ───────────────────────────────

        void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pos = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame && !m_Pressed && HitTest(pos))
                Press();
            else if (mouse.leftButton.wasReleasedThisFrame && m_Pressed)
                Release();
        }

        // ── Touch (모바일) ───────────────────────────────────────────

        void HandleTouch()
        {
            var screen = Touchscreen.current;
            if (screen == null) return;

            foreach (var touch in screen.touches)
            {
                var   phase = touch.phase.ReadValue();
                Vector2 pos = touch.position.ReadValue();
                int      id = touch.touchId.ReadValue();

                if (phase == TouchPhase.Began && !m_Pressed && HitTest(pos))
                {
                    m_TouchId = id;
                    Press();
                }
                else if (id == m_TouchId && (phase == TouchPhase.Ended || phase == TouchPhase.Canceled))
                {
                    m_TouchId = int.MinValue;
                    Release();
                }
            }
        }

        // ── 공개 API ─────────────────────────────────────────────────

        public void SetAction(MobileAction action)
        {
            m_Action = action;
        }

        // ── 내부 ─────────────────────────────────────────────────────

        void Press()
        {
            m_Pressed = true;
            transform.localScale = Vector3.one * 0.9f;
            FireAction();
        }

        void Release()
        {
            m_Pressed = false;
            transform.localScale = Vector3.one;
        }

        void FireAction()
        {
            var input = InputManager.Instance;
            if (input == null) return;

            switch (m_Action)
            {
                case MobileAction.Dash:     input.InjectDash();     break;
                case MobileAction.Skill1:   input.InjectSkill1();   break;
                case MobileAction.Skill2:   input.InjectSkill2();   break;
                case MobileAction.Interact: input.InjectInteract(); break;
                case MobileAction.Ultimate: input.InjectUltimate(); break;
            }
        }

        bool HitTest(Vector2 screenPos)
        {
            Camera cam = (m_RootCanvas != null && m_RootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? m_RootCanvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(
                transform as RectTransform, screenPos, cam);
        }
    }
}
