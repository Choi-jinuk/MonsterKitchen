using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    /// <summary>
    /// 씬에 놓인 드롭 아이템. 플레이어가 접촉하면 인벤토리에 추가된다.
    /// </summary>
    public class ItemDrop : MonoBehaviour
    {
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] float pickupRadius = 0.35f;

        IngredientData _ingredient;
        int            _qty;
        bool           _pickedUp;

        public void Init(IngredientData ingredient, int qty)
        {
            _ingredient = ingredient;
            _qty        = qty;

            if (spriteRenderer != null && ingredient.sprite != null)
                spriteRenderer.sprite = ingredient.sprite;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_pickedUp) return;
            if (!other.CompareTag("Player")) return;

            _pickedUp = true;
            Inventory.Instance?.Add(_ingredient.id, _qty);
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}
