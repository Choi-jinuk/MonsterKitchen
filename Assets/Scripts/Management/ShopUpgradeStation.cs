using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Management
{
    /// <summary>
    /// 가게 업그레이드 스테이션.
    /// 플레이어가 Trigger 안에 있는 동안 Interact 키 → NetworkManager 를 통해 좌석 또는 팁 배율 업그레이드.
    /// </summary>
    public class ShopUpgradeStation : MonoBehaviour
    {
        public enum UpgradeType { Seats, Tip }

        [SerializeField] UpgradeType m_UpgradeType = UpgradeType.Seats;

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
                Debug.LogWarning("[ShopUpgradeStation] NetworkManager 없음.");
                return;
            }

            PlayerUpgradeType type = m_UpgradeType == UpgradeType.Seats
                ? PlayerUpgradeType.ShopSeats
                : PlayerUpgradeType.ShopTip;

            // 비용 미리 캡처 (실패 메시지용)
            var upg = PlayerDataManager.Instance?.Upgrades;
            int cost = m_UpgradeType == UpgradeType.Seats
                ? upg?.ShopSeatUpgradeCost ?? 0
                : upg?.ShopTipUpgradeCost  ?? 0;

            NetworkManager.Instance.RequestUpgrade(type, success =>
            {
                if (!success)
                    Debug.Log($"[ShopUpgradeStation] 골드 부족. 필요: {cost}G");
            });
        }
    }
}
