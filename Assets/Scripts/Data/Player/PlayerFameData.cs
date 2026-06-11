using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  PlayerFameData — 식당 명성(평판) 추적
    //
    //  ▶ 명성 획득: (일일 평균 만족도 - 50) * 0.8f, 하한 0
    //  ▶ 마일스톤
    //    100  → 손님 4명/일
    //    250  → 메뉴 슬롯 4개
    //    500  → 특수 손님 해금
    //    1000 → 비밀 레시피 힌트
    // ====================================================================

    public class PlayerFameData
    {
        int m_TotalFame;

        public int TotalFame => m_TotalFame;

        public event Action<int, int> OnFameChanged;  // (oldFame, newFame)

        // ── 명성 변경 ────────────────────────────────────────────────

        public void AddFame(int amount)
        {
            int old = m_TotalFame;
            m_TotalFame = Mathf.Max(0, m_TotalFame + amount);
            OnFameChanged?.Invoke(old, m_TotalFame);
        }

        // ── 일일 명성 계산 ────────────────────────────────────────────

        /// <summary>일일 평균 만족도 → 획득 명성. (avgSat - 50) * 0.8f, 반올림.</summary>
        public int GetDailyFame(float avgSatisfaction)
            => Mathf.RoundToInt((avgSatisfaction - 50f) * 0.8f);

        // ── 마일스톤 조회 ─────────────────────────────────────────────

        /// <summary>현재 명성 기준 하루 최대 손님 수.</summary>
        public int MaxGuestsPerDay()
        {
            if (m_TotalFame >= 100) return 4;
            return 3;
        }

        /// <summary>현재 명성 기준 메뉴 슬롯 수.</summary>
        public int MenuSlotCount()
        {
            if (m_TotalFame >= 250) return 4;
            return 3;
        }

        /// <summary>특수 손님 해금 여부 (명성 500 이상).</summary>
        public bool IsSpecialGuestUnlocked() => m_TotalFame >= 500;

        // ── 저장 / 로드 ───────────────────────────────────────────────

        public int  Save() => m_TotalFame;
        public void Load(int savedFame) => m_TotalFame = Mathf.Max(0, savedFame);
    }
}
