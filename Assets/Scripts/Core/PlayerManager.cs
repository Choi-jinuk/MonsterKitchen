using MonsterKitchen.Data;
using MonsterKitchen.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  PlayerManager — 플레이어 단일 인스턴스 관리자 (DontDestroyOnLoad)
    //
    //  ▶ 역할
    //    - 게임 시작 시 PlayerSpawnData 기반으로 Player를 1회 스폰.
    //    - 씬 전환 후 새 씬의 "PlayerSpawnPoint" GO 위치로 Player를 재배치.
    //    - SpawnManager, GameHUD 등이 Player.transform을 참조할 때 이 클래스를 경유.
    //
    //  ▶ 배치
    //    ManagementScene의 GameManager (또는 별도 PlayerManager GO)에 추가.
    //    DontDestroyOnLoad이므로 씬 전환 후에도 유지된다.
    //
    //  ▶ PlayerSpawnPoint 명명 규칙
    //    각 씬에 이름이 "PlayerSpawnPoint"인 빈 GO를 배치하면
    //    씬 전환 시 자동으로 해당 위치에 Player가 이동한다.
    //    없으면 (0, 0, 0)에 배치.
    // ====================================================================

    [DisallowMultipleComponent]
    public class PlayerManager : MonoBehaviour
    {
        public static PlayerManager Instance { get; private set; }

        PlayerSpawnData _playerData;

        /// <summary>현재 살아있는 Player 인스턴스.</summary>
        public PlayerController Player { get; private set; }

        // ----------------------------------------------------------------

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

        void Start()
        {
            _playerData = AssetLoadManager.Instance?.Load<PlayerSpawnData>(AssetKeys.DataPlayerSpawn);
            if (_playerData == null)
                Debug.LogError("[PlayerManager] PlayerSpawnData를 AssetManifest에서 찾을 수 없습니다. 키: " + AssetKeys.DataPlayerSpawn);

            SpawnOrReposition(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // ----------------------------------------------------------------

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SpawnOrReposition(scene);
        }

        void SpawnOrReposition(Scene scene)
        {
            var spawnPoint = FindSpawnPoint();
            Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;

            if (Player == null || Player.gameObject == null)
            {
                // 최초 스폰 (또는 Player가 파괴된 경우)
                Player = SpawnPlayer(pos);
            }
            else
            {
                // 씬 전환: 위치만 재배치
                Player.transform.position = pos;
                Debug.Log($"[PlayerManager] 씬 '{scene.name}' — Player 위치 이동: {pos}");
            }
        }

        static Transform FindSpawnPoint()
        {
            var go = GameObject.Find("PlayerSpawnPoint");
            return go != null ? go.transform : null;
        }

        PlayerController SpawnPlayer(Vector3 pos)
        {
            if (_playerData == null || _playerData.prefab == null)
            {
                Debug.LogError("[PlayerManager] PlayerSpawnData 또는 prefab이 없습니다.");
                return null;
            }

            // InstantiateDisabled 패턴 — Init 전에 Awake/OnEnable 방지
            bool wasActive = _playerData.prefab.gameObject.activeSelf;
            _playerData.prefab.gameObject.SetActive(false);

            var player = Instantiate(_playerData.prefab, pos, Quaternion.identity);

            _playerData.prefab.gameObject.SetActive(wasActive);

            player.Init(_playerData);
            player.gameObject.SetActive(true);

            // Player도 씬 전환 후 유지
            DontDestroyOnLoad(player.gameObject);

            Debug.Log($"[PlayerManager] Player 스폰 완료: {pos}");
            return player;
        }
    }
}
