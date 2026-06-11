using System.Collections.Generic;

namespace MonsterKitchen.Restaurant
{
    // ====================================================================
    //  DailyMenuData — 하루 메뉴 구성 (비저장, 매 식당 방문 시 초기화)
    //
    //  ▶ 슬롯 수: PlayerFameData.MenuSlotCount() 기준
    //  ▶ 가격 범위: FoodData.BasePrice ± 30%
    // ====================================================================

    public class DailyMenuData
    {
        public struct MenuSlot
        {
            public uint FoodId;
            public int  CustomPrice;
            public bool IsEmpty => FoodId == 0u;
        }

        List<MenuSlot> m_Slots = new();

        public IReadOnlyList<MenuSlot> Slots    => m_Slots;
        public int                     SlotCount => m_Slots.Count;

        // ── 초기화 ────────────────────────────────────────────────────

        /// <summary>식당 씬 진입 시 슬롯 수 지정 후 초기화.</summary>
        public void Reset(int slotCount)
        {
            m_Slots.Clear();
            for (int i = 0; i < slotCount; i++)
                m_Slots.Add(new MenuSlot());
        }

        // ── 슬롯 조작 ────────────────────────────────────────────────

        public void SetSlot(int index, uint foodId, int customPrice)
        {
            if (index < 0 || index >= m_Slots.Count) return;
            m_Slots[index] = new MenuSlot { FoodId = foodId, CustomPrice = customPrice };
        }

        public void ClearSlot(int index)
        {
            if (index < 0 || index >= m_Slots.Count) return;
            m_Slots[index] = new MenuSlot();
        }

        // ── 조회 ─────────────────────────────────────────────────────

        public bool IsOnMenu(uint foodId)
        {
            if (foodId == 0u) return false;
            foreach (var slot in m_Slots)
                if (!slot.IsEmpty && slot.FoodId == foodId) return true;
            return false;
        }

        public int GetPrice(uint foodId)
        {
            foreach (var slot in m_Slots)
                if (!slot.IsEmpty && slot.FoodId == foodId) return slot.CustomPrice;
            return 0;
        }

        public bool HasAnyItem()
        {
            foreach (var slot in m_Slots)
                if (!slot.IsEmpty) return true;
            return false;
        }

        // ── 빈 슬롯 인덱스 ────────────────────────────────────────────

        public int FirstEmptySlotIndex()
        {
            for (int i = 0; i < m_Slots.Count; i++)
                if (m_Slots[i].IsEmpty) return i;
            return -1;
        }
    }
}
