using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.UI;
using UnityEngine;

namespace MonsterKitchen.Management
{
    // ====================================================================
    //  ShopUpgradeStation — 가게 업그레이드 스테이션
    //
    //  ▶ 동작
    //    플레이어 Trigger 진입 → 현재 레벨·비용 HUD 알림 표시
    //    Interact 키(E) → NetworkManager.RequestUpgrade → 성공/실패 HUD 알림
    // ====================================================================
    public class ShopUpgradeStation : InteractableBehaviour
    {
        public enum UpgradeType { Seats, Tip }

        [SerializeField] UpgradeType m_UpgradeType = UpgradeType.Seats;

        protected override void OnPlayerEnter() => ShowCostHint();

        public override void Interact() => Upgrade();

        // ================================================================
        //  내부
        // ================================================================

        void ShowCostHint()
        {
            var upg = PlayerDataManager.Instance?.Upgrades;
            if (upg == null) return;

            if (m_UpgradeType == UpgradeType.Seats)
                GameHUD.Instance?.ShowNotification(
                    $"[가게 좌석] Lv{upg.ShopSeatLevel} → Lv{upg.ShopSeatLevel + 1}  비용: {upg.ShopSeatUpgradeCost}G  (E 업그레이드)", 3f);
            else
                GameHUD.Instance?.ShowNotification(
                    $"[가게 팁] Lv{upg.ShopTipLevel} → Lv{upg.ShopTipLevel + 1}  비용: {upg.ShopTipUpgradeCost}G  (E 업그레이드)", 3f);
        }

        void Upgrade()
        {
            if (NetworkManager.Instance == null)
            {
                DebugUtil.LogWarning("[ShopUpgradeStation] NetworkManager 없음.");
                return;
            }

            var upg  = PlayerDataManager.Instance?.Upgrades;
            PlayerUpgradeType type = m_UpgradeType == UpgradeType.Seats
                ? PlayerUpgradeType.ShopSeats
                : PlayerUpgradeType.ShopTip;

            int cost = m_UpgradeType == UpgradeType.Seats
                ? upg?.ShopSeatUpgradeCost ?? 0
                : upg?.ShopTipUpgradeCost  ?? 0;

            string label = m_UpgradeType == UpgradeType.Seats ? "좌석" : "팁 배율";

            NetworkManager.Instance.RequestUpgrade(type, success =>
            {
                if (success)
                {
                    int newLevel = m_UpgradeType == UpgradeType.Seats
                        ? upg?.ShopSeatLevel ?? 0
                        : upg?.ShopTipLevel  ?? 0;
                    GameHUD.Instance?.ShowNotification($"[가게 {label}] Lv{newLevel} 업그레이드 완료!", 2f);
                }
                else
                {
                    GameHUD.Instance?.ShowNotification($"골드 부족 — {cost}G 필요합니다.", 2f);
                }
            });
        }
    }
}
