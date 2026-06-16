using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.UI;
using UnityEngine;

namespace MonsterKitchen.Management
{
    // ====================================================================
    //  BagUpgradeStation — 가방 용량 업그레이드 스테이션
    //
    //  ▶ 동작
    //    플레이어 Trigger 진입 → 현재 레벨·비용 HUD 알림 표시
    //    Interact 키(E) → NetworkManager.RequestUpgrade(BagCapacity) → 성공/실패 HUD 알림
    // ====================================================================
    public class BagUpgradeStation : InteractableBehaviour
    {
        protected override void OnPlayerEnter() => ShowCostHint();

        public override void Interact() => Upgrade();

        // ================================================================
        //  내부
        // ================================================================

        void ShowCostHint()
        {
            var upg = PlayerDataManager.Instance?.Upgrades;
            if (upg == null) return;

            GameHUD.Instance?.ShowNotification(
                $"[가방 용량] Lv{upg.BagCapacityLevel} → Lv{upg.BagCapacityLevel + 1}" +
                $"  현재: {upg.BagCurrentCapacity}kg  비용: {upg.BagUpgradeCost}G  (E 업그레이드)", 3f);
        }

        void Upgrade()
        {
            if (NetworkManager.Instance == null)
            {
                DebugUtil.LogWarning("[BagUpgradeStation] NetworkManager 없음.");
                return;
            }

            var upg  = PlayerDataManager.Instance?.Upgrades;
            int cost = upg?.BagUpgradeCost ?? 0;

            NetworkManager.Instance.RequestUpgrade(PlayerUpgradeType.BagCapacity, success =>
            {
                if (success)
                {
                    int newLevel   = upg?.BagCapacityLevel   ?? 0;
                    int newCap     = upg?.BagCurrentCapacity ?? 0;
                    GameHUD.Instance?.ShowNotification(
                        $"[가방 용량] Lv{newLevel} 업그레이드 완료!  용량: {newCap}kg", 2f);
                }
                else
                {
                    GameHUD.Instance?.ShowNotification($"골드 부족 — {cost}G 필요합니다.", 2f);
                }
            });
        }
    }
}
