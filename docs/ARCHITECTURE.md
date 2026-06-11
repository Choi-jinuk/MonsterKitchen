# Technical Architecture — 몬스터 키친

> 최종 수정: 2026-06-01

---

## 1. 플랫폼 빌드 전략

| 플랫폼 | 빌드 타깃 | 비고 |
|---|---|---|
| PC (Windows) | StandaloneWindows64 | 기준 개발 환경 (Phase 1) |
| Mobile | iOS / Android | Phase 2 |

---

## 2. 씬 구조

| Build Index | 파일 | 역할 |
|---|---|---|
| **0** | `Assets/Scenes/StartScene.unity` | 앱 진입점. GlobalController·GameStartup·"Touch To Start" |
| **1** | `Assets/Scenes/ManagementScene.unity` | 기지. 던전 포털·상점·업그레이드 |
| **2** | `Assets/Scenes/KitchenScene.unity` | 주방. CookingStation 에서 재료 → 음식 |
| **3** | `Assets/Scenes/DungeonScene.unity` | 던전. 몬스터 처치 → 재료 드롭 |
| **4** | `Assets/Scenes/RestaurantScene.unity` | 식당. 손님 서빙 → 골드 획득 → 다음 날 |
| **5** | `Assets/Scenes/LoadingScene.unity` | 씬 전환 버퍼 (메모리 해제·GC) |

플레이는 반드시 **StartScene (Index 0)** 에서 시작.
DontDestroyOnLoad 싱글톤은 StartScene에서 생성 → 씬 전환 후 유지.

---

## 3. 스크립트 폴더 구조

