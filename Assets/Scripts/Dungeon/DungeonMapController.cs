using System.Collections;
using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Navigation;
using MonsterKitchen.UI.Mobile;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonMapController — DungeonScene 총괄. SceneControllerBase 상속.
    //
    //  ▶ 역할
    //    DungeonSpawnZone[] 초기화 + DungeonBag 생성 + DungeonExit 활성화
    //    + NavGrid.Bake() + CameraConfiner.Refresh() + 미니맵 데이터 제공
    //
    //  ▶ 미니맵
    //    m_MinimapSprite: Inspector 연결 (던전 탑뷰 이미지)
    //    m_WorldBounds  : Inspector 연결 (맵 월드 영역)
    //    GameHUD 가 이 값을 읽어 UIToolkit 미니맵 렌더링에 사용.
    // ====================================================================

    [DisallowMultipleComponent]
    public class DungeonMapController : SceneControllerBase
    {
        public static DungeonMapController Instance { get; private set; }

        [Header("스폰 존 — Inspector 직접 연결")]
        [SerializeField] DungeonSpawnZone[] m_SpawnZones;

        [Header("출구 — Inspector 직접 연결 (항상 활성 배치)")]
        [SerializeField] DungeonExit m_Exit;

        [Header("카메라 컨파이너 — Inspector 직접 연결")]
        [SerializeField] DungeonCameraConfiner m_CameraConfiner;

        [Header("미니맵")]
        [SerializeField] Sprite m_MinimapSprite;
        [SerializeField] Rect   m_WorldBounds;

        public Sprite                          MinimapSprite => m_MinimapSprite;
        public Rect                            WorldBounds   => m_WorldBounds;
        public IReadOnlyList<DungeonSpawnZone> SpawnZones    => m_SpawnZones;

        // ── Unity ────────────────────────────────────────────────────

        void Awake()
        {
            Instance = this;
            SetupLayerCollisions();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                DungeonBag.ClearCurrent();
            }
        }

        // ── SceneControllerBase ──────────────────────────────────────

        protected override void OnInit()
        {
            MobileHUD.Instance?.SetContext(MobileContext.Dungeon);
            GlobalController.Instance?.Player?.RepositionInScene(m_PlayerSpawnPoint);
            StartCoroutine(InitAsync());
        }

        IEnumerator InitAsync()
        {
            yield return null;   // 1프레임 대기

            // DungeonBag 초기화
            int maxWeight = PlayerDataManager.Instance?.Upgrades?.BagCurrentCapacity ?? 10;
            _ = new DungeonBag(maxWeight);   // DungeonBag.Current 자동 설정

            // NavGrid 베이크
            NavGrid.Instance?.Bake();

            // 동료 스폰 — 선행조건 충족 시점: 리더 리포지션(OnInit) + NavGrid 베이크 완료.
            CompanionManager.GetOrCreate().SpawnParty();

            // 카메라 경계 갱신
            m_CameraConfiner?.Refresh();

            // 스폰 존 초기화
            var player = GetPlayer();
            if (player != null)
            {
                if (m_SpawnZones != null)
                {
                    foreach (var zone in m_SpawnZones)
                    {
                        if (zone != null) zone.Init(player);
                    }
                }
            }
            else
            {
                DebugUtil.LogWarning("[DungeonMapController] 플레이어를 찾을 수 없습니다.");
            }

            // 출구 활성화 (항상)
            if (m_Exit != null)
                m_Exit.gameObject.SetActive(true);
            else
                DebugUtil.LogWarning("[DungeonMapController] DungeonExit 가 연결되지 않았습니다.", this);

            CompleteInit();
            DebugUtil.Log($"[DungeonMapController] 초기화 완료. 스폰 존: {m_SpawnZones?.Length ?? 0}개. 가방 용량: {maxWeight}");
        }

        // ── 플레이어 참조 ────────────────────────────────────────────

        Transform m_CachedPlayer;

        Transform GetPlayer()
        {
            if (m_CachedPlayer != null) return m_CachedPlayer;
            var player = PlayerManager.Instance?.Player;
            if (player != null)
            {
                m_CachedPlayer = player.transform;
                return m_CachedPlayer;
            }
            DebugUtil.LogWarning("[DungeonMapController] PlayerManager.Player 없음.");
            return null;
        }

        // ── 레이어 충돌 설정 ─────────────────────────────────────────

        static void SetupLayerCollisions()
        {
            int enemy  = LayerMask.NameToLayer("Enemy");
            int player = LayerMask.NameToLayer("Player");
            if (enemy < 0)
            {
                DebugUtil.LogWarning("[DungeonMapController] 'Enemy' 레이어 없음.");
                return;
            }
            Physics2D.IgnoreLayerCollision(enemy, enemy, true);
            if (player >= 0)
                Physics2D.IgnoreLayerCollision(player, enemy, true);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_SpawnZones == null || m_SpawnZones.Length == 0)
                DebugUtil.LogWarning("[DungeonMapController] SpawnZones 가 비어있습니다.");
            if (m_Exit == null)
                DebugUtil.LogWarning("[DungeonMapController] DungeonExit 가 연결되지 않았습니다.");
        }
#endif
    }
}
