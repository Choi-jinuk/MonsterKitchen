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
    ///
    /// ▶ 플레이어 데이터 로드 방식
    ///   TableData.Players 에서 DefaultPlayerId 로 PlayerCharData 를 조회한다.
    ///   플레이어 프리팹은 PlayerCharData.prefabAddress → AssetManifest 키로 로드한다.
    /// </summary>
    public class PlayerManager
    {
        public static PlayerManager Instance { get; private set; }

        /// <summary>기본 플레이어 캐릭터 ID. 향후 캐릭터 선택 시스템으로 교체 가능.</summary>
        public static uint DefaultPlayerId = 9001;

        PlayerCharData m_PlayerData;

        /// <summary>현재 살아있는 Player 인스턴스.</summary>
        public PlayerController Player { get; private set; }

        public void Init() => Instance = this;

        /// <summary>GlobalController.Start() 에서 호출 — AssetLoadManager 초기화 이후 실행.</summary>
        public void Start()
        {
            // TableData.Players 에서 PlayerCharData 조회
            m_PlayerData = DataRegistry.Instance?.GetPlayer(DefaultPlayerId);
            if (m_PlayerData == null)
                Debug.LogError($"[PlayerManager] Players 테이블에서 id={DefaultPlayerId} 를 찾을 수 없습니다. " +
                               "Players.csv 를 확인하고 Sync All SO 를 실행하세요.");

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
            if (m_PlayerData == null || string.IsNullOrEmpty(m_PlayerData.PrefabAddress))
            {
                Debug.LogError("[PlayerManager] PlayerCharData 가 없거나 prefabAddress 가 비어 있습니다.");
                return null;
            }

            // 프리팹을 AssetManifest 에서 주소 키로 로드
            var prefab = AssetLoadManager.Instance?.Load<PlayerController>(m_PlayerData.PrefabAddress);
            if (prefab == null)
            {
                Debug.LogError($"[PlayerManager] 플레이어 프리팹 로드 실패 — 키: {m_PlayerData.PrefabAddress}. " +
                               "AssetManifest 에 등록되어 있는지 확인하세요.");
                return null;
            }

            bool wasActive = prefab.gameObject.activeSelf;
            prefab.gameObject.SetActive(false);

            var player = Object.Instantiate(prefab, pos, Quaternion.identity);

            prefab.gameObject.SetActive(wasActive);

            player.Init(m_PlayerData);
            player.gameObject.SetActive(true);
            Object.DontDestroyOnLoad(player.gameObject);

            Debug.Log($"[PlayerManager] Player '{m_PlayerData.DisplayName}' 스폰 완료: {pos}");
            return player;
        }
    }
}
