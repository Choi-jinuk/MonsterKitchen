using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Management
{
    /// <summary>
    /// 도구 업그레이드 스테이션.
    /// 플레이어가 Trigger 안에 있는 동안 Interact 키 → ToolManager를 통해 해당 스탯 업그레이드.
    /// </summary>
    public class ToolUpgradeStation : MonoBehaviour
    {
        public enum UpgradeType { Damage, Range, Cooldown }

        [SerializeField] UpgradeType upgradeType = UpgradeType.Damage;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract += Upgrade;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract -= Upgrade;
        }

        void OnDisable()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract -= Upgrade;
        }

        void Upgrade()
        {
            if (ToolManager.Instance == null)
            {
                Debug.LogWarning("[ToolUpgradeStation] ToolManager 없음.");
                return;
            }

            bool success;
            int  cost;

            switch (upgradeType)
            {
                case UpgradeType.Damage:
                    cost    = ToolManager.Instance.DamageUpgradeCost;
                    success = ToolManager.Instance.UpgradeDamage();
                    break;
                case UpgradeType.Range:
                    cost    = ToolManager.Instance.RangeUpgradeCost;
                    success = ToolManager.Instance.UpgradeRange();
                    break;
                default:
                    cost    = ToolManager.Instance.CooldownUpgradeCost;
                    success = ToolManager.Instance.UpgradeCooldown();
                    break;
            }

            if (!success)
                Debug.Log($"[ToolUpgradeStation] 골드 부족. 필요: {cost}G");
        }
    }
}
