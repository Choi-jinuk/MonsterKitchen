using MonsterKitchen.Core;
using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  PlayerUpgradeType — 업그레이드 항목 식별자
    //  NetworkManager.RequestUpgrade() 및 PlayerDataManager.ApplyUpgradeLevel() 의 공유 열거형.
    // ====================================================================
    public enum PlayerUpgradeType
    {
        ToolDamage, ToolRange, ToolCooldown,
        ShopSeats,  ShopTip,
        BagCapacity,
    }

    // ====================================================================
    //  PlayerUpgradeData — 도구·가게 업그레이드 레벨 관리
    //
    //  ▶ 역할
    //    도구(공격력·범위·쿨다운)·가게(좌석·팁 배율) 업그레이드 레벨 보관.
    //    PlayerDataManager 가 생성하고 Init() 을 호출한다.
    //
    //  ▶ 접근 방법 (PlayerDataManager 경유만 허용)
    //    PlayerDataManager.Instance.Upgrades.ToolCurrentDamage
    //    PlayerDataManager.Instance.Upgrades.ShopTipMultiplier
    //
    //  ▶ 변경 방법 (NetworkManager 경유만 허용)
    //    NetworkManager.Instance.RequestUpgrade(PlayerUpgradeType.ToolDamage, callback)
    //    → 콜백 → PlayerDataManager.ApplyUpgradeLevel(type, newLevel) → SetLevel()
    // ====================================================================

    public class PlayerUpgradeData
    {
        // ── 기본값 상수 ──────────────────────────────────────────────
        const int   BASE_DAMAGE               = 10;
        const float BASE_RANGE                = 0.8f;
        const float BASE_COOLDOWN             = 0.4f;
        const int   DAMAGE_UPGRADE_COST_BASE  = 50;
        const int   RANGE_UPGRADE_COST_BASE   = 60;
        const int   COOLDOWN_UPGRADE_COST_BASE = 70;
        const int   SEAT_UPGRADE_COST_BASE    = 100;
        const int   TIP_UPGRADE_COST_BASE     = 80;
        const int   BAG_UPGRADE_COST_BASE     = 90;

        // ── 레벨 상태 ─────────────────────────────────────────────────
        int m_ToolDamageLevel;
        int m_ToolRangeLevel;
        int m_ToolCooldownLevel;
        int m_ShopSeatLevel;
        int m_ShopTipLevel;
        int m_BagCapacityLevel;

        // ── 이벤트 ───────────────────────────────────────────────────
        public event Action OnToolUpgraded;
        public event Action OnShopUpgraded;
        public event Action OnBagUpgraded;

        // ── 생명주기 ─────────────────────────────────────────────────

        public void Init() { }

        // ── 도구 업그레이드 — 조회 ───────────────────────────────────

        public int   ToolDamageLevel   => m_ToolDamageLevel;
        public int   ToolRangeLevel    => m_ToolRangeLevel;
        public int   ToolCooldownLevel => m_ToolCooldownLevel;

        public int   ToolCurrentDamage   => BASE_DAMAGE   + m_ToolDamageLevel   * 5;
        public float ToolCurrentRange    => BASE_RANGE    + m_ToolRangeLevel    * 0.1f;
        public float ToolCurrentCooldown => Mathf.Max(0.1f, BASE_COOLDOWN - m_ToolCooldownLevel * 0.05f);

        public int ToolDamageUpgradeCost   => (m_ToolDamageLevel   + 1) * DAMAGE_UPGRADE_COST_BASE;
        public int ToolRangeUpgradeCost    => (m_ToolRangeLevel    + 1) * RANGE_UPGRADE_COST_BASE;
        public int ToolCooldownUpgradeCost => (m_ToolCooldownLevel + 1) * COOLDOWN_UPGRADE_COST_BASE;

        // ── 가게 업그레이드 — 조회 ───────────────────────────────────

        public int   ShopSeatLevel     => m_ShopSeatLevel;
        public int   ShopTipLevel      => m_ShopTipLevel;
        public float ShopTipMultiplier => 1f + m_ShopTipLevel * 0.1f;
        public int   ShopTotalSeats    => 1  + m_ShopSeatLevel;

        public int ShopSeatUpgradeCost => (m_ShopSeatLevel + 1) * SEAT_UPGRADE_COST_BASE;
        public int ShopTipUpgradeCost  => (m_ShopTipLevel  + 1) * TIP_UPGRADE_COST_BASE;

        public int   BagCapacityLevel   => m_BagCapacityLevel;
        public int   BagCurrentCapacity => 10 + m_BagCapacityLevel * 5;   // Lv0=10, Lv1=15, ..., Lv4=30
        public int   BagUpgradeCost     => (m_BagCapacityLevel + 1) * BAG_UPGRADE_COST_BASE;

        // ── Apply 메서드 — PlayerDataManager 만 호출 ─────────────────

        /// <summary>NetworkManager 콜백 → PlayerDataManager.ApplyUpgradeLevel() 가 호출한다.</summary>
        public void SetLevel(PlayerUpgradeType type, int newLevel)
        {
            switch (type)
            {
                case PlayerUpgradeType.ToolDamage:
                    m_ToolDamageLevel   = newLevel;
                    OnToolUpgraded?.Invoke();
                    DebugUtil.Log($"[ToolUpgrade] 공격력 Lv{newLevel} → {ToolCurrentDamage}");
                    break;
                case PlayerUpgradeType.ToolRange:
                    m_ToolRangeLevel    = newLevel;
                    OnToolUpgraded?.Invoke();
                    DebugUtil.Log($"[ToolUpgrade] 범위 Lv{newLevel} → {ToolCurrentRange:F2}");
                    break;
                case PlayerUpgradeType.ToolCooldown:
                    m_ToolCooldownLevel = newLevel;
                    OnToolUpgraded?.Invoke();
                    DebugUtil.Log($"[ToolUpgrade] 쿨다운 Lv{newLevel} → {ToolCurrentCooldown:F2}s");
                    break;
                case PlayerUpgradeType.ShopSeats:
                    m_ShopSeatLevel     = newLevel;
                    OnShopUpgraded?.Invoke();
                    DebugUtil.Log($"[ShopUpgrade] 좌석 Lv{newLevel} → {ShopTotalSeats}석");
                    break;
                case PlayerUpgradeType.ShopTip:
                    m_ShopTipLevel      = newLevel;
                    OnShopUpgraded?.Invoke();
                    DebugUtil.Log($"[ShopUpgrade] 팁 Lv{newLevel} → {ShopTipMultiplier:P0}");
                    break;
                case PlayerUpgradeType.BagCapacity:
                    m_BagCapacityLevel  = newLevel;
                    OnBagUpgraded?.Invoke();
                    DebugUtil.Log($"[BagUpgrade] 가방 Lv{newLevel} → {BagCurrentCapacity}kg");
                    break;
            }
        }

        // ── 세이브/로드 — ServerDBManager 전용 ──────────────────────

        /// <summary>저장 데이터 복원 시 ServerDBManager 가 호출.</summary>
        public void LoadLevels(int toolDamage, int toolRange, int toolCooldown,
                               int shopSeat, int shopTip, int bagCapacity = 0)
        {
            m_ToolDamageLevel   = toolDamage;
            m_ToolRangeLevel    = toolRange;
            m_ToolCooldownLevel = toolCooldown;
            m_ShopSeatLevel     = shopSeat;
            m_ShopTipLevel      = shopTip;
            m_BagCapacityLevel  = bagCapacity;
        }
    }
}
