using System;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 가게(레스토랑) 업그레이드 상태 싱글톤.
    /// 좌석 수와 팁 배율을 관리한다.
    /// CustomerAI가 EatAndPay에서 TipMultiplier를 참조한다.
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        [Header("Upgrade Costs (per level)")]
        [SerializeField] int seatUpgradeCost = 100;
        [SerializeField] int tipUpgradeCost  = 80;

        public int SeatLevel { get; private set; }   // +1 테이블 per level
        public int TipLevel  { get; private set; }   // +10% 팁 per level

        public float TipMultiplier => 1f + TipLevel * 0.1f;
        public int   TotalSeats    => 1  + SeatLevel;          // 기본 1석

        public int SeatUpgradeCost => (SeatLevel + 1) * seatUpgradeCost;
        public int TipUpgradeCost  => (TipLevel  + 1) * tipUpgradeCost;

        public event Action OnUpgraded;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

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
