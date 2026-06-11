// ====================================================================
//  RestaurantSceneController — RestaurantScene 컨트롤러
//
//  ▶ 상태 흐름
//    Init    : DayManager 설정 + 메뉴 구성 UI 열기
//    Menu    : MenuSetupUI에서 음식 선택 → "영업 시작" 클릭
//    Running : DayManager.StartDay() → 손님 서빙
//    End     : DayManager.OnAllGuestsLeft → SettlementUI 표시
//    Done    : SettlementUI "다음 날로" → DayManager.CompleteDay()
// ====================================================================

using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.UI;
using MonsterKitchen.UI.Mobile;
using UnityEngine;

namespace MonsterKitchen.Restaurant
{
    [DisallowMultipleComponent]
    public class RestaurantSceneController : SceneControllerBase
    {
        [SerializeField] Transform         m_GuestSpawnPoint;
        [SerializeField] RestaurantTable[] m_Tables;

        protected override void OnInit()
        {
            var day = DayManager.Instance;
            if (day == null)
            {
                DebugUtil.LogError("[RestaurantSceneController] DayManager 없음 — StartScene 부터 시작하세요.", this);
                return;
            }

            // 1. 씬 로컬 레퍼런스 주입
            var customerPrefab = AssetLoadManager.Instance?.Load<CustomerAI>(AssetKeys.PREFAB_CUSTOMER);
            if (customerPrefab == null)
            {
                DebugUtil.LogError("[RestaurantSceneController] Customer 프리팹 로드 실패. AssetManifest 확인 필요. 키: " + AssetKeys.PREFAB_CUSTOMER, this);
                return;
            }

            day.SetRestaurantConfig(m_GuestSpawnPoint, customerPrefab, m_Tables);
            MobileHUD.Instance?.SetContext(MobileContext.Exploration);

            // 2. 플레이어 위치 재조정
            GlobalController.Instance?.Player?.RepositionInScene();

            // 3. 일일 메뉴 초기화 (명성 기반 슬롯 수)
            int slotCount = PlayerDataManager.Instance?.Fame?.MenuSlotCount() ?? 3;
            PlayerDataManager.Instance?.DailyMenu?.Reset(slotCount);

            // 4. OnAllGuestsLeft 이벤트 구독 → 정산 UI 표시
            day.OnAllGuestsLeft += HandleAllGuestsLeft;

            // 5. 메뉴 구성 UI 열기
            var menuUI = UIManager.Instance?.GetPanel<MenuSetupUI>("MenuSetupUI");
            if (menuUI != null)
            {
                menuUI.OnStartDay = () => day.StartDay();
                UIManager.Instance?.Open("MenuSetupUI");
            }
            else
            {
                DebugUtil.LogWarning("[RestaurantSceneController] MenuSetupUI 없음 — 직접 StartDay 호출.", this);
                day.StartDay();
            }

            CompleteInit();
        }

        void HandleAllGuestsLeft()
        {
            DayManager.Instance.OnAllGuestsLeft -= HandleAllGuestsLeft;
            UIManager.Instance?.Open("SettlementUI");
        }

        void OnDestroy()
        {
            if (DayManager.Instance != null)
                DayManager.Instance.OnAllGuestsLeft -= HandleAllGuestsLeft;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_GuestSpawnPoint == null)
                DebugUtil.LogWarning("[RestaurantSceneController] GuestSpawnPoint 미연결.", this);
            if (m_Tables == null || m_Tables.Length == 0)
                DebugUtil.LogWarning("[RestaurantSceneController] Tables 비어있음.", this);
        }
#endif
    }
}