```
Assets/Scripts/
├── Core/
│   ├── Startup/
│   │   ├── GameStartup.cs           — SDK/Data/Server/Save 순서 보장 시작 프로세스
│   │   └── StartSceneController.cs  — "Touch To Start" UI + 첫 씬 전환
│   ├── Managers/
│   │   ├── GlobalController.cs      — 전체 매니저 생성·초기화 총괄 (DontDestroyOnLoad)
│   │   ├── NetworkManager.cs        — 서버↔클라이언트 패킷 관리 (Server Stub 포함)
│   │   ├── PlayerDataManager.cs     — 런타임 상태 접근점 + Apply 메서드
│   │   ├── ServerDBManager.cs       — 저장/로드 (JSON ↔ PlayerData 변환)
│   │   ├── DayManager.cs            — 날짜 카운터 + 식당 영업 루프
│   │   ├── PhaseManager.cs          — 게임 페이즈 (아침/던전/저녁/식당)
│   │   ├── PlayerManager.cs         — 플레이어 스폰·참조 관리
│   │   └── InputManager.cs          — New Input System 래퍼 (C# 이벤트 배포)
│   ├── Scene/
│   │   ├── SceneLoader.cs           — 씬 전환 (DontDestroyOnLoad)
│   │   ├── SceneFader.cs            — 씬 전환 페이드 이펙트
│   │   └── SceneControllerBase.cs   — 씬별 컨트롤러 공통 베이스
│   ├── Asset/
│   │   ├── AssetKeys.cs             — 에셋 주소 키 상수
│   │   ├── AssetLoadManager.cs      — AssetManifest 기반 에셋 로드
│   │   ├── AssetManifest.cs         — key+asset 쌍 SO
│   │   └── IAssetLoader.cs          — 에셋 로더 인터페이스
│   ├── Camera/
│   │   └── CameraFollow.cs          — 카메라 추적
│   ├── Rendering/
│   │   ├── PerspectiveManager.cs    — pure static class, tiltAngleDeg 읽기
│   │   ├── PerspectiveEntityTilt.cs — 엔티티 Z = Y·tan(tilt) 동기화
│   │   ├── PerspectiveMapTilt.cs    — 배경/맵 GO X 회전 + GameConfig 등록
│   │   └── PerspectiveCameraSync.cs — 카메라 Z 동기화
│   ├── Pool/
│   │   ├── ObjectPool.cs            — 제네릭 오브젝트 풀
│   │   └── PooledList.cs            — 풀 기반 리스트
│   ├── LocaleManager.cs             — 다국어 문자열 싱글톤 (StringTable SO)
│   └── Util/
│       ├── CommonString.cs          — 자주 쓰는 문자열 상수
│       ├── RandomUtil.cs            — 유틸리티 난수
│       ├── DebugUtil.cs             — 디버그 헬퍼
│       └── StringUtil.cs            — 문자열 유틸
│
├── Data/
│   ├── Table/
│   │   ├── TableData.cs             — 정적 테이블 SO 집약 컨테이너 [Layer 1]
│   │   ├── DataRegistry.cs          — ID → 데이터 O(1) 조회 레지스트리
│   │   ├── GameEnums.cs             — 공용 열거형 (AttributeType, IngredientState…)
│   │   ├── GameConfig.cs            — 맵 공통 설정 SO (tiltAngleDeg 등)
│   │   ├── AbilEntry.cs             — AbilType + AbilEntry struct
│   │   ├── MonsterTable.cs          — 몬스터 데이터 (CSV 직렬화)
│   │   ├── IngredientTable.cs       — 재료 데이터
│   │   ├── RecipeTable.cs           — 레시피 데이터
│   │   ├── FoodTable.cs             — 완성 요리 데이터
│   │   ├── DropTable.cs             — 드롭 테이블 데이터
│   │   ├── WeaponTable.cs           — 무기 데이터
│   │   ├── SkillStepTable.cs        — 단일 스킬 타격 수치 (구 SkillData)
│   │   ├── SkillGroupTable.cs       — 스킬 그룹 (콤보 체인 소유)
│   │   ├── DungeonSpawnTable.cs     — 던전 스폰 테이블
│   │   ├── PlayerCharTable.cs       — 플레이어 캐릭터 정의
│   │   ├── GatheringToolTable.cs    — 채집 도구 데이터
│   │   ├── ResourceNodeTable.cs     — 자원 노드 데이터
│   │   ├── StringTable.cs           — 다국어 문자열 테이블
│   │   └── UITable.cs               — UI 텍스트/레이아웃 데이터
│   ├── Player/
│   │   ├── PlayerCoinData.cs        — 골드 상태 + OnGoldChanged 이벤트 [Layer 2]
│   │   ├── PlayerInventoryData.cs   — 재료·음식 인벤토리 + 이벤트 [Layer 2]
│   │   └── PlayerUpgradeData.cs     — 업그레이드 레벨 + 이벤트 [Layer 2]
│   ├── Server/
│   │   └── ServerSaveData.cs        — JSON 직렬화 DTO [Layer 3]
│   ├── Util/
│   │   ├── DataTable.cs             — 제네릭 데이터 테이블 컨테이너
│   │   └── SerializedDictionary.cs  — Unity 직렬화 딕셔너리
│   └── Pipeline/
│       ├── CsvParser.cs             — CSV → Dictionary 파싱
│       ├── CsvReadWriter.cs         — CSV 읽기/쓰기 (주석·무시컬럼 보존)
│       └── ScriptableObjectSync.cs  — SO 생성/갱신/삭제
│
├── AI/
│   └── BehaviorTree/
│       ├── (core)
│       │   ├── BTRunner.cs          — BT 실행기 (MonoBehaviour)
│       │   ├── BTAsset.cs           — BT 에셋 SO
│       │   ├── BTBlackboard.cs      — 블랙보드 (공유 상태)
│       │   ├── BTNode.cs            — 노드 베이스
│       │   ├── BTComposite.cs       — 복합 노드 베이스
│       │   ├── BTSelector.cs        — 셀렉터 (OR)
│       │   ├── BTSequence.cs        — 시퀀스 (AND)
│       │   ├── BTParallel.cs        — 병렬 노드
│       │   ├── BTCondition.cs       — 조건 노드
│       │   ├── BTDecorator.cs       — 데코레이터
│       │   ├── BTService.cs         — 서비스 노드
│       │   ├── BTInverter.cs        — 인버터
│       │   ├── BTNodeAttribute.cs   — 노드 속성
│       │   ├── BTNodeRegistry.cs    — 노드 레지스트리
│       │   ├── BTContext.cs         — 실행 컨텍스트
│       │   ├── BTStatus.cs          — 상태 열거형
│       │   └── IBTBlackboardInitializer.cs
│       ├── Monster/
│       │   ├── BTAction_Attack.cs
│       │   ├── BTAction_MoveToPlayer.cs
│       │   ├── BTAction_Patrol.cs
│       │   └── BTCondition_PlayerInRange.cs
│       └── Player/
│           ├── BTAction_PlayerAutoAttack.cs
│           ├── BTAction_PlayerDash.cs
│           ├── BTAction_PlayerMove.cs
│           ├── BTAction_PlayerSkill.cs
│           └── BTCondition_PlayerEnemyInAttackRange.cs
│
├── Player/
│   ├── PlayerController.cs          — 입력 → 이동/공격/대시
│   ├── PlayerStats.cs               — 무기·스킬 슬롯, 최종 스탯 계산
│   └── WeaponSocketController.cs    — WeaponSocket 스프라이트·애니메이터 교체
│
├── Enemy/
│   ├── Movement/
│   │   ├── MonsterMovementBase.cs
│   │   ├── SlimeMovement.cs
│   │   └── IMonsterSeparation.cs
│   ├── MonsterBase.cs               — 몬스터 공통 베이스
│   ├── BTMonsterController.cs       — BT 기반 몬스터 AI 컨트롤러
│   ├── EnemyHpBar.cs                — World Space HP 바
│   ├── SpawnManager.cs              — 몬스터 스폰 (InstantiateDisabled → Init → SetActive)
│   └── MonsterRespawnManager.cs     — 리스폰 관리
│
├── Combat/
│   ├── Health.cs                    — HP 컴포넌트 (TakeDamage, IsInvincible, 이벤트)
│   ├── Projectile.cs                — 투사체 (유도·AoE·직선)
│   ├── ItemDrop.cs                  — 드롭 아이템 물리 연출
│   ├── DropResolver.cs              — 막타 속성 기반 드롭 계산
│   ├── CrowdControlComponent.cs     — 넉백·풀인·스턴
│   └── CCType.cs                    — CC 타입 열거형
│
├── Navigation/
│   ├── NavGrid.cs                   — IsWalkable 격자
│   ├── NavPathfinder.cs             — A* 경로 탐색
│   ├── NavAgent.cs                  — 이동 위임
│   ├── NavObstacleLayer.cs          — 장애물 레이어
│   └── NavPolyObstacle.cs           — 폴리곤 장애물
│
├── Cooking/
│   ├── CookingStation.cs            — 조리 실행 (cookDuration → FoodInventory)
│   ├── CookingUI.cs                 — 주방 UI
│   ├── RecipeMatcher.cs             — 레시피 매칭 (FindMatchable / FindSlotMatch)
│   ├── KitchenExit.cs               — 주방 → 식당 전환
│   ├── RestaurantEntry.cs           — 식당 진입 처리
│   └── KitchenSceneController.cs    — 주방 씬 컨트롤러
│
├── Dungeon/
│   ├── DungeonRoom.cs               — 방 + 몬스터 등록 + 클리어 이벤트
│   ├── DungeonDoor.cs               — 방 간 이동 문
│   ├── DungeonExit.cs               — 던전 출구 + ManagementScene 귀환
│   ├── DungeonSceneController.cs    — 던전 씬 컨트롤러
│   ├── DungeonCameraConfiner.cs     — 씬 전환 카메라 경계
│   └── ResourceNode.cs              — 채집 자원 노드
│
├── Management/
│   ├── DungeonPortal.cs             — 던전 포털 상호작용
│   ├── EveningStarter.cs            — 저녁 시작 (Kitchen 전환)
│   ├── FarmManager.cs               — 농장 관리
│   ├── FarmPlot.cs                  — 농장 구획
│   ├── ShopUpgradeStation.cs        — 상점 업그레이드
│   ├── ToolUpgradeStation.cs        — 도구 업그레이드
│   └── ManagementSceneController.cs — 관리 씬 컨트롤러
│
├── Restaurant/
│   ├── CustomerAI.cs                — 손님 FSM (Entering→Seated→Waiting→Served→Leaving)
│   ├── ServingSystem.cs             — 서빙 (TryServe, E키)
│   ├── RestaurantTable.cs           — 테이블 상태 관리
│   ├── RestaurantSetup.cs           — 식당 씬 초기화
│   ├── RestaurantOpener.cs          — 영업 시작 트리거
│   └── RestaurantSceneController.cs — 식당 씬 컨트롤러
│
├── UI/
│   ├── Core/
│   │   ├── UILayer.cs               — UI 레이어 열거형
│   │   ├── UIManager.cs             — 싱글톤 UI 관리자 (팝업 스택, ESC)
│   │   ├── UIPanel.cs               — 패널 베이스
│   │   └── LocalizedLabel.cs        — 다국어 라벨 컴포넌트
│   ├── HUD/
│   │   ├── GameHUD.cs               — 골드·Day·HP·스킬 쿨타임 (UI Toolkit)
│   │   ├── InteractionPrompt.cs     — 상호작용 말풍선
│   │   └── InventoryUI.cs           — 인벤토리 UI
│   ├── Combat/
│   │   ├── DamagePopup.cs           — TMP 데미지 팝업 (풀 기반)
│   │   ├── DamagePopupManager.cs    — 팝업 풀 관리
│   │   └── DamagePopupTrigger.cs    — 피격 이벤트 → 팝업 트리거
│   └── Reward/
│       ├── RewardEntry.cs           — 보상 항목 데이터
│       └── RewardPopup.cs           — 보상 팝업 UI
│
└── ZString/                         — (third-party, 수정 금지)
```

