using System;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 플레이어 도구(무기) 업그레이드 상태를 씬 간 유지하는 싱글톤.
    /// PlayerController가 Start()에서 이 값을 읽어 스탯을 적용한다.
    /// </summary>
    public class ToolManager : MonoBehaviour
    {
        public static ToolManager Instance { get; private set; }

        [Header("Base Stats")]
        [SerializeField] int   baseDamage   = 10;
        [SerializeField] float baseRange    = 0.8f;
        [SerializeField] float baseCooldown = 0.4f;

        [Header("Upgrade Costs (per level)")]
        [SerializeField] int damageUpgradeCost   = 50;
        [SerializeField] int rangeUpgradeCost    = 60;
        [SerializeField] int cooldownUpgradeCost = 70;

        public int DamageLevel   { get; private set; }
        public int RangeLevel    { get; private set; }
        public int CooldownLevel { get; private set; }

        public int   CurrentDamage   => baseDamage   + DamageLevel   * 5;
        public float CurrentRange    => baseRange    + RangeLevel    * 0.1f;
        public float CurrentCooldown => Mathf.Max(0.1f, baseCooldown - CooldownLevel * 0.05f);

        public int DamageUpgradeCost   => (DamageLevel   + 1) * damageUpgradeCost;
        public int RangeUpgradeCost    => (RangeLevel    + 1) * rangeUpgradeCost;
        public int CooldownUpgradeCost => (CooldownLevel + 1) * cooldownUpgradeCost;

        public event Action OnUpgraded;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

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
