# Hardening Design — 코드 품질 고도화

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MVP 완료 후 이벤트 누수·널 안전성·성능·아키텍처·에러 피드백 5개 영역을 체계적으로 보강해 안정적인 게임 루프를 확보한다.

**Architecture:** 기존 코드 구조는 유지하면서 방어 코드를 시스템 경계(씬 진입·콜백·데이터 로드)에만 추가한다. 이벤트 구독은 OnEnable/OnDisable 또는 OnOpen/OnClose 쌍으로 통일한다. 인터페이스는 콜사이트 변경 없이 추출한다.

**Tech Stack:** Unity 6, C#, UIToolkit, MonsterKitchen namespace

---

## 섹션 A — 이벤트 누수 (Event Leak)

### 문제

| 파일 | 구독 위치 | 해제 위치 | 증상 |
|---|---|---|---|
| `CookingUI.cs` | `OnFirstOpen()` | 없음 | 패널 열 때마다 핸들러 누적 → 인벤토리 변경 시 중복 Refresh |
| `InventoryUI.cs` | `OnOpen()` 내부 | 없음 | 동일 |
| `GameHUD.cs` | `Start()` | `OnDestroy()` | 씬 리로드 시 OnDestroy 미실행 가능 → 중복 구독 |

### 수정 원칙

- UI 패널(`UIPanel` 서브클래스): `OnOpen()` 구독 ↔ `OnClose()` 해제 쌍
- MonoBehaviour 일반: `OnEnable()` 구독 ↔ `OnDisable()` 해제 쌍
- `Start()`/`OnDestroy()` 구독은 사용하지 않는다

### 수정 내용

**`CookingUI.cs`**
```csharp
protected override void OnOpen()
{
    if (PlayerDataManager.Instance?.Inventory != null)
        PlayerDataManager.Instance.Inventory.OnIngredientChanged += OnIngredientChanged;
    // 기존 OnFirstOpen 내 구독 로직 이동
}

protected override void OnClose()
{
    if (PlayerDataManager.Instance?.Inventory != null)
        PlayerDataManager.Instance.Inventory.OnIngredientChanged -= OnIngredientChanged;
}
```

**`InventoryUI.cs`**
```csharp
protected override void OnOpen()
{
    if (PlayerDataManager.Instance?.Inventory != null)
        PlayerDataManager.Instance.Inventory.OnIngredientChanged += RefreshInventory;
}

protected override void OnClose()
{
    if (PlayerDataManager.Instance?.Inventory != null)
        PlayerDataManager.Instance.Inventory.OnIngredientChanged -= RefreshInventory;
}
```

**`GameHUD.cs`**
```csharp
// Start() 에서 SaveScheduler 구독 제거
// OnDestroy() 에서 SaveScheduler 해제 제거

void OnEnable()
{
    if (SaveScheduler.Instance != null)
        SaveScheduler.Instance.OnSaveStateChanged += OnSaveStateChanged;
}

void OnDisable()
{
    if (SaveScheduler.Instance != null)
        SaveScheduler.Instance.OnSaveStateChanged -= OnSaveStateChanged;
}
```

---

## 섹션 B — 널 안전성 (Null Safety)

### 원칙

- **시스템 경계**에서만 방어: 씬 진입, 콜백, 데이터 로드, 외부 API 반환값
- 내부 코드는 호출자가 이미 검증했다고 신뢰
- null 감지 시: `Debug.LogError(message, this)` 후 `return` (silent skip 금지)

### 수정 내용

**`CustomerAI.cs` — `Serve()` null 가드**
```csharp
public bool Serve(FoodData food)
{
    if (food == null)
    {
        Debug.LogError("[CustomerAI] Serve() — food is null", this);
        return false;
    }
    if (OrderedFood == null)
    {
        Debug.LogError("[CustomerAI] Serve() — OrderedFood is null", this);
        return false;
    }
    // 기존 로직
}
```

**`RestaurantSceneController.cs` — 프리팹 로드 실패 시 초기화 중단**
```csharp
void OnInit()
{
    var customerPrefab = AssetLoadManager.Instance?.Load<CustomerAI>(AssetKeys.PREFAB_CUSTOMER);
    if (customerPrefab == null)
    {
        Debug.LogError("[RestaurantSceneController] CustomerAI 프리팹 로드 실패. AssetManifest 확인 필요.", this);
        return;   // null을 SetRestaurantConfig에 전달하지 않는다
    }
    DayManager.Instance?.SetRestaurantConfig(..., customerPrefab, ...);
}
```

**`PlayerManager.cs` — 플레이어 데이터 null 시 스폰 중단**
```csharp
void SpawnOrReposition()
{
    if (m_PlayerData == null)
    {
        Debug.LogError("[PlayerManager] PlayerCharData 없음 — 스폰 불가. DataRegistry에 등록됐는지 확인.", this);
        return;
    }
    // 기존 로직
}
```