---

## 4. 핵심 아키텍처

### 4.1 렌더링
- **URP 2D Renderer** (`Assets/Settings/Renderer2D.asset`) 전용.
- Built-in Pipeline 셰이더/머티리얼 사용 금지.
- 모든 씬에 `UniversalAdditionalCameraData` + `Global Light 2D` 필수.

---

### 4.2 데이터 4-레이어 아키텍처

```
┌──────────────────────────────────────────────────────────────┐
│  Layer 1: TableData (ScriptableObject)                       │
│  역할: 정적 게임 정의 — 읽기 전용                               │
│  예: MonsterTable, WeaponTable, PlayerCharTable,             │
│      IngredientTable, RecipeTable, FoodTable, DropTable,     │
│      SkillStepTable, SkillGroupTable, StringTable, UITable   │
│  흐름: Excel → CSV → DataManagerWindow(Sync) → SO           │
│  접근: DataRegistry.Instance.GetMonster(id) 등               │
└──────────────────────────────┬───────────────────────────────┘
                               │ 읽기만
┌──────────────────────────────▼───────────────────────────────┐
│  Layer 2: PlayerData (순수 C# 클래스)                         │
│  역할: 런타임 게임플레이 상태 — 플레이 중 변이                   │
│  구성: PlayerCoinData · PlayerInventoryData · PlayerUpgradeData│
│  접근점: PlayerDataManager.Instance.Coin / Inventory / Upgrades│
│  변경: NetworkManager.Request*() 경유만 허용                   │
└──────────────────────────────┬───────────────────────────────┘
          Request*() ↑ ↓ Apply*()
┌──────────────────────────────▼───────────────────────────────┐
│  NetworkManager (순수 C# 클래스)                              │
│  역할: 모든 PlayerData 변경을 서버에 중계하고 콜백으로 반영      │
│  현재: Server Stub (로컬 연산) — HTTP/WebSocket 으로 교체 예정  │
│  흐름: Client → Request*() → [SERVER STUB] → Apply*() → 콜백 │
└──────────────────────────────┬───────────────────────────────┘
                    저장 ↕ 로드
┌──────────────────────────────▼───────────────────────────────┐
│  Layer 3: ServerSaveData (JSON)                              │
│  역할: 영속 저장 — 디스크/서버 직렬화                           │
│  관리: ServerDBManager (singleton, pure C# class)            │
│  현재: Application.persistentDataPath/save.json              │
│  향후: HTTP 서버 API 교체 가능 (ServerDBManager 만 수정)        │
└──────────────────────────────────────────────────────────────┘
```

