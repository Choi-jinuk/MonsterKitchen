# Technical Architecture — 몬스터 키친 (가제)

> 최종 수정: 2026-04-15

---

## 1. 플랫폼 빌드 전략

| 플랫폼 | 빌드 타깃 | 특이사항 |
|---|---|---|
| PC (Windows/Mac) | StandaloneWindows64 / StandaloneMac | 기준 개발 환경 |
| Mobile iOS | iOS | 터치 입력, 성능 최적화 필수 |
| Mobile Android | Android | 다양한 디바이스 해상도 대응 |
| Web | WebGL 2.0 | 메모리 제한 주의, 로딩 최적화 |

**WebGL 주의사항**
- 멀티스레딩 미지원 → Job System 사용 시 주의
- 메모리 제한 (~512MB 권장) → Addressables 분할 로딩 필수
- 마이크/파일 접근 제한 → 저장은 PlayerPrefs 또는 서버 동기화

---

## 2. 씬 구조

```
Scenes/
├── Boot.unity          # 초기화, Addressables 로드, 로딩 화면
├── MainMenu.unity      # 타이틀, 세이브 선택, 설정
├── Game/
│   ├── Dungeon.unity   # 탑뷰 던전 탐험 (방 기반 액션)
│   └── Restaurant.unity  # 탑뷰 식당 운영
└── UI.unity            # (Additive) 전역 HUD, 팝업 레이어
```

**씬 전환 흐름**
```
Boot → MainMenu → [Dungeon ↔ Restaurant] (Game 내 전환)
```

---

## 3. 스크립트 폴더 구조