**`DayManager.cs` — `SetRestaurantConfig` 입력 검증**
```csharp
public void SetRestaurantConfig(RestaurantTable table, CustomerAI customerPrefab, Transform[] spawnPoints)
{
    if (customerPrefab == null)
    {
        Debug.LogError("[DayManager] SetRestaurantConfig — customerPrefab is null. 식당 손님 스폰 불가.", this);
        return;
    }
    // 기존 저장 로직
}
```

**`CookingStation.cs` — 조리 콜백 null 가드**
```csharp
NetworkManager.Instance?.RequestCook(recipe, grade, (success, food) =>
{
    cookSuccess = success;
    resultFood  = food;
});

if (!cookSuccess || resultFood == null)
{
    DebugUtil.LogWarning("[CookingStation] 요리 실패 또는 결과 음식 null.");
    m_IsCooking = false;
    yield break;
}
// 이하 성공 경로만
```

**`GameHUD.cs` — 미니맵 null 체크 통일**
```csharp
void FindAndBindMinimap()
{
    if (DungeonMapController.Instance == null) return;
    m_MinimapRoot?.SetActive(true);
    m_BagWeightBar?.gameObject.SetActive(true);
    // 이하 ?. 연산자 통일
}
```

**`CookingUI.cs` — DataRegistry null 가드**
```csharp
void RefreshRecipeMatch()
{
    var all = DataRegistry.Instance?.Recipes?.All;
    if (all == null) return;
    var match = RecipeMatcher.FindSlotMatch(all, m_SlotIds);
    // 기존 로직
}
```

---

## 섹션 D — 성능: Update() 폴링 → 이벤트

### 현재 문제

`GameHUD.Update()` 매 프레임 호출:
- `UpdateMinimapDot()` — 플레이어 위치 조회 (이동 없어도 매 프레임 실행)
- `UpdateSpawnMarkers()` — 스폰존 몬스터 수 조회
- 가방 무게 바 — `DungeonBag`을 매 프레임 폴링

### 수정 내용

**`DungeonBag.cs` — 이벤트 추가**
```csharp
/// <summary>무게 또는 아이템 변경 시 발생. (currentWeight, capacity)</summary>
public event Action<float, float> OnWeightChanged;

// TryAdd(), Remove() 말미에 invoke:
OnWeightChanged?.Invoke(CurrentWeight, m_Capacity);
```

**`PlayerController.cs` — 이동 이벤트 추가**
```csharp
/// <summary>플레이어 위치 변경 시 발생.</summary>
public event Action<Vector3> OnMoved;

// 실제 이동 처리 후:
OnMoved?.Invoke(transform.position);
```

**`DungeonSpawnZone.cs` — 스폰/사망 이벤트 추가 (없는 경우)**
```csharp
public event Action OnMonsterCountChanged;
// 스폰·사망 후 invoke
```

**`GameHUD.cs` — Update() 폴링 제거, 이벤트 구독으로 교체**

`DungeonSpawnZone[]`은 `FindObjectsByType` 금지(CLAUDE.md) → `[SerializeField]` Inspector 직결.
`PlayerController` 정적 접근자(`LocalPlayer` 또는 `Instance`) 존재 여부 구현 시 확인.

```csharp
// Update()에서 미니맵·가방 관련 호출 제거

[SerializeField] DungeonSpawnZone[] m_SpawnZones;  // Inspector 직결

void BindDungeonEvents()
{
    // PlayerController.Instance 또는 .LocalPlayer — 구현 시 실제 접근자 확인
    if (PlayerController.Instance != null)
        PlayerController.Instance.OnMoved += OnPlayerMoved;

    if (DungeonBag.Instance != null)
        DungeonBag.Instance.OnWeightChanged += OnBagWeightChanged;

    foreach (var zone in m_SpawnZones)
        if (zone != null) zone.OnMonsterCountChanged += RefreshSpawnMarkers;
}

void UnbindDungeonEvents()
{
    if (PlayerController.Instance != null)
        PlayerController.Instance.OnMoved -= OnPlayerMoved;

    if (DungeonBag.Instance != null)
        DungeonBag.Instance.OnWeightChanged -= OnBagWeightChanged;

    foreach (var zone in m_SpawnZones)
        if (zone != null) zone.OnMonsterCountChanged -= RefreshSpawnMarkers;
}

// SceneLoader.OnSceneLoadFinished 구독 → DungeonScene 진입 시 BindDungeonEvents()
// DungeonScene 이탈 시 UnbindDungeonEvents()
```

---

## 섹션 E — 아키텍처: 인터페이스 추출

### 목표

`NetworkManager`, `ServerDBManager`를 인터페이스 뒤로 숨겨 EditMode 테스트에서 Fake 주입 가능하게 한다. **기존 콜사이트(`NetworkManager.Instance.*`) 변경 없음.**

