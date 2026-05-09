using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    /// <summary>
    /// 몬스터 사망 시 드롭 테이블을 굴려 씬에 ItemDrop을 스폰한다.
    /// </summary>
    public class DropResolver : MonoBehaviour
    {
        [SerializeField] DropTableData dropTable;
        [SerializeField] GameObject    itemDropPrefab;

        Health _health;

        void Awake()
        {
            _health = GetComponent<Health>();
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
            if (dropTable == null) return;

            var drops = dropTable.Roll();
            foreach (var (ingredient, qty) in drops)
            {
                if (ingredient == null || qty <= 0) continue;
                SpawnDrop(ingredient, qty);
            }
        }

        void SpawnDrop(IngredientData ingredient, int qty)
        {
            if (itemDropPrefab == null)
            {
                // 프리팹 없으면 인벤토리에 직접 추가 (MVP 폴백)
                Inventory.Instance?.Add(ingredient.id, qty);
                return;
            }

            Vector2 offset = Random.insideUnitCircle * 0.4f;
            var go = Instantiate(itemDropPrefab,
                                 (Vector2)transform.position + offset,
                                 Quaternion.identity);

            var drop = go.GetComponent<ItemDrop>();
            if (drop != null)
                drop.Init(ingredient, qty);
        }
    }
}
