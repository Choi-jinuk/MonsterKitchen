// ====================================================================
//  KitchenSceneController — KitchenScene 컨트롤러
//
//  ▶ 상태 흐름
//    Init    : 플레이어 스폰 포인트 재조정
//    Running : 요리 게임플레이 (CookingStation, KitchenExit)
//    End     : KitchenExit 또는 RestaurantEntry 에서 ExitScene 호출
// ====================================================================

using MonsterKitchen.Core;
using MonsterKitchen.UI.Mobile;
using UnityEngine;

namespace MonsterKitchen.Kitchen
{
    [DisallowMultipleComponent]
    public class KitchenSceneController : SceneControllerBase
    {
        // ================================================================
        //  SceneControllerBase
        // ================================================================

        protected override void OnInit()
        {
            MobileHUD.Instance?.SetContext(MobileContext.Exploration);
            GlobalController.Instance?.Player?.RepositionInScene();
            CompleteInit();
        }
    }
}
