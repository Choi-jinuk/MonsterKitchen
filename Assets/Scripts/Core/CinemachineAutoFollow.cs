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
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && _vcam != null)
                _vcam.Follow = player.transform;
        }
    }
}
