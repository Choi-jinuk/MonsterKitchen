using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  GlobalController — 씬 전환 후에도 유지되는 글로벌 매니저 총괄
    //
    //  ▶ 역할
    //    · Inspector SerializeField 목록으로 글로벌 매니저 전체를 한눈에 파악.
    //    · Awake() 에서 InitManagers() 를 호출해 초기화 순서를 코드로 명시.
    //    · [DefaultExecutionOrder(-300)] 으로 다른 모든 Awake() 보다 먼저 실행.
    //
    //  ▶ 폴백
    //    GlobalController 없이 매니저 단독 배치 시(테스트 씬 등)
    //    각 매니저의 Awake() 에 들어있는 폴백이 자동으로 Init() 를 호출한다.
    //
    //  ▶ 배치
    //    ManagementScene 의 GlobalController GameObject 에 단독 배치.
    //    DontDestroyOnLoad 이므로 씬 전환 후에도 유지된다.
    // ====================================================================

    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public class GlobalController : MonoBehaviour
    {
        public static GlobalController Instance { get; private set; }

        [Header("── 글로벌 매니저  (위 → 아래 순서로 초기화됩니다)")]
        [SerializeField] AssetLoadManager _assetLoadManager;
        [SerializeField] SceneLoader      _sceneLoader;
        [SerializeField] GoldManager      _goldManager;
        [SerializeField] Inventory        _inventory;
        [SerializeField] FoodInventory    _foodInventory;
        [SerializeField] ToolManager      _toolManager;
        [SerializeField] DayManager       _dayManager;
        [SerializeField] ShopManager      _shopManager;
        [SerializeField] PhaseManager     _phaseManager;
        [SerializeField] InputManager     _inputManager;
        [SerializeField] PlayerManager    _playerManager;

        // ================================================================
        //  Mono
        // ================================================================

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitManagers();
        }

        // ================================================================
        //  초기화 — 순서가 곧 의존 관계를 표현한다
        // ================================================================

        void InitManagers()
        {
            _assetLoadManager?.Init();   // 에셋 로딩 기반 — 가장 먼저
            _sceneLoader?.Init();        // 씬 전환 시스템
            _goldManager?.Init();        // 독립 데이터
            _inventory?.Init();
            _foodInventory?.Init();
            _toolManager?.Init();
            _dayManager?.Init();
            _shopManager?.Init();        // GoldManager 메서드 사용 (Init 이후 메서드만 호출)
            _phaseManager?.Init();       // SceneLoader 메서드 사용
            _inputManager?.Init();       // InputSystem_Actions 생성
            _playerManager?.Init();      // AssetLoadManager 기반 (Start 에서 Load 호출)
        }

        // ================================================================
        //  편의 — Inspector 에서 미연결 슬롯 즉시 감지
        // ================================================================

#if UNITY_EDITOR
        void OnValidate()
        {
            if (_assetLoadManager == null) Debug.LogWarning("[GlobalController] AssetLoadManager 미연결");
            if (_sceneLoader      == null) Debug.LogWarning("[GlobalController] SceneLoader 미연결");
            if (_goldManager      == null) Debug.LogWarning("[GlobalController] GoldManager 미연결");
            if (_inventory        == null) Debug.LogWarning("[GlobalController] Inventory 미연결");
            if (_foodInventory    == null) Debug.LogWarning("[GlobalController] FoodInventory 미연결");
            if (_toolManager      == null) Debug.LogWarning("[GlobalController] ToolManager 미연결");
            if (_dayManager       == null) Debug.LogWarning("[GlobalController] DayManager 미연결");
            if (_shopManager      == null) Debug.LogWarning("[GlobalController] ShopManager 미연결");
            if (_phaseManager     == null) Debug.LogWarning("[GlobalController] PhaseManager 미연결");
            if (_inputManager     == null) Debug.LogWarning("[GlobalController] InputManager 미연결");
            if (_playerManager    == null) Debug.LogWarning("[GlobalController] PlayerManager 미연결");
        }
#endif
    }
}
