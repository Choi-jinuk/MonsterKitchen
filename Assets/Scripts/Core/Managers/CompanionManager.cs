using System.Collections.Generic;
using MonsterKitchen.Data;
using MonsterKitchen.Player;
using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  CompanionManager — 던전 동료 스폰/디스폰 (싱글톤, 던전 한정)
    //
    //  ▶ 호출 시점: DungeonMapController.OnInit() 에서 리더 리포지션 직후 SpawnParty().
    //    선행: DataRegistry 로드, 던전 NavGrid 존재, 리더(PlayerManager.Player) 배치 완료.
    //  ▶ SpawnManager 패턴: InstantiateDisabled → InitAsCompanion → SetActive.
    // ====================================================================
    public sealed class CompanionManager : MonoBehaviour
    {
        public static CompanionManager Instance { get; private set; }

        const string CompanionBtAddress = "bt/companion/9001";
        const string PlayerPrefabKey    = "prefab/player/9001";

        readonly List<GameObject> m_Spawned = new();

        public static CompanionManager GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[CompanionManager]");
            return Instance = go.AddComponent<CompanionManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>현재 파티 동료를 리더 주변에 스폰. 리더 배치 직후 호출.</summary>
        public void SpawnParty()
        {
            DespawnAll();

            var leaderPc = PlayerManager.Instance?.Player;
            if (leaderPc == null)
            {
                DebugUtil.LogError("[CompanionManager] 리더(Player)가 없어 동료 스폰 불가.", this);
                return;
            }

            var ids = PlayerDataManager.Instance?.PartyCompanionIds;
            if (ids == null || ids.Count == 0) return;

            var prefab = AssetLoadManager.Instance?.Load<GameObject>(PlayerPrefabKey);
            if (prefab == null)
            {
                DebugUtil.LogError($"[CompanionManager] 동료 프리팹 로드 실패: {PlayerPrefabKey}", this);
                return;
            }

            Vector3 leaderPos = leaderPc.transform.position;
            for (int i = 0; i < ids.Count; i++)
            {
                var data = DataRegistry.Instance?.PlayerChars?.Get(ids[i]);
                if (data == null)
                {
                    DebugUtil.LogError($"[CompanionManager] PlayerChar({ids[i]}) 데이터 없음 — 스킵.", this);
                    continue;
                }

                Vector3 offset = OffsetFor(i, ids.Count);
                var go = InstantiateDisabled(prefab, leaderPos + offset);

                var pc = go.GetComponent<PlayerController>();
                if (pc == null)
                {
                    DebugUtil.LogError("[CompanionManager] 프리팹에 PlayerController 없음.", this);
                    Destroy(go);
                    continue;
                }

                // SetActive(true) 전에 동료/리더/BT 주입 → BTRunner.Start 가 companion BT 로 시작
                pc.InitAsCompanion(data, leaderPc.transform, CompanionBtAddress);
                go.SetActive(true);

                m_Spawned.Add(go);
            }
        }

        public void DespawnAll()
        {
            for (int i = 0; i < m_Spawned.Count; i++)
                if (m_Spawned[i] != null) Destroy(m_Spawned[i]);
            m_Spawned.Clear();
        }

        // 리더 뒤쪽 호에 분산 (겹침 스폰 방지)
        static Vector3 OffsetFor(int index, int count)
        {
            const float Spread = 1.5f;
            float t     = count <= 1 ? 0.5f : (float)index / (count - 1);
            float angle = Mathf.Lerp(200f, 340f, t) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Spread;
        }

        static GameObject InstantiateDisabled(GameObject prefab, Vector3 position)
        {
            bool wasActive = prefab.activeSelf;
            prefab.SetActive(false);
            var instance = Instantiate(prefab, position, Quaternion.identity);
            prefab.SetActive(wasActive);
            return instance;
        }
    }
}
