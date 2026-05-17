using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    /// <summary>
    /// 완성된 요리 인벤토리 싱글톤.
    /// foodId → 수량 관리 + 등급(FoodGrade) 큐 관리.
    ///
    /// 등급 흐름:
    ///   CookingUI → Add(id, grade)  → grade 큐 적재
    ///   ServingSystem → TakeGrade(id) → grade 큐 소비 → EatAndPay(grade) 가격 반영
    /// </summary>
    public class FoodInventory : MonoBehaviour
    {
        public static FoodInventory Instance { get; private set; }

        readonly Dictionary<string, int>              _foods  = new();
        readonly Dictionary<string, Queue<FoodGrade>> _grades = new();

        public event Action<string, int> OnFoodChanged;  // (foodId, newQty)

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

        // ── 추가 ─────────────────────────────────────────────────────

        /// <summary>등급 없이 추가 (Normal 등급으로 처리).</summary>
        public void Add(string foodId, int qty = 1)
        {
            for (int i = 0; i < qty; i++)
                AddWithGrade(foodId, FoodGrade.Normal);
        }

        /// <summary>등급을 지정해 1개 추가. CookingUI → CookingStation 경유 호출.</summary>
        public void AddWithGrade(string foodId, FoodGrade grade)
        {
            if (string.IsNullOrEmpty(foodId)) return;

            _foods.TryGetValue(foodId, out int cur);
            _foods[foodId] = cur + 1;

            if (!_grades.TryGetValue(foodId, out var q))
            {
                q = new Queue<FoodGrade>();
                _grades[foodId] = q;
            }
            q.Enqueue(grade);

            OnFoodChanged?.Invoke(foodId, _foods[foodId]);
            Debug.Log($"[FoodInventory] +1 {foodId} [{grade}]  (total: {_foods[foodId]})");
        }

        // ── 소비 ─────────────────────────────────────────────────────

        /// <summary>
        /// 등급 큐에서 하나 꺼낸다. 큐가 비어 있으면 Normal 반환.
        /// ServingSystem이 서빙 직전에 호출해 EatAndPay 가격 산정에 사용.
        /// </summary>
        public FoodGrade TakeGrade(string foodId)
        {
            if (_grades.TryGetValue(foodId, out var q) && q.Count > 0)
                return q.Dequeue();
            return FoodGrade.Normal;
        }

        public bool Remove(string foodId, int qty = 1)
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

        public int GetCount(string foodId)
            => _foods.TryGetValue(foodId, out int v) ? v : 0;

        public IReadOnlyDictionary<string, int> All => _foods;
    }
}
