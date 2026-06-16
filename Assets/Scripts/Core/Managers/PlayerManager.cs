using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Player;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 플레이어 단일 인스턴스 관리자.
    /// GlobalController 가 new 로 생성하고 Init() 를 호출한다.
    ///
    /// ▶ 라이프사이클
    ///   Init()             : 싱글톤 등록
    ///   Start()            : ManagementSceneController.OnInit() 에서 첫 1회 호출
    ///                        — 플레이어 데이터 로드 + 스폰 + 위치 설정
    ///   RepositionInScene(): 씬 전환 후 각 SceneController.OnInit() 에서 명시적 호출
    ///                        — 현재 씬의 PlayerSpawnPoint 로 이동
    ///   Dispose()          : GlobalController.OnDestroy() 에서 호출
    ///
    /// ▶ sceneLoaded 콜백 없이 각 SceneController 가 명시적으로 제어한다.
    /// </summary>
    public class PlayerManager
    {
        public static PlayerManager Instance { get; private set; }

        /// <summary>기본 플레이어 캐릭터 ID. 향후 캐릭터 선택 시스템으로 교체 가능.</summary>
        public static uint DefaultPlayerId = 9001;

        PlayerCharData m_PlayerData;

        /// <summary>현재 살아있는 Player 인스턴스.</summary>
        public PlayerController Player { get; private set; }

        /// <summary>Start() 가 1회 이상 호출됐는지 여부.</summary>
        public bool IsStarted { get; private set; }

        public void Init() => Instance = this;

        /// <summary>
        /// ManagementSceneController.OnInit() 에서 첫 진입 시 1회 호출.
        /// 플레이어 데이터 로드 + 스폰 + 현재 씬 스폰 포인트 위치 설정.
        /// spawnPoint: SceneController 가 Inspector 로 연결한 스폰 위치. null 이면 이름 검색 폴백.
        /// </summary>
        public void Start(Transform spawnPoint = null)
        {
            if (IsStarted) return;
            IsStarted = true;

            m_PlayerData = DataRegistry.Instance?.PlayerChars?.Get(DefaultPlayerId);
            if (m_PlayerData == null)
                DebugUtil.LogError($"[PlayerManager] Players 테이블에서 id={DefaultPlayerId} 를 찾을 수 없습니다. " +
                                   "Players.csv 를 확인하고 Sync All SO 를 실행하세요.");

            SpawnOrReposition(spawnPoint);
        }

        /// <summary>
        /// 씬 전환 후 각 SceneController.OnInit() 에서 명시적으로 호출.
        /// 현재 씬의 스폰 포인트로 플레이어를 이동시킨다.
        /// </summary>
        public void RepositionInScene(Transform spawnPoint = null)
        {
            if (!IsStarted || Player == null) return;
            SpawnOrReposition(spawnPoint);
        }

        /// <summary>GlobalController.OnDestroy() 에서 호출.</summary>
        public void Dispose() { }

        // ================================================================
        //  내부
        // ================================================================

        void SpawnOrReposition(Transform spawnPoint)
        {
            if (m_PlayerData == null)
            {
                DebugUtil.LogError("[PlayerManager] PlayerCharData 없음 — 스폰 불가. DataRegistry에 등록됐는지 확인.");
                return;
            }

            if (spawnPoint == null) spawnPoint = FindSpawnPointFallback();
            Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;

            if (Player == null || Player.gameObject == null)
            {
                Player = SpawnPlayer(pos);
            }
            else
            {
                Player.transform.position = pos;
                DebugUtil.Log($"[PlayerManager] 플레이어 위치 이동 → {pos}");
            }
        }

        /// <summary>SceneController 가 스폰 포인트를 연결하지 않은 경우의 이름 검색 폴백.</summary>
        static Transform FindSpawnPointFallback()
        {
            var go = GameObject.Find("PlayerSpawnPoint");
            if (go == null)
                DebugUtil.LogWarning("[PlayerManager] PlayerSpawnPoint 미발견 — (0,0,0) 스폰. " +
                                     "SceneController 의 m_PlayerSpawnPoint 연결 권장.");
            return go != null ? go.transform : null;
        }

        PlayerController SpawnPlayer(Vector3 pos)
        {
            if (m_PlayerData == null || string.IsNullOrEmpty(m_PlayerData.PrefabAddress))
            {
                DebugUtil.LogError("[PlayerManager] PlayerCharData 가 없거나 prefabAddress 가 비어 있습니다.");
                return null;
            }

            var prefab = AssetLoadManager.Instance?.Load<PlayerController>(m_PlayerData.PrefabAddress);
            if (prefab == null)
            {
                DebugUtil.LogError($"[PlayerManager] 플레이어 프리팹 로드 실패 — 키: {m_PlayerData.PrefabAddress}. " +
                                   "AssetManifest 에 등록되어 있는지 확인하세요.");
                return null;
            }

            // 비활성 부모 밑에 Instantiate — Awake/OnEnable 억제. 프리팹 에셋의 activeSelf 는 건드리지 않는다.
            var holder = new GameObject("PlayerSpawnHolder");
            holder.SetActive(false);

            var player = Object.Instantiate(prefab, pos, Quaternion.identity, holder.transform);
            player.Init(m_PlayerData);

            // 부모 분리 → 하이어라키 활성화 → Awake/OnEnable 실행
            player.transform.SetParent(null, worldPositionStays: true);
            Object.DontDestroyOnLoad(player.gameObject);
            Object.Destroy(holder);

            DebugUtil.Log($"[PlayerManager] Player '{m_PlayerData.DisplayName}' 스폰 완료: {pos}");
            return player;
        }
    }
}
