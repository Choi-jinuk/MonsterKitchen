using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Management
{
    /// <summary>
    /// 가게 업그레이드 스테이션.
    /// 플레이어가 Trigger 안에 있는 동안 Interact 키 → ShopManager를 통해 좌석 또는 팁 배율 업그레이드.
    /// </summary>
    public class ShopUpgradeStation : MonoBehaviour
    {
        public enum UpgradeType { Seats, Tip }

        [SerializeField] UpgradeType upgradeType = UpgradeType.Seats;

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
            if (ShopManager.Instance == null)
            {
                Debug.LogWarning("[ShopUpgradeStation] ShopManager 없음.");
                return;
            }

            bool success;
            int  cost;

            if (upgradeType == UpgradeType.Seats)
            {
                cost    = ShopManager.Instance.SeatUpgradeCost;
                success = ShopManager.Instance.UpgradeSeats();
            }
            else
            {
                cost    = ShopManager.Instance.TipUpgradeCost;
                success = ShopManager.Instance.UpgradeTip();
            }

            if (!success)
                Debug.Log($"[ShopUpgradeStation] 골드 부족. 필요: {cost}G");
        }
    }
}
