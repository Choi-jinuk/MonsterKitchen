# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Project Overview

**몬스터 키친 (MonsterKitchen)** — Unity 6 (6000.4.2f1) 기반 2D 타이쿤 게임.
폴더명은 `FantasyTycoon`이지만 실제 게임명/네임스페이스는 **MonsterKitchen**.

플레이 사이클: 던전 탐험(재료 획득) → 주방 요리 → 식당 운영 → 다음 날 반복.
빌드 타겟: Desktop (우선) + Mobile (Phase 2).

---

## Unity CLI 명령어

```bash
# Unity Editor 열기
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon"

# PlayMode 테스트 (headless)
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform PlayMode -testResults results.xml -batchmode -quit

# EditMode 테스트 (headless)
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit

# Windows 빌드
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -buildTarget Win64 -batchmode -quit
```

테스트는 `Assets/Tests/` (EditMode) 및 `Assets/Tests/PlayMode/`에 위치.

---

## 씬 구조 (Build Settings 순서)

| Build Index | 파일명 | 역할 |
|---|---|---|
| **0** | `Assets/Scenes/ManagementScene.unity` | 기지/마을. 던전 포털·저녁 시작·상점 업그레이드 오브젝트 배치 |
| **1** | `Assets/Scenes/KitchenScene.unity` | 주방. CookingStation에서 재료→음식 요리 |
| **2** | `Assets/Scenes/DungeonScene.unity` | 던전. 몬스터 처치 → 재료 드롭 → 던전 클리어 |
| **3** | `Assets/Scenes/RestaurantScene.unity` | 식당. 손님 서빙 → 골드 획득 → 다음 날 전환 |

플레이는 반드시 **ManagementScene(Build Index 0)** 에서 시작.

---

## 아키텍처 원칙

### 렌더링
- **URP 2D Renderer** (`Assets/Settings/Renderer2D.asset`) 사용.
- 빌트인 파이프라인 셰이더/머티리얼 사용 금지.
- 모든 씬에 `UniversalAdditionalCameraData` + `Global Light 2D` 필수.

### 싱글톤 & DontDestroyOnLoad
아래 매니저는 ManagementScene에서 생성되어 씬 전환 후에도 유지된다.
새 씬에서 같은 타입의 두 번째 인스턴스가 생기면 `Destroy(gameObject)`로 자신을 제거한다.

| 클래스 | 위치 | 역할 |
|---|---|---|
| `SceneLoader` | `Assets/Scripts/Core/SceneLoader.cs` | 씬 전환 |
| `PhaseManager` | `Assets/Scripts/Core/PhaseManager.cs` | 게임 페이즈 (아침/던전/저녁/식당) 관리 |
| `DayManager` | `Assets/Scripts/Core/DayManager.cs` | 날짜 카운터 + 식당 영업 루프 |
| `GoldManager` | `Assets/Scripts/Core/GoldManager.cs` | 골드 수치 |
| `Inventory` | `Assets/Scripts/Inventory/Inventory.cs` | 재료 인벤토리 |
| `FoodInventory` | `Assets/Scripts/Inventory/FoodInventory.cs` | 요리 완성품 인벤토리 |
| `ToolManager` | `Assets/Scripts/Core/ToolManager.cs` | 공격 업그레이드 수치 |

**주의**: `DayManager`는 DontDestroyOnLoad 싱글톤이므로 RestaurantScene에서 GameManager 오브젝트가 중복 파괴된다.
`RestaurantOpener`(StartDay 호출)는 **GameManager가 아닌 별도 오브젝트**(RestaurantSetup)에 배치해야 한다.

### SpawnManager 소환 패턴
```
1. InstantiateDisabled(prefab)  → Awake/OnEnable 실행 안 됨
2. entity.Init(data)            → GetComponent + 스탯 세팅
3. entity.SetActive(true)       → OnEnable → 코루틴/입력 활성화
```
PlayerController, MonsterAI 모두 이 패턴을 따른다.
PlayerController는 SpawnManager 없이 씬에 직접 배치된 경우 `Start()`에서 자동 Init된다.

---

## 스크립트 구조

```
Assets/Scripts/
├── Core/           DayManager, PhaseManager, SceneLoader, GoldManager, ToolManager
├── Player/         PlayerController, PlayerSpawnData (SO)
├── Enemy/          MonsterBase, MonsterAI, SpawnManager, DropResolver
├── Combat/         Health
├── Inventory/      Inventory, FoodInventory
├── Kitchen/        CookingStation, RecipeMatcher
├── Restaurant/     CustomerAI, ServingSystem, RestaurantTable, RestaurantSetup, RestaurantOpener
├── Dungeon/        DungeonRoom, DungeonExit, PhaseManager (씬 전환)
├── Data/           MonsterData, IngredientData, RecipeData, FoodData, DropTableData (SO 클래스)
│                   DataRegistry (인프라만 존재, MVP에서는 미사용)
├── UI/             GameHUD (UIDocument), UIManager
└── Editor/
    └── DataPipeline/  DataManagerWindow, ScriptableObjectSync, CsvParser
```

---

## 코드 컨벤션

