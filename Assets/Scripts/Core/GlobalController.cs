using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  GlobalController — 씬 전환 후에도 유지되는 글로벌 매니저 총괄
    //
    //  ▶ 설계 원칙
    //    모든 글로벌 매니저는 MonoBehaviour 를 상속하지 않는 순수 C# 클래스.
    //    이 컨트롤러가 유일한 MonoBehaviour 로서 전체 생명주기를 위임 관리한다.
    //    Inspector 슬롯은 코드로 생성 불가한 에셋(AssetManifest) 하나만 사용.
    //
    //  ▶ 생성/초기화 순서 (Awake)
    //    AssetLoadManager → SceneLoader → Gold/Inventory/FoodInv/Tools →
    //    DayManager → Shop → Phase → Input → Player
    //
    //  ▶ 생명주기 위임
    //    Start()     → Player.Start()    (씬 로드 후 플레이어 스폰)
    //    OnEnable()  → Input.OnEnable()  (InputSystem 활성화)
    //    OnDisable() → Input.OnDisable()
    //    Update()    → Input.Update()    (UI 토글 · 스킬 단축키)
    //    OnDestroy() → Player.Dispose()  (sceneLoaded 구독 해제)
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

        // 유일한 Inspector 슬롯 — 코드로 생성 불가한 에셋 참조만
        [Header("Assets")]
        [SerializeField] AssetManifest _manifest;

        // 매니저 인스턴스 — Awake 에서 new 로 생성, Inspector 슬롯 없음
        public AssetLoadManager AssetLoad   { get; private set; }
        public SceneLoader      SceneLoader { get; private set; }
        public GoldManager      Gold        { get; private set; }
        public Inventory        Inventory   { get; private set; }
        public FoodInventory    FoodInv     { get; private set; }
        public ToolManager      Tools       { get; private set; }
        public DayManager       Day         { get; private set; }
        public ShopManager      Shop        { get; private set; }
        public PhaseManager     Phase       { get; private set; }
        public InputManager     Input       { get; private set; }
        public PlayerManager    Player      { get; private set; }

        // ================================================================
        //  Mono
        // ================================================================

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateManagers();
            InitManagers();
        }

        void Start()    => Player.Start();
        void OnEnable() => Input?.OnEnable();
        void OnDisable()=> Input?.OnDisable();
        void Update()   => Input?.Update();
        void OnDestroy()=> Player?.Dispose();

        // ================================================================
        //  생성 — 의존 주입 (this = 코루틴 러너)
        // ================================================================

        void CreateManagers()
        {
            AssetLoad   = new AssetLoadManager(_manifest);
            SceneLoader = new SceneLoader(this);
            Gold        = new GoldManager();
            Inventory   = new Inventory();
            FoodInv     = new FoodInventory();
            Tools       = new ToolManager();
            Day         = new DayManager(this);
            Shop        = new ShopManager();
            Phase       = new PhaseManager();
            Input       = new InputManager();
            Player      = new PlayerManager();
        }

        // ================================================================
        //  초기화 — 순서가 곧 의존 관계를 표현한다
        // ================================================================

        void InitManagers()
        {
            AssetLoad.Init();   // 에셋 로딩 기반 — 가장 먼저
            SceneLoader.Init(); // 씬 전환 시스템
            Gold.Init();
            Inventory.Init();
            FoodInv.Init();
            Tools.Init();
            Day.Init();
            Shop.Init();        // GoldManager 메서드 사용 (Init 이후)
            Phase.Init();       // SceneLoader 메서드 사용
            Input.Init();       // InputSystem_Actions 생성
            Player.Init();      // Instance 설정 (Start 에서 스폰)
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (_manifest == null) Debug.LogWarning("[GlobalController] AssetManifest 미연결");
        }
#endif
    }
}
