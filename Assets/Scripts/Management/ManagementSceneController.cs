// ====================================================================
//  ManagementSceneController — ManagementScene 컨트롤러
//
//  ▶ 상태 흐름
//    Init    : 플레이어 스폰(첫 진입) 또는 위치 재조정(복귀)
//    Running : 던전 포털·저녁 시작 등 Management 게임플레이
//    End     : 씬 전환 (DungeonPortal / EveningStarter 가 ExitScene 호출)
//
//  ▶ PlayerManager
//    첫 진입 : PlayerManager.Start() → 플레이어 데이터 로드 + 스폰
//    재진입  : PlayerManager.RepositionInScene() → 스폰 포인트로 이동
// ====================================================================

using MonsterKitchen.Core;
using MonsterKitchen.UI.Mobile;
using UnityEngine;

namespace MonsterKitchen.Management
{
    [DisallowMultipleComponent]
    public class ManagementSceneController : SceneControllerBase
    {
        // ================================================================
        //  SceneControllerBase
        // ================================================================

        protected override void OnInit()
        {
            var pm = GlobalController.Instance?.Player;
            if (pm != null)
            {
                if (!pm.IsStarted)
                    pm.Start(m_PlayerSpawnPoint);              // 첫 진입: 데이터 로드 + 스폰
                else
                    pm.RepositionInScene(m_PlayerSpawnPoint);  // 재진입: 스폰 포인트로 이동
            }
            else
            {
                DebugUtil.LogError("[ManagementSceneController] PlayerManager 없음.", this);
            }

            MobileHUD.Instance?.SetContext(MobileContext.Exploration);
            CompleteInit();
        }
    }
}
