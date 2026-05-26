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
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] float pickupRadius = 0.35f;

        uint _ingredientId;
        int  _qty;
        bool _pickedUp;

        public void Init(uint ingredientId, int qty)
        {
            _ingredientId = ingredientId;
            _qty          = qty;

            if (spriteRenderer != null)
            {
                var data   = DataRegistry.Instance?.GetIngredient(ingredientId);
                var sprite = AssetLoadManager.Instance?.Load<Sprite>(data?.spriteAddress);
                if (sprite != null) spriteRenderer.sprite = sprite;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_pickedUp) return;
            if (!other.CompareTag("Player")) return;

            _pickedUp = true;
            Inventory.Instance?.Add(_ingredientId, _qty);
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}
