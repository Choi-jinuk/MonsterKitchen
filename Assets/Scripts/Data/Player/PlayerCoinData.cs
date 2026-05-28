using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  PlayerCoinData — 골드(재화) 상태 관리
    //
    //  ▶ 역할
    //    런타임 골드 수치 보관 + 변경 이벤트 발행.
    //    PlayerDataManager 가 생성하고 Init() 을 호출한다.
    //
    //  ▶ 접근 방법 (PlayerDataManager 경유만 허용)
    //    PlayerDataManager.Instance.Coin.Gold
    //    PlayerDataManager.Instance.Coin.OnGoldChanged
    //
    //  ▶ 변경 방법 (NetworkManager 경유만 허용)
    //    NetworkManager.Instance.RequestEarnGold(amount)
    //    NetworkManager.Instance.RequestSpendGold(amount, callback)
    //    → 콜백 → PlayerDataManager.ApplyGold(newGold) → SetGold()
    // ====================================================================

    public class PlayerCoinData
    {
        int m_Gold;

        public int Gold => m_Gold;

        /// <summary>골드 변경 시 발행. (newGold)</summary>
        public event Action<int> OnGoldChanged;

        // ── 생명주기 ─────────────────────────────────────────────────

        public void Init() => m_Gold = 0;

        // ── Apply (PlayerDataManager 경유 호출) ──────────────────────

        /// <summary>NetworkManager 콜백 → PlayerDataManager.ApplyGold() 가 호출한다.</summary>
        public void SetGold(int gold)
        {
            m_Gold = Mathf.Max(0, gold);
            OnGoldChanged?.Invoke(m_Gold);
        }
    }
}