```
Assets/
├── Data/
│   ├── CSV/                        # CSV 원본 파일 (git 추적)
│   └── ScriptableObjects/          # 생성된 SO 에셋 (git 추적)
│       ├── Monsters/
│       ├── Ingredients/
│       ├── Recipes/
│       ├── DropTables/
│       └── Characters/
│
├── Editor/
│   └── DataPipeline/               # 양방향 데이터 관리 툴 (빌드 제외)
│       ├── DataManagerWindow.cs    # 통합 Editor 창 (메뉴: MonsterKitchen → Data Manager)
│       ├── Tabs/                   # Browser(조회/추가/삭제) / Statistics(시각화)
│       ├── Sync/                   # SyncCoordinator → CSV → SO → Excel 순차 동기화
│       ├── Core/                   # CsvParser, DataSchema, DataValidator
│       └── Importers/              # 타입별 SO 임포터
│
├── RawData/                        # Excel/XML 기획 원본 (git LFS 권장)
│
└── Scripts/
├── Core/
│   ├── GameManager.cs         # 전역 게임 상태 관리
│   ├── SceneLoader.cs         # 씬 전환 (Addressables 기반)
│   ├── EventBus.cs            # 시스템 간 이벤트 브로커
│   ├── SaveSystem.cs          # 저장/불러오기 (JSON + PlayerPrefs)
│   └── ServiceLocator.cs      # 의존성 해소
│
├── Dungeon/
│   ├── DungeonManager.cs      # 던전 진행, 방(Room) 관리
│   ├── RoomGenerator.cs       # 타일맵 기반 방 생성 (탑뷰)
│   ├── Monster/
│   │   ├── MonsterBase.cs     # 몬스터 기반 클래스
│   │   ├── MonsterAI.cs       # FSM 기반 AI (순찰/추적/공격/사망)
│   │   └── DropTable.cs       # 식재료 드롭 테이블 (ScriptableObject 참조)
│   └── DungeonReward.cs       # 층 클리어 보상
│
├── Player/
│   ├── PlayerController.cs    # 이동/점프/공격 (New Input System)
│   ├── PlayerStats.cs         # HP, 공격력 등 스탯
│   ├── PlayerSkill.cs         # 스킬 쿨타임, 궁극기 게이지
│   └── CharacterData.cs       # ScriptableObject — 캐릭터 정의
│
├── Combat/
│   ├── AttributeSystem.cs     # 속성 정의 (불꽃/전기/물/땅/바람/냉기/마법/독)
│   ├── DamageCalculator.cs    # 데미지 계산, 약점 배율
│   ├── FinishingBlow.cs       # 막타 속성 추적 → DropResolver에 전달
│   └── StatusEffect.cs        # 화상/감전/빙결/중독 등 상태이상
│
├── Drop/
│   ├── DropResolver.cs        # 막타 속성 + 처치 방식 → 드롭 재료 상태 결정
│   ├── DropTable.cs           # ScriptableObject — 몬스터별 부위/확률 정의
│   └── IngredientState.cs     # 재료 상태 Enum (Raw/Cooked/Frozen/Dried/Poisoned/etc)
│
├── Inventory/
│   ├── Inventory.cs           # 범용 인벤토리 (아이템 컨테이너)
│   ├── IngredientItem.cs      # 식재료 아이템 (ScriptableObject 참조)
│   └── EquipmentItem.cs       # 장비 아이템
│
├── Cooking/
│   ├── RecipeBook.cs          # 레시피 목록, 해금 관리
│   ├── CookingStation.cs      # 요리 제작 로직, 등급 산정
│   ├── RecipeData.cs          # ScriptableObject — 레시피 정의
│   └── FoodQuality.cs         # 요리 등급 Enum 및 계산
│
├── Restaurant/
│   ├── RestaurantManager.cs   # 식당 전반 관리
│   ├── MenuBoard.cs           # 메뉴판, 등록/삭제
│   ├── CustomerAI.cs          # 손님 AI (대기/주문/식사/결제/퇴장)
│   ├── ReputationSystem.cs    # 명성(Fame) 관리
│   └── RestaurantUpgrade.cs   # 좌석/주방/인테리어 업그레이드
│
├── Economy/
│   ├── CurrencyManager.cs     # 골드, 에테르, 젬 재화 관리
│   └── ShopSystem.cs          # 상점 (레시피 구매, 아이템 구매)
│
├── AutoFarm/
│   └── AutoExpedition.cs      # 자동 탐험 (소탕) — 시간 기반 재료 수급
│
├── UI/
│   ├── HUD/
│   │   ├── ResourceHUD.cs     # 상단 자원 표시
│   │   ├── DungeonHUD.cs      # 체력바, 스킬 버튼
│   │   └── RestaurantHUD.cs   # 골드, 명성, 메뉴 패널
│   ├── Panels/
│   │   ├── InventoryPanel.cs
│   │   ├── RecipePanel.cs
│   │   ├── UpgradePanel.cs
│   │   └── ResultPanel.cs     # 던전 결과 화면
│   └── Popups/
│       ├── DialoguePopup.cs   # NPC 대화
│       └── RewardPopup.cs     # 보상 연출
│
├── Input/
│   └── InputHandler.cs        # InputSystem_Actions 래퍼
│
├── Camera/
│   ├── DungeonCamera.cs       # Cinemachine 탑뷰 카메라 (플레이어 추적)
│   └── RestaurantCamera.cs    # Cinemachine 탑뷰 카메라 (드래그 패닝)
│
├── Data/                      # ScriptableObject 클래스 정의 (CSV 필드와 1:1 매핑)
│   ├── GameEnums.cs           # AttributeType, IngredientState, RarityType 등 공용 Enum
│   ├── MonsterData.cs         # 몬스터 SO 클래스
│   ├── IngredientData.cs      # 식재료 SO 클래스
│   ├── RecipeData.cs          # 레시피 SO 클래스
│   ├── DropTableData.cs       # 드롭 테이블 SO 클래스 (막타 속성별 분리)
│   └── CharacterData.cs       # 캐릭터 SO 클래스
│
└── DataAccess/
    └── DataRegistry.cs        # ID로 SO를 빠르게 조회하는 런타임 레지스트리
```

---

## 4. 핵심 아키텍처 결정

### 4.1 렌더링
- **URP 2D Renderer** 전용 — Built-in Pipeline 셰이더/머티리얼 사용 금지
- 던전 조명: Point Light 2D (횃불, 마법 이펙트) + Global Light 2D (전역 어두운 조명)
- 식당 조명: Global Light 2D (밝은 황금빛)
- 파티클: URP 호환 파티클 시스템 (Shader Graph 기반 이펙트)

### 4.2 입력 (크로스플랫폼)
- **New Input System** 전용 (`InputSystem_Actions.inputactions`)
- 액션 맵: `Dungeon` (이동/점프/공격/스킬) / `Restaurant` (클릭/드래그) / `UI` (공통)
- 모바일: 가상 버튼 UI → InputSystem 연결
- WebGL: 키보드+마우스 지원

