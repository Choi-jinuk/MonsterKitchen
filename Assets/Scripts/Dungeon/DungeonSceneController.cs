using System;
using System.Collections;
using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Enemy;
using MonsterKitchen.Navigation;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonSceneController — DungeonScene 총괄 컨트롤러
    //
    //  ▶ 스폰 흐름 (프레임 분산)
    //    IEnumerator Start() → 1프레임 대기 → StartCoroutine(SpawnChain)
    //    SpawnChain → SpawnRoomAsync (프레임당 SPAWN_PER_FRAME 마리씩 분산)
    //
    //  ▶ 방 전환 흐름
    //    방 클리어 → DungeonDoor.Open() → 플레이어 통과
    //    → FadeOut → 방 교체 → NavGrid.Bake() → 스폰(분산) + FadeIn 병렬
    // ====================================================================

    [DisallowMultipleComponent]
    public class DungeonSceneController : MonoBehaviour
    {
        // 프레임당 최대 스폰 수. 낮출수록 분산이 넓어진다.
        const int SPAWN_PER_FRAME = 3;

        [Serializable]
        public class RoomSetup
        {
            [Tooltip("방 클리어 조건을 관리하는 DungeonRoom 컴포넌트")]
            public DungeonRoom room;

            [Tooltip("이 방의 루트 GameObject. 방 2 이상은 시작 시 비활성으로 배치한다.")]
            public GameObject roomRoot;

            [Tooltip("이 방을 클리어한 뒤 다음 방으로 넘어가는 문. 마지막 방은 비워도 된다.")]
            public DungeonDoor exitDoor;

            [Tooltip("이 방에 스폰할 테이블 ID (TableData.DungeonSpawnTables 키)")]
            public uint spawnTableId;

            [Tooltip("스폰 포인트 목록. 비우면 (0,0,0) 기준으로 스폰.")]
            public Transform[] spawnPoints;
        }

        [Header("Rooms — Inspector 에서 직접 연결")]
        [SerializeField] RoomSetup[] m_Rooms;

        [Header("Exit — Inspector 에서 직접 연결 (씬에서 비활성 배치)")]
        [SerializeField] DungeonExit m_Exit;

        [Header("Camera Confiner — Inspector 에서 직접 연결")]
        [SerializeField] DungeonCameraConfiner m_CameraConfiner;

        [Header("Player — 씬 직접 배치 시 연결 / 씬 전환 진입 시 빈 칸")]
        [SerializeField] Transform m_PlayerRef;

        Transform m_CachedPlayer;
        int m_CurrentRoomIndex;

        // ================================================================
        //  Mono
        // ================================================================

        void Awake()
        {
            var respawn = new MonsterRespawnManager(this);
            respawn.Init();
            SetupLayerCollisions();
            EnsureSceneFader();
        }

        IEnumerator Start()
        {
            if (!ValidateRefs()) yield break;

            m_Exit.gameObject.SetActive(false);

            for (int i = 0; i < m_Rooms.Length; i++)
            {
                if (m_Rooms[i].roomRoot != null)
                    m_Rooms[i].roomRoot.SetActive(i == 0);
            }

            // 씬 활성화 직후 스파이크 방지 — 1프레임 대기 후 스폰 시작
            yield return null;

            StartCoroutine(SpawnChain(0));
        }

        // ================================================================
        //  SceneFader 보장
        // ================================================================

        static void EnsureSceneFader()
        {
            if (SceneFader.Instance != null) return;
            var go = new GameObject("SceneFader");
            go.AddComponent<SceneFader>();
        }

        // ================================================================
        //  Inspector 검증
        // ================================================================

        bool ValidateRefs()
        {
            bool ok = true;

            if (m_Rooms == null || m_Rooms.Length == 0)
            {
                Debug.LogError("[DungeonSceneController] Rooms 배열이 비어있습니다.", this);
                ok = false;
            }
            else
            {
                for (int i = 0; i < m_Rooms.Length; i++)
                {
                    var r = m_Rooms[i];
                    if (r.room == null)
                    {
                        Debug.LogError(
                            $"[DungeonSceneController] Rooms[{i}].Room 이 연결되지 않았습니다.", this);
                        ok = false;
                    }
                    if (r.spawnTableId == 0)
                        Debug.LogWarning(
                            $"[DungeonSceneController] Rooms[{i}].SpawnTableId 가 0 입니다.", this);
                }
            }

            if (m_Exit == null)
            {
                Debug.LogError("[DungeonSceneController] Exit 가 연결되지 않았습니다.", this);
                ok = false;
            }

            if (m_CameraConfiner == null)
                Debug.LogWarning(
                    "[DungeonSceneController] CameraConfiner 가 연결되지 않았습니다. " +
                    "카메라 경계가 자동 갱신되지 않습니다.", this);

            return ok;
        }

        // ================================================================
        //  스폰 체인 (코루틴)
        // ================================================================

        IEnumerator SpawnChain(int index)
        {
            if (m_Rooms == null || index >= m_Rooms.Length) yield break;

            var setup = m_Rooms[index];
            if (setup.room == null) yield break;

            m_CurrentRoomIndex = index;

            // OnRoomCleared 이벤트를 스폰 전에 먼저 등록
            bool isLast = index + 1 >= m_Rooms.Length;
            if (isLast)
                setup.room.OnRoomCleared += ActivateExit;
            else
                setup.room.OnRoomCleared += () => OpenDoorForRoom(index);

            // 몬스터 스폰 (프레임당 SPAWN_PER_FRAME 마리씩 분산)
            var spawned = new List<MonsterBase>();
            yield return StartCoroutine(SpawnRoomAsync(setup, spawned));

            setup.room.RegisterMonsters(spawned);
        }

        // ================================================================
        //  방 전환 코루틴
        // ================================================================

        void OpenDoorForRoom(int index)
        {
            var door = m_Rooms[index].exitDoor;
            if (door == null)
            {
                Debug.LogWarning(
                    $"[DungeonSceneController] Rooms[{index}].ExitDoor 가 연결되지 않았습니다.", this);
                return;
            }

            door.Open();
            door.OnPlayerPassedThrough += () => StartCoroutine(RoomTransition(index + 1));
        }

        IEnumerator RoomTransition(int nextIndex)
        {
            // 1. 페이드 아웃
            if (SceneFader.Instance != null)
                yield return StartCoroutine(SceneFader.Instance.FadeOut(0.35f));

            // 2. 이전 방 비활성 / 다음 방 활성
            int prevIndex = nextIndex - 1;
            if (prevIndex >= 0 && prevIndex < m_Rooms.Length)
                m_Rooms[prevIndex].roomRoot?.SetActive(false);

            if (nextIndex < m_Rooms.Length)
                m_Rooms[nextIndex].roomRoot?.SetActive(true);

            // 3. Physics2D 콜라이더 활성화 반영
            yield return null;

            // 4. NavGrid 재베이크
            NavGrid.Instance?.Bake();

            // 5. 카메라 경계 갱신
            m_CameraConfiner?.Refresh();

            // 6. 스폰 시작 (페이드 인과 병렬 — 페이드 중에 몬스터 생성 완료)
            StartCoroutine(SpawnChain(nextIndex));

            yield return new WaitForSeconds(0.15f);

            // 7. 페이드 인
            if (SceneFader.Instance != null)
                yield return StartCoroutine(SceneFader.Instance.FadeIn(0.35f));

            Debug.Log($"[DungeonSceneController] 방 {nextIndex} 전환 완료");
        }

        // ================================================================
        //  스폰 (프레임 분산)
        // ================================================================

        IEnumerator SpawnRoomAsync(RoomSetup setup, List<MonsterBase> result)
        {
            if (setup.spawnTableId == 0) yield break;

            var spawnTable = DataRegistry.Instance?.GetDungeonSpawnTable(setup.spawnTableId);
            if (spawnTable == null)
            {
                Debug.LogWarning(
                    $"[DungeonSceneController] spawnTableId={setup.spawnTableId} 를 찾을 수 없습니다.", this);
                yield break;
            }

            Transform player = GetPlayer();
            int pointIndex    = 0;
            int spawnedThisFrame = 0;

            foreach (var entry in spawnTable.Monsters)
            {
                var data = DataRegistry.Instance?.GetMonster(entry.MonsterId);
                if (data == null || string.IsNullOrEmpty(data.PrefabAddress))
                {
                    Debug.LogWarning(
                        $"[DungeonSceneController] monsterId={entry.MonsterId} 의 MonsterData 또는 prefabAddress 가 없습니다.", this);
                    continue;
                }

                var prefab = AssetLoadManager.Instance?.Load<MonsterBase>(data.PrefabAddress);
                if (prefab == null)
                {
                    Debug.LogWarning(
                        $"[DungeonSceneController] prefabAddress='{data.PrefabAddress}' 로드 실패.", this);
                    continue;
                }

                for (int i = 0; i < entry.Count; i++)
                {
                    Vector3 pos     = PickPoint(setup.spawnPoints, ref pointIndex, entry.SpawnRadius);
                    var     monster = SpawnSingle(prefab, data, player, pos);
                    if (monster != null) result.Add(monster);

                    spawnedThisFrame++;
                    if (spawnedThisFrame >= SPAWN_PER_FRAME)
                    {
                        spawnedThisFrame = 0;
                        yield return null; // 다음 프레임으로 분산
                    }
                }
            }
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
            m_Exit.gameObject.SetActive(true);
            Debug.Log("[DungeonSceneController] 던전 출구 활성화!");
        }

        // ================================================================
        //  플레이어 참조
        // ================================================================

        Transform GetPlayer()
        {
            if (m_PlayerRef != null) return m_PlayerRef;
            if (m_CachedPlayer != null) return m_CachedPlayer;

            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                m_CachedPlayer = go.transform;
            else
                Debug.LogWarning(
                    "[DungeonSceneController] 'Player' 태그 오브젝트를 찾을 수 없습니다.", this);

            return m_CachedPlayer;
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
            if (m_Rooms == null || m_Rooms.Length == 0)
            {
                Debug.LogWarning("[DungeonSceneController] Rooms 가 비어있습니다.");
                return;
            }
            for (int i = 0; i < m_Rooms.Length; i++)
            {
                if (m_Rooms[i].room == null)
                    Debug.LogWarning($"[DungeonSceneController] Rooms[{i}].Room 이 연결되지 않았습니다.");
                if (m_Rooms[i].spawnTableId == 0)
                    Debug.LogWarning($"[DungeonSceneController] Rooms[{i}].SpawnTableId 가 0 입니다.");
                if (i < m_Rooms.Length - 1 && m_Rooms[i].exitDoor == null)
                    Debug.LogWarning($"[DungeonSceneController] Rooms[{i}].ExitDoor 가 연결되지 않았습니다.");
            }
            if (m_Exit == null)
                Debug.LogWarning("[DungeonSceneController] Exit 가 연결되지 않았습니다.");
        }
#endif
    }
}
