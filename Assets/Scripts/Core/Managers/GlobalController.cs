using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  GlobalController — 씬 전환 후에도 유지되는 글로벌 매니저 총괄
    //
    //  ▶ 데이터 4-레이어
    //    TableData(DataRegistry) — 정적 게임 정의 (CSV → SO, 읽기 전용)
    //    PlayerDataManager       — 런타임 게임플레이 상태
    //    NetworkManager          — 서버 패킷 중계
    //    ServerDBManager         — 영속 저장/로드
    //
    //  ▶ 초기화 흐름
    //    Awake  : CreateManagers → InitManagers  (Instance 등록, 이벤트 연결)
    //    Start  : StartCoroutine(Startup.Run()) — StartScene 에서 모든 Phase 완료
    //    Player.Start() 는 ManagementSceneController.OnInit() 에서 호출
    //
    //  ▶ 시작 프로세스 (GameStartup)
    //    Phase 1 SdkInit       — 3rd-party SDK
    //    Phase 2 DataLoad      — DataRegistry.Load() (TableData)
    //    Phase 3 ServerConnect — 서버 인증
    //    Phase 4 SaveLoad      — ServerDB.Load()
    //    → 자세한 내용: Core/Startup/GameStartup.cs
    //
    //  ▶ 생명주기 위임
    //    OnEnable/OnDisable → Input
    //    Update             → Input
    //    OnDestroy          → Player.Dispose
    //
    //  ▶ 배치
    //    StartScene GlobalController GameObject 단독 배치.
    //    DontDestroyOnLoad — 씬 전환 후에도 유지.
    // ====================================================================

    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public class GlobalController : MonoBehaviour
    {
        public static GlobalController Instance { get; private set; }

        // 유일한 Inspector 슬롯 — 코드로 생성 불가한 에셋 참조만
        [Header("Assets")]
        [SerializeField] AssetManifest m_Manifest;
        [SerializeField] GameObject    m_MobileHUDPrefab;

        // ── 매니저 프로퍼티 ───────────────────────────────────────────────
        public AssetLoadManager  AssetLoad    { get; private set; }
        public SceneLoader       SceneLoader  { get; private set; }
        public PlayerDataManager PlayerData   { get; private set; }
        public NetworkManager    Network      { get; private set; }
        public DayManager        Day          { get; private set; }
        public PhaseManager      Phase        { get; private set; }
        public InputManager      Input        { get; private set; }
        public PlayerManager     Player       { get; private set; }
        public ServerDBManager   ServerDB     { get; private set; }
        public SaveScheduler     SaveSched    { get; private set; }
        public DataRegistry      Registry     { get; private set; }
        public LocaleManager     Locale       { get; private set; }
        public GameStartup       Startup      { get; private set; }

        // ── 편의 접근자 ───────────────────────────────────────────────────
        public int Gold => PlayerData?.Coin?.Gold ?? 0;

        // ================================================================
        //  Mono
        // ================================================================

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SpawnMobileHUD();
            CreateManagers();
            InitManagers();
        }

        void Start()
        {
            StartCoroutine(Startup.Run());
            SaveSched.StartAutoSaveCycle(this);
        }

        void OnEnable()  => Input?.OnEnable();
        void OnDisable() => Input?.OnDisable();
        void Update()    => Input?.Update();
        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) SaveSched?.ForceSave();
        }

        void OnApplicationQuit() => SaveSched?.ForceSave();

        void OnDestroy() => Player?.Dispose();

        // ================================================================
        //  모바일 HUD 스폰
        // ================================================================

        void SpawnMobileHUD()
        {
            if (m_MobileHUDPrefab == null) return;
            Instantiate(m_MobileHUDPrefab);  // MobileHUD.Awake 에서 DontDestroyOnLoad 처리
        }

        // ================================================================
        //  생성 — 의존 주입 (this = 코루틴 러너)
        // ================================================================

        void CreateManagers()
        {
            AssetLoad   = new AssetLoadManager(m_Manifest);
            SceneLoader = new SceneLoader(this);
            PlayerData  = new PlayerDataManager();
            Network     = new NetworkManager();
            Day         = new DayManager(this);
            Phase       = new PhaseManager();
            Input       = new InputManager();
            Player      = new PlayerManager();
            ServerDB    = new ServerDBManager();
            SaveSched   = new SaveScheduler();
            Registry    = new DataRegistry();
            Locale      = new LocaleManager();
            Startup     = new GameStartup();
        }

        // ================================================================
        //  초기화 — 순서가 곧 의존 관계를 표현한다
        //  NOTE: 데이터 로드(Registry.Load, ServerDB.Load)는 GameStartup 에서 처리
        // ================================================================

        void InitManagers()
        {
            AssetLoad.Init();    // 에셋 로딩 기반 — 가장 먼저
            SceneLoader.Init();  // 씬 전환 시스템
            PlayerData.Init();   // 런타임 상태 초기화
            Network.Init();      // 서버 요청 중계 (PlayerData 이후)
            Day.Init();
            Phase.Init();        // SceneLoader 메서드 사용
            Input.Init();        // InputSystem_Actions 생성
            Player.Init();       // Instance 설정 (Start 에서 스폰)
            ServerDB.Init();     // Instance 설정
            SaveSched.Init();    // Instance 설정
            Registry.Init();     // Instance 설정 (Load 는 GameStartup Phase 2)
            Locale.Init();       // Instance 설정 (Load 는 DataRegistry.Load 이후)
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_Manifest == null) DebugUtil.LogWarning("[GlobalController] AssetManifest 미연결");
        }
#endif
    }
}
