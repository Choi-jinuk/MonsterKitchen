using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsterKitchen.Management
{
    /// <summary>
    /// 도구 업그레이드 스테이션.
    /// Player 접근 후 E키 → ToolManager를 통해 해당 스탯 업그레이드.
    /// </summary>
    public class ToolUpgradeStation : MonoBehaviour
    {
        public enum UpgradeType { Damage, Range, Cooldown }

        [SerializeField] UpgradeType upgradeType = UpgradeType.Damage;

        bool _playerNearby;

        void Update()
        {
            if (!_playerNearby) return;
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                Upgrade();
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

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player")) _playerNearby = true;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player")) _playerNearby = false;
        }
    }
}
