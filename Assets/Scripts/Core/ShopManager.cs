using System;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 가게(레스토랑) 업그레이드 상태 싱글톤.
    /// GlobalController 가 new 로 생성하고 Init() 를 호출한다.
    /// </summary>
    public class ShopManager
    {
        public static ShopManager Instance { get; private set; }

        const int SeatUpgradeCostBase = 100;
        const int TipUpgradeCostBase  = 80;

        public int SeatLevel { get; private set; }
        public int TipLevel  { get; private set; }

        public float TipMultiplier  => 1f + TipLevel  * 0.1f;
        public int   TotalSeats     => 1  + SeatLevel;

        public int SeatUpgradeCost => (SeatLevel + 1) * SeatUpgradeCostBase;
        public int TipUpgradeCost  => (TipLevel  + 1) * TipUpgradeCostBase;

        public event Action OnUpgraded;

        public void Init() => Instance = this;

        public bool UpgradeSeats()
        {
            if (GoldManager.Instance == null || !GoldManager.Instance.Spend(SeatUpgradeCost)) return false;
            SeatLevel++;
            OnUpgraded?.Invoke();
            Debug.Log($"[ShopManager] 좌석 Lv{SeatLevel} → {TotalSeats}석");
            return true;
        }

        public bool UpgradeTip()
        {
            if (GoldManager.Instance == null || !GoldManager.Instance.Spend(TipUpgradeCost)) return false;
            TipLevel++;
            OnUpgraded?.Invoke();
            Debug.Log($"[ShopManager] 팁 Lv{TipLevel} → {TipMultiplier:P0}");
            return true;
        }
    }
}
