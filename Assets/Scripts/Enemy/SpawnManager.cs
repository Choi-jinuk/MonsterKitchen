using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  SpawnManager — 던전 몬스터 초기 스폰 매니저 (씬 단위 싱글톤)
    //
    //  DungeonSceneController 가 new SpawnManager(spawnTable, spawnPoints) 로
    //  생성하고 Init() → SpawnAll() 순으로 호출한다.
    // ====================================================================

    public class SpawnManager
    {
        public static SpawnManager Instance { get; private set; }

        const string MonsterLayerName = "Enemy";
        const string PlayerLayerName  = "Player";

        readonly DungeonSpawnTable _spawnTable;
        readonly Transform[]       _spawnPoints;

        public SpawnManager(DungeonSpawnTable spawnTable, Transform[] spawnPoints)
        {
            _spawnTable  = spawnTable;
            _spawnPoints = spawnPoints;
        }

        /// <summary>PlayerManager 에서 Player 를 가져온다.</summary>
        public Player.PlayerController Player =>
            PlayerManager.Instance != null ? PlayerManager.Instance.Player : null;

        public void Init()
        {
            Instance = this;
            SetupLayerCollisions();
        }

        // ── Public API ────────────────────────────────────────────────

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

        /// <summary>단일 몬스터를 지정 위치에 스폰한다. MonsterRespawnManager 에서도 호출.</summary>
        public MonsterAI SpawnSingle(MonsterAI prefab, MonsterData data, Vector3 position)
        {
            Transform playerTf = Player != null ? Player.transform : FindPlayerTransform();
            return SpawnSingle(prefab, data, playerTf, position);
        }

        // ── Helpers ───────────────────────────────────────────────────

        MonsterAI SpawnSingle(MonsterAI prefab, MonsterData data, Transform playerTf, Vector3 position)
        {
            var monster = InstantiateDisabled(prefab, position);
            monster.Init(data, playerTf, position);
            monster.gameObject.SetActive(true);

            MonsterRespawnManager.Instance?.Track(monster, prefab, data, position);

            return monster;
        }

        Vector3 PickSpawnPoint(ref int pointIndex, float radius)
        {
            Vector3 center;

            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                var pt = _spawnPoints[pointIndex % _spawnPoints.Length];
                pointIndex++;
                center = pt != null ? pt.position : Vector3.zero;
            }
            else
            {
                center = Vector3.zero;
            }

            return center + (Vector3)(Random.insideUnitCircle * radius);
        }

        void SetupLayerCollisions()
        {
            int enemyLayer  = LayerMask.NameToLayer(MonsterLayerName);
            int playerLayer = LayerMask.NameToLayer(PlayerLayerName);

            if (enemyLayer >= 0)
            {
                Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);

                if (playerLayer >= 0)
                    Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);
                else
                    Debug.LogWarning($"[SpawnManager] 레이어 '{PlayerLayerName}' 를 찾을 수 없습니다.");
            }
            else
            {
                Debug.LogWarning($"[SpawnManager] 레이어 '{MonsterLayerName}' 를 찾을 수 없습니다.");
            }
        }

        static T InstantiateDisabled<T>(T prefab, Vector3 position) where T : Component
        {
            bool wasActive = prefab.gameObject.activeSelf;
            prefab.gameObject.SetActive(false);
            T instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
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
