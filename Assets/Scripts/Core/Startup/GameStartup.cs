using System;
using System.Collections;
using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  GameStartup — 인게임 시작 프로세스 총괄
    //
    //  ▶ 실행 순서 (GlobalController.Start → StartCoroutine(Startup.Run()))
    //
    //    Phase 1  SdkInit       — 3rd-party SDK 초기화
    //                             (Analytics, Firebase, Crash Reporting 등)
    //    Phase 2  DataLoad      — 정적 게임 데이터 로드
    //                             (TableData SO → DataRegistry)
    //    Phase 3  ServerConnect — 서버 인증 & 세션 수립
    //                             (WebSocket 연결, JWT 토큰 교환)
    //    Phase 4  SaveLoad      — 플레이어 세이브 데이터 로드
    //                             (서버 연결 이후 실행 — 서버 데이터 우선)
    //
    //  ▶ 확장 방법
    //    각 Step* 메서드에 실제 비동기 작업을 추가한다.
    //    Coroutine 기반 → UniTask / async 로 교체 가능 (Run() 시그니처 변경).
    //
    //  ▶ 에러 처리
    //    OnError 이벤트로 UI 레이어에 알린다.
    //    치명적 단계(DataLoad 실패)는 IsAborted 플래그를 세우고 중단.
    //
    //  ▶ 이벤트 구독 예시
    //    GlobalController.Instance.Startup.OnPhaseStart   += p => ShowLoadingStep(p);
    //    GlobalController.Instance.Startup.OnComplete     += () => HideLoadingScreen();
    //    GlobalController.Instance.Startup.OnError        += msg => ShowErrorDialog(msg);
    // ====================================================================

    public enum StartupPhase
    {
        SdkInit,        // 1) Analytics / Crash / Firebase
        DataLoad,       // 2) TableData (정적 게임 정의)
        ServerConnect,  // 3) 서버 인증 & 핸드셰이크
        SaveLoad,       // 4) 플레이어 세이브 데이터
    }

    public class GameStartup
    {
        // ── 이벤트 ───────────────────────────────────────────────────────
        /// <summary>각 단계 시작 직전에 발행. 로딩 UI 업데이트에 활용.</summary>
        public event Action<StartupPhase> OnPhaseStart;

        /// <summary>각 단계 완료 직후에 발행.</summary>
        public event Action<StartupPhase> OnPhaseComplete;

        /// <summary>전체 시퀀스 정상 완료 시 발행.</summary>
        public event Action OnComplete;

        /// <summary>복구 불가능한 오류 발생 시 발행. arg: 오류 메시지.</summary>
        public event Action<string> OnError;

        // ── 상태 ─────────────────────────────────────────────────────────
        public bool IsComplete { get; private set; }
        public bool IsAborted  { get; private set; }

        // ================================================================
        //  진입점 — GlobalController.Start() 에서 StartCoroutine(Startup.Run())
        // ================================================================

        public IEnumerator Run()
        {
            IsComplete = false;
            IsAborted  = false;

            yield return RunPhase(StartupPhase.SdkInit,       StepSdkInit);
            if (IsAborted) yield break;

            yield return RunPhase(StartupPhase.DataLoad,      StepDataLoad);
            if (IsAborted) yield break;

            yield return RunPhase(StartupPhase.ServerConnect, StepServerConnect);
            if (IsAborted) yield break;

            yield return RunPhase(StartupPhase.SaveLoad,      StepSaveLoad);
            if (IsAborted) yield break;

            IsComplete = true;
            Debug.Log("[GameStartup] 시작 프로세스 완료.");
            OnComplete?.Invoke();
        }

        // ================================================================
        //  Phase 1 — SDK 초기화
        // ================================================================

        /// <summary>
        /// 3rd-party SDK 초기화.
        /// 실패해도 게임 진행은 가능하므로 IsAborted 세우지 않음.
        /// </summary>
        IEnumerator StepSdkInit()
        {
            // ── [SDK STUB START] ──────────────────────────────────────────
            // TODO: Firebase.CheckAndFixDependenciesAsync().AsCoroutine()
            // TODO: GameAnalytics.Initialize()
            // TODO: AppsFlyer.initSDK(devKey, appId, this)
            // ── [SDK STUB END] ────────────────────────────────────────────
            yield break;
        }

        // ================================================================
        //  Phase 2 — 정적 데이터 로드
        // ================================================================

        /// <summary>
        /// TableData SO 를 AssetManifest 에서 로드해 DataRegistry 에 등록한다.
        /// 실패 시 치명적 오류 — IsAborted = true.
        /// </summary>
        IEnumerator StepDataLoad()
        {
            GlobalController.Instance.Registry.Load();

            if (!GlobalController.Instance.Registry.IsReady)
            {
                string msg = "[GameStartup] TableData 로드 실패 — AssetManifest 'data/table_data' 키 확인 요망";
                Debug.LogError(msg);
                IsAborted = true;
                OnError?.Invoke(msg);
            }

            yield break;
        }

        // ================================================================
        //  Phase 3 — 서버 연결
        // ================================================================

        /// <summary>
        /// 서버 인증 & 세션 수립.
        /// 오프라인 모드 지원 시 실패해도 진행 가능하도록 설계.
        /// </summary>
        IEnumerator StepServerConnect()
        {
            // ── [SERVER STUB START] ───────────────────────────────────────
            // TODO: WebSocket.ConnectAsync(serverUrl).AsCoroutine()
            // TODO: AuthService.RequestTokenAsync(userId).AsCoroutine()
            // TODO: IsAborted = true on critical auth failure
            // ── [SERVER STUB END] ─────────────────────────────────────────
            yield break;
        }

        // ================================================================
        //  Phase 4 — 세이브 데이터 로드
        // ================================================================

        /// <summary>
        /// 플레이어 세이브 데이터를 로드해 PlayerDataManager 에 적용한다.
        /// ServerConnect 이후에 실행 — 서버 데이터가 로컬 캐시보다 우선.
        /// </summary>
        IEnumerator StepSaveLoad()
        {
            GlobalController.Instance.ServerDB.Load();
            yield break;
        }

        // ================================================================
        //  내부
        // ================================================================

        IEnumerator RunPhase(StartupPhase phase, Func<IEnumerator> step)
        {
            Debug.Log(StringUtil.Format("[GameStartup] ▶ {0}", phase));
            OnPhaseStart?.Invoke(phase);
            yield return step();
            if (!IsAborted)
                OnPhaseComplete?.Invoke(phase);
        }
    }
}
