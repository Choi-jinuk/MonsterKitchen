using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    /// <summary>
    /// 런타임 재료 인벤토리 싱글톤.
    /// GlobalController 가 new 로 생성하고 Init() 를 호출한다.
    /// </summary>
    public class Inventory
    {
        public static Inventory Instance { get; private set; }

        readonly Dictionary<uint, int> _items = new();

        public event Action<uint, int> OnItemChanged;  // (id, newQty)

        public void Init() => Instance = this;

        public void Add(uint ingredientId, int qty = 1)
        {
            if (qty <= 0) return;
            _items.TryGetValue(ingredientId, out int cur);
            _items[ingredientId] = cur + qty;
            OnItemChanged?.Invoke(ingredientId, _items[ingredientId]);
            Debug.Log($"[Inventory] +{qty} id:{ingredientId}  (total: {_items[ingredientId]})");
        }

        public bool Remove(uint ingredientId, int qty = 1)
        {
            if (!_items.TryGetValue(ingredientId, out int cur) || cur < qty) return false;
            _items[ingredientId] = cur - qty;
            if (_items[ingredientId] <= 0) _items.Remove(ingredientId);
            OnItemChanged?.Invoke(ingredientId, _items.TryGetValue(ingredientId, out int r) ? r : 0);
            return true;
        }

        public int  GetCount(uint ingredientId) => _items.TryGetValue(ingredientId, out int v) ? v : 0;
        public bool Has(uint ingredientId, int qty = 1) => GetCount(ingredientId) >= qty;

        public IReadOnlyDictionary<uint, int> All => _items;
    }
}
