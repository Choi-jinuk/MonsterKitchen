// ====================================================================
//  SceneControllerBase — 씬 State Machine 베이스
//
//  ▶ 상태 흐름
//    Init → Running → End
//
//  ▶ 사용법
//    1. 씬 전용 MonoBehaviour 에서 상속
//    2. OnInit()   : 씬 세팅 완료 후 CompleteInit() 호출
//    3. OnRunning(): 매 프레임 게임플레이 로직
//    4. OnEnd()    : 씬 정리 로직 (ExitScene 이 자동 호출)
//
//  ▶ 씬 전환
//    ExitScene("SceneName") 호출 → OnEnd() → SceneLoader.LoadScene()
//    중복 호출 방지: State == End 면 무시
//
//  ▶ 비동기 Init
//    OnInit() 에서 StartCoroutine 후 완료 시 CompleteInit() 호출 가능
// ====================================================================

using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Core
{
    public abstract class SceneControllerBase : MonoBehaviour
    {
        public enum SceneState { Init, Running, End }

        public SceneState State { get; private set; } = SceneState.Init;

        /// <summary>씬의 플레이어 스폰 위치. Inspector 연결 — 미연결 시 PlayerManager 가 이름 검색 폴백.</summary>
        [SerializeField] protected Transform m_PlayerSpawnPoint;

        // ── Unity ────────────────────────────────────────────────────
        void Start()
        {
            State = SceneState.Init;
            OnInit();
        }

        // ================================================================
        //  상태 전이 — 서브클래스에서 호출
        // ================================================================

        /// <summary>OnInit() 작업 완료 시 호출 → Running 상태 진입.</summary>
        protected void CompleteInit()
        {
            State = SceneState.Running;
            OnRunning();
        }

        /// <summary>씬 종료 → OnEnd() 후 다음 씬 로드.</summary>
        protected void ExitScene(string nextScene)
        {
            if (State == SceneState.End) return;
            State = SceneState.End;
            OnEnd();
            SceneLoader.Instance?.LoadScene(nextScene);
        }

        // ================================================================
        //  오버라이드 포인트
        // ================================================================

        /// <summary>씬 진입 초기화. 완료 후 CompleteInit() 호출 필수.</summary>
        protected virtual void OnInit()    => CompleteInit();

        /// <summary>Running 상태 진입 직후 1회 호출.</summary>
        protected virtual void OnRunning() { }

        /// <summary>씬 전환 직전 정리. ExitScene() 에서 자동 호출.</summary>
        protected virtual void OnEnd()     { }
    }
}
