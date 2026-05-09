using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Dungeon;
using UnityEngine;



namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  SpawnManager — 몬스터 소환 전담 매니저 (씬 단위, 싱글톤)
    //
    //  ▶ Player 소환은 PlayerManager(DontDestroyOnLoad)가 담당한다.
    //    이 클래스는 몬스터 스폰에만 집중한다.
    //
    //  ▶ 소환 흐름:
    //    1. InstantiateDisabled(prefab)  → Awake/OnEnable 실행 안 됨
    //    2. monster.Init(data)           → 스탯 세팅
    //    3. SetActive(true)              → OnEnable → AI 시작
    // ====================================================================

    public class SpawnManager : MonoBehaviour
    {
        [Header("Physics")]
        [SerializeField] string monsterLayerName = "Enemy";
        [SerializeField] string playerLayerName  = "Player";

        DungeonSpawnTable _spawnTable;

        public static SpawnManager Instance { get; private set; }

        /// <summary>PlayerManager에서 Player를 가져온다.</summary>
        public Player.PlayerController Player =>
            PlayerManager.Instance != null ? PlayerManager.Instance.Player : null;

        // ----------------------------------------------------------------

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _spawnTable = AssetLoadManager.Instance?.Load<DungeonSpawnTable>(AssetKeys.DataDungeonSpawn);
            if (_spawnTable == null)
                Debug.LogWarning("[SpawnManager] DungeonSpawnTable을 AssetManifest에서 찾을 수 없습니다. 키: " + AssetKeys.DataDungeonSpawn);

            int enemyLayer  = LayerMask.NameToLayer(monsterLayerName);
            int playerLayer = LayerMask.NameToLayer(playerLayerName);

            if (enemyLayer >= 0)
            {
                Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);

                if (playerLayer >= 0)
                    Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);
                else
                    Debug.LogWarning($"[SpawnManager] 레이어 '{playerLayerName}' 를 찾을 수 없습니다.");
            }
            else
            {
                Debug.LogWarning($"[SpawnManager] 레이어 '{monsterLayerName}' 를 찾을 수 없습니다.");
            }
        }

        // ----------------------------------------------------------------
        //  Public API
        // ----------------------------------------------------------------

        /// <summary>방 하나를 스폰 테이블에 따라 채운다. DungeonRoom.Start()에서 호출.</summary>
        public void SpawnRoom(DungeonRoom room)
        {
            if (_spawnTable == null)
            {
                Debug.LogWarning("[SpawnManager] DungeonSpawnTable 이 로드되지 않았습니다.");
                return;
            }

            var config = _spawnTable.GetRoom(room.RoomId);
            if (config == null)
            {
                Debug.LogWarning($"[SpawnManager] roomId '{room.RoomId}' 를 테이블에서 찾을 수 없습니다.");
                return;
            }

            Transform playerTf = Player != null ? Player.transform : FindPlayerTransform();

            foreach (var entry in config.monsters)
            {
                if (entry.prefab == null) continue;

                MonsterData data = entry.dataOverride != null ? entry.dataOverride : entry.prefab.Data;

                for (int i = 0; i < entry.count; i++)
                {
                    Vector3 spawnPos = PickSpawnPoint(room, entry.spawnRadius);

                    var monster = InstantiateDisabled(entry.prefab, spawnPos);
                    monster.Init(data, playerTf, spawnPos);
                    monster.gameObject.SetActive(true);
                    room.RegisterMonster(monster);
                }
            }
        }

        // ----------------------------------------------------------------
        //  Helpers
        // ----------------------------------------------------------------

        static T InstantiateDisabled<T>(T prefab, Vector3 position) where T : Component
        {
            bool wasActive = prefab.gameObject.activeSelf;
            prefab.gameObject.SetActive(false);
            T instance = Instantiate(prefab, position, Quaternion.identity);
            prefab.gameObject.SetActive(wasActive);
            return instance;
        }

        static Vector3 PickSpawnPoint(DungeonRoom room, float radius)
        {
            Transform[] points = room.SpawnPoints;

            if (points != null && points.Length > 0)
            {
                var valid = System.Array.FindAll(points, p => p != null);
                points = valid.Length > 0 ? valid : null;
            }

            Vector3 center = points is { Length: > 0 }
                ? points[Random.Range(0, points.Length)].position
                : room.transform.position;

            return center + (Vector3)(Random.insideUnitCircle * radius);
        }

        static Transform FindPlayerTransform()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            return go != null ? go.transform : null;
        }
    }
}
