using System;
using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonSceneController — DungeonScene 총괄 컨트롤러
    //
    //  ▶ 모든 참조는 Inspector 에서 직접 연결한다.
    //    자동 탐색(Find/FindObjectsByType) 을 사용하지 않는다.
    //    참조가 없으면 LogError 를 출력하고 해당 기능을 건너뛴다.
    //
    //  ▶ 클리어 흐름
    //    마지막 방 DungeonRoom.OnRoomCleared → DungeonExit 활성화
    //
    //  ▶ 설정 방법 (Inspector)
    //    1. Rooms 배열에 각 방의 DungeonRoom, SpawnTable, SpawnPoints 연결.
    //    2. Exit 에 DungeonExit 오브젝트 연결 (씬에서 비활성 상태로 배치).
    //    3. Player Ref 는 씬에 플레이어가 직접 배치된 경우 연결.
    //       씬 전환으로 진입할 경우 빈 칸 — 태그 조회로 한 번만 캐시한다.
    // ====================================================================

    [DisallowMultipleComponent]
    public class DungeonSceneController : MonoBehaviour
    {
        [Serializable]
        public class RoomSetup
        {
            [Tooltip("방 클리어 조건을 관리하는 DungeonRoom 컴포넌트")]
            public DungeonRoom room;
            [Tooltip("이 방에 스폰할 테이블 ID (TableData.DungeonSpawnTables 키)")]
            public uint spawnTableId;
            [Tooltip("스폰 포인트 목록. 비우면 (0,0,0) 기준으로 스폰.")]
            public Transform[] spawnPoints;
        }

        [Header("Rooms — Inspector 에서 직접 연결")]
        [SerializeField] RoomSetup[] _rooms;

        [Header("Exit — Inspector 에서 직접 연결 (씬에서 비활성 배치)")]
        [SerializeField] DungeonExit _exit;

        [Header("Player — 씬 직접 배치 시 연결 / 씬 전환 진입 시 빈 칸")]
        [Tooltip("비워두면 런타임에 'Player' 태그로 한 번만 조회 후 캐시한다.")]
        [SerializeField] Transform _playerRef;

        // 런타임 플레이어 캐시 (태그 조회 결과)
        Transform _cachedPlayer;

        // ================================================================
        //  Mono
        // ================================================================

        void Awake()
        {
            // MonsterRespawnManager Instance 초기화 (stale 방지)
            // 방 몬스터는 Track 하지 않으므로 리스폰은 발생하지 않는다.
            var respawn = new MonsterRespawnManager(this);
            respawn.Init();

            SetupLayerCollisions();
        }

        void Start()
        {
            if (!ValidateRefs()) return;

            // 출구는 마지막 방 클리어 전까지 비활성
            _exit.gameObject.SetActive(false);

            // 첫 번째 방 스폰. 이후 방은 이전 방 클리어 시 순차 스폰.
            SpawnChain(0);
        }

        // ================================================================
        //  Inspector 검증
        // ================================================================

        bool ValidateRefs()
        {
            bool ok = true;

            if (_rooms == null || _rooms.Length == 0)
            {
                Debug.LogError(
                    "[DungeonSceneController] Rooms 배열이 비어있습니다. " +
                    "Inspector 에서 방(DungeonRoom)을 연결하세요.", this);
                ok = false;
            }
            else
            {
                for (int i = 0; i < _rooms.Length; i++)
                {
                    var r = _rooms[i];
                    if (r.room == null)
                    {
                        Debug.LogError(
                            $"[DungeonSceneController] Rooms[{i}].Room 이 연결되지 않았습니다.", this);
                        ok = false;
                    }
                    if (r.spawnTableId == 0)
                    {
                        Debug.LogWarning(
                            $"[DungeonSceneController] Rooms[{i}].SpawnTableId 가 0 입니다. " +
                            $"방 '{r.room?.name}' 에 몬스터가 스폰되지 않습니다.", this);
                    }
                }
            }

            if (_exit == null)
            {
                Debug.LogError(
                    "[DungeonSceneController] Exit 가 연결되지 않았습니다. " +
                    "Inspector 에서 DungeonExit 오브젝트를 연결하세요.", this);
                ok = false;
            }

            return ok;
        }

        // ================================================================
        //  스폰 체인
        // ================================================================

        void SpawnChain(int index)
        {
            if (_rooms == null || index >= _rooms.Length) return;

            var setup = _rooms[index];
            if (setup.room == null) return;

            var spawned = SpawnRoom(setup);
            setup.room.RegisterMonsters(spawned);

            bool isLast = index + 1 >= _rooms.Length;
            if (isLast)
                setup.room.OnRoomCleared += ActivateExit;
            else
                setup.room.OnRoomCleared += () => SpawnChain(index + 1);
        }

        // ================================================================
        //  스폰
        // ================================================================

        List<MonsterBase> SpawnRoom(RoomSetup setup)
        {
            var result = new List<MonsterBase>();
            if (setup.spawnTableId == 0) return result;

            var spawnTable = DataRegistry.Instance?.GetDungeonSpawnTable(setup.spawnTableId);
            if (spawnTable == null)
            {
                Debug.LogWarning(
                    $"[DungeonSceneController] spawnTableId={setup.spawnTableId} 을 TableData 에서 찾을 수 없습니다.", this);
                return result;
            }

            Transform player = GetPlayer();
            int pointIndex = 0;

            foreach (var entry in spawnTable.monsters)
            {
                var data = DataRegistry.Instance?.GetMonster(entry.monsterId);
                if (data == null || string.IsNullOrEmpty(data.prefabAddress))
                {
                    Debug.LogWarning(
                        $"[DungeonSceneController] monsterId={entry.monsterId} 의 MonsterData 또는 prefabAddress 가 없습니다. 건너뜁니다.", this);
                    continue;
                }

                var prefab = AssetLoadManager.Instance?.Load<MonsterBase>(data.prefabAddress);
                if (prefab == null)
                {
                    Debug.LogWarning(
                        $"[DungeonSceneController] prefabAddress='{data.prefabAddress}' 프리팹 로드 실패. 건너뜁니다.", this);
                    continue;
                }

                for (int i = 0; i < entry.count; i++)
                {
                    Vector3 pos     = PickPoint(setup.spawnPoints, ref pointIndex, entry.spawnRadius);
                    var     monster = SpawnSingle(prefab, data, player, pos);
                    if (monster != null) result.Add(monster);
                }
            }
            return result;
        }

        static MonsterBase SpawnSingle(MonsterBase prefab, MonsterData data,
                                       Transform playerTf, Vector3 pos)
        {
            bool wasActive = prefab.gameObject.activeSelf;
            prefab.gameObject.SetActive(false);
            var inst = Instantiate(prefab, pos, Quaternion.identity);
            prefab.gameObject.SetActive(wasActive);
            inst.Init(data, playerTf, pos);
            inst.gameObject.SetActive(true);
            return inst;
        }

        static Vector3 PickPoint(Transform[] points, ref int index, float radius)
        {
            Vector3 center = Vector3.zero;
            if (points != null && points.Length > 0)
            {
                var pt = points[index % points.Length];
                index++;
                center = pt != null ? pt.position : Vector3.zero;
            }
            return center + RandomUtil.InCircle3D(radius);
        }

        void ActivateExit()
        {
            _exit.gameObject.SetActive(true);
            Debug.Log("[DungeonSceneController] 던전 출구 활성화!");
        }

        // ================================================================
        //  플레이어 참조
        //  Inspector 연결 우선. 없으면 태그 조회를 한 번만 수행 후 캐시.
        // ================================================================

        Transform GetPlayer()
        {
            if (_playerRef != null) return _playerRef;
            if (_cachedPlayer != null) return _cachedPlayer;

            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
            {
                _cachedPlayer = go.transform;
            }
            else
            {
                Debug.LogWarning(
                    "[DungeonSceneController] 'Player' 태그 오브젝트를 찾을 수 없습니다. " +
                    "씬에 Player 를 배치하거나 Inspector 의 Player Ref 에 연결하세요.", this);
            }
            return _cachedPlayer;
        }

        // ================================================================
        //  레이어 충돌 설정
        // ================================================================

        static void SetupLayerCollisions()
        {
            int enemy  = LayerMask.NameToLayer("Enemy");
            int player = LayerMask.NameToLayer("Player");
            if (enemy < 0)
            {
                Debug.LogWarning("[DungeonSceneController] 'Enemy' 레이어가 없습니다.");
                return;
            }
            Physics2D.IgnoreLayerCollision(enemy, enemy, true);
            if (player >= 0)
                Physics2D.IgnoreLayerCollision(player, enemy, true);
        }

        // ================================================================
        //  에디터 검증
        // ================================================================

#if UNITY_EDITOR
        void OnValidate()
        {
            if (_rooms == null || _rooms.Length == 0)
            {
                Debug.LogWarning(
                    "[DungeonSceneController] Rooms 가 비어있습니다. Inspector 에서 연결하세요.");
                return;
            }
            for (int i = 0; i < _rooms.Length; i++)
            {
                if (_rooms[i].room == null)
                    Debug.LogWarning(
                        $"[DungeonSceneController] Rooms[{i}].Room 이 연결되지 않았습니다.");
                if (_rooms[i].spawnTableId == 0)
                    Debug.LogWarning(
                        $"[DungeonSceneController] Rooms[{i}].SpawnTableId 가 0 입니다.");
            }
            if (_exit == null)
                Debug.LogWarning(
                    "[DungeonSceneController] Exit 가 연결되지 않았습니다. Inspector 에서 연결하세요.");
        }
#endif
    }
}
