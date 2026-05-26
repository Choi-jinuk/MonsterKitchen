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
        [SerializeField] GameObject itemDropPrefab;

        Health      _health;
        MonsterBase _monster;

        void Awake()
        {
            _health  = GetComponent<Health>();
            _monster = GetComponent<MonsterBase>();
        }

        void OnEnable()
        {
            if (_health != null)
                _health.OnDeath += HandleDeath;
        }

        void OnDisable()
        {
            if (_health != null)
                _health.OnDeath -= HandleDeath;
        }

        void HandleDeath(AttributeType killAttribute)
        {
            uint dropTableId = _monster?.Data?.dropTableId ?? 0u;
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
            if (itemDropPrefab == null)
            {
                // 프리팹 없으면 인벤토리에 직접 추가 (MVP 폴백)
                Inventory.Instance?.Add(ingredientId, qty);
                return;
            }

            Vector2 offset = RandomUtil.InCircle(0.4f);
            var go = Instantiate(itemDropPrefab,
                                 (Vector2)transform.position + offset,
                                 Quaternion.identity);

            var drop = go.GetComponent<ItemDrop>();
            if (drop != null)
                drop.Init(ingredientId, qty);
        }
    }
}
