using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    /// <summary>
    /// 몬스터 사망 시 드롭 테이블을 굴려 씬에 ItemDrop을 스폰한다.
    /// 드롭 테이블 ID는 MonsterBase.Data.dropTableId 로 조회한다.
    /// </summary>
    public class DropResolver : MonoBehaviour
    {
        [SerializeField] GameObject m_ItemDropPrefab;

        Health      m_Health;
        MonsterBase m_Monster;

        void Awake()
        {
            m_Health  = GetComponent<Health>();
            m_Monster = GetComponent<MonsterBase>();
        }

        void OnEnable()
        {
            if (m_Health != null)
                m_Health.OnDeath += HandleDeath;
        }

        void OnDisable()
        {
            if (m_Health != null)
                m_Health.OnDeath -= HandleDeath;
        }

        void HandleDeath(AttributeType killAttribute)
        {
            uint dropTableId = m_Monster?.Data?.DropTableId ?? 0u;
            if (dropTableId == 0u) return;

            var dropTable = DataRegistry.Instance?.GetDropTable(dropTableId);
            if (dropTable == null) return;

            var drops = dropTable.Roll();
            foreach (var (ingredientId, qty) in drops)
            {
                if (ingredientId == 0u || qty <= 0) continue;
                SpawnDrop(ingredientId, qty);
            }
        }

        void SpawnDrop(uint ingredientId, int qty)
        {
            if (m_ItemDropPrefab == null)
            {
                // 프리팹 없으면 인벤토리에 직접 추가 (MVP 폴백)
                NetworkManager.Instance?.RequestAddIngredient(ingredientId, qty);
                return;
            }

            Vector2 offset = RandomUtil.InCircle(0.4f);
            var go = Instantiate(m_ItemDropPrefab,
                                 (Vector2)transform.position + offset,
                                 Quaternion.identity);

            var drop = go.GetComponent<ItemDrop>();
            if (drop != null)
                drop.Init(ingredientId, qty);
        }
    }
}
