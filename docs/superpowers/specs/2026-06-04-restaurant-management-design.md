# 식당 경영 시스템 설계 문서

> 최종 갱신: 2026-06-04
> 참조: Dave the Diver 식당 시스템

---

## Goal

Module 8 잔여 기능 구현 (Dave the Diver 방식 참조).
- 8-2 메뉴판 UI (영업 전 준비 단계)
- 8-8 손님 만족도 점수 시스템
- 8-9 테이블 청소 시스템
- 8-10 영업 정산 화면 UI
- 8-11 명성(Fame) 시스템

---

## 섹션 1: 데이터 레이어

### PlayerFameData (신규 순수 C# 클래스)

```csharp
public class PlayerFameData
{
    int m_TotalFame;
    public int TotalFame => m_TotalFame;

    public void AddFame(int amount);        // 음수 허용 (하한 0)
    public int  GetDailyFame(float avgSatisfaction); // (avgSat - 50) * 0.8f, round

    public event Action<int, int> OnFameChanged;  // (oldFame, newFame)

    public void Load(int savedFame);
    public int  Save();
}
```

**명성 마일스톤 — `FameMilestone` static 배열:**

| 누적 명성 | 해금 내용 |
|---|---|
| 0 (시작) | 손님 3명/일, 메뉴 슬롯 3 |
| 100 | 손님 4명/일 |
| 250 | 메뉴 슬롯 +1 (4슬롯) |
| 500 | 특수 손님 등장 (높은 팁, 까다로운 취향) |
| 1000 | 비밀 레시피 힌트 해금 |

**명성 조회 헬퍼:**
```csharp
public int  MaxGuestsPerDay();   // 명성 기준 현재 최대 손님 수
public int  MenuSlotCount();     // 명성 기준 현재 메뉴 슬롯 수
public bool IsSpecialGuestUnlocked(); // 명성 ≥ 500
```

### DailyMenuData (신규 — 하루 메뉴 구성)

```csharp
public class DailyMenuData
{
    // 슬롯 수 = PlayerFameData.MenuSlotCount()
    List<MenuSlot> m_Slots;

    public struct MenuSlot
    {
        public uint  FoodId;
        public int   CustomPrice;  // BasePrice ± 30%
        public bool  IsEmpty;
    }

    public bool IsOnMenu(uint foodId);
    public int  GetPrice(uint foodId);
    public void SetSlot(int index, uint foodId, int price);
    public void ClearSlot(int index);
    public IReadOnlyList<MenuSlot> Slots { get; }
}
```

DailyMenuData는 하루 시작 시 초기화. 저장 불필요 (당일만 유효).

### CustomerAI 확장

```csharp
// 기존 필드 유지, 추가:
int   m_Satisfaction;    // 0~100, 초기값 50
float m_PatienceDecayMult; // 더러운 테이블 시 1.5f, 기본 1.0f

public int Satisfaction => m_Satisfaction;
public void AddSatisfaction(int delta);  // Mathf.Clamp(0,100)
public int  CalculateTip(int basePayment); // 만족도 기반 팁 계산
```

**만족도 변화 규칙:**

| 이벤트 | 만족도 변화 |
|---|---|
| 정확한 음식 서빙 | +20 |
| Perfect 등급 음식 | +10 |
| Legendary 등급 음식 | +15 |
| Patience 50% 이내 서빙 (빠른 서빙) | +10 |
| Patience 50% 초과 서빙 (느린 서빙) | -10 |
| 더러운 테이블 착석 | -5 |
| Patience 만료 (미서빙) | -30 |

**팁 계산:**
```
satisfaction < 70  → 팁 없음
satisfaction 70~84 → basePayment * 0.1f (기본 팁)
satisfaction 85~99 → basePayment * 0.2f
satisfaction 100   → basePayment * 0.2f + "완벽한 서비스!" 팝업
```

### RestaurantTable 확장

```csharp
// 기존 IsOccupied / Occupant 유지, 추가:
bool  IsDirty { get; }
void  SetDirty(bool dirty);
```

### DayManager 확장 — 일일 통계 추적

```csharp
// 영업 중 누적 (Day 시작 시 초기화)
int   m_DayRevenue;
int   m_DayTips;
int   m_GuestsServed;
int   m_GuestsArrived;
List<int> m_SatisfactionScores;  // 서빙 완료 손님 만족도

public int   DayRevenue    => m_DayRevenue;
public int   DayTips       => m_DayTips;
public int   GuestsServed  => m_GuestsServed;
public int   GuestsArrived => m_GuestsArrived;
public float AvgSatisfaction => m_SatisfactionScores.Count > 0
    ? m_SatisfactionScores.Average() : 50f;

public void RecordServing(int payment, int tip, int satisfaction);
```

