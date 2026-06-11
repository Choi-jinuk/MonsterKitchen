using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Dungeon;
using MonsterKitchen.UI;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    /// <summary>
    /// 씬에 놓인 드롭 아이템. 플레이어가 접촉하면 인벤토리에 추가된다.
    /// </summary>
    public class ItemDrop : MonoBehaviour
    {
        [SerializeField] SpriteRenderer m_SpriteRenderer;
        [SerializeField] float          m_PickupRadius = 0.35f;

        uint              m_IngredientId;
        int               m_Qty;
        IngredientQuality m_Quality = IngredientQuality.I;
        bool              m_PickedUp;

        /// <summary>품질 없는 초기화 (MVP 폴백, 품질 I 적용).</summary>
        public void Init(uint ingredientId, int qty)
            => Init(ingredientId, qty, IngredientQuality.I);

        /// <summary>품질 포함 초기화.</summary>
        public void Init(uint ingredientId, int qty, IngredientQuality quality)
        {
            m_IngredientId = ingredientId;
            m_Qty          = qty;
            m_Quality      = quality;

            if (m_SpriteRenderer != null)
            {
                var data   = DataRegistry.Instance?.Ingredients?.Get(ingredientId);
                var sprite = AssetLoadManager.Instance?.Load<Sprite>(data?.SpriteAddress);
                if (sprite != null) m_SpriteRenderer.sprite = sprite;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (m_PickedUp) return;
            if (!other.CompareTag("Player")) return;

            // 던전 씬: DungeonBag 경유
            if (DungeonBag.Current != null)
            {
                var data       = DataRegistry.Instance?.Ingredients?.Get(m_IngredientId);
                int weightUnit = data?.Weight ?? 1;

                if (DungeonBag.Current.TryAdd(m_IngredientId, m_Qty, m_Quality, weightUnit))
                {
                    m_PickedUp = true;
                    Destroy(gameObject);
                }
                else
                {
                    GameHUD.Instance?.ShowNotification("가방이 가득 찼습니다!", 2f);
                }
                return;
            }

            // 던전 외 씬 폴백 (정상 경로에선 발생 안 함)
            m_PickedUp = true;
            NetworkManager.Instance?.RequestAddIngredient(m_IngredientId, m_Qty, m_Quality);
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_PickupRadius);
        }
    }
}
