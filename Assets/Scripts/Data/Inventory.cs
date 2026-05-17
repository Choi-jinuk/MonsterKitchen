using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    /// <summary>
    /// 런타임 재료 인벤토리 싱글톤.
    /// 씬 전환 시에도 유지된다 (DontDestroyOnLoad).
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public static Inventory Instance { get; private set; }

        // ingredientId → 수량
        readonly Dictionary<string, int> _items = new();

        public event Action<string, int> OnItemChanged;  // (id, newQty)

        public void Init()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            if (Instance == null) Init();
        }

        public void Add(string ingredientId, int qty = 1)
        {
            if (string.IsNullOrEmpty(ingredientId) || qty <= 0) return;
            _items.TryGetValue(ingredientId, out int cur);
            _items[ingredientId] = cur + qty;
            OnItemChanged?.Invoke(ingredientId, _items[ingredientId]);
            Debug.Log($"[Inventory] +{qty} {ingredientId}  (total: {_items[ingredientId]})");
        }

        public bool Remove(string ingredientId, int qty = 1)
        {
            if (!_items.TryGetValue(ingredientId, out int cur) || cur < qty) return false;
            _items[ingredientId] = cur - qty;
            if (_items[ingredientId] <= 0) _items.Remove(ingredientId);
            OnItemChanged?.Invoke(ingredientId, _items.TryGetValue(ingredientId, out int r) ? r : 0);
            return true;
        }

        public int GetCount(string ingredientId)
            => _items.TryGetValue(ingredientId, out int v) ? v : 0;

        public bool Has(string ingredientId, int qty = 1)
            => GetCount(ingredientId) >= qty;

        public IReadOnlyDictionary<string, int> All => _items;
    }
}
