using MonsterKitchen.Core;
using MonsterKitchen.Navigation;
using MonsterKitchen.UI;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonGate — 심층 보스 구역 하드 게이트
    //
    //  ▶ 도구 공격력 레벨(PlayerUpgradeData.ToolDamageLevel)이
    //    m_RequiredToolLevel 미만이면 데미지 0 + 안내 토스트.
    //  ▶ 내구도 0 → 파괴: 스프라이트/콜라이더 비활성 +
    //    m_WallTilemap 의 m_SealedArea 벽 타일 제거 + NavGrid 재베이크.
    //  ▶ 세션 한정 — 씬 리로드 시 자연 복원 (벽 타일은 씬에 저장된 상태).
    //  ▶ Awake 에서 ResourceNode 레이어 자동 배정 — PlayerController
    //    TryHarvestNode 의 OverlapCircle 에 함께 잡힌다.
    // ====================================================================

    [RequireComponent(typeof(SpriteRenderer))]
    public class DungeonGate : MonoBehaviour
    {
        [Header("Gate 설정")]
        [SerializeField] int m_RequiredToolLevel = 2;
        [SerializeField] int m_MaxHp            = 100;

        [Header("봉인 타일 — Inspector 직접 연결")]
        [SerializeField] Tilemap   m_WallTilemap;
        [SerializeField] BoundsInt m_SealedArea;   // 게이트 통로 셀 영역

        int  m_CurrentHp;
        bool m_Destroyed;

        public bool IsDestroyed       => m_Destroyed;
        public int  CurrentHp         => m_CurrentHp;
        public int  RequiredToolLevel => m_RequiredToolLevel;

        // ── 순수 로직 (EditMode 테스트 대상) ─────────────────────────

        /// <summary>도구 레벨이 요구치 미만이면 0, 충족하면 최소 1 데미지.</summary>
        public static int ComputeGateDamage(int damage, int toolLevel, int requiredLevel)
        {
            if (toolLevel < requiredLevel) return 0;
            return Mathf.Max(1, damage);
        }

        // ── Mono ─────────────────────────────────────────────────────

        void Awake()
        {
            m_CurrentHp = m_MaxHp;

            int layer = LayerMask.NameToLayer("ResourceNode");
            if (layer >= 0) gameObject.layer = layer;
        }

        // ── 공격 수신 (PlayerController.TryHarvestNode 에서 호출) ────

        public void TakeGateDamage(int damage)
        {
            if (m_Destroyed) return;

            int toolLevel = PlayerDataManager.Instance?.Upgrades?.ToolDamageLevel ?? 0;
            int effective = ComputeGateDamage(damage, toolLevel, m_RequiredToolLevel);

            if (effective <= 0)
            {
                GameHUD.Instance?.ShowNotification(
                    $"단단한 바위다 — 더 강한 도구가 필요하다 (도구 공격력 Lv{m_RequiredToolLevel})", 2.5f);
                return;
            }

            m_CurrentHp -= effective;
            if (m_CurrentHp <= 0) DestroyGate();
        }

        // ── 파괴 ─────────────────────────────────────────────────────

        void DestroyGate()
        {
            m_Destroyed = true;

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            ClearSealedTiles();
            NavGrid.Instance?.Bake();

            GameHUD.Instance?.ShowNotification("길이 열렸다!", 2f);
            DebugUtil.Log("[DungeonGate] 게이트 파괴 — 심층 안쪽 개방.");
        }

        void ClearSealedTiles()
        {
            if (m_WallTilemap == null)
            {
                DebugUtil.LogError("[DungeonGate] m_WallTilemap 미연결 — 봉인 해제 불가.", this);
                return;
            }

            foreach (var cell in m_SealedArea.allPositionsWithin)
                m_WallTilemap.SetTile(cell, null);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_WallTilemap == null)
                DebugUtil.LogWarning("[DungeonGate] m_WallTilemap 미연결.", this);
        }
#endif
    }
}
