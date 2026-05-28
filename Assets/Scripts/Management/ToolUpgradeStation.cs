using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Management
{
    /// <summary>
    /// 도구 업그레이드 스테이션.
    /// 플레이어가 Trigger 안에 있는 동안 Interact 키 → NetworkManager 를 통해 해당 스탯 업그레이드.
    /// </summary>
    public class ToolUpgradeStation : MonoBehaviour
    {
        public enum UpgradeType { Damage, Range, Cooldown }

        [SerializeField] UpgradeType m_UpgradeType = UpgradeType.Damage;

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
            if (NetworkManager.Instance == null)
            {
                Debug.LogWarning("[ToolUpgradeStation] NetworkManager 없음.");
                return;
            }

            PlayerUpgradeType type = m_UpgradeType switch
            {
                UpgradeType.Damage  => PlayerUpgradeType.ToolDamage,
                UpgradeType.Range   => PlayerUpgradeType.ToolRange,
                _                   => PlayerUpgradeType.ToolCooldown,
            };

            // 비용 미리 캡처 (실패 메시지용)
            var upg = PlayerDataManager.Instance?.Upgrades;
            int cost = type switch
            {
                PlayerUpgradeType.ToolDamage   => upg?.ToolDamageUpgradeCost   ?? 0,
                PlayerUpgradeType.ToolRange    => upg?.ToolRangeUpgradeCost    ?? 0,
                _                              => upg?.ToolCooldownUpgradeCost  ?? 0,
            };

            NetworkManager.Instance.RequestUpgrade(type, success =>
            {
                if (!success)
                    Debug.Log($"[ToolUpgradeStation] 골드 부족. 필요: {cost}G");
            });
        }
    }
}
