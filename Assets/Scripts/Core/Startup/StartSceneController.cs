// ====================================================================
//  StartSceneController — StartScene 진입점 컨트롤러
//
//  ▶ 상태 흐름
//    Init    : GlobalController.Startup.Run() 완료 대기
//              (이미 완료됐으면 즉시 CompleteInit)
//    Running : "TOUCH TO START" 표시 + 입력 대기
//    End     : ManagementScene 전환
// ====================================================================

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsterKitchen.Core
{
    public class StartSceneController : SceneControllerBase
    {
        // ── Inspector ────────────────────────────────────────────────
        [SerializeField] TextMeshProUGUI m_StatusText;
        [SerializeField] TextMeshProUGUI m_TouchToStartText;

        // ── 단계 표시 문자열 (StartupPhase 인덱스 순서) ───────────────
        static readonly string[] s_PhaseLabels =
        {
            "SDK 초기화 중...",
            "데이터 로드 중...",
            "서버 연결 중...",
            "저장 데이터 로드 중...",
        };

        // ================================================================
        //  SceneControllerBase
        // ================================================================

        protected override void OnInit()
        {
            SetTouchToStart(false);
            SetStatusText("초기화 중...");

            var startup = GlobalController.Instance?.Startup;
            if (startup == null)
            {
                DebugUtil.LogError("[StartSceneController] GlobalController.Startup 없음.");
                return;
            }

            // GlobalController.Start() 가 먼저 실행돼 Startup 이 이미 완료됐을 수 있다.
            if (startup.IsComplete)
            {
                CompleteInit();
                return;
            }

            startup.OnPhaseStart += HandlePhaseStart;
            startup.OnComplete   += HandleStartupComplete;
            startup.OnError      += HandleStartupError;
        }

        protected override void OnRunning()
        {
            SetStatusText(string.Empty);
            SetTouchToStart(true);
        }

        protected override void OnEnd()
        {
            var startup = GlobalController.Instance?.Startup;
            if (startup == null) return;
            startup.OnPhaseStart -= HandlePhaseStart;
            startup.OnComplete   -= HandleStartupComplete;
            startup.OnError      -= HandleStartupError;
        }

        // ================================================================
        //  Unity
        // ================================================================

        void Update()
        {
            if (State != SceneState.Running) return;
            if (!AnyInputPressed()) return;
            ExitScene(CommonString.SceneManagement);
        }

        // ================================================================
        //  Startup 이벤트
        // ================================================================

        void HandlePhaseStart(StartupPhase phase)
        {
            int idx = (int)phase;
            SetStatusText(idx < s_PhaseLabels.Length ? s_PhaseLabels[idx] : phase.ToString());
        }

        void HandleStartupComplete()
        {
            var startup = GlobalController.Instance?.Startup;
            if (startup != null)
            {
                startup.OnPhaseStart -= HandlePhaseStart;
                startup.OnComplete   -= HandleStartupComplete;
                startup.OnError      -= HandleStartupError;
            }
            CompleteInit();
        }

        void HandleStartupError(string msg) => SetStatusText($"오류: {msg}");

        // ================================================================
        //  유틸
        // ================================================================

        void SetStatusText(string text)
        {
            if (m_StatusText == null) return;
            m_StatusText.text = text;
            m_StatusText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        void SetTouchToStart(bool visible)
        {
            if (m_TouchToStartText == null) return;
            m_TouchToStartText.gameObject.SetActive(visible);
        }

        static bool AnyInputPressed()
        {
            if (Keyboard.current?.anyKey.wasPressedThisFrame == true) return true;
            if (Mouse.current?.leftButton.wasPressedThisFrame == true) return true;
            if (Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true) return true;
            return false;
        }
    }
}