**데이터 변경 흐름 (예: 골드 획득)**
```
CustomerAI.EatAndPay()
  → NetworkManager.Instance.RequestEarnGold(pay)
       ↓ [SERVER STUB: newGold = gold + pay]
  → PlayerDataManager.Instance.ApplyGold(newGold)
       ↓
  → PlayerCoinData.SetGold(newGold)  →  OnGoldChanged 이벤트
       ↓
  → GameHUD.UpdateGold(newGold)      (UI 갱신)
```

---

### 4.3 NetworkManager — Request*/Server Stub 패턴

클라이언트 코드는 PlayerData 를 **직접 수정하지 않는다**.
모든 변경은 NetworkManager.Request*() → [SERVER STUB] → PlayerDataManager.Apply*() 경로.

```csharp
// ✅ 올바른 패턴
NetworkManager.Instance.RequestEarnGold(pay);
NetworkManager.Instance.RequestAddIngredient(id, qty);
NetworkManager.Instance.RequestCook(recipe, grade, (success, food) => { ... });
NetworkManager.Instance.RequestServeFood(id, (success, grade, remaining) => { ... });
NetworkManager.Instance.RequestUpgrade(PlayerUpgradeType.ToolDamage, success => { ... });

// ❌ 잘못된 패턴 (직접 수정 금지)
PlayerDataManager.Instance.ApplyGold(newGold);
PlayerDataManager.Instance.Coin.SetGold(newGold);
```

