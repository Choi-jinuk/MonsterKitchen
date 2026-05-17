using System.Collections;
using System.Collections.Generic;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  MonsterRespawnManager — 사망 몬스터 리스폰 매니저 (씬 단위 싱글톤)
    //
    //  DungeonSceneController 가 new MonsterRespawnManager(this) 로 생성하고
    //  Init() 를 호출한다. 코루틴은 runner(DungeonSceneController) 로 실행.
    //
    //  ▶ 흐름
    //    1. SpawnManager.SpawnSingle() → Track(instance, prefab, data, pos)
    //    2. MonsterAI.EnterDie()       → NotifyDeath(instance)
    //    3. _respawnDelay 초 대기 후 SpawnManager.SpawnSingle() 로 재소환
    // ====================================================================

    public class MonsterRespawnManager
    {
        public static MonsterRespawnManager Instance { get; private set; }

        readonly MonoBehaviour _runner;
        const float RespawnDelay = 30f;

        struct RespawnEntry
        {
            public MonsterAI   prefab;
            public MonsterData data;
            public Vector3     spawnPos;
        }

        readonly Dictionary<MonsterAI, RespawnEntry> _tracked = new();

        public MonsterRespawnManager(MonoBehaviour runner) => _runner = runner;

        public void Init() => Instance = this;

        // ── Public API ─────────────────────────────────────────────────

        public void Track(MonsterAI instance, MonsterAI prefab, MonsterData data, Vector3 spawnPos)
        {
            _tracked[instance] = new RespawnEntry
            {
                prefab   = prefab,
                data     = data,
                spawnPos = spawnPos
            };
        }

        public void NotifyDeath(MonsterAI instance)
        {
            if (!_tracked.TryGetValue(instance, out var entry)) return;
            _tracked.Remove(instance);
            _runner.StartCoroutine(RespawnCoroutine(entry));
        }

        // ── Helpers ────────────────────────────────────────────────────

        IEnumerator RespawnCoroutine(RespawnEntry entry)
        {
            yield return new WaitForSeconds(RespawnDelay);

            if (SpawnManager.Instance == null) yield break;
            if (entry.prefab == null)          yield break;

            var newMonster = SpawnManager.Instance.SpawnSingle(entry.prefab, entry.data, entry.spawnPos);
            Debug.Log($"[MonsterRespawnManager] '{entry.data?.displayName ?? "?"}' 리스폰 완료 @ {entry.spawnPos}");

            if (newMonster != null)
                Track(newMonster, entry.prefab, entry.data, entry.spawnPos);
        }
    }
}
