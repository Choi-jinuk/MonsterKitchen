using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace MonsterKitchen.UI.Mobile
{
    // ====================================================================
    //  VirtualJoystick — Dynamic floating 가상 조이스틱
    //
    //  ▶ New Input System (Mouse.current / Touchscreen.current) 직접 폴링
    //    — EventSystem / InputSystemUIInputModule 불필요
    //  ▶ 터치 시작 위치로 배경 재배치 (화면 왼쪽 절반만 허용)
    //  ▶ 드래그 → 핸들 이동 + InputManager.InjectMove(dir)
    //  ▶ 릴리즈 → 핸들 중앙 복귀 + InjectMove(zero)
    //  ▶ 멀티터치: 첫 손가락 ID 캐시, 마우스와 터치 독립 처리
    // ====================================================================

    public class VirtualJoystick : MonoBehaviour
    {
        [SerializeField] float         m_Radius   = 80f;
        [SerializeField] float         m_DeadZone = 0.1f;
        [SerializeField] RectTransform m_Background;
        [SerializeField] RectTransform m_Handle;

        bool    m_Active;
        int     m_TouchId = -1;  // -1 = mouse, ≥0 = touch finger ID
        Vector2 m_Center;        // 배경 중심 (Canvas 로컬 좌표)

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

            if (mouse.leftButton.wasPressedThisFrame && !m_Active && IsInLeftHalf(pos))
            {
                m_TouchId = -1;
                BeginJoystick(pos);
            }
            else if (mouse.leftButton.wasReleasedThisFrame && m_Active && m_TouchId == -1)
            {
                EndJoystick();
            }
            else if (mouse.leftButton.isPressed && m_Active && m_TouchId == -1)
            {
                UpdateJoystick(pos);
            }
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

                if (phase == TouchPhase.Began && !m_Active && IsInLeftHalf(pos))
                {
                    m_TouchId = id;
                    BeginJoystick(pos);
                }
                else if (id == m_TouchId)
                {
                    if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
                        EndJoystick();
                    else if (phase == TouchPhase.Moved || phase == TouchPhase.Stationary)
                        UpdateJoystick(pos);
                }
            }
        }

        // ── 내부 ─────────────────────────────────────────────────────

        void BeginJoystick(Vector2 screenPos)
        {
            m_Active = true;
            ScreenToLocal(screenPos, out m_Center);
            m_Background.localPosition = m_Center;
            m_Handle.localPosition     = m_Center;
        }

        void UpdateJoystick(Vector2 screenPos)
        {
            ScreenToLocal(screenPos, out Vector2 localPos);
            Vector2 delta   = localPos - m_Center;
            float   mag     = Mathf.Min(delta.magnitude, m_Radius);
            Vector2 clamped = delta.normalized * mag;
            m_Handle.localPosition = m_Center + clamped;

            Vector2 dir = clamped / m_Radius;
            if (dir.magnitude < m_DeadZone)
                InputManager.Instance?.InjectMove(Vector2.zero);
            else
                InputManager.Instance?.InjectMove(dir);
        }

        void EndJoystick()
        {
            m_Active  = false;
            m_TouchId = -1;
            m_Handle.localPosition = m_Background.localPosition;
            InputManager.Instance?.InjectMove(Vector2.zero);
        }

        void ScreenToLocal(Vector2 screenPos, out Vector2 localPoint)
        {
            Camera cam = (m_RootCanvas != null && m_RootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? m_RootCanvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_Background.parent as RectTransform, screenPos, cam, out localPoint);
        }

        bool IsInLeftHalf(Vector2 screenPos) => screenPos.x < Screen.width * 0.5f;
    }
}
