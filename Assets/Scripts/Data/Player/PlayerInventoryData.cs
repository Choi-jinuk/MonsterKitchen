using MonsterKitchen.Core;
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
        readonly Dictionary<uint, int>                                    m_Ingredients        = new();
        readonly Dictionary<uint, int>                                    m_Foods              = new();
        readonly Dictionary<uint, Queue<FoodGrade>>                       m_FoodGrades         = new();
        readonly Dictionary<uint, Dictionary<IngredientQuality, int>>     m_IngredientQualities = new();

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
            DebugUtil.Log($"[Inventory] id:{id} qty:{(qty <= 0 ? 0 : qty)}");
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
            DebugUtil.Log($"[FoodInventory] +1 id:{id} [{grade}]  (total: {m_Foods[id]})");
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

        // ── 품질 스토리지 API ─────────────────────────────────────────

        /// <summary>품질 포함 재료 추가. 총 수량(m_Ingredients)과 품질 카운트 모두 갱신한다.</summary>
        public void AddIngredientWithQuality(uint id, int qty, IngredientQuality quality)
        {
            if (qty <= 0) return;

            // 총 수량 갱신
            m_Ingredients.TryGetValue(id, out int cur);
            int newTotal = cur + qty;
            m_Ingredients[id] = newTotal;
            OnIngredientChanged?.Invoke(id, newTotal);

            // 품질 카운트 갱신
            if (!m_IngredientQualities.TryGetValue(id, out var qDict))
            {
                qDict = new Dictionary<IngredientQuality, int>();
                m_IngredientQualities[id] = qDict;
            }
            qDict.TryGetValue(quality, out int qCur);
            qDict[quality] = qCur + qty;

            DebugUtil.Log($"[Inventory] +{qty} id:{id} [{quality}] (total: {newTotal})");
        }

        /// <summary>품질별 재료 수량. 품질 정보 없으면 0.</summary>
        public int GetIngredientCount(uint id, IngredientQuality quality)
        {
            if (!m_IngredientQualities.TryGetValue(id, out var qDict)) return 0;
            qDict.TryGetValue(quality, out int c);
            return c;
        }

        /// <summary>해당 재료의 최고 품질. 품질 정보 없으면 I.</summary>
        public IngredientQuality GetBestQuality(uint id)
        {
            if (!m_IngredientQualities.TryGetValue(id, out var qDict)) return IngredientQuality.I;
            if (qDict.TryGetValue(IngredientQuality.III, out int c3) && c3 > 0) return IngredientQuality.III;
            if (qDict.TryGetValue(IngredientQuality.II,  out int c2) && c2 > 0) return IngredientQuality.II;
            return IngredientQuality.I;
        }

        /// <summary>요리 시 재료를 품질 높은 것부터 qty 개 소모한다.</summary>
        public void ConsumeIngredientQuality(uint id, int qty)
        {
            if (!m_IngredientQualities.TryGetValue(id, out var qDict)) return;
            foreach (var q in new[] { IngredientQuality.III, IngredientQuality.II, IngredientQuality.I })
            {
                if (qty <= 0) break;
                if (!qDict.TryGetValue(q, out int c)) continue;
                int take = System.Math.Min(c, qty);
                qDict[q] = c - take;
                qty -= take;
                if (qDict[q] == 0) qDict.Remove(q);
            }
            if (qDict.Count == 0) m_IngredientQualities.Remove(id);
        }

        /// <summary>레시피 재료 품질 평균 점수 → FoodGrade 도출. (읽기 전용)</summary>
        public FoodGrade CalculateCookingGrade(RecipeData recipe)
        {
            if (recipe == null) return FoodGrade.Normal;

            var seenIds = new HashSet<uint>();
            float total = 0f;
            int   count = 0;

            foreach (var req in recipe.Ingredients)
            {
                if (req.IngredientId == 0u) continue;
                if (!seenIds.Add(req.IngredientId)) continue;

                total += (int)GetBestQuality(req.IngredientId);
                count++;
            }

            if (count == 0) return FoodGrade.Normal;

            float avg = total / count;
            return avg >= 2.5f ? FoodGrade.Perfect
                 : avg >= 1.5f ? FoodGrade.Good
                 :               FoodGrade.Normal;
        }

        // ── 세이브/로드 — ServerDBManager 전용 ──────────────────────

        /// <summary>저장 데이터 복원 시 ServerDBManager 가 호출. 기존 데이터를 덮어쓴다.</summary>
        public void LoadIngredients(IEnumerable<(uint id, int qty)> entries)
        {
            m_Ingredients.Clear();
            m_IngredientQualities.Clear();
            foreach (var (id, qty) in entries)
                if (qty > 0) m_Ingredients[id] = qty;
        }

        /// <summary>저장 데이터 복원 시 품질 카운트를 복원한다. LoadIngredients 이후 호출.</summary>
        public void LoadIngredientQualities(IEnumerable<(uint id, IngredientQuality quality, int count)> entries)
        {
            m_IngredientQualities.Clear();
            foreach (var (id, quality, count) in entries)
            {
                if (count <= 0) continue;
                if (!m_IngredientQualities.TryGetValue(id, out var qDict))
                {
                    qDict = new Dictionary<IngredientQuality, int>();
                    m_IngredientQualities[id] = qDict;
                }
                qDict[quality] = count;
            }
        }

        /// <summary>저장용 품질 카운트 열거. ServerDBManager 전용.</summary>
        public IEnumerable<(uint id, IngredientQuality quality, int count)> AllIngredientQualities()
        {
            foreach (var kv in m_IngredientQualities)
                foreach (var q in kv.Value)
                    yield return (kv.Key, q.Key, q.Value);
        }

        /// <summary>
        /// 저장 데이터 복원 시 ServerDBManager 가 호출.
        /// 등급 큐는 일단 Normal 로 채우고, 이후 LoadFoodGrades() 가 실 등급으로 교체한다.
        /// </summary>
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

        /// <summary>저장 데이터 복원 시 등급 큐를 실 등급으로 교체. LoadFoods 이후 호출.</summary>
        public void LoadFoodGrades(IEnumerable<(uint id, FoodGrade grade, int count)> entries)
        {
            m_FoodGrades.Clear();
            foreach (var (id, grade, count) in entries)
            {
                if (count <= 0 || !m_Foods.ContainsKey(id)) continue;
                if (!m_FoodGrades.TryGetValue(id, out var q))
                {
                    q = new Queue<FoodGrade>();
                    m_FoodGrades[id] = q;
                }
                for (int i = 0; i < count; i++) q.Enqueue(grade);
            }

            // 수량 대비 등급 데이터 부족분은 Normal 패딩 (구버전 세이브 호환)
            foreach (var kv in m_Foods)
            {
                if (!m_FoodGrades.TryGetValue(kv.Key, out var q))
                {
                    q = new Queue<FoodGrade>();
                    m_FoodGrades[kv.Key] = q;
                }
                while (q.Count < kv.Value) q.Enqueue(FoodGrade.Normal);
            }
        }

        /// <summary>저장용 음식 등급 카운트 열거 ((id, grade) 별 집계). ServerDBManager 전용.</summary>
        public IEnumerable<(uint id, FoodGrade grade, int count)> AllFoodGrades()
        {
            foreach (var kv in m_FoodGrades)
            {
                var counts = new Dictionary<FoodGrade, int>();
                foreach (var g in kv.Value)
                {
                    counts.TryGetValue(g, out int c);
                    counts[g] = c + 1;
                }
                foreach (var c in counts)
                    yield return (kv.Key, c.Key, c.Value);
            }
        }
    }
}
