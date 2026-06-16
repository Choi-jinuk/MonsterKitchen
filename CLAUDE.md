# CLAUDE.md

> 최종 갱신: 2026-06-01

---

## 프로젝트

**몬스터 키친 (MonsterKitchen)** — Unity 6 (6000.4.2f1) URP 2D 타이쿤.
폴더명 `FantasyTycoon`, 게임명/네임스페이스 **MonsterKitchen**.
사이클: 던전(재료) → 주방(요리) → 식당(운영) → 다음 날.
빌드: Desktop 우선, Mobile Phase 2.

---

## Unity CLI

```bash
# Editor 열기
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon"

# PlayMode 테스트
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform PlayMode -testResults results.xml -batchmode -quit

# EditMode 테스트
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit

# Windows 빌드
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -buildTarget Win64 -batchmode -quit
```

테스트 위치: `Assets/Tests/` (EditMode), `Assets/Tests/PlayMode/`.

---

## 씬 구조

| Build Index | 파일명 | 역할 |
|---|---|---|
| **0** | `Assets/Scenes/StartScene.unity` | 앱 진입점. GlobalController·Startup·"Touch To Start" |
| **1** | `Assets/Scenes/ManagementScene.unity` | 기지/마을. 던전 포털·저녁 시작·상점 |
| **2** | `Assets/Scenes/KitchenScene.unity` | 주방. CookingStation 재료→음식 |
| **3** | `Assets/Scenes/DungeonScene.unity` | 던전. 단일 오픈맵, 몬스터 리스폰, 가방(무게) 채집 후 귀환 |
| **4** | `Assets/Scenes/RestaurantScene.unity` | 식당. 서빙→골드→다음 날 |
| **5** | `Assets/Scenes/LoadingScene.unity` | 씬 전환 버퍼 (메모리 해제·GC) |

시작: **StartScene(Index 0)** 고정.

---

## 인게임 시작 프로세스 (GameStartup)

`Assets/Scripts/Core/Startup/GameStartup.cs` — 앱 시작 시 순서 보장.

| Phase | 역할 | 현재 |
|---|---|---|
| 1. SdkInit | Analytics/Firebase/Crash SDK 초기화 | Stub (TODO) |
| 2. DataLoad | Localization 초기화 → `DataRegistry.Load()` — TableData SO 로드 | 구현 완료 |
| 3. ServerConnect | 서버 인증 & WebSocket 핸드셰이크 | Stub (TODO) |
| 4. SaveLoad | `ServerDB.Load()` — 플레이어 세이브 로드 | 구현 완료 |

- `GlobalController.Start()` → `StartCoroutine(Startup.Run())` — **StartScene** 에서 실행
- Startup 완료 후 `StartSceneController` 가 "Touch To Start" 표시
- 입력 감지 → `SceneLoader.LoadScene("ManagementScene")`
- ManagementScene 첫 로드 시 `GlobalController.OnFirstManagementSceneLoaded` → `Player.Start()`
- 각 Phase는 독립 IEnumerator — UniTask/async 교체 가능
- `OnPhaseStart / OnPhaseComplete / OnComplete / OnError` 이벤트 제공
- DataLoad 실패 → `IsAborted = true`, 이후 단계 중단

---

## 아키텍처

### 렌더링
- **URP 2D Renderer** (`Assets/Settings/Renderer2D.asset`).
- 빌트인 파이프라인 셰이더/머티리얼 금지.
- 모든 씬: `UniversalAdditionalCameraData` + `Global Light 2D` 필수.

### 데이터 4-레이어

| 레이어 | 클래스 | 역할 |
|---|---|---|
| **TableData** | `TableData` SO + `DataRegistry` | 정적 정의 (CSV→SO, 읽기 전용) |
| **PlayerData** | `PlayerCoinData` · `PlayerInventoryData` · `PlayerUpgradeData` | 런타임 상태 |
| **NetworkManager** | `NetworkManager` | 변경 요청 중계 (Server Stub → 실 서버) |
| **ServerDB** | `ServerSaveData` + `ServerDBManager` | JSON 직렬화 |

