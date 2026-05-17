using MonsterKitchen.Data;
using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonSceneController — DungeonScene 전용 씬 매니저 총괄
    //
    //  ▶ 설계 원칙
    //    SpawnManager / MonsterRespawnManager 는 순수 C# 클래스.
    //    이 컨트롤러가 유일한 MonoBehaviour 로서 생명주기와 코루틴을 위임한다.
    //    Inspector 슬롯은 에셋/씬 참조(_spawnTable, _spawnPoints) 만 사용.
    //
    //  ▶ 초기화 순서
    //    MonsterRespawnManager.Init() → SpawnManager.Init() → SpawnAll()
    //    (Respawn 이 Spawn 을 참조하므로 Respawn 이 먼저 Init)
    // ====================================================================

    [DisallowMultipleComponent]
    public class DungeonSceneController : MonoBehaviour
    {
        [Header("Spawn Config")]
        [SerializeField] DungeonSpawnTable _spawnTable;
        [Tooltip("몬스터 배치 포인트 목록. 비우면 (0,0,0) 기준으로 스폰.")]
        [SerializeField] Transform[]       _spawnPoints;

        SpawnManager          _spawnManager;
        MonsterRespawnManager _respawnManager;

        // ================================================================
        //  Mono
        // ================================================================

        void Awake()
        {
            _respawnManager = new MonsterRespawnManager(this);
            _spawnManager   = new SpawnManager(_spawnTable, _spawnPoints);

            _respawnManager.Init();
            _spawnManager.Init();
        }

        void Start() => _spawnManager.SpawnAll();

#if UNITY_EDITOR
        void OnValidate()
        {
            if (_spawnTable == null) Debug.LogWarning("[DungeonSceneController] DungeonSpawnTable 미연결");
        }
#endif
    }
}
