using System.Collections;
using MonsterKitchen.Combat;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Enemy;
using MonsterKitchen.Navigation;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonSpawnZone — 단일 스폰 구역. 자동 리스폰 관리.
    //
    //  ▶ Init(player) 호출 → SpawnAsync (프레임 분산) → 몬스터 Health.OnDeath 구독
    //  ▶ 몬스터 사망 → RespawnAfterDelay → 동일 위치·테이블에서 재스폰
    //  ▶ HasAliveMonsters / ZoneCenter : 미니맵 마커 색상 결정에 사용
    // ====================================================================

    [DisallowMultipleComponent]
    public class DungeonSpawnZone : MonoBehaviour
    {
        const int SpawnPerFrame = 3;

        [Header("Spawn 설정")]
        [SerializeField] uint        m_SpawnTableId;
        [SerializeField] Transform[] m_SpawnPoints;
        [SerializeField] float       m_RespawnDelay = 8f;

        Transform m_Player;
        int       m_AliveCount;

        public bool    HasAliveMonsters => m_AliveCount > 0;
        public Vector3 ZoneCenter       => transform.position;

        /// <summary>스폰 또는 사망으로 m_AliveCount 변경 시 발생. 미니맵 마커 갱신에 사용.</summary>
        public event System.Action OnMonsterCountChanged;

        // ── 공개 API ─────────────────────────────────────────────────

        public void Init(Transform player)
        {
            m_Player = player;
            StartCoroutine(SpawnAsync());
        }

        // ── 내부: 초기 스폰 (프레임 분산) ────────────────────────────

        IEnumerator SpawnAsync()
        {
            yield return null;   // 1프레임 대기 — 씬 활성화 직후 스파이크 방지

            if (m_SpawnTableId == 0) yield break;

            var table = DataRegistry.Instance?.DungeonSpawnTables?.Get(m_SpawnTableId);
            if (table == null)
            {
                DebugUtil.LogWarning(
                    $"[DungeonSpawnZone] spawnTableId={m_SpawnTableId} 를 찾을 수 없습니다.", this);
                yield break;
            }

            int pointIndex       = 0;
            int spawnedThisFrame = 0;

            foreach (var entry in table.Monsters)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    Vector3 pos = PickPoint(ref pointIndex, entry.SpawnRadius);
                    SpawnMonster(entry.MonsterId, pos);

                    spawnedThisFrame++;
                    if (spawnedThisFrame >= SpawnPerFrame)
                    {
                        spawnedThisFrame = 0;
                        yield return null;
                    }
                }
            }
        }

        // ── 단일 몬스터 스폰 ─────────────────────────────────────────

        void SpawnMonster(uint monsterId, Vector3 pos)
        {
            var data = DataRegistry.Instance?.Monsters?.Get(monsterId);
            if (data == null || string.IsNullOrEmpty(data.PrefabAddress))
            {
                DebugUtil.LogWarning(
                    $"[DungeonSpawnZone] monsterId={monsterId} 데이터 또는 prefabAddress 없음.", this);
                return;
            }

            var prefab = AssetLoadManager.Instance?.Load<MonsterBase>(data.PrefabAddress);
            if (prefab == null)
            {
                DebugUtil.LogWarning(
                    $"[DungeonSpawnZone] prefabAddress='{data.PrefabAddress}' 로드 실패.", this);
                return;
            }

            // InstantiateDisabled → Init → SetActive (SpawnManager 패턴 준수)
            bool wasActive = prefab.gameObject.activeSelf;
            prefab.gameObject.SetActive(false);
            var inst = Instantiate(prefab, pos, Quaternion.identity);
            prefab.gameObject.SetActive(wasActive);

            inst.Init(data, m_Player, pos);
            inst.gameObject.SetActive(true);

            // 사망 이벤트 구독
            var health = inst.GetComponent<Health>();
            if (health != null)
            {
                uint    capturedId  = monsterId;
                Vector3 capturedPos = pos;
                health.OnDeath += _ => OnMonsterDied(capturedId, capturedPos);
            }

            m_AliveCount++;
            OnMonsterCountChanged?.Invoke();
        }

        // ── 사망 처리 ─────────────────────────────────────────────────

        void OnMonsterDied(uint monsterId, Vector3 pos)
        {
            m_AliveCount = Mathf.Max(0, m_AliveCount - 1);
            OnMonsterCountChanged?.Invoke();
            StartCoroutine(RespawnAfterDelay(monsterId, pos));
        }

        IEnumerator RespawnAfterDelay(uint monsterId, Vector3 pos)
        {
            yield return new WaitForSeconds(m_RespawnDelay);
            if (this == null || !gameObject.activeSelf) yield break;   // 씬 전환 방어
            SpawnMonster(monsterId, pos);
        }

        // ── 스폰 포인트 선택 ─────────────────────────────────────────

        Vector3 PickPoint(ref int index, float radius)
        {
            Vector3 center = Vector3.zero;
            if (m_SpawnPoints != null && m_SpawnPoints.Length > 0)
            {
                var pt = m_SpawnPoints[index % m_SpawnPoints.Length];
                index++;
                center = pt != null ? pt.position : Vector3.zero;
            }

            if (NavGrid.Instance != null)
            {
                Vector2 candidate = (Vector2)center + RandomUtil.InCircle(radius);
                Vector2 valid     = NavAgent.IsValid(candidate)
                    ? candidate
                    : NavAgent.GetNearestValid(candidate);
                return new Vector3(valid.x, valid.y, center.z);
            }

            return center + RandomUtil.InCircle3D(radius);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_SpawnTableId == 0)
                DebugUtil.LogWarning("[DungeonSpawnZone] SpawnTableId 가 0 입니다.", this);
        }
#endif
    }
}
