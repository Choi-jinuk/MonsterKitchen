# Mobile Input Design — MonsterKitchen
> 작성일: 2026-06-05

## 목표

모바일(터치) 환경에서 가상 조이스틱(이동) + 액션 버튼(대시/스킬/상호작용)으로
기존 `InputManager` 이벤트 시스템을 그대로 활용하는 모바일 입력 레이어 추가.

---

## 열거형

```csharp
// Assets/Scripts/UI/Mobile/MobileAction.cs
namespace MonsterKitchen.UI.Mobile
{
    public enum MobileAction { Dash, Skill1, Skill2, Interact, Ultimate }
}

// Assets/Scripts/UI/Mobile/MobileContext.cs
namespace MonsterKitchen.UI.Mobile
{
    public enum MobileContext
    {
        Dungeon,          // [Dash][Skill1][Skill2]
        DungeonInteract,  // [Dash][Skill1][Interact]  ← Skill2 슬롯 교체
        Exploration,      // [Dash][Interact]
    }
}
```

---

## 컴포넌트 설계

### 1. InputManager — Inject 메서드 추가

`Assets/Scripts/Core/Managers/InputManager.cs` 에 public 메서드 추가.
기존 이벤트를 외부(모바일 UI)에서 발화할 수 있게 노출.

```csharp
public void InjectMove(Vector2 dir)   => OnMove?.Invoke(dir);
public void InjectDash()              => OnDash?.Invoke();
public void InjectSkill1()            => OnSkill1?.Invoke();
public void InjectSkill2()            => OnSkill2?.Invoke();
public void InjectInteract()          => OnInteract?.Invoke();
public void InjectUltimate()          => OnUltimate?.Invoke();
```

### 2. VirtualJoystick

**파일**: `Assets/Scripts/UI/Mobile/VirtualJoystick.cs`  
**네임스페이스**: `MonsterKitchen.UI.Mobile`

**동작 모델**: Dynamic (floating) — 터치 시작 위치를 배경 중심으로 재배치.

```
[PointerDown]  배경 center = 터치 월드 위치 (화면 왼쪽 절반만 허용)
[Drag]         delta = touchPos - center
               clamp delta.magnitude to m_Radius
               dir = delta / m_Radius  (아날로그 0~1 크기)
               if dir.magnitude < m_DeadZone → InjectMove(zero)
               else → InjectMove(dir)
[PointerUp]    handle → center 복귀, InjectMove(Vector2.zero)
```

**Inspector 필드**:
```csharp
[SerializeField] float         m_Radius       = 80f;
[SerializeField] float         m_DeadZone     = 0.1f;
[SerializeField] RectTransform m_Background;
[SerializeField] RectTransform m_Handle;
```

**인터페이스**: `IPointerDownHandler`, `IDragHandler`, `IPointerUpHandler`  
멀티터치 안전: `PointerEventData.pointerId` 캐시, 등록된 손가락 외 무시.

### 3. MobileButton

**파일**: `Assets/Scripts/UI/Mobile/MobileButton.cs`  
**네임스페이스**: `MonsterKitchen.UI.Mobile`

```csharp
[SerializeField] MobileAction m_Action;
```

- `IPointerDownHandler.OnPointerDown` → `MobileAction` 에 따라 `InputManager.Instance.Inject*()` 호출
- 시각 피드백: PointerDown → `transform.localScale = Vector3.one * 0.9f`, PointerUp → 복귀 (DOTween 불필요, 직접 Lerp)

**액션 → Inject 매핑**:
| MobileAction | Inject 메서드 |
|---|---|
| Dash | InjectDash() |
| Skill1 | InjectSkill1() |
| Skill2 | InjectSkill2() |
| Interact | InjectInteract() |
| Ultimate | InjectUltimate() |

### 4. MobileHUD

**파일**: `Assets/Scripts/UI/Mobile/MobileHUD.cs`  
**네임스페이스**: `MonsterKitchen.UI.Mobile`  
**패턴**: 싱글톤 (`DontDestroyOnLoad`)

