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
    //    1. SpawnManager.SpawnSingle() → Track(instance, data, pos)
    //    2. MonsterBase.OnDied() → NotifyDeath(instance)
    //    3. RespawnDelay 초 후 SpawnManager.SpawnSingle(data, pos) 로 재소환
    //
    //  ▶ 프리팹은 MonsterData.prefabAddress 키로 AssetLoadManager 에서 로드하므로 별도 보관 불필요.
    // ====================================================================

    public class MonsterRespawnManager
    {
        public static MonsterRespawnManager Instance { get; private set; }

        readonly MonoBehaviour _runner;
        const float RespawnDelay = 30f;

        struct RespawnEntry
        {
            public MonsterData data;
            public Vector3     spawnPos;
        }

        readonly Dictionary<MonsterBase, RespawnEntry> _tracked = new();

        public MonsterRespawnManager(MonoBehaviour runner) => _runner = runner;

        public void Init() => Instance = this;

        // ── Public API ─────────────────────────────────────────────────

        public void Track(MonsterBase instance, MonsterData data, Vector3 spawnPos)
        {
            _tracked[instance] = new RespawnEntry { data = data, spawnPos = spawnPos };
        }

        public void NotifyDeath(MonsterBase instance)
        {
            if (!_tracked.TryGetValue(instance, out var entry)) return;
            _tracked.Remove(instance);
            _runner.StartCoroutine(RespawnCoroutine(entry));
        }

        // ── Helpers ────────────────────────────────────────────────────

        IEnumerator RespawnCoroutine(RespawnEntry entry)
        {
            yield return new WaitForSeconds(RespawnDelay);

            if (SpawnManager.Instance == null)                        yield break;
            if (string.IsNullOrEmpty(entry.data?.prefabAddress))      yield break;

            var newMonster = SpawnManager.Instance.SpawnSingle(entry.data, entry.spawnPos);
            Debug.Log($"[MonsterRespawnManager] '{entry.data.displayName}' 리스폰 완료 @ {entry.spawnPos}");

            if (newMonster != null)
                Track(newMonster, entry.data, entry.spawnPos);
        }
    }
}
