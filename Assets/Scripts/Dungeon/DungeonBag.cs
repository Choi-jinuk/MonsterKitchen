using System;
using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonBag — 던전 세션 전용 임시 재료 컨테이너 (순수 C# 클래스)
    //
    //  ▶ 무게 기반. 최대 무게 초과 시 TryAdd 거부.
    //  ▶ (재료 ID, 품질) 별 독립 스택 — 품질 승급/병합 없음.
    //  ▶ DungeonMapController 가 생성하고 Current 에 자동 등록.
    //  ▶ FlushToInventory() 호출 시 NetworkManager 경유 인벤토리 이전 후 초기화.
    // ====================================================================

    public class DungeonBag
    {
        public static DungeonBag Current { get; private set; }

        readonly Dictionary<(uint id, IngredientQuality quality), int> m_Contents      = new();
        readonly Dictionary<uint, int>                                 m_WeightPerUnit = new();

        int m_CurrentWeight;
        int m_MaxWeight;

        public int  CurrentWeight => m_CurrentWeight;
        public int  MaxWeight     => m_MaxWeight;
        public bool IsFull        => m_CurrentWeight >= m_MaxWeight;

        /// <summary>(재료 ID, 품질) → 수량. 품질별 독립 스택.</summary>
        public IReadOnlyDictionary<(uint id, IngredientQuality quality), int> Contents => m_Contents;

        public event Action OnBagChanged;

        // ── 생성 ─────────────────────────────────────────────────────

        public DungeonBag(int maxWeight)
        {
            m_MaxWeight = maxWeight;
            Current     = this;
        }

        public static void ClearCurrent()
        {
            Current = null;
        }

        // ── 조작 API ─────────────────────────────────────────────────

        /// <summary>
        /// 재료 추가 시도. 무게 초과 시 false 반환 (부분 추가 없음).
        /// weightPerUnit: 단위 무게 (IngredientData.Weight 에서 전달).
        /// 같은 재료라도 품질이 다르면 별도 스택으로 보관한다.
        /// </summary>
        public bool TryAdd(uint ingredientId, int qty, IngredientQuality quality, int weightPerUnit)
        {
            if (qty <= 0) return false;

            int addWeight = weightPerUnit * qty;
            if (m_CurrentWeight + addWeight > m_MaxWeight) return false;

            var key = (ingredientId, quality);
            m_Contents.TryGetValue(key, out int cur);
            m_Contents[key] = cur + qty;
            m_WeightPerUnit[ingredientId] = weightPerUnit;

            m_CurrentWeight += addWeight;
            OnBagChanged?.Invoke();
            return true;
        }

        /// <summary>특정 (재료, 품질) 스택에서 qty 개 제거 (버리기). qty > 보유량이면 전량 제거.</summary>
        public void Remove(uint ingredientId, IngredientQuality quality, int qty)
        {
            var key = (ingredientId, quality);
            if (!m_Contents.TryGetValue(key, out int cur)) return;

            int weightPerUnit = m_WeightPerUnit.TryGetValue(ingredientId, out int w) ? w : 1;
            int removeQty     = Math.Min(qty, cur);
            int newQty        = cur - removeQty;

            if (newQty <= 0) m_Contents.Remove(key);
            else             m_Contents[key] = newQty;

            if (!HasAnyStackOf(ingredientId))
                m_WeightPerUnit.Remove(ingredientId);

            m_CurrentWeight -= weightPerUnit * removeQty;
            if (m_CurrentWeight < 0) m_CurrentWeight = 0;
            OnBagChanged?.Invoke();
        }

        /// <summary>사망 시 전체 내용물을 버리고 초기화. 인벤토리 이전 없음.</summary>
        public void ClearAll()
        {
            m_Contents.Clear();
            m_WeightPerUnit.Clear();
            m_CurrentWeight = 0;
            OnBagChanged?.Invoke();
            DebugUtil.Log("[DungeonBag] ClearAll — 사망으로 모든 재료 소실.");
        }

        /// <summary>귀환 시 전체 내용물을 NetworkManager 경유 인벤토리로 이전 후 클리어.</summary>
        public void FlushToInventory()
        {
            foreach (var kv in m_Contents)
                NetworkManager.Instance?.RequestAddIngredient(kv.Key.id, kv.Value, kv.Key.quality);

            m_Contents.Clear();
            m_WeightPerUnit.Clear();
            m_CurrentWeight = 0;
            OnBagChanged?.Invoke();
            DebugUtil.Log("[DungeonBag] FlushToInventory 완료. 가방 초기화.");
        }

        // ── 내부 ─────────────────────────────────────────────────────

        bool HasAnyStackOf(uint ingredientId)
        {
            foreach (var kv in m_Contents)
                if (kv.Key.id == ingredientId) return true;
            return false;
        }
    }
}
