using MonsterKitchen.Data;
using MonsterKitchen.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 플레이어 단일 인스턴스 관리자.
    /// GlobalController 가 new 로 생성하고 Init() 를 호출한다.
    /// GlobalController.Start() 에서 Start() 를 위임 호출해 플레이어를 스폰한다.
    /// GlobalController.OnDestroy() 에서 Dispose() 를 위임 호출한다.
    /// </summary>
    public class PlayerManager
    {
        public static PlayerManager Instance { get; private set; }

        PlayerSpawnData _playerData;

        /// <summary>현재 살아있는 Player 인스턴스.</summary>
        public PlayerController Player { get; private set; }

        public void Init() => Instance = this;

        /// <summary>GlobalController.Start() 에서 호출 — AssetLoadManager 초기화 이후 실행.</summary>
        public void Start()
        {
            _playerData = AssetLoadManager.Instance?.Load<PlayerSpawnData>(AssetKeys.DataPlayerSpawn);
            if (_playerData == null)
                Debug.LogError("[PlayerManager] PlayerSpawnData 를 찾을 수 없습니다. 키: " + AssetKeys.DataPlayerSpawn);

            SpawnOrReposition(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>GlobalController.OnDestroy() 에서 호출 — 이벤트 구독 해제.</summary>
        public void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SpawnOrReposition(scene);

        void SpawnOrReposition(Scene scene)
        {
            var spawnPoint = FindSpawnPoint();
            Vector3 pos    = spawnPoint != null ? spawnPoint.position : Vector3.zero;

            if (Player == null || Player.gameObject == null)
            {
                Player = SpawnPlayer(pos);
            }
            else
            {
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
                Debug.LogError("[PlayerManager] PlayerSpawnData 또는 prefab 이 없습니다.");
                return null;
            }

            bool wasActive = _playerData.prefab.gameObject.activeSelf;
            _playerData.prefab.gameObject.SetActive(false);

            var player = Object.Instantiate(_playerData.prefab, pos, Quaternion.identity);

            _playerData.prefab.gameObject.SetActive(wasActive);

            player.Init(_playerData);
            player.gameObject.SetActive(true);
            Object.DontDestroyOnLoad(player.gameObject);

            Debug.Log($"[PlayerManager] Player 스폰 완료: {pos}");
            return player;
        }
    }
}