```csharp
public static MobileHUD Instance { get; private set; }

[SerializeField] bool            m_ForceShow;        // 에디터 테스트용
[SerializeField] MobileButton[]  m_Slots;            // 우측 버튼 슬롯 배열
[SerializeField] ContextConfig[] m_Configs;          // 컨텍스트별 슬롯 구성

[System.Serializable]
struct ContextConfig
{
    public MobileContext  Context;
    public MobileAction[] SlotActions;   // 길이 ≤ m_Slots.Length
}
```

**`SetContext(MobileContext ctx)`**:
1. `m_Configs` 에서 `ctx` 매칭 항목 탐색
2. 슬롯별 `MobileButton.SetAction(MobileAction)` 호출로 액션 교체
3. 슬롯 수 > `SlotActions.Length` 인 슬롯 → `SetActive(false)`

> `MobileButton` 에 `public void SetAction(MobileAction action)` 메서드 필요.

**표시 조건**:
```csharp
void Awake()
{
    bool show = Application.isMobilePlatform || m_ForceShow;
    gameObject.SetActive(show);
}
```

**생명주기**: `GlobalController.Start()` 에서 `MobileHUD.prefab` Instantiate → DontDestroyOnLoad.

---

## 씬 연동

| 호출자 | 메서드 | 컨텍스트 |
|---|---|---|
| `DungeonMapController.Start()` | `SetContext` | `Dungeon` |
| `InteractionPrompt.Show()` | `SetContext` | `DungeonInteract` |
| `InteractionPrompt.Hide()` | `SetContext` | `Dungeon` |
| `ManagementSceneController.Start()` | `SetContext` | `Exploration` |
| `KitchenSceneController.Start()` | `SetContext` | `Exploration` |
| `RestaurantSceneController.Start()` | `SetContext` | `Exploration` |

---

## 프리팹 구조 (MobileHUD.prefab)

```
MobileHUD               ← Canvas (Screen Space - Overlay), MobileHUD.cs
├── Joystick            ← VirtualJoystick.cs
│   ├── Background      ← Image (반투명 원)
│   └── Handle          ← Image (작은 원)
└── ButtonPanel         ← 우측 하단 영역
    ├── Slot0           ← MobileButton.cs (Dash)
    ├── Slot1           ← MobileButton.cs (Skill1)
    └── Slot2           ← MobileButton.cs (Skill2/Interact — 컨텍스트로 교체)
```

---

## 파일 목록

| 파일 | 신규/수정 |
|---|---|
| `Assets/Scripts/Core/Managers/InputManager.cs` | 수정 (Inject* 추가) |
| `Assets/Scripts/UI/Mobile/MobileAction.cs` | 신규 |
| `Assets/Scripts/UI/Mobile/MobileContext.cs` | 신규 |
| `Assets/Scripts/UI/Mobile/VirtualJoystick.cs` | 신규 |
| `Assets/Scripts/UI/Mobile/MobileButton.cs` | 신규 |
| `Assets/Scripts/UI/Mobile/MobileHUD.cs` | 신규 |
| `Assets/Prefabs/UI/MobileHUD.prefab` | 신규 |
| `Assets/Scripts/Core/Startup/GameStartup.cs` 또는 `GlobalController.cs` | 수정 (prefab spawn) |
| `Assets/Scripts/Dungeon/DungeonMapController.cs` | 수정 (SetContext) |
| `Assets/Scripts/UI/HUD/InteractionPrompt.cs` | 수정 (SetContext) |
| `Assets/Scripts/Management/ManagementSceneController.cs` | 수정 (SetContext) |
| `Assets/Scripts/Cooking/KitchenSceneController.cs` | 수정 (SetContext) |
| `Assets/Scripts/Restaurant/RestaurantSceneController.cs` | 수정 (SetContext) |

---

## 테스트 계획

- EditMode: `InputManager.InjectMove` → `OnMove` 이벤트 발화 확인
- EditMode: `MobileHUD.SetContext(DungeonInteract)` → Slot2가 Interact로 변경 확인
- PlayMode (에디터, `m_ForceShow=true`): 조이스틱 드래그 → 플레이어 이동
- PlayMode: Skill2 슬롯 → InteractionPrompt 진입 시 Interact로 교체 확인
