using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  PlayerInventoryData — 재료·음식 인벤토리 상태 관리
    //
    //  ▶ 역할
    //    런타임 재료(ingredients)·음식(foods) 수량 + 음식 등급 큐 보관 + 이벤트 발행.
    //    PlayerDataManager 가 생성하고 Init() 을 호출한다.
    //
    //  ▶ 접근 방법 (PlayerDataManager 경유만 허용)
    //    PlayerDataManager.Instance.Inventory.AllIngredients
    //    PlayerDataManager.Instance.Inventory.OnIngredientChanged
    //
    //  ▶ 변경 방법 (NetworkManager 경유만 허용)
    //    NetworkManager.Instance.RequestAddIngredient(id, qty)
    //    NetworkManager.Instance.RequestCook(recipe, grade, callback)
    //    NetworkManager.Instance.RequestServeFood(id, callback)
    //    → 콜백 → PlayerDataManager.Apply*() → 이 클래스의 내부 메서드 호출
    // ====================================================================

    public class PlayerInventoryData
    {
        readonly Dictionary<uint, int>              m_Ingredients = new();
        readonly Dictionary<uint, int>              m_Foods       = new();
        readonly Dictionary<uint, Queue<FoodGrade>> m_FoodGrades  = new();

        /// <summary>재료 수량 변경 시 발행. (ingredientId, newQty)</summary>
        public event Action<uint, int> OnIngredientChanged;

        /// <summary>음식 수량 변경 시 발행. (foodId, newQty)</summary>
        public event Action<uint, int> OnFoodChanged;

        // ── 생명주기 ─────────────────────────────────────────────────

        public void Init() { }

        // ── 읽기 전용 공개 API ────────────────────────────────────────

        public IReadOnlyDictionary<uint, int> AllIngredients => m_Ingredients;
        public IReadOnlyDictionary<uint, int> AllFoods       => m_Foods;

        public int  GetIngredientCount(uint id) =>
            m_Ingredients.TryGetValue(id, out int v) ? v : 0;

        public bool HasIngredient(uint id, int qty = 1) =>
            GetIngredientCount(id) >= qty;

        public int  GetFoodCount(uint id) =>
            m_Foods.TryGetValue(id, out int v) ? v : 0;

        // ── Apply 메서드 — PlayerDataManager 만 호출 ─────────────────

        /// <summary>재료 수량을 절댓값으로 설정한다. qty ≤ 0 이면 항목 제거.</summary>
        public void SetIngredientQty(uint id, int qty)
        {
            if (qty <= 0)
            {
                m_Ingredients.Remove(id);
                OnIngredientChanged?.Invoke(id, 0);
            }
            else
            {
                m_Ingredients[id] = qty;
                OnIngredientChanged?.Invoke(id, qty);
            }
            Debug.Log($"[Inventory] id:{id} qty:{(qty <= 0 ? 0 : qty)}");
        }

        /// <summary>음식 1개와 등급을 추가한다.</summary>
        public void AddFoodEntry(uint id, FoodGrade grade)
        {
            m_Foods.TryGetValue(id, out int cur);
            m_Foods[id] = cur + 1;

            if (!m_FoodGrades.TryGetValue(id, out var q))
            {
                q = new Queue<FoodGrade>();
                m_FoodGrades[id] = q;
            }
            q.Enqueue(grade);

            OnFoodChanged?.Invoke(id, m_Foods[id]);
            Debug.Log($"[FoodInventory] +1 id:{id} [{grade}]  (total: {m_Foods[id]})");
        }

        /// <summary>음식 1개를 소모하고 (등급, 잔여 수량) 을 반환한다.</summary>
        public (FoodGrade grade, int remaining) ConsumeFood(uint id)
        {
            if (!m_Foods.TryGetValue(id, out int cur) || cur <= 0)
                return (FoodGrade.Normal, 0);

            FoodGrade grade = FoodGrade.Normal;
            if (m_FoodGrades.TryGetValue(id, out var q) && q.Count > 0)
                grade = q.Dequeue();

            m_Foods[id] = cur - 1;
            if (m_Foods[id] <= 0)
            {
                m_Foods.Remove(id);
                m_FoodGrades.Remove(id);
            }

            int remaining = m_Foods.TryGetValue(id, out int r) ? r : 0;
            OnFoodChanged?.Invoke(id, remaining);
            return (grade, remaining);
        }

        // ── 세이브/로드 — ServerDBManager 전용 ──────────────────────

        /// <summary>저장 데이터 복원 시 ServerDBManager 가 호출. 기존 데이터를 덮어쓴다.</summary>
        public void LoadIngredients(IEnumerable<(uint id, int qty)> entries)
        {
            m_Ingredients.Clear();
            foreach (var (id, qty) in entries)
                if (qty > 0) m_Ingredients[id] = qty;
        }

        /// <summary>저장 데이터 복원 시 ServerDBManager 가 호출. FoodGrade 는 Normal 로 복원.</summary>
        public void LoadFoods(IEnumerable<(uint id, int qty)> entries)
        {
            m_Foods.Clear();
            m_FoodGrades.Clear();
            foreach (var (id, qty) in entries)
            {
                if (qty <= 0) continue;
                m_Foods[id] = qty;
                var q = new Queue<FoodGrade>();
                for (int i = 0; i < qty; i++) q.Enqueue(FoodGrade.Normal);
                m_FoodGrades[id] = q;
            }
        }
    }
}
