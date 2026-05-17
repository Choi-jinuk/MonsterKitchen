using System;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 플레이어 도구(무기) 업그레이드 상태를 씬 간 유지하는 싱글톤.
    /// GlobalController 가 new 로 생성하고 Init() 를 호출한다.
    /// </summary>
    public class ToolManager
    {
        public static ToolManager Instance { get; private set; }

        // 기본 스탯
        const int   BaseDamage          = 10;
        const float BaseRange           = 0.8f;
        const float BaseCooldown        = 0.4f;
        const int   DamageUpgradeCostBase   = 50;
        const int   RangeUpgradeCostBase    = 60;
        const int   CooldownUpgradeCostBase = 70;

        public int DamageLevel   { get; private set; }
        public int RangeLevel    { get; private set; }
        public int CooldownLevel { get; private set; }

        public int   CurrentDamage   => BaseDamage   + DamageLevel   * 5;
        public float CurrentRange    => BaseRange    + RangeLevel    * 0.1f;
        public float CurrentCooldown => Mathf.Max(0.1f, BaseCooldown - CooldownLevel * 0.05f);

        public int DamageUpgradeCost   => (DamageLevel   + 1) * DamageUpgradeCostBase;
        public int RangeUpgradeCost    => (RangeLevel    + 1) * RangeUpgradeCostBase;
        public int CooldownUpgradeCost => (CooldownLevel + 1) * CooldownUpgradeCostBase;

        public event Action OnUpgraded;

        public void Init() => Instance = this;

        public bool UpgradeDamage()
        {
            if (GoldManager.Instance == null || !GoldManager.Instance.Spend(DamageUpgradeCost)) return false;
            DamageLevel++;
            OnUpgraded?.Invoke();
            Debug.Log($"[ToolManager] 공격력 Lv{DamageLevel} → {CurrentDamage}");
            return true;
        }

        public bool UpgradeRange()
        {
            if (GoldManager.Instance == null || !GoldManager.Instance.Spend(RangeUpgradeCost)) return false;
            RangeLevel++;
            OnUpgraded?.Invoke();
            Debug.Log($"[ToolManager] 범위 Lv{RangeLevel} → {CurrentRange:F2}");
            return true;
        }

        public bool UpgradeCooldown()
        {
            if (GoldManager.Instance == null || !GoldManager.Instance.Spend(CooldownUpgradeCost)) return false;
            CooldownLevel++;
            OnUpgraded?.Invoke();
            Debug.Log($"[ToolManager] 쿨다운 Lv{CooldownLevel} → {CurrentCooldown:F2}s");
            return true;
        }
    }
}