**Server Stub 교체 방법**
각 Request*() 메서드 내 `[SERVER STUB START] ~ [SERVER STUB END]` 블록을
HTTP 요청으로 교체하고 응답 파싱 후 Apply*() 를 호출한다.

---

### 4.4 싱글톤 & DontDestroyOnLoad

StartScene 에서 생성되어 씬 전환 후에도 유지. 중복 인스턴스는 자신을 Destroy.

| 클래스 | 역할 |
|---|---|
| `GlobalController` | 전체 매니저 총괄 (MonoBehaviour) |
| `SceneLoader` | 씬 전환 |
| `PhaseManager` | 게임 페이즈 관리 |
| `DayManager` | 날짜 카운터 |
| `PlayerDataManager` | 런타임 상태 접근점 |
| `NetworkManager` | 서버 패킷 중계 |
| `ServerDBManager` | 저장/로드 |
| `LocaleManager` | 다국어 문자열 관리 |

---

### 4.5 2.5D 원근 효과

배경/맵 GO를 X 축 기울이고, 엔티티 Z = Y * tan(tilt°) 로 동기화.
Perspective 카메라가 Z 거리 차이를 자연스러운 원근감으로 변환.

- tilt 값 관리: `GameConfig.asset` 단일 SO (`tiltAngleDeg` 필드)
- `PerspectiveMapTilt` → 씬 로드 시 `GameConfig.Current` 자동 등록
- `PerspectiveManager` → pure static class, `GameConfig.Current.tiltAngleDeg` 읽기 (미로드 시 기본값 5°)
- 전 씬 카메라: Perspective FOV=55, TransparencySort=Distance

---

### 4.6 BehaviorTree 아키텍처

`BTRunner` (MonoBehaviour) → `BTAsset` (SO) → 노드 트리 실행.
`BTBlackboard` — 공유 상태 저장소.