### 새 파일: `INetworkManager.cs`

```csharp
namespace MonsterKitchen.Core
{
    public interface INetworkManager
    {
        void RequestEarnGold(int amount);
        void RequestSpendGold(int amount, System.Action<bool> onResult);
        void RequestAddIngredient(uint id, int qty);
        void RequestCook(RecipeData recipe, FoodGrade grade, System.Action<bool, FoodData> onResult);
        void RequestServeFood(uint foodId, System.Action<bool> onResult);
        void RequestUpgrade(UpgradeType type, System.Action<bool> onResult);
    }
}
```

### 새 파일: `IServerDBManager.cs`

```csharp
namespace MonsterKitchen.Core
{
    public interface IServerDBManager
    {
        void Save(ServerSaveData data);
        ServerSaveData Load();
        bool HasSave();
    }
}
```

### 기존 파일 수정

```csharp
// NetworkManager.cs
public class NetworkManager : MonoBehaviour, INetworkManager { ... }

// ServerDBManager.cs
public class ServerDBManager : MonoBehaviour, IServerDBManager { ... }
```

### 테스트용 Fake 패턴 (테스트 어셈블리에만 존재)

```csharp
// Tests/EditMode/Fakes/FakeNetworkManager.cs
public class FakeNetworkManager : INetworkManager
{
    public bool CookSucceeds = true;
    public FoodData CookResult;

    public void RequestCook(RecipeData r, FoodGrade g, Action<bool, FoodData> cb)
        => cb(CookSucceeds, CookResult);
    // 나머지 no-op 구현
}
```

---

## 섹션 C — 에러 피드백

### 원칙

| 수신자 | 채널 |
|---|---|
| 플레이어 (런타임) | `GameHUD.ShowNotification(msg, duration)` |
| 개발자 (설정 오류) | `Debug.LogError(msg, this)` |
| 데이터 오류 (CSV) | `Debug.LogWarning` + 안전한 기본값 |

### 수정 내용

**요리 재료 부족 → 플레이어 알림**

`CookingUI.cs` — `RequestCook` 콜백에서 실패 처리:
```csharp
// NetworkManager.RequestCook 콜백
if (!success)
{
    GameHUD.Instance?.ShowNotification("재료가 부족합니다.", 2f);
    return;
}
```

**`DungeonBag.TryAdd` — weightPerUnit 검증**

`DataRegistry.cs` 데이터 로드 시:
```csharp
foreach (var ing in ingredients)
{
    if (ing.Weight <= 0f)
    {
        Debug.LogWarning($"[DataRegistry] 재료 ID:{ing.Id} Weight={ing.Weight} — 1.0 으로 보정.");
        ing.Weight = 1f;
    }
}
```

**`PlayerDataManager.LoadFrom` — 신규/실패 구분**

```csharp
public void LoadFrom(ServerSaveData save)
{
    if (save == null)
    {
        DebugUtil.Log("[PlayerDataManager] save == null → 새 게임으로 초기화.");
        return;
    }
    // 기존 로드 로직
}
```

---

## 파일 영향 범위 요약

| 파일 | 카테고리 | 변경 종류 |
|---|---|---|
| `CookingUI.cs` | A, B, C | OnOpen/OnClose 구독 이동, null 가드, 실패 알림 |
| `InventoryUI.cs` | A | OnOpen/OnClose 구독 이동 |
| `GameHUD.cs` | A, B, D | SaveScheduler OnEnable/OnDisable, null 통일, Update 폴링 제거 |
| `CustomerAI.cs` | B | Serve null 가드 |
| `RestaurantSceneController.cs` | B | 프리팹 null → return |
| `PlayerManager.cs` | B | m_PlayerData null → return |
| `DayManager.cs` | B | SetRestaurantConfig 검증 |
| `CookingStation.cs` | B | 콜백 null 가드 |
| `DungeonBag.cs` | D, C | OnWeightChanged 이벤트, weight 검증 |
| `PlayerController.cs` | D | OnMoved 이벤트 |
| `DungeonSpawnZone.cs` | D | OnMonsterCountChanged 이벤트 |
| `NetworkManager.cs` | E | `: INetworkManager` 추가 |
| `ServerDBManager.cs` | E | `: IServerDBManager` 추가 |
| `DataRegistry.cs` | C | weight 검증 |
| `PlayerDataManager.cs` | C | LoadFrom 신규/실패 구분 |
| `INetworkManager.cs` | E | **신규** |
| `IServerDBManager.cs` | E | **신규** |
| `FakeNetworkManager.cs` | E | **신규 (Tests/)** |

---

## 테스트 전략

- 기존 EditMode 74종 회귀 통과 확인
- `FakeNetworkManager`로 요리 성공/실패 시나리오 테스트 추가
- 이벤트 누수 테스트: 패널 open/close 3회 반복 후 구독 수 == 1 확인
