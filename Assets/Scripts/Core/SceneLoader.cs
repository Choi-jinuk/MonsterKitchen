using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 씬 전환 싱글톤.
    /// GlobalController 가 new SceneLoader(this) 로 생성하고 Init() 를 호출한다.
    /// 코루틴은 주입받은 runner(GlobalController) 를 통해 실행한다.
    /// </summary>
    public class SceneLoader
    {
        public static SceneLoader Instance { get; private set; }

        readonly MonoBehaviour _runner;

        public bool IsLoading { get; private set; }

        public event Action<string> OnSceneLoadStarted;
        public event Action<string> OnSceneLoadFinished;

        public SceneLoader(MonoBehaviour runner) => _runner = runner;

        public void Init() => Instance = this;

        public void LoadScene(string sceneName)
        {
            if (IsLoading) return;
            _runner.StartCoroutine(LoadAsync(sceneName));
        }

        IEnumerator LoadAsync(string sceneName)
        {
            IsLoading = true;
            OnSceneLoadStarted?.Invoke(sceneName);

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] 씬 '{sceneName}' 로드 실패. Build Settings 에 씬이 추가됐는지 확인하세요.");
                IsLoading = false;
                OnSceneLoadFinished?.Invoke(sceneName);
                yield break;
            }

            while (!op.isDone)
                yield return null;

            IsLoading = false;
            OnSceneLoadFinished?.Invoke(sceneName);
        }
    }
}
