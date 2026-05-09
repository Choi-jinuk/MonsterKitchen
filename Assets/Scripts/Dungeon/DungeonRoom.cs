using System;
using System.Collections;
using System.Collections.Generic;
using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonRoom — 방 1개의 상태 관리
    //
    //  책임:
    //    - SpawnManager 에 자신을 넘겨 몬스터 소환을 위임한다.
    //    - 소환된 몬스터의 생존 수를 추적한다.
    //    - 전멸 시 출구 개방 + OnRoomCleared 이벤트 발행.
    //
    //  스폰 위치 설정:
    //    - SpawnPoints : 방 안에 배치할 Transform 목록 (씬에서 자식으로 배치)
    //      비어 있으면 SpawnManager 가 방 중심을 기준으로 소환한다.
    //
    //  데이터 연결:
    //    - RoomId 가 DungeonSpawnTable 의 roomId 와 일치해야 몬스터가 소환된다.
    // ====================================================================

    public class DungeonRoom : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("DungeonSpawnTable.RoomSpawnConfig.roomId 와 일치시킬 것")]
        [SerializeField] string roomId;

        [Header("Scene Refs")]
        [Tooltip("몬스터가 소환될 위치 목록. 비워 두면 방 중심 사용.")]
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] GameObject  exitDoor;

        public string      RoomId      => roomId;
        public Transform[] SpawnPoints => spawnPoints;

        public event Action OnRoomCleared;

        readonly List<MonsterAI> _monsters = new();
        int  _aliveCount;
        bool _cleared;

        // ================================================================
        //  Mono
        // ================================================================

        // Start 를 코루틴으로 선언해 1프레임 대기.
        // SpawnManager.Start() 에서 플레이어를 먼저 소환하므로
        // 그 다음 프레임에 SpawnRoom 을 호출해야 _player 가 올바르게 전달된다.
        IEnumerator Start()
        {
            if (exitDoor != null)
                exitDoor.SetActive(false);

            yield return null; // SpawnManager.Start() 완료 대기

            if (SpawnManager.Instance != null)
                SpawnManager.Instance.SpawnRoom(this);
            else
                Debug.LogWarning($"[DungeonRoom '{roomId}'] SpawnManager 가 씬에 없습니다.");
        }

        // ================================================================
        //  Public API — SpawnManager 가 소환 직후 호출
        // ================================================================

        /// <summary>
        /// SpawnManager 가 몬스터를 소환한 뒤 호출해 생존 추적을 등록한다.
        /// </summary>
        public void RegisterMonster(MonsterAI monster)
        {
            if (monster == null || _monsters.Contains(monster)) return;

            _monsters.Add(monster);
            _aliveCount++;

            var hp = monster.GetComponent<Combat.Health>();
            if (hp != null)
                hp.OnDeath += _ => HandleMonsterDeath();
        }

        // ================================================================
        //  내부
        // ================================================================

        void HandleMonsterDeath()
        {
            if (_cleared) return;

            _aliveCount--;
            if (_aliveCount > 0) return;

            _cleared = true;
            if (exitDoor != null)
                exitDoor.SetActive(true);

            OnRoomCleared?.Invoke();
            Debug.Log($"[DungeonRoom '{roomId}'] Room Cleared!");
        }
    }
}