### PlayerDataManager 확장

```csharp
public PlayerFameData   Fame   { get; }
public DailyMenuData    DailyMenu { get; }
```

### ServerSaveData 확장

```csharp
public int TotalFame = 0;
```

---

## 섹션 2: 메뉴판 UI (8-2)

### 흐름

RestaurantScene 진입 → `RestaurantSceneController.Awake()` → **MenuSetupUI 자동 열림** → 플레이어 메뉴 구성 → "영업 시작" 버튼 → `DayManager.StartDay()`.

### 컴포넌트

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/UI/Restaurant/MenuSetupUI.cs` | UIPanel 상속, 메뉴 구성 UI |
| `Assets/UI/MenuSetupPanel.uxml` | 레이아웃 |
| `Assets/UI/MenuSetupPanel.uss` | 스타일 |

### UIPanel 설정

- `m_IsPopup = false` (전용 화면, 다른 패널 차단)
- `m_Layer = UILayer.Panel`
- 영업 시작 전 자동 표시, 수동 닫기 불가

### 레이아웃

```
┌─────────────── 오늘의 메뉴 구성 ────────────────┐
│                                                  │
│  슬롯 1  [슬라임 스튜 ★★★]  가격: [850] G    │
│  슬롯 2  [드래곤 구이   ★★]  가격: [620] G    │
│  슬롯 3  [비어있음      ───]  [+ 추가]           │
│                                                  │
│  보유 음식 목록:                                  │
│  [슬라임 스튜 ×3 ★★★]  [드래곤 구이 ×1 ★★]  │
│                                                  │
│              [영업 시작]                          │
└──────────────────────────────────────────────────┘
```

**가격 조정:** 기본가 ± 30% 범위, +/- 버튼 (50G 단위).  
**음식 클릭** → 비어있는 슬롯에 자동 배치 (또는 슬롯 교체).  
**"영업 시작" 조건:** 슬롯 1개 이상 채워야 활성화.

### 손님 주문 로직 변경

`CustomerAI.ChooseOrder()` — DailyMenuData에 있는 음식만 선택:

```csharp
var available = DataRegistry.Instance.AllFoods
    .Where(f => PlayerDataManager.Instance.DailyMenu.IsOnMenu(f.Id)
             && PlayerDataManager.Instance.Inventory.GetFoodCount(f.Id) > 0)
    .ToList();
```

---

## 섹션 3: 테이블 청소 (8-9)

### 흐름

1. 손님 퇴장 (`CustomerAI.OnLeave()`) → `table.SetDirty(true)` → 더러운 비주얼 오버레이 활성화
2. 더러운 테이블에 다음 손님 착석 시 → `customer.AddSatisfaction(-5)` + `m_PatienceDecayMult = 1.5f`
3. 플레이어 E키 → 더러운 테이블 1.5초 청소 → `table.SetDirty(false)` → 오버레이 비활성화

### RestaurantTable 비주얼

```csharp
// RestaurantTable.SetDirty() 호출 시:
m_DirtyOverlay.SetActive(dirty);  // [SerializeField] GameObject m_DirtyOverlay
```

`m_DirtyOverlay`: 테이블 위 반투명 "더러운" 스프라이트 오버레이 (Inspector 연결).

### IInteractable 구현

`RestaurantTable`이 `IInteractable` 구현 (기존 서빙 상호작용과 별도):

```csharp
public bool CanInteract()  => IsDirty && !IsOccupied;
public void Interact()     => StartCoroutine(CleanRoutine());
public string PromptText() => "청소하기 [E]";
```

---

## 섹션 4: 정산 화면 (8-10)

### 흐름

마지막 손님 퇴장 → `DayManager.OnAllGuestsLeft` 이벤트 → `SettlementUI.Show()` → 정산 데이터 표시 → "다음 날로" 버튼 → `DayManager.EndDay()` → ManagementScene 귀환.

### 컴포넌트

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/UI/Restaurant/SettlementUI.cs` | UIPanel 상속 |
| `Assets/UI/SettlementPanel.uxml` | 레이아웃 |
| `Assets/UI/SettlementPanel.uss` | 스타일 |

### UIPanel 설정

- `m_IsPopup = false`
- `m_Layer = UILayer.Overlay` (다른 UI 위에)
- 자동 표시, 수동 닫기 불가 ("다음 날로" 버튼만)

### 레이아웃