**PlayerData 규칙:**
- 읽기: `PlayerDataManager.Instance.Coin.Gold` / `.Inventory.AllFoods` 직접 허용
- 변경: `NetworkManager.Instance.Request*()` 경유만
  - `RequestEarnGold(amount)`
  - `RequestAddIngredient(id, qty)`
  - `RequestCook(recipe, grade, callback)`
  - `RequestServeFood(id, callback)`
  - `RequestUpgrade(type, callback)`

### 싱글톤 (DontDestroyOnLoad)
ManagementScene 생성 → 씬 전환 후 유지. 중복 인스턴스 → `Destroy(gameObject)`.

| 클래스 | 위치 | 역할 |
|---|---|---|
| `SceneLoader` | `Core/Scene/SceneLoader.cs` | 씬 전환 |
| `PhaseManager` | `Core/Managers/PhaseManager.cs` | 페이즈 관리 |
| `DayManager` | `Core/Managers/DayManager.cs` | 날짜 + 영업 루프 |
| `PlayerDataManager` | `Core/Managers/PlayerDataManager.cs` | 런타임 상태 접근점 |
| `NetworkManager` | `Core/Managers/NetworkManager.cs` | 서버 중계 |
| `ServerDBManager` | `Core/Managers/ServerDBManager.cs` | 저장/로드 |
| `DataRegistry` | `Data/Table/DataRegistry.cs` | ID→데이터 조회 (pure C# class) |

**주의**: `DayManager` DontDestroyOnLoad → RestaurantScene서 GameManager 중복 파괴됨.
`RestaurantOpener`(StartDay) → GameManager 아닌 별도 오브젝트(RestaurantSetup)에 배치.
**GlobalController 배치**: StartScene 단독. ManagementScene에 없음.
**PlayerManager.Start()**: ManagementScene 첫 로드 시점(`GlobalController.OnFirstManagementSceneLoaded`)에서 호출 — DataRegistry 보장 후.

### SpawnManager 패턴
```
1. InstantiateDisabled(prefab)  → Awake/OnEnable 실행 안 됨
2. entity.Init(data)            → GetComponent + 스탯 세팅
3. entity.SetActive(true)       → OnEnable → 코루틴/입력 활성화
```
PlayerController는 씬 직접 배치 시 `Start()`에서 자동 Init.

---

## 스크립트 구조

```
Assets/Scripts/
├── Core/
│   ├── Startup/     GameStartup, StartSceneController
│   ├── Managers/    GlobalController, DayManager, PhaseManager, PlayerDataManager,
│   │                NetworkManager, ServerDBManager, PlayerManager, InputManager
│   ├── Scene/       SceneLoader, SceneFader, SceneControllerBase
│   ├── Asset/       AssetKeys, AssetLoadManager, AssetManifest, IAssetLoader
│   ├── Camera/      CameraFollow, CinemachineAutoFollow
│   ├── Rendering/   PerspectiveManager, PerspectiveEntityTilt, PerspectiveMapTilt,
│   │                PerspectiveCameraSync
│   ├── Pool/        ObjectPool, PooledList
│   └── Util/        FontPreloader, CommonString, RandomUtil, DebugUtil, StringUtil, Loc
│
├── Data/
│   ├── Table/       TableData, DataRegistry, GameEnums, GameConfig, AbilEntry
│   │                MonsterTable, IngredientTable, RecipeTable, FoodTable,
│   │                DropTable, WeaponTable, SkillStepTable, SkillGroupTable,
│   │                DungeonSpawnTable, PlayerCharTable, GatheringToolTable,
│   │                ResourceNodeTable, UITable
│   ├── Player/      PlayerCoinData, PlayerInventoryData, PlayerUpgradeData  [Layer 2]
│   ├── Server/      ServerSaveData  [Layer 3]
│   ├── Util/        DataTable<T>, SerializedDictionary
│   └── Pipeline/    CsvParser, CsvReadWriter, ScriptableObjectSync
│
├── AI/
│   └── BehaviorTree/
│       ├── (core)   BTRunner, BTAsset, BTBlackboard, BTNode, BTComposite,
│       │            BTSelector, BTSequence, BTParallel, BTCondition, BTDecorator,
│       │            BTService, BTInverter, BTNodeAttribute, BTNodeRegistry,
│       │            BTContext, BTStatus, IBTBlackboardInitializer
│       ├── Monster/ BTAction_Attack, BTAction_MoveToPlayer, BTAction_Patrol,
│       │            BTCondition_PlayerInRange
│       └── Player/  BTAction_PlayerAutoAttack, BTAction_PlayerDash,
│                    BTAction_PlayerMove, BTAction_PlayerSkill,
│                    BTCondition_PlayerEnemyInAttackRange
│
├── Player/          PlayerController, PlayerStats, WeaponSocketController
│
├── Enemy/
│   ├── Movement/    MonsterMovementBase, SlimeMovement, IMonsterSeparation
│   └── (root)       MonsterBase, MonsterAI, BTMonsterController, EnemyHpBar,
│                    SpawnManager
│
├── Combat/          Health, Projectile, ItemDrop, DropResolver,
│                    CrowdControlComponent, CCType
├── Navigation/      NavGrid, NavPathfinder, NavAgent, NavObstacleLayer, NavPolyObstacle
├── Cooking/         CookingStation, CookingUI, RecipeMatcher, KitchenExit,
│                    RestaurantEntry, KitchenSceneController
├── Dungeon/         DungeonMapController, DungeonSpawnZone, DungeonBag, DungeonExit,
│                    DungeonCameraConfiner, ResourceNode
├── Management/      DungeonPortal, EveningStarter, FarmManager, FarmPlot,
│                    ShopUpgradeStation, ToolUpgradeStation, ManagementSceneController
├── Restaurant/      CustomerAI, ServingSystem, RestaurantTable, RestaurantSetup,
│                    RestaurantOpener, RestaurantSceneController
├── UI/
│   ├── Core/        UILayer, UIManager, UIPanel, LocalizedLabel
│   ├── HUD/         GameHUD, InteractionPrompt, InventoryUI
│   ├── Combat/      DamagePopup, DamagePopupManager, DamagePopupTrigger
│   └── Reward/      RewardEntry, RewardPopup
└── ZString/         (third-party, 수정 금지)
```

---

## 코드 컨벤션

### 네이밍 (Rider C#)

| 대상 | 규칙 | 예시 |
|---|---|---|
| private/protected 인스턴스 필드 | `m_` + PascalCase | `m_AliveCount`, `m_FacingDir` |
| static readonly 필드 | `s_` + PascalCase | `s_HashMoveX`, `s_WaitFixed` |
| const | PascalCase | `MaxRetries` |
| public 프로퍼티 | PascalCase | `IsCleared`, `PanelId` |
| 이벤트 | PascalCase | `OnRoomCleared`, `OnDeath` |
| 메서드 (전체) | PascalCase | `RegisterMonsters` |
| 로컬 변수 | camelCase | `hp`, `target` |
| 파라미터 | camelCase | `monsters`, `amount` |

**필드 선언:**
```csharp
// [SerializeField] — private 생략
[SerializeField] UILayer m_Layer   = UILayer.Panel;
[SerializeField] bool    m_IsPopup = false;

// private 필드 — private 생략
int  m_AliveCount;
bool m_Cleared;

// static readonly — s_ 접두사
static readonly int                s_HashMoveX = Animator.StringToHash("MoveX");
static readonly WaitForFixedUpdate s_WaitFixed = new WaitForFixedUpdate();
```

**코드 구조:**
```csharp
// ── 섹션 구분 ──────────────────────────────────────────────────────

// ================================================================
//  Region Title
// ================================================================

// 파일 상단
// ====================================================================
//  ClassName — 한 줄 설명
//
//  ▶ 소항목
//    내용
// ====================================================================
```

**접근 제한자**: `private` 생략. `public`/`protected`/`internal` 명시.

**정렬**: 같은 그룹 필드는 스페이스로 세로 정렬.
```csharp
[SerializeField] UILayer m_Layer     = UILayer.Panel;
[SerializeField] bool    m_IsPopup   = false;
[SerializeField] Key     m_ToggleKey = Key.None;

static readonly int s_HashMoveX = Animator.StringToHash("MoveX");
static readonly int s_HashMoveY = Animator.StringToHash("MoveY");
static readonly int s_HashSpeed = Animator.StringToHash("Speed");
```

### 네임스페이스
`MonsterKitchen.{System}`: Core, Player, Enemy, Combat, Inventory, Kitchen, Restaurant, Dungeon, Data.

### 입력
New Input System (`InputSystem_Actions.inputactions`) 전용. `Input.GetKey` 등 레거시 금지.

### 에셋 로딩
- `Resources.Load` 금지.
- `AssetLoadManager.Instance.Load<T>(key)` 경유.
- 키: `AssetKeys` 정적 클래스 또는 헬퍼 (`AssetKeys.MonsterPrefab(id)`).
- 런타임 에셋 → `AssetManifest` SO에 `key + asset` 쌍 등록.

### 데이터 클래스 순수성 규칙

TableData 데이터 클래스 — CSV 읽기 가능한 값만.

```csharp
// ❌ Unity Object 직접 참조 금지
public Sprite                    icon;
public RuntimeAnimatorController animCtrl;
public MonsterBase               prefab;
public GameObject                go;

// ✅ string 주소 키 + uint ID
public string spriteAddress;   // "sprite/ingredient/{id}"
public string prefabAddress;   // "prefab/monster/{id}"
public string animAddress;     // "anim/weapon/{id}"
public uint   dropTableId;     // DataRegistry.GetDropTable(id)
public uint   resultFoodId;    // DataRegistry.GetFood(id)
```

**런타임 로딩:**
```csharp
var sprite = AssetLoadManager.Instance?.Load<Sprite>(data.spriteAddress);
var food   = DataRegistry.Instance?.GetFood(recipe.resultFoodId);
```

### LayerMask 직렬화
`{"value": 256}` 형태 (Layer 8 = Enemy). `manage_components` 설정 시 int 아닌 object로.

### Inspector 캐싱 원칙

씬 오브젝트 참조 → `[SerializeField]` Inspector 직접 연결.

```csharp
// ❌ 금지 — GC/성능 비용, 버그 추적 어려움
FindObjectsByType<T>()
FindFirstObjectByType<T>()
FindObjectsOfType<T>()
GameObject.Find()
transform.Find()

// ✅ 동적 스폰 오브젝트만 허용, 필드에 캐시
Transform m_CachedPlayer;
Transform GetPlayer()
{
    if (m_CachedPlayer != null) return m_CachedPlayer;
    m_CachedPlayer = GameObject.FindGameObjectWithTag("Player")?.transform;
    return m_CachedPlayer;
}

// ✅ Instantiate — 몬스터·투사체 등 런타임 생성만
Instantiate(prefab, pos, Quaternion.identity);
```

null 참조 → `Debug.LogError(message, this)` 후 기능 skip. `OnValidate()`에서도 경고. AutoDiscover fallback 금지.

---

## 데이터 파이프라인

```
Assets/Data/CSV/      ← Excel 원본 CSV (`;` 주석, `_` 무시 컬럼)
Assets/Data/SO/       ← CSV 동기화 ScriptableObject
Assets/Editor/DataPipeline/DataManagerWindow.cs  ← 동기화 Editor 창
```

SO ID: MON_001~005 / ING_001~008 / RCP_001~005 / FOOD_001~005 / DRP_001~005.
CsvParser: `;` 주석, `_` 컬럼 무시, `#TYPE` 타입 지정, `|` 배열 구분자.

---

## 패키지

| 패키지 | 용도 |
|---|---|
| `com.unity.addressables` | 에셋 로딩 |
| `com.unity.localization` | 다국어 (Strings 컬렉션 + CSV Import) |
| `com.unity.cinemachine` | 카메라 전환 |
| `com.unity.inputsystem` | 플레이어 입력 |
| `com.unity.2d.animation` | 2D 스켈레탈 애니메이션 |
| `com.unity.2d.aseprite` | Aseprite 임포트 |
| `com.unity.2d.tilemap` + extras | 타일맵 맵 |
| `com.unity.2d.spriteshape` | 유기적 지형 |
| `com.unity.timeline` | 컷씬 |
| `com.unity.feature.mobile` | 모바일 빌드 |
| `com.unity.test-framework` | 유닛 테스트 |
| `com.coplaydev.unity-mcp` | MCP Unity Editor 연동 |

---

## MCP 제약

- `execute_code`: Windows MAX_PATH 실패 가능 → `manage_components`/`manage_gameobject` 사용.
- 비활성 GameObject: `find_gameobjects`/`manage_gameobject` 이름 검색 안 될 수 있음.
- LayerMask: `{"value": N}` 오브젝트 형태.
- InstanceID: 씬 리로드 후 무효 → `by_name` 방식 사용.

---

## AI 능력 — 설치된 플러그인 & 스킬

### 플러그인 목록

| 플러그인 | 설명 |
|---|---|
| **superpowers** | 소프트웨어 엔지니어링 워크플로 스킬 묶음 |
| **caveman** | 토큰 절약 커뮤니케이션 모드 (현재 활성) |
| **karpathy-skills** | Andrej Karpathy 스타일 코딩 가이드라인 |
| **understand-anything** | 코드베이스 분석 & 지식 그래프 생성 |
| **watch/claude-video** | yt-dlp + ffmpeg 기반 영상 분석 |

### 핵심 스킬 — 사용 시점

#### 기획/설계
| 스킬 | 언제 | 용도 |
|---|---|---|
| `superpowers:brainstorming` | 새 기능/모듈 시작 전 | 요구사항 탐색 → 설계안 3종 → 스펙 문서 작성 |
| `superpowers:writing-plans` | 스펙 확정 후 | 단계별 구현 계획 작성 (`docs/superpowers/plans/`) |
| `superpowers:executing-plans` | 계획 실행 시 | 체크포인트 리뷰와 함께 계획 단계 실행 |

#### 구현 품질
| 스킬 | 언제 | 용도 |
|---|---|---|
| `superpowers:test-driven-development` | 기능/버그픽스 구현 전 | TDD 사이클 — 테스트 먼저 |
| `superpowers:systematic-debugging` | 버그/예외/예상 외 동작 발생 시 | 원인 추적 → 수정 |
| `superpowers:verification-before-completion` | 완료 선언 전 | 실제 동작 검증 체크리스트 |
| `andrej-karpathy-skills:karpathy-guidelines` | 코드 작성/리뷰 시 | LLM 코딩 실수 방지 가이드라인 |

#### 코드 리뷰
| 스킬 | 언제 | 용도 |
|---|---|---|
| `superpowers:requesting-code-review` | 기능 완료 후 | 코드 리뷰 요청 준비 |
| `superpowers:receiving-code-review` | 리뷰 수신 시 | 피드백 처리 방식 결정 |
| `caveman:cavecrew-reviewer` | 특정 파일/diff 리뷰 | 1줄 severity 태그 리뷰 |

#### 코드베이스 이해
| 스킬 | 언제 | 용도 |
|---|---|---|
| `understand-anything:understand` | 대규모 구조 파악 필요 시 | 지식 그래프 생성 |
| `understand-anything:understand-explain` | 특정 파일/함수 심층 설명 | 구조 설명 |
| `understand-anything:understand-diff` | PR/git diff 분석 | 변경 영향 파악 |

#### Unity MCP
| 스킬 | 언제 | 용도 |
|---|---|---|
| `unity-mcp-skill` | Unity Editor 작업 시 | MCP 도구 오케스트레이션 |

#### 유틸리티
| 스킬 | 언제 | 용도 |
|---|---|---|
| `caveman:caveman` | 토큰 절약 필요 시 | 압축 커뮤니케이션 |
| `caveman:caveman-commit` | 커밋 메시지 작성 | 노이즈 없는 커밋 |
| `watch:watch` | 동영상 참고 자료 분석 | URL/로컬 영상 분석 |

### 새 기능 개발 표준 워크플로

```
1. brainstorming      → 설계안 확정 + 스펙 문서 작성
2. writing-plans      → 단계별 구현 계획
3. (사용자 승인)
4. executing-plans    → 계획 실행 (체크포인트 리뷰)
5. verification       → 완료 검증
6. (사용자 요청 시) commit
```

### 버그 수정 표준 워크플로

```
1. systematic-debugging → 원인 파악
2. TDD (필요시)         → 재현 테스트 먼저
3. 수정
4. verification         → 회귀 확인
```

---

## 작업 규칙

1. 시작 전: `WORK_IN_PROGRESS.md` 단계 목록 작성.
2. 완료 후: `WORK_IN_PROGRESS.md` 완료 체크 업데이트.
3. 모듈 현황: `docs/MODULES.md` (전체 명세), `WORK_IN_PROGRESS.md` (완료 체크).
4. 씬 수정 후: `manage_scene(action: save)` 호출.
5. 스크립트 수정 후: `read_console` 컴파일 에러 확인.
6. 새 컴포넌트 추가 시: `isCompiling: false` 확인 후 씬 배치.
7. 새 기능 시작 전: `brainstorming` 스킬 → `writing-plans` 스킬 순서 필수.
8. 커밋: 사용자 명시적 요청 시만. 모듈 완료 후 "커밋할까요?" 확인 먼저.

---

## 현재 구현 상태

- **핵심 사이클**: Management → Dungeon(채집) → Kitchen(요리) → Restaurant(서빙) → 다음 날
- **플레이어**: 8방향 이동, 근거리/원거리 공격, 대시(무적), 속성 시스템, BT 기반 AI
- **몬스터**: BT(BTRunner) + FSM, 드롭, Separation Steering, A* 내비게이션
- **던전**: 단일 오픈맵 리워크 예정 (스펙: `docs/superpowers/specs/2026-06-03-dungeon-rework-design.md`)
  - DungeonMapController + DungeonSpawnZone(리스폰) + DungeonBag(무게 기반) + 미니맵
- **재료 품질**: 처치 방식(속성·CC·무기 등급) → IngredientQuality I/II/III → FoodGrade
- **인벤토리**: PlayerInventoryData (재료/음식 + 품질 side-dict), InventoryUI 품질 배지
- **주방**: RecipeMatcher + CookingStation (품질 기반 FoodGrade 도출)
- **식당**: 손님 AI(FSM), 서빙(E키), Perfect 서빙 30% 팁
- **저장**: SaveScheduler (dirty flag + 주기 저장 + ForceSave), ServerDBManager JSON
- **UI**: GameHUD (UIToolkit) — 골드, Day, HP바, 스킬 쿨타임, RewardPopup
- **다국어**: Unity Localization (`com.unity.localization`) — `Strings` 컬렉션, ko 기본/en + en→ko 폴백,
  미등록 키는 키 자체 표시. 코드: `Loc.Create(key)` / `Loc.Get(ref cache, key)`, UI 는 `StringChanged` 자가 갱신.
  문자열 편집: `Assets/Data/CSV/StringData.csv` (포맷 `Key,Id,Shared Comments,Korean(ko),English(en)`)
  → 메뉴 `MonsterKitchen/Localization/Import CSV`
- **다음 우선 작업**: 던전 리워크 (모듈 5-2, 5-4, 5-5, 5-6)

---

## 언어 규칙

- 작업 진행 중 설명·중간 메시지: **영어**
- 최종 완료 보고: **한글**
