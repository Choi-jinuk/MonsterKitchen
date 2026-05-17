using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 씬 전환 싱글톤. DontDestroyOnLoad.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        public bool IsLoading { get; private set; }

        public event Action<string> OnSceneLoadStarted;
        public event Action<string> OnSceneLoadFinished;

        public void Init()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            if (Instance == null) Init();
        }

        public void LoadScene(string sceneName)
        {
            if (IsLoading) return;

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("[SceneLoader] 비활성 오브젝트에서 LoadScene 호출됨. SceneLoader GameObject가 활성 상태인지 확인하세요.");
                return;
            }

            StartCoroutine(LoadAsync(sceneName));
        }

        IEnumerator LoadAsync(string sceneName)
        {
            IsLoading = true;
            OnSceneLoadStarted?.Invoke(sceneName);

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] 씬 '{sceneName}'을(를) 로드할 수 없습니다. " +
                               "File → Build Settings 에 씬이 추가됐는지 확인하세요.");
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
