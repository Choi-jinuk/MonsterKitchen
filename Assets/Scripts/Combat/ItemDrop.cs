using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    /// <summary>
    /// 씬에 놓인 드롭 아이템. 플레이어가 접촉하면 인벤토리에 추가된다.
    /// </summary>
    public class ItemDrop : MonoBehaviour
    {
        [SerializeField] SpriteRenderer m_SpriteRenderer;
        [SerializeField] float m_PickupRadius = 0.35f;

        uint m_IngredientId;
        int  m_Qty;
        bool m_PickedUp;

        public void Init(uint ingredientId, int qty)
        {
            m_IngredientId = ingredientId;
            m_Qty          = qty;

            if (m_SpriteRenderer != null)
            {
                var data   = DataRegistry.Instance?.GetIngredient(ingredientId);
                var sprite = AssetLoadManager.Instance?.Load<Sprite>(data?.SpriteAddress);
                if (sprite != null) m_SpriteRenderer.sprite = sprite;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (m_PickedUp) return;
            if (!other.CompareTag("Player")) return;

            m_PickedUp = true;
            NetworkManager.Instance?.RequestAddIngredient(m_IngredientId, m_Qty);
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_PickupRadius);
        }
    }
}
