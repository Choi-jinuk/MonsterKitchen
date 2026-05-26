using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    /// <summary>
    /// 완성된 요리 인벤토리 싱글톤.
    /// foodId → 수량 관리 + 등급(FoodGrade) 큐 관리.
    /// GlobalController 가 new 로 생성하고 Init() 를 호출한다.
    /// </summary>
    public class FoodInventory
    {
        public static FoodInventory Instance { get; private set; }

        readonly Dictionary<uint, int>              _foods  = new();
        readonly Dictionary<uint, Queue<FoodGrade>> _grades = new();

        public event Action<uint, int> OnFoodChanged;  // (foodId, newQty)

        public void Init() => Instance = this;

        // ── 추가 ─────────────────────────────────────────────────────

        /// <summary>등급 없이 추가 (Normal 등급으로 처리).</summary>
        public void Add(uint foodId, int qty = 1)
        {
            for (int i = 0; i < qty; i++)
                AddWithGrade(foodId, FoodGrade.Normal);
        }

        /// <summary>등급을 지정해 1개 추가.</summary>
        public void AddWithGrade(uint foodId, FoodGrade grade)
        {
            _foods.TryGetValue(foodId, out int cur);
            _foods[foodId] = cur + 1;

            if (!_grades.TryGetValue(foodId, out var q))
            {
                q = new Queue<FoodGrade>();
                _grades[foodId] = q;
            }
            q.Enqueue(grade);

            OnFoodChanged?.Invoke(foodId, _foods[foodId]);
            Debug.Log($"[FoodInventory] +1 id:{foodId} [{grade}]  (total: {_foods[foodId]})");
        }

        // ── 소비 ─────────────────────────────────────────────────────

        public FoodGrade TakeGrade(uint foodId)
        {
            if (_grades.TryGetValue(foodId, out var q) && q.Count > 0)
                return q.Dequeue();
            return FoodGrade.Normal;
        }

        public bool Remove(uint foodId, int qty = 1)
        {
            if (!_foods.TryGetValue(foodId, out int cur) || cur < qty) return false;
            _foods[foodId] = cur - qty;
            if (_foods[foodId] <= 0)
            {
                _foods.Remove(foodId);
                _grades.Remove(foodId);
            }
            OnFoodChanged?.Invoke(foodId, _foods.TryGetValue(foodId, out int r) ? r : 0);
            return true;
        }

        // ── 조회 ─────────────────────────────────────────────────────

        public int GetCount(uint foodId)
            => _foods.TryGetValue(foodId, out int v) ? v : 0;

        public IReadOnlyDictionary<uint, int> All => _foods;
    }
}