### 네임스페이스
`MonsterKitchen.{System}` 형태 사용:
- `MonsterKitchen.Core`, `MonsterKitchen.Player`, `MonsterKitchen.Enemy`
- `MonsterKitchen.Combat`, `MonsterKitchen.Inventory`, `MonsterKitchen.Kitchen`
- `MonsterKitchen.Restaurant`, `MonsterKitchen.Dungeon`, `MonsterKitchen.Data`

### 입력 시스템
- **New Input System** (`InputSystem_Actions.inputactions`) 만 사용.
- `Input.GetKey` 등 레거시 입력 API 사용 금지.

### 에셋 로딩
- MVP 단계: ScriptableObject를 `[SerializeField]`로 직접 참조 (Inspector 연결).
- `Resources.Load` 사용 금지.
- `DataRegistry` + Addressable은 인프라가 존재하지만 MVP에서는 미사용.
  추후 에셋 수가 많아지면 전환 검토.

### LayerMask 직렬화
`enemyLayer = {"value": 256}` 형태 (Layer 8 = Enemy, LayerMask bit format).
`manage_components`로 설정 시 int가 아닌 object 형태로 전달해야 한다.

---

## 데이터 파이프라인

```
Assets/Data/CSV/      ← Excel에서 내보낸 원본 CSV (`;` 주석, `_` 무시 컬럼)
Assets/Data/SO/       ← CSV에서 동기화된 ScriptableObject
Assets/Editor/DataPipeline/DataManagerWindow.cs  ← 동기화 에디터 창
```

SO 파일 ID 규칙:
- Monster: `MON_001` ~ `MON_005`
- Ingredient: `ING_001` ~ `ING_008`
- Recipe: `RCP_001` ~ `RCP_005`
- Food: `FOOD_001` ~ `FOOD_005`
- DropTable: `DRP_001` ~ `DRP_005`

CsvParser 특징: `;` 줄 주석, `_` 시작 컬럼 무시, `#TYPE` 행 타입 지정, `|` 배열 구분자.

---

## 설치된 패키지

| 패키지 | 용도 |
|---|---|
| `com.unity.addressables` | 에셋 로딩 (MVP 이후 DataRegistry와 연동 예정) |
| `com.unity.cinemachine` | 카메라 전환 |
| `com.unity.inputsystem` | 플레이어 입력 |
| `com.unity.2d.animation` | 2D 스켈레탈 애니메이션 |
| `com.unity.2d.aseprite` | Aseprite 스프라이트 직접 임포트 |
| `com.unity.2d.tilemap` + extras | 타일맵 기반 맵 |
| `com.unity.2d.spriteshape` | 유기적 지형 |
| `com.unity.timeline` | 컷씬 |
| `com.unity.feature.mobile` | 모바일 빌드 지원 |
| `com.unity.test-framework` | 유닛 테스트 |
| `com.coplaydev.unity-mcp` | Claude Code ↔ Unity Editor MCP 연동 |

---

## MCP / AI 통합

`com.coplaydev.unity-mcp` (Git 패키지)가 설치되어 있어 Claude Code가 MCP 도구로 Unity Editor를 직접 제어할 수 있다.

MCP 도구 사용 시 알려진 제약:
- `execute_code` 도구: Windows MAX_PATH 문제로 실패할 수 있음 (`파일 이름이나 확장명이 너무 깁니다`). 대신 `manage_components`, `manage_gameobject` 등 전용 도구 사용.
- 비활성 GameObject는 `find_gameobjects`나 `manage_gameobject`로 이름 검색이 안 될 수 있음.
- `manage_components`에서 LayerMask는 `{"value": N}` 오브젝트 형태로 전달.
- InstanceID는 씬 리로드 후 무효화됨 — `by_name` 검색 방식 사용.

---

## 작업 규칙

1. **작업 시작 전**: 할 일 목록을 `WORK_IN_PROGRESS.md`에 먼저 기록한다.
2. **작업 완료 후**: `WORK_IN_PROGRESS.md`의 완료 현황을 업데이트한다.
3. **모듈 현황**: `docs/MODULES.md`가 전체 모듈 명세, `WORK_IN_PROGRESS.md`가 완료 체크 기준.
4. **씬 수정 후**: 반드시 `manage_scene(action: save)` 호출.
5. **스크립트 수정 후**: `read_console`로 컴파일 에러 확인.
6. **새 컴포넌트 추가 시**: 컴파일 완료(`isCompiling: false`) 확인 후 씬에 배치.

---

## 현재 구현 상태 요약

- **완료된 핵심 사이클**: ManagementScene → DungeonScene(몬스터 처치) → KitchenScene(요리) → RestaurantScene(서빙) → 다음 날
- **플레이어**: 8방향 이동, 근거리 공격, 대시(무적), 속성 시스템
- **몬스터**: FSM(Idle/Patrol/Chase/Attack/Die), 드롭 시스템, Separation Steering
- **인벤토리**: 재료/음식 싱글톤 (UI 미구현)
- **주방**: RecipeMatcher + CookingStation, 요리 완성 → FoodInventory
- **식당**: 손님 AI(FSM), 서빙(E키), 골드 획득, 일일 손님 수 처리
- **UI**: GameHUD (UIToolkit) — 골드 표시, Day 카운터
- **다음 우선 작업**: U-02 플레이어 HP 바, I-02 인벤토리 UI, S-06 Save/Load
