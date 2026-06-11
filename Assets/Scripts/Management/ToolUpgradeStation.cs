using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.UI;
using UnityEngine;

namespace MonsterKitchen.Management
{
    // ====================================================================
    //  ToolUpgradeStation — 도구 업그레이드 스테이션
    //
    //  ▶ 동작
    //    플레이어 Trigger 진입 → 현재 레벨·비용 HUD 알림 표시
    //    Interact 키(E) → NetworkManager.RequestUpgrade → 성공/실패 HUD 알림
    // ====================================================================
    public class ToolUpgradeStation : MonoBehaviour
    {
        public enum UpgradeType { Damage, Range, Cooldown }

        [SerializeField] UpgradeType m_UpgradeType = UpgradeType.Damage;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            ShowCostHint();
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

        // ================================================================
        //  내부
        // ================================================================

        void ShowCostHint()
        {
            var upg = PlayerDataManager.Instance?.Upgrades;
            if (upg == null) return;

            (string label, int level, int cost) = m_UpgradeType switch
            {
                UpgradeType.Damage   => ("공격력", upg.ToolDamageLevel,   upg.ToolDamageUpgradeCost),
                UpgradeType.Range    => ("범위",   upg.ToolRangeLevel,    upg.ToolRangeUpgradeCost),
                _                    => ("쿨다운", upg.ToolCooldownLevel, upg.ToolCooldownUpgradeCost),
            };

            GameHUD.Instance?.ShowNotification($"[도구 {label}] Lv{level} → Lv{level + 1}  비용: {cost}G  (E 업그레이드)", 3f);
        }

        void Upgrade()
        {
            if (NetworkManager.Instance == null)
            {
                DebugUtil.LogWarning("[ToolUpgradeStation] NetworkManager 없음.");
                return;
            }

            var upg = PlayerDataManager.Instance?.Upgrades;
            PlayerUpgradeType type = m_UpgradeType switch
            {
                UpgradeType.Damage => PlayerUpgradeType.ToolDamage,
                UpgradeType.Range  => PlayerUpgradeType.ToolRange,
                _                  => PlayerUpgradeType.ToolCooldown,
            };

            int cost = m_UpgradeType switch
            {
                UpgradeType.Damage => upg?.ToolDamageUpgradeCost   ?? 0,
                UpgradeType.Range  => upg?.ToolRangeUpgradeCost    ?? 0,
                _                  => upg?.ToolCooldownUpgradeCost ?? 0,
            };

            string label = m_UpgradeType switch
            {
                UpgradeType.Damage => "공격력",
                UpgradeType.Range  => "범위",
                _                  => "쿨다운",
            };

            NetworkManager.Instance.RequestUpgrade(type, success =>
            {
                if (success)
                {
                    int newLevel = m_UpgradeType switch
                    {
                        UpgradeType.Damage => upg?.ToolDamageLevel   ?? 0,
                        UpgradeType.Range  => upg?.ToolRangeLevel    ?? 0,
                        _                  => upg?.ToolCooldownLevel ?? 0,
                    };
                    GameHUD.Instance?.ShowNotification($"[도구 {label}] Lv{newLevel} 업그레이드 완료!", 2f);
                }
                else
                {
                    GameHUD.Instance?.ShowNotification($"골드 부족 — {cost}G 필요합니다.", 2f);
                }
            });
        }
    }
}