### 4.3 에셋 관리
- 런타임 에셋: **Addressables** 기반 로드 (`Resources.Load` 금지)
- ScriptableObject: 몬스터/식재료/레시피/캐릭터 데이터 정의
- 씬 전환: `SceneLoader.cs`가 Addressables Scene 로드 처리

### 4.4 이벤트 시스템
- **EventBus** (Publish-Subscribe 패턴) — 시스템 간 직접 참조 최소화
- 예: `EventBus.Publish(new IngredientDropped(ingredientId, amount))`
- 던전 이벤트 → 인벤토리, 식당 이벤트 → UI, 경제 이벤트 → HUD

### 4.5 데이터 파이프라인
- **원본**: Excel (.xlsx) / XML → **CSV** → **ScriptableObject** 순서로 변환
- **관리 툴**: `Assets/Editor/DataPipeline/DataManagerWindow.cs` (Editor Only, 빌드 미포함)
  - 메뉴: `MonsterKitchen → Data Manager`
  - **양방향 동기화**: 툴에서 조회/추가/삭제 → CSV → SO → Excel 자동 반영
  - `;` 시작 행 = 주석, `_` 접두사 컬럼 = 무시 (보존은 됨)
  - Excel 쓰기: EPPlus (또는 NPOI) 라이브러리 사용
- **정적 데이터** (게임 규칙): ScriptableObject — CSV 임포트로 생성/갱신
- **런타임 조회**: `DataRegistry` — 부팅 시 ID→SO Dictionary 구성, O(1) 접근
- **런타임 상태** (플레이 중 변하는 값): C# 클래스 + EventBus 알림
- **저장 데이터** (플레이어 진행): JSON 직렬화 → `Application.persistentDataPath`
  - WebGL: PlayerPrefs 또는 서버 동기화 (LocalStorage 한계 주의)
- 상세 포맷/규칙: `docs/DATA_PIPELINE.md` 참조

### 4.6 카메라
- **Cinemachine** Virtual Camera 기반
- 던전/식당 모두 탑뷰 시점 통일
- 던전: `CinemachineVirtualCamera` — 플레이어 추적, 방 전환 시 블렌드
- 식당: `CinemachineVirtualCamera` — 고정 탑뷰, 드래그 패닝

### 4.7 FSM (유한 상태 머신)
- 몬스터 AI: `MonsterAI.cs` — Idle/Patrol/Chase/Attack/Dead
- 손님 AI: `CustomerAI.cs` — Entering/Waiting/Ordering/Eating/Paying/Leaving
- 플레이어: `PlayerController.cs` — Idle/Running/Jumping/Attacking/Skill/Dead

---

## 5. 패키지 목록

| 패키지 | 용도 |
|---|---|
| `com.unity.addressables` | 런타임 에셋 로드, WebGL 분할 로딩 |
| `com.unity.cinemachine` | 던전/식당 카메라 제어 |
| `com.unity.inputsystem` | 크로스플랫폼 입력 (모바일/PC/Web) |
| `com.unity.2d.animation` | 캐릭터/몬스터 스켈레탈 애니메이션 |
| `com.unity.2d.aseprite` | Aseprite 스프라이트 직접 임포트 |
| `com.unity.2d.tilemap` + extras | 던전 타일맵 기반 월드 구성 |
| `com.unity.2d.spriteshape` | 던전 유기적 지형 표현 |
| `com.unity.timeline` | 보스 연출, 컷씬 |
| `com.unity.feature.mobile` | iOS/Android 플랫폼 지원 |
| `com.unity.test-framework` | 유닛/통합 테스트 |
| `com.coplaydev.unity-mcp` | Claude Code MCP 연동 |

---

## 6. 코드 컨벤션

- **네임스페이스**: `MonsterKitchen.{System}` (예: `MonsterKitchen.Dungeon`, `MonsterKitchen.Cooking`)
- MonoBehaviour: 씬/오브젝트 생명주기와 직접 연결된 경우에만 사용
- 순수 로직: 일반 C# 클래스로 분리
- 데이터: ScriptableObject로 외부화
- 이벤트 통신: EventBus 우선, Unity Events는 인스펙터 연결 필요 시만
- 파일 경로: Addressable Key 사용, 하드코딩 경로 금지