**몬스터 BT 구조 (Selector 기반)**
```
Selector
  ├── Sequence [공격]
  │     ├── BTCondition_PlayerInRange (attackRange)
  │     └── BTAction_Attack
  ├── Sequence [추적]
  │     ├── BTCondition_PlayerInRange (detectRange)
  │     └── BTAction_MoveToPlayer
  └── BTAction_Patrol
```

**플레이어 BT 구조 (Parallel 기반)**
```
Parallel
  ├── BTAction_PlayerMove         (항상 실행)
  ├── BTAction_PlayerDash         (Space 입력 시)
  ├── BTAction_PlayerAutoAttack   (공격 범위 내 적 존재 시)
  └── BTAction_PlayerSkill        (Q/R/F 입력 시)
```

---

### 4.7 데이터 클래스 순수성 규칙

`TableData` 에 포함되는 모든 데이터 클래스는 **CSV 에서 읽을 수 있는 값만** 가져야 한다.

```csharp
// ❌ 금지 — Unity Object 직접 참조
public Sprite sprite;
public GameObject prefab;

// ✅ 허용 — string 주소 키 + uint ID
public string spriteAddress;  // AssetLoadManager.Load<Sprite>(key)
public uint   dropTableId;    // DataRegistry.GetDropTable(id)
```

---

### 4.8 SpawnManager 소환 패턴

```
1. InstantiateDisabled(prefab)  → Awake/OnEnable 실행 안 됨
2. entity.Init(data)            → GetComponent + 스탯 세팅
3. entity.SetActive(true)       → OnEnable → 코루틴/입력 활성화
```

---

### 4.9 Inspector 캐싱 원칙

씬 오브젝트 참조는 `[SerializeField]` 로 Inspector 에서 연결한다.
`FindObjectsByType`, `FindFirstObjectByType`, `GameObject.Find` 사용 금지.

---

## 5. 코딩 컨벤션

### 5.1 네이밍 규칙 (Rider C# 컨벤션)

| 대상 | 규칙 | 예시 |
|---|---|---|
| **private/protected 인스턴스 필드** | `m_` + PascalCase | `m_AliveCount`, `m_IsPopup`, `m_FacingDir` |
| **static readonly 필드** | `s_` + PascalCase | `s_HashMoveX`, `s_WaitFixed`, `s_OverlapBuffer` |
| **상수 (`const`)** | PascalCase | `MaxRetries`, `DefaultSpeed` |
| **public 프로퍼티** | PascalCase | `IsCleared`, `PanelId`, `Layer` |
| **이벤트** | PascalCase | `OnRoomCleared`, `OnDeath`, `OnDamaged` |
| **메서드 (모든 접근 제한자)** | PascalCase | `RegisterMonsters`, `TriggerCleared` |
| **로컬 변수** | camelCase | `hp`, `target`, `searchRange` |
| **파라미터** | camelCase | `monsters`, `amount`, `visible` |

### 5.2 선언 스타일

```csharp
// ✅ [SerializeField] — private 키워드 생략
[SerializeField] UILayer m_Layer    = UILayer.Panel;
[SerializeField] bool    m_IsPopup  = false;

// ✅ private 일반 필드 — private 키워드 생략
int  m_AliveCount;
bool m_Cleared;

// ✅ static readonly — s_ 접두사
static readonly int               s_HashMoveX = Animator.StringToHash("MoveX");
static readonly WaitForFixedUpdate s_WaitFixed = new WaitForFixedUpdate();

// ✅ private 메서드 — private 키워드 생략
void OnMonsterDied() { ... }

// ✅ public/protected — 접근 제한자 명시
public bool IsCleared => m_Cleared;
protected virtual void Awake() { ... }
```

### 5.3 정렬(Alignment)

