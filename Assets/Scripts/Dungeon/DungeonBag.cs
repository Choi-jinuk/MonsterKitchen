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
    //  ▶ DungeonMapController 가 생성하고 Current 에 자동 등록.
    //  ▶ FlushToInventory() 호출 시 NetworkManager 경유 인벤토리 이전 후 초기화.
    // ====================================================================

    public class DungeonBag
    {
        public static DungeonBag Current { get; private set; }

        readonly Dictionary<uint, (int qty, IngredientQuality quality)> m_Contents      = new();
        readonly Dictionary<uint, int>                                   m_WeightPerUnit = new();

        int m_CurrentWeight;
        int m_MaxWeight;

        public int  CurrentWeight => m_CurrentWeight;
        public int  MaxWeight     => m_MaxWeight;
        public bool IsFull        => m_CurrentWeight >= m_MaxWeight;

        public IReadOnlyDictionary<uint, (int qty, IngredientQuality quality)> Contents => m_Contents;

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
        /// 같은 재료 재추가 시 더 높은 품질 유지.
        /// </summary>
        public bool TryAdd(uint ingredientId, int qty, IngredientQuality quality, int weightPerUnit)
        {
            int addWeight = weightPerUnit * qty;
            if (m_CurrentWeight + addWeight > m_MaxWeight) return false;

            if (m_Contents.TryGetValue(ingredientId, out var existing))
            {
                var bestQuality = (IngredientQuality)System.Math.Max((int)existing.quality, (int)quality);
                m_Contents[ingredientId] = (existing.qty + qty, bestQuality);
            }
            else
            {
                m_Contents[ingredientId]      = (qty, quality);
                m_WeightPerUnit[ingredientId] = weightPerUnit;
            }

            m_CurrentWeight += addWeight;
            OnBagChanged?.Invoke();
            return true;
        }

        /// <summary>특정 재료 qty 개 제거 (버리기). qty > 보유량이면 전량 제거.</summary>
        public void Remove(uint ingredientId, int qty)
        {
            if (!m_Contents.TryGetValue(ingredientId, out var existing)) return;

            int weightPerUnit = m_WeightPerUnit.TryGetValue(ingredientId, out int w) ? w : 1;
            int removeQty     = System.Math.Min(qty, existing.qty);
            int newQty        = existing.qty - removeQty;

            if (newQty <= 0)
            {
                m_Contents.Remove(ingredientId);
                m_WeightPerUnit.Remove(ingredientId);
            }
            else
            {
                m_Contents[ingredientId] = (newQty, existing.quality);
            }

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
                NetworkManager.Instance?.RequestAddIngredient(kv.Key, kv.Value.qty, kv.Value.quality);

            m_Contents.Clear();
            m_WeightPerUnit.Clear();
            m_CurrentWeight = 0;
            OnBagChanged?.Invoke();
            DebugUtil.Log("[DungeonBag] FlushToInventory 완료. 가방 초기화.");
        }
    }
}
