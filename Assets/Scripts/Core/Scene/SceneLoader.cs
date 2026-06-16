using MonsterKitchen.Core;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 씬 전환 싱글톤.
    ///
    /// ▶ 전환 흐름
    ///   FadeOut → LoadingScene (메모리 해제 + GC) → 타겟씬 비동기 로드
    ///   → allowSceneActivation (Awake/Start 실행) → FadeIn
    ///
    /// ▶ LoadingScene 이 Build Settings 에 없으면 버퍼 단계를 건너뛴다.
    /// </summary>
    public class SceneLoader
    {
        public static SceneLoader Instance { get; private set; }

        const string LOADING_SCENE = CommonString.SceneLoading;

        readonly MonoBehaviour m_Runner;

        public bool  IsLoading      { get; private set; }
        public float LoadingProgress { get; private set; }   // 0~1, 타겟씬 비동기 진행률

        public event Action<string> OnSceneLoadStarted;
        public event Action<string> OnSceneLoadFinished;
        public event Action<float>  OnProgressChanged;        // LoadingProgress 갱신 시

        public SceneLoader(MonoBehaviour runner) => m_Runner = runner;

        public void Init() => Instance = this;

        public void LoadScene(string sceneName)
        {
            if (IsLoading) return;
            m_Runner.StartCoroutine(LoadAsync(sceneName));
        }

        IEnumerator LoadAsync(string sceneName)
        {
            IsLoading      = true;
            LoadingProgress = 0f;
            OnSceneLoadStarted?.Invoke(sceneName);

            // SceneFader 보장
            EnsureSceneFader();

            // ── 1. 페이드 아웃 ───────────────────────────────────────────
            if (SceneFader.Instance != null)
                yield return m_Runner.StartCoroutine(SceneFader.Instance.FadeOut(0.3f));

            // ── 2. 로딩씬 경유 (현재 씬 언로드 → 메모리 해제) ───────────
            bool usedLoadingScene = false;
            if (LoadingSceneExists())
            {
                var loadOp = SceneManager.LoadSceneAsync(LOADING_SCENE);
                if (loadOp != null)
                {
                    while (!loadOp.isDone)
                        yield return null;

                    // GC 수집 후 2프레임 대기 — 해제된 메모리 정리
                    System.GC.Collect();
                    yield return null;
                    yield return null;

                    // 로딩씬 텍스트 노출 — FadeIn 으로 검정 해제
                    if (SceneFader.Instance != null)
                        yield return m_Runner.StartCoroutine(SceneFader.Instance.FadeIn(0.2f));

                    usedLoadingScene = true;
                }
            }

            // ── 3. 타겟씬 비동기 로드 — 활성화는 아직 보류 ──────────────
            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                DebugUtil.LogError(
                    $"[SceneLoader] 씬 '{sceneName}' 로드 실패. " +
                    "Build Settings 에 씬이 추가됐는지 확인하세요.");
                IsLoading = false;
                OnSceneLoadFinished?.Invoke(sceneName);
                yield break;
            }

            // allowSceneActivation = false:
            //   로드 완료(progress=0.9)까지 Awake/Start 를 실행하지 않는다.
            //   → 씬 데이터 로드와 초기화 스파이크를 분리한다.
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                // progress: 0 ~ 0.9 → normalize to 0 ~ 1
                LoadingProgress = Mathf.Clamp01(op.progress / 0.9f);
                OnProgressChanged?.Invoke(LoadingProgress);
                yield return null;
            }
            LoadingProgress = 1f;
            OnProgressChanged?.Invoke(1f);
            yield return null;   // 1프레임 대기 — 바 100% 시각 확인

            // ── 4. 타겟씬 활성화 전 페이드 아웃 (로딩씬 가리기) ─────────
            if (usedLoadingScene && SceneFader.Instance != null)
                yield return m_Runner.StartCoroutine(SceneFader.Instance.FadeOut(0.2f));

            // ── 5. 씬 활성화 (Awake → Start 실행) ───────────────────────
            op.allowSceneActivation = true;
            while (!op.isDone)
                yield return null;

            // ── 6. 페이드 인 ─────────────────────────────────────────────
            if (SceneFader.Instance != null)
                yield return m_Runner.StartCoroutine(SceneFader.Instance.FadeIn(0.3f));

            IsLoading = false;
            OnSceneLoadFinished?.Invoke(sceneName);
        }

        // ================================================================
        //  유틸
        // ================================================================

        /// <summary>Build Settings 에 LoadingScene 이 등록됐는지 확인.</summary>
        static bool LoadingSceneExists()
        {
            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (path.IndexOf(LOADING_SCENE,
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>SceneFader 가 없으면 런타임에 생성한다.</summary>
        static void EnsureSceneFader()
        {
            if (SceneFader.Instance != null) return;
            var go = new GameObject("SceneFader");
            go.AddComponent<SceneFader>();
        }
    }
}
