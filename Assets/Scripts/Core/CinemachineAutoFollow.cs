using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// CinemachineCamera 에 플레이어를 자동으로 Follow 타겟으로 연결한다.
    /// PlayerManager 가 DontDestroyOnLoad 로 플레이어를 스폰하므로
    /// 씬 전환 시에도 SceneManager.sceneLoaded 를 통해 재탐색한다.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class CinemachineAutoFollow : MonoBehaviour
    {
        CinemachineCamera _vcam;

        void Awake() => _vcam = GetComponent<CinemachineCamera>();

        void Start() => FindAndAssign();

        void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => FindAndAssign();

        void FindAndAssign()
        {
            // PlayerManager 를 우선 사용 (Find 없이 직접 참조)
            var playerTf = PlayerManager.Instance?.Player?.transform;

            // PlayerManager 없으면 태그 조회 (최초 씬 로드 등 엣지케이스)
            if (playerTf == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                playerTf = go != null ? go.transform : null;
            }

            if (playerTf != null && _vcam != null)
                _vcam.Follow = playerTf;
        }
    }
}