```
┌──────────────── Day N 영업 결산 ───────────────────┐
│                                                     │
│  👥 손님      4 / 4명                               │
│  💰 매출      1,250 G  (요리 1,100 + 팁 150)        │
│  ⭐ 평균 만족도   82점  ★★★★☆                    │
│  🏅 명성 획득  +32                                  │
│                                                     │
│  ── 마스터리 레벨업 ──────────────────────────────  │
│  🍖 슬라임 구이   ★★★ → ★★★★                   │
│                                                     │
│                    [다음 날로]                       │
└─────────────────────────────────────────────────────┘
```

**만족도 → 별 표시:** 0~39점=★☆☆☆☆, 40~59=★★☆☆☆, 60~74=★★★☆☆, 75~89=★★★★☆, 90+=★★★★★

### DayManager 이벤트 추가

```csharp
public event Action OnAllGuestsLeft;  // 마지막 손님 Done 상태 진입 시
```

---

## 섹션 5: 명성 시스템 (8-11)

### 명성 획득 계산

정산 시점 (`SettlementUI.Show()` 직전):

```csharp
float avgSat   = DayManager.Instance.AvgSatisfaction;
int   fameDelta = PlayerDataManager.Instance.Fame.GetDailyFame(avgSat);
// (avgSat - 50) * 0.8f, 반올림
// 범위: 만족도 0 → -40, 만족도 50 → 0, 만족도 100 → +40
PlayerDataManager.Instance.Fame.AddFame(fameDelta);
```

### 마일스톤 해금 처리

`PlayerFameData.AddFame()` → 마일스톤 통과 시 `OnFameChanged` 이벤트 → `GameHUD.ShowNotification()` 토스트.

**DayManager.SpawnGuestsRoutine()** — 스폰 수를 `PlayerFameData.MaxGuestsPerDay()`로 결정:
```csharp
int guestCount = PlayerDataManager.Instance.Fame.MaxGuestsPerDay();
```

**MenuSetupUI** — 슬롯 수를 `PlayerFameData.MenuSlotCount()`로 결정.

### 저장/로드

```csharp
// Save:
data.TotalFame = PlayerDataManager.Instance.Fame.Save();

// Load:
PlayerDataManager.Instance.Fame.Load(data.TotalFame);
```

---

## 섹션 6: 파일 변경 요약

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Data/Player/PlayerFameData.cs` | **신규** |
| `Assets/Scripts/Restaurant/DailyMenuData.cs` | **신규** |
| `Assets/Scripts/UI/Restaurant/MenuSetupUI.cs` | **신규** |
| `Assets/Scripts/UI/Restaurant/SettlementUI.cs` | **신규** |
| `Assets/UI/MenuSetupPanel.uxml` | **신규** |
| `Assets/UI/MenuSetupPanel.uss` | **신규** |
| `Assets/UI/SettlementPanel.uxml` | **신규** |
| `Assets/UI/SettlementPanel.uss` | **신규** |
| `Assets/Scripts/Restaurant/CustomerAI.cs` | 만족도 필드 + 팁 계산 추가 |
| `Assets/Scripts/Restaurant/RestaurantTable.cs` | `IsDirty` + `IInteractable` 구현 |
| `Assets/Scripts/Restaurant/RestaurantSceneController.cs` | MenuSetupUI 자동 열기 |
| `Assets/Scripts/Core/Managers/DayManager.cs` | 일일 통계 + OnAllGuestsLeft 이벤트 |
| `Assets/Scripts/Core/Managers/PlayerDataManager.cs` | `Fame` + `DailyMenu` 프로퍼티 추가 |
| `Assets/Scripts/Data/Server/ServerSaveData.cs` | `TotalFame` 필드 추가 |

---

## 테스트 기준 (모듈 8 완료 조건)

1. RestaurantScene 진입 → 메뉴 구성 UI 자동 열림
2. FoodInventory 음식 선택 → 슬롯 배치 → 가격 조정 → "영업 시작"
3. 손님 등장 → 메뉴 내 음식만 주문
4. 빠른 서빙 (Patience 50% 이내) → 만족도 높음 → 팁 발생
5. 느린 서빙 → 만족도 낮음 → 팁 없음
6. 손님 퇴장 → 테이블 더러운 오버레이 표시 → E키 청소 → 오버레이 제거
7. 더러운 테이블 착석 손님 → Patience 감소 속도 1.5x 확인
8. 마지막 손님 퇴장 → 정산 화면 자동 표시 (매출/만족도/명성 표시)
9. 명성 100 → "손님 4명/일" 해금 알림 → 다음 날 손님 4명 스폰
10. 명성 250 → "메뉴 슬롯 4개" 해금 알림 → MenuSetupUI 슬롯 4칸
11. 저장 → 재시작 → 명성 수치 유지
