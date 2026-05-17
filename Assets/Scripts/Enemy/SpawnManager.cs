using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  SpawnManager — 던전 몬스터 초기 스폰 매니저 (씬 단위 싱글톤)
    //
    //  ▶ v2 (오픈월드 자유 탐험)
    //    방 개념 없음. DungeonSpawnTable 의 몬스터 목록을 던전 시작 시
    //    _spawnPoints 에 배분해 일괄 스폰한다.
    //
    //  ▶ 스폰 흐름
    //    1. InstantiateDisabled(prefab) → Awake/OnEnable 실행 안 됨
    //    2. monster.Init(data)          → 스탯 세팅
    //    3. SetActive(true)             → OnEnable → AI 시작
    //
    //  ▶ 리스폰
    //    MonsterRespawnManager 가 사망한 몬스터를 감지해
    //    SpawnSingle() 을 호출하는 방식으로 확장된다.
    // ====================================================================

    public class SpawnManager : MonoBehaviour
    {
        [Header("Physics")]
        [SerializeField] string monsterLayerName = "Enemy";
        [SerializeField] string playerLayerName  = "Player";

        [Header("Spawn")]
        [SerializeField] DungeonSpawnTable _spawnTable;

        [Tooltip("몬스터를 배치할 스폰 포인트 목록. 비우면 이 오브젝트 위치 사용.")]
        [SerializeField] Transform[] _spawnPoints;

        public static SpawnManager Instance { get; private set; }

        /// <summary>PlayerManager 에서 Player 를 가져온다.</summary>
        public Player.PlayerController Player =>
            PlayerManager.Instance != null ? PlayerManager.Instance.Player : null;

        // ================================================================
        //  Mono
        // ================================================================

        public void Init()
        {
            Instance = this;
            SetupLayerCollisions();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            if (Instance == null) Init();
        }

        void Start()
        {
            SpawnAll();
        }

        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>테이블 전체를 스폰 포인트에 분배해 스폰한다.</summary>
        public void SpawnAll()
        {
            if (_spawnTable == null)
            {
                Debug.LogWarning("[SpawnManager] DungeonSpawnTable 이 연결되지 않았습니다.");
                return;
            }

            Transform playerTf = Player != null ? Player.transform : FindPlayerTransform();

            int pointIndex = 0;
            foreach (var entry in _spawnTable.monsters)
            {
                if (entry.prefab == null) continue;

                MonsterData data = entry.dataOverride != null ? entry.dataOverride : entry.prefab.Data;

                for (int i = 0; i < entry.count; i++)
                {
                    Vector3 spawnPos = PickSpawnPoint(ref pointIndex, entry.spawnRadius);
                    SpawnSingle(entry.prefab, data, playerTf, spawnPos);
                }
            }
        }

        /// <summary>
        /// 단일 몬스터를 지정 위치에 스폰한다.
        /// MonsterRespawnManager 등 외부에서도 호출 가능.
        /// </summary>
        public MonsterAI SpawnSingle(MonsterAI prefab, MonsterData data, Vector3 position)
        {
            Transform playerTf = Player != null ? Player.transform : FindPlayerTransform();
            return SpawnSingle(prefab, data, playerTf, position);
        }

        // ================================================================
        //  Helpers
        // ================================================================

        MonsterAI SpawnSingle(MonsterAI prefab, MonsterData data, Transform playerTf, Vector3 position)
        {
            var monster = InstantiateDisabled(prefab, position);
            monster.Init(data, playerTf, position);
            monster.gameObject.SetActive(true);

            // 리스폰 매니저에 등록 (씬에 MonsterRespawnManager 가 있을 때만)
            MonsterRespawnManager.Instance?.Track(monster, prefab, data, position);

            return monster;
        }

        Vector3 PickSpawnPoint(ref int pointIndex, float radius)
        {
            Vector3 center;

            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                // 스폰 포인트를 순환하며 배분
                var pt = _spawnPoints[pointIndex % _spawnPoints.Length];
                pointIndex++;
                center = pt != null ? pt.position : transform.position;
            }
            else
            {
                center = transform.position;
            }

            return center + (Vector3)(Random.insideUnitCircle * radius);
        }

        void SetupLayerCollisions()
        {
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

        static T InstantiateDisabled<T>(T prefab, Vector3 position) where T : Component
        {
            bool wasActive = prefab.gameObject.activeSelf;
            prefab.gameObject.SetActive(false);
            T instance = Instantiate(prefab, position, Quaternion.identity);
            prefab.gameObject.SetActive(wasActive);
            return instance;
        }

        static Transform FindPlayerTransform()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            return go != null ? go.transform : null;
        }
    }
}
