using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Enemy;
using MonsterKitchen.Player;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    /// <summary>
    /// 몬스터 사망 시 드롭 테이블을 굴려 씬에 ItemDrop 을 스폰한다.
    /// 처치 조건(속성·CC·무기 등급)으로 재료 품질을 결정한다.
    /// </summary>
    public class DropResolver : MonoBehaviour
    {
        [SerializeField] GameObject m_ItemDropPrefab;

        Health                m_Health;
        MonsterBase           m_Monster;
        CrowdControlComponent m_CC;

        void Awake()
        {
            m_Health  = GetComponent<Health>();
            m_Monster = GetComponent<MonsterBase>();
            m_CC      = GetComponent<CrowdControlComponent>();
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

            var dropTable = DataRegistry.Instance?.DropTables?.Get(dropTableId);
            if (dropTable == null) return;

            // ── 품질 계산 ────────────────────────────────────────────
            bool hadSoftCC = m_CC != null &&
                             (m_CC.CurrentCC == CCType.Sleep || m_CC.CurrentCC == CCType.Hypnosis);

            int weaponTier = PlayerManager.Instance?.Player
                                 ?.GetComponent<PlayerStats>()
                                 ?.EquippedWeapon?.Tier ?? 1;

            var quality = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)(m_Monster.Data?.Rarity ?? RarityType.Common),
                monsterAttribute: m_Monster.Data?.Attribute ?? AttributeType.None,
                killAttribute:    killAttribute,
                hadSoftCC:        hadSoftCC,
                weaponTier:       weaponTier);
            // ─────────────────────────────────────────────────────────

            var drops = dropTable.Roll();
            foreach (var (ingredientId, qty) in drops)
            {
                if (ingredientId == 0u || qty <= 0) continue;
                SpawnDrop(ingredientId, qty, quality);
            }
        }

        void SpawnDrop(uint ingredientId, int qty, IngredientQuality quality)
        {
            if (m_ItemDropPrefab == null)
            {
                NetworkManager.Instance?.RequestAddIngredient(ingredientId, qty, quality);
                return;
            }

            Vector2 offset = RandomUtil.InCircle(0.4f);
            var go = Instantiate(m_ItemDropPrefab,
                                 (Vector2)transform.position + offset,
                                 Quaternion.identity);

            var drop = go.GetComponent<ItemDrop>();
            drop?.Init(ingredientId, qty, quality);
        }
    }
}