```csharp
[SerializeField] UILayer m_Layer     = UILayer.Panel;
[SerializeField] bool    m_IsPopup   = false;
[SerializeField] Key     m_ToggleKey = Key.None;

static readonly int s_HashMoveX  = Animator.StringToHash("MoveX");
static readonly int s_HashMoveY  = Animator.StringToHash("MoveY");
static readonly int s_HashSpeed  = Animator.StringToHash("Speed");
```

### 5.4 파일·클래스 주석 구조

```csharp
// ====================================================================
//  ClassName — 한 줄 설명
//
//  ▶ 소항목
//    내용
// ====================================================================

// 클래스 내부 섹션 구분
// ── 섹션 이름 ─────────────────────────────────────────────────────

// ================================================================
//  Region Title
// ================================================================
```

---

## 6. 데이터 파이프라인 (정적 데이터)

```
Assets/Data/CSV/    ← Excel 에서 내보낸 원본 CSV (진리의 원천)
Assets/Data/SO/     ← CSV 에서 동기화된 ScriptableObject
Assets/Editor/DataPipeline/DataManagerWindow.cs  ← 동기화 에디터 창
```

- 메뉴: `MonsterKitchen → Data Manager → Sync All SO`
- `;` 시작 행 = 주석, `_` 접두사 컬럼 = 무시, `|` = 배열 구분자

SO ID 규칙:

| 타입 | 접두사 | 예시 |
|---|---|---|
| Monster | `MON_` | `MON_001` |
| Ingredient | `ING_` | `ING_001` |
| Recipe | `RCP_` | `RCP_001` |
| Food | `FOOD_` | `FOOD_001` |
| DropTable | `DRP_` | `DRP_001` |
| Weapon | `WPN_` | `WPN_001` |
| SkillStep | `SKS_` | `SKS_001` |
| SkillGroup | `SGD_` | `SGD_001` |
| PlayerChar | `CHR_` | `CHR_001` |

---

## 7. 설치된 패키지

| 패키지 | 용도 |
|---|---|
| `com.unity.addressables` | 에셋 로딩 |
| `com.unity.cinemachine` | 카메라 전환 |
| `com.unity.inputsystem` | 플레이어 입력 (New Input System) |
| `com.unity.2d.animation` | 2D 스켈레탈 애니메이션 |
| `com.unity.2d.aseprite` | Aseprite 스프라이트 직접 임포트 |
| `com.unity.2d.tilemap` + extras | 타일맵 기반 맵 |
| `com.unity.2d.spriteshape` | 유기적 지형 |
| `com.unity.timeline` | 컷씬 |
| `com.unity.feature.mobile` | 모바일 빌드 지원 |
| `com.unity.test-framework` | 유닛 테스트 |
| `com.coplaydev.unity-mcp` | Claude Code ↔ Unity Editor MCP 연동 |

---

## 8. AI 도구 — Claude Code 플러그인

### 설치된 플러그인

| 플러그인 | 용도 |
|---|---|
| **superpowers** | brainstorming, writing-plans, systematic-debugging, TDD, verification, code review |
| **caveman** | 토큰 절약 커뮤니케이션 모드 |
| **karpathy-skills** | 코딩 품질 가이드라인 |
| **understand-anything** | 코드베이스 지식 그래프 분석 |
| **watch/claude-video** | 영상(URL/로컬) 분석 |

### 새 기능 표준 워크플로

```
brainstorming → writing-plans → (승인) → executing-plans → verification → commit
```

### MCP Unity Tools

| 도구 | 용도 |
|---|---|
| `manage_scene` | 씬 로드/저장/쿼리 |
| `manage_gameobject` | GO 생성/수정/삭제 |
| `manage_components` | 컴포넌트 추가/설정 |
| `manage_script` | 스크립트 생성/수정 |
| `read_console` | 컴파일 에러·로그 확인 |
| `find_gameobjects` | 씬 내 GO 검색 |
| `execute_code` | 에디터 C# 코드 즉시 실행 (Windows MAX_PATH 주의) |
| `manage_prefabs` | 프리팹 생성/수정/저장 |
