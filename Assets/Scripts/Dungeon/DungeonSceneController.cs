using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonSceneController — DungeonScene 전용 씬 매니저 총괄
    //
    //  ▶ 역할
    //    · Inspector SerializeField 목록으로 던전 씬 매니저 전체를 한눈에 파악.
    //    · Awake() 에서 Init() 를 순서대로 호출해 초기화 순서를 보장.
    //
    //  ▶ 초기화 순서 근거
    //    SpawnManager   : MonsterRespawnManager 보다 먼저 (Respawn 이 Spawn 을 참조)
    //    MonsterRespawnManager : SpawnManager.Instance 를 코루틴에서 사용
    //
    //  ▶ 폴백
    //    DungeonSceneController 가 없을 때도 각 매니저 Awake() 폴백이 동작한다.
    // ====================================================================

    [DisallowMultipleComponent]
    public class DungeonSceneController : MonoBehaviour
    {
        [Header("── 던전 씬 매니저  (위 → 아래 순서로 초기화됩니다)")]
        [SerializeField] SpawnManager          _spawnManager;
        [SerializeField] MonsterRespawnManager _monsterRespawnManager;

        // ================================================================
        //  Mono
        // ================================================================

        void Awake()
        {
            _spawnManager?.Init();
            _monsterRespawnManager?.Init();
        }

        // ================================================================
        //  편의 — Inspector 에서 미연결 슬롯 즉시 감지
        // ================================================================

#if UNITY_EDITOR
        void OnValidate()
        {
            if (_spawnManager          == null) Debug.LogWarning("[DungeonSceneController] SpawnManager 미연결");
            if (_monsterRespawnManager == null) Debug.LogWarning("[DungeonSceneController] MonsterRespawnManager 미연결");
        }
#endif
    }
}
