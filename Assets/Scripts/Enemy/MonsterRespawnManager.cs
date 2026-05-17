using System.Collections;
using System.Collections.Generic;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  MonsterRespawnManager — 사망 몬스터 리스폰 매니저 (씬 단위 싱글톤)
    //
    //  ▶ 흐름
    //    1. SpawnManager.SpawnSingle() 이 몬스터를 소환할 때
    //       Track(instance, prefab, data, spawnPos) 를 호출해 등록한다.
    //    2. MonsterAI.EnterDie() 가 NotifyDeath(instance) 를 호출한다.
    //    3. _respawnDelay 초 대기 후 SpawnManager.SpawnSingle() 로 재소환한다.
    //
    //  ▶ 설정
    //    Inspector 의 _respawnDelay 로 전역 리스폰 딜레이를 조정한다.
    // ====================================================================

    public class MonsterRespawnManager : MonoBehaviour
    {
        [Header("Respawn")]
        [Tooltip("사망 후 재소환까지 걸리는 시간(초).")]
        [SerializeField] float _respawnDelay = 30f;

        public static MonsterRespawnManager Instance { get; private set; }

        // ────────────────────────────────────────────────────────────────
        //  내부 데이터
        // ────────────────────────────────────────────────────────────────

        struct RespawnEntry
        {
            public MonsterAI  prefab;
            public MonsterData data;
            public Vector3    spawnPos;
        }

        readonly Dictionary<MonsterAI, RespawnEntry> _tracked = new();

        // ================================================================
        //  Mono
        // ================================================================

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>
        /// SpawnManager 가 몬스터를 소환할 때 호출해 추적 목록에 등록한다.
        /// </summary>
        public void Track(MonsterAI instance, MonsterAI prefab, MonsterData data, Vector3 spawnPos)
        {
            _tracked[instance] = new RespawnEntry
            {
                prefab   = prefab,
                data     = data,
                spawnPos = spawnPos
            };
        }

        /// <summary>
        /// MonsterAI.EnterDie() 에서 호출한다.
        /// 추적 목록에 있으면 리스폰 코루틴을 시작한다.
        /// </summary>
        public void NotifyDeath(MonsterAI instance)
        {
            if (!_tracked.TryGetValue(instance, out var entry)) return;
            _tracked.Remove(instance);
            StartCoroutine(RespawnCoroutine(entry));
        }

        // ================================================================
        //  Helpers
        // ================================================================

        IEnumerator RespawnCoroutine(RespawnEntry entry)
        {
            yield return new WaitForSeconds(_respawnDelay);

            if (SpawnManager.Instance == null) yield break;
            if (entry.prefab == null)          yield break;

            var newMonster = SpawnManager.Instance.SpawnSingle(entry.prefab, entry.data, entry.spawnPos);
            Debug.Log($"[MonsterRespawnManager] '{entry.data?.displayName ?? "?"}' 리스폰 완료 @ {entry.spawnPos}");

            // 새 인스턴스를 다시 추적
            if (newMonster != null)
                Track(newMonster, entry.prefab, entry.data, entry.spawnPos);
        }
    }
}
