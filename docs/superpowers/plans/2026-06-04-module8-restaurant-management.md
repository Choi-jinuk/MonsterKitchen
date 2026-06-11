# Module 8 — Restaurant Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Dave the Diver–style restaurant management: pre-service menu board, per-customer satisfaction scoring, table cleaning, daily settlement screen, and fame/reputation system.

**Architecture:**
- `PlayerFameData` (pure C#) tracks total fame, computes daily delta, exposes milestone helpers.
- `DailyMenuData` (pure C#) holds today's menu slots; reset each restaurant visit.
- `CustomerAI` gains `m_Satisfaction` field; `DayManager` aggregates daily stats.
- `DayManager.OnAllGuestsLeft` replaces direct `EndDayRoutine` trigger; `RestaurantSceneController` coordinates `MenuSetupUI → gameplay → SettlementUI` flow.

**Tech Stack:** C# (pure data classes), Unity UI Toolkit (UIPanel subclasses), NUnit (EditMode tests).

**Spec:** `docs/superpowers/specs/2026-06-04-restaurant-management-design.md`

---

## File Map

| Action | File |
|---|---|
| **Create** | `Assets/Scripts/Data/Player/PlayerFameData.cs` |
| **Create** | `Assets/Tests/EditMode/PlayerFameDataTests.cs` |
| **Create** | `Assets/Scripts/Restaurant/DailyMenuData.cs` |
| **Create** | `Assets/Scripts/UI/Restaurant/MenuSetupUI.cs` |
| **Create** | `Assets/Scripts/UI/Restaurant/SettlementUI.cs` |
| **Create** | `Assets/UI/MenuSetupPanel.uxml` |
| **Create** | `Assets/UI/MenuSetupPanel.uss` |
| **Create** | `Assets/UI/SettlementPanel.uxml` |
| **Create** | `Assets/UI/SettlementPanel.uss` |
| Modify | `Assets/Scripts/Core/Managers/PlayerDataManager.cs` |
| Modify | `Assets/Scripts/Data/Server/ServerSaveData.cs` |
| Modify | `Assets/Scripts/Restaurant/CustomerAI.cs` |
| Modify | `Assets/Scripts/Restaurant/RestaurantTable.cs` |
| Modify | `Assets/Scripts/Core/Managers/DayManager.cs` |
| Modify | `Assets/Scripts/Restaurant/RestaurantSceneController.cs` |

---

## Task 1: PlayerFameData — data class + save/load

**Files:**
- Create: `Assets/Scripts/Data/Player/PlayerFameData.cs`
- Create: `Assets/Tests/EditMode/PlayerFameDataTests.cs`
- Modify: `Assets/Scripts/Data/Server/ServerSaveData.cs`
- Modify: `Assets/Scripts/Core/Managers/PlayerDataManager.cs`

- [ ] **Step 1: Write failing tests**

Create `Assets/Tests/EditMode/PlayerFameDataTests.cs`:

```csharp
using NUnit.Framework;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class PlayerFameDataTests
    {
        PlayerFameData m_Fame;

        [SetUp] public void SetUp() => m_Fame = new PlayerFameData();

        [Test]
        public void TotalFame_Initial_IsZero()
            => Assert.AreEqual(0, m_Fame.TotalFame);

        [Test]
        public void AddFame_Positive_IncreasesFame()
        {
            m_Fame.AddFame(50);
            Assert.AreEqual(50, m_Fame.TotalFame);
        }

        [Test]
        public void AddFame_Negative_ClampedAtZero()
        {
            m_Fame.AddFame(-999);
            Assert.AreEqual(0, m_Fame.TotalFame);
        }

        [Test]
        public void GetDailyFame_Satisfaction50_Returns0()
            => Assert.AreEqual(0, m_Fame.GetDailyFame(50f));

        [Test]
        public void GetDailyFame_Satisfaction100_Returns40()
            => Assert.AreEqual(40, m_Fame.GetDailyFame(100f));

        [Test]
        public void GetDailyFame_Satisfaction0_ReturnsMinus40()
            => Assert.AreEqual(-40, m_Fame.GetDailyFame(0f));

        [Test]
        public void MaxGuestsPerDay_Fame0_Returns3()
            => Assert.AreEqual(3, m_Fame.MaxGuestsPerDay());

        [Test]
        public void MaxGuestsPerDay_Fame100_Returns4()
        {
            m_Fame.AddFame(100);
            Assert.AreEqual(4, m_Fame.MaxGuestsPerDay());
        }

        [Test]
        public void MenuSlotCount_Fame0_Returns3()
            => Assert.AreEqual(3, m_Fame.MenuSlotCount());

        [Test]
        public void MenuSlotCount_Fame250_Returns4()
        {
            m_Fame.AddFame(250);
            Assert.AreEqual(4, m_Fame.MenuSlotCount());
        }

        [Test]
        public void Load_RestoresFame()
        {
            m_Fame.Load(300);
            Assert.AreEqual(300, m_Fame.TotalFame);
        }

        [Test]
        public void OnFameChanged_FiredOnAddFame()
        {
            int fired = 0;
            m_Fame.OnFameChanged += (_, __) => fired++;
            m_Fame.AddFame(10);
            Assert.AreEqual(1, fired);
        }
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

Expected: compile error — `PlayerFameData` not defined.

- [ ] **Step 3: Create PlayerFameData.cs**

Create `Assets/Scripts/Data/Player/PlayerFameData.cs`:

```csharp
using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  PlayerFameData — 식당 명성(평판) 추적
    //
    //  ▶ 명성 획득: (일일 평균 만족도 - 50) * 0.8f, 하한 0
    //  ▶ 마일스톤
    //    100  → 손님 4명/일
    //    250  → 메뉴 슬롯 4개
    //    500  → 특수 손님 해금
    //    1000 → 비밀 레시피 힌트
    // ====================================================================

    public class PlayerFameData
    {
        int m_TotalFame;

        public int TotalFame => m_TotalFame;

        public event Action<int, int> OnFameChanged;  // (oldFame, newFame)

        // ── 명성 변경 ────────────────────────────────────────────────

        public void AddFame(int amount)
        {
            int old = m_TotalFame;
            m_TotalFame = Mathf.Max(0, m_TotalFame + amount);
            OnFameChanged?.Invoke(old, m_TotalFame);
        }

        // ── 일일 명성 계산 ────────────────────────────────────────────

        /// <summary>일일 평균 만족도 → 획득 명성. (avgSat - 50) * 0.8f, 반올림.</summary>
        public int GetDailyFame(float avgSatisfaction)
            => Mathf.RoundToInt((avgSatisfaction - 50f) * 0.8f);

        // ── 마일스톤 조회 ─────────────────────────────────────────────

        /// <summary>현재 명성 기준 하루 최대 손님 수.</summary>
        public int MaxGuestsPerDay()
        {
            if (m_TotalFame >= 100) return 4;
            return 3;
        }

        /// <summary>현재 명성 기준 메뉴 슬롯 수.</summary>
        public int MenuSlotCount()
        {
            if (m_TotalFame >= 250) return 4;
            return 3;
        }

        /// <summary>특수 손님 해금 여부 (명성 500 이상).</summary>
        public bool IsSpecialGuestUnlocked() => m_TotalFame >= 500;

        // ── 저장 / 로드 ───────────────────────────────────────────────

        public int  Save() => m_TotalFame;
        public void Load(int savedFame) => m_TotalFame = Mathf.Max(0, savedFame);
    }
}
```

- [ ] **Step 4: Add TotalFame to ServerSaveData**

In `Assets/Scripts/Data/Server/ServerSaveData.cs`, add after `public int Gold;`:

```csharp
// ── 명성 ──────────────────────────────────────────────────────
public int TotalFame;
```

- [ ] **Step 5: Add Fame to PlayerDataManager**

In `Assets/Scripts/Core/Managers/PlayerDataManager.cs`:

Add property after `Mastery`:
```csharp
public PlayerFameData Fame { get; private set; }
```

In `Init()`, add after `Mastery = new PlayerCookingMasteryData();`:
```csharp
Fame = new PlayerFameData();
```

In `LoadFrom()`, add after `Mastery.LoadCounts(...)` block:
```csharp
Fame.Load(save.TotalFame);
```

In `ServerDBManager.Save()` — open `ServerDBManager.cs` and add fame save. Find the save block and add:
```csharp
data.TotalFame = PlayerDataManager.Instance.Fame.Save();
```

- [ ] **Step 6: Run tests — verify they pass**

All `PlayerFameDataTests` should pass.

- [ ] **Step 7: Check compilation in Unity**

Open Unity, `read_console` — no compile errors.

- [ ] **Step 8: Commit**

```
git add Assets/Scripts/Data/Player/PlayerFameData.cs \
        Assets/Tests/EditMode/PlayerFameDataTests.cs \
        Assets/Scripts/Data/Server/ServerSaveData.cs \
        Assets/Scripts/Core/Managers/PlayerDataManager.cs
git commit -m "feat: add PlayerFameData with milestone helpers, wire to PlayerDataManager + ServerSaveData"
```

---

## Task 2: DailyMenuData + PlayerDataManager.DailyMenu

**Files:**
- Create: `Assets/Scripts/Restaurant/DailyMenuData.cs`
- Modify: `Assets/Scripts/Core/Managers/PlayerDataManager.cs`
- Modify: `Assets/Scripts/Restaurant/CustomerAI.cs`

- [ ] **Step 1: Create DailyMenuData.cs**

Create `Assets/Scripts/Restaurant/DailyMenuData.cs`:

```csharp
using System.Collections.Generic;

namespace MonsterKitchen.Restaurant
{
    // ====================================================================
    //  DailyMenuData — 하루 메뉴 구성 (비저장, 매 식당 방문 시 초기화)
    //
    //  ▶ 슬롯 수: PlayerFameData.MenuSlotCount() 기준
    //  ▶ 가격 범위: FoodData.BasePrice ± 30%
    // ====================================================================

    public class DailyMenuData
    {
        public struct MenuSlot
        {
            public uint FoodId;
            public int  CustomPrice;
            public bool IsEmpty => FoodId == 0u;
        }

        List<MenuSlot> m_Slots = new();

        public IReadOnlyList<MenuSlot> Slots => m_Slots;
        public int SlotCount => m_Slots.Count;

        // ── 초기화 ────────────────────────────────────────────────────

        /// <summary>식당 씬 진입 시 슬롯 수 지정 후 초기화.</summary>
        public void Reset(int slotCount)
        {
            m_Slots.Clear();
            for (int i = 0; i < slotCount; i++)
                m_Slots.Add(new MenuSlot());
        }

        // ── 슬롯 조작 ────────────────────────────────────────────────

        public void SetSlot(int index, uint foodId, int customPrice)
        {
            if (index < 0 || index >= m_Slots.Count) return;
            m_Slots[index] = new MenuSlot { FoodId = foodId, CustomPrice = customPrice };
        }

        public void ClearSlot(int index)
        {
            if (index < 0 || index >= m_Slots.Count) return;
            m_Slots[index] = new MenuSlot();
        }

        // ── 조회 ─────────────────────────────────────────────────────

        public bool IsOnMenu(uint foodId)
        {
            if (foodId == 0u) return false;
            foreach (var slot in m_Slots)
                if (!slot.IsEmpty && slot.FoodId == foodId) return true;
            return false;
        }

        public int GetPrice(uint foodId)
        {
            foreach (var slot in m_Slots)
                if (!slot.IsEmpty && slot.FoodId == foodId) return slot.CustomPrice;
            return 0;
        }

        public bool HasAnyItem()
        {
            foreach (var slot in m_Slots)
                if (!slot.IsEmpty) return true;
            return false;
        }

        // ── 빈 슬롯 인덱스 ────────────────────────────────────────────

        public int FirstEmptySlotIndex()
        {
            for (int i = 0; i < m_Slots.Count; i++)
                if (m_Slots[i].IsEmpty) return i;
            return -1;
        }
    }
}
```

- [ ] **Step 2: Add DailyMenu to PlayerDataManager**

In `Assets/Scripts/Core/Managers/PlayerDataManager.cs`:

Add property after `Fame`:
```csharp
public DailyMenuData DailyMenu { get; private set; }
```

Add using at top if not present:
```csharp
using MonsterKitchen.Restaurant;
```

In `Init()`, add after `Fame = new PlayerFameData();`:
```csharp
DailyMenu = new DailyMenuData();
```

- [ ] **Step 3: Update CustomerAI.PickOrder() to respect DailyMenu**

In `Assets/Scripts/Restaurant/CustomerAI.cs`, replace `PickOrder()`:

```csharp
FoodData PickOrder()
{
    if (m_PossibleOrderIds == null || m_PossibleOrderIds.Length == 0) return null;

    var inv      = PlayerDataManager.Instance?.Inventory;
    var menu     = PlayerDataManager.Instance?.DailyMenu;
    var registry = DataRegistry.Instance;

    // 메뉴 + 인벤토리 교집합에서 첫 번째 주문 가능 음식
    foreach (var foodId in m_PossibleOrderIds)
    {
        if (foodId == 0u) continue;
        bool onMenu = menu == null || !menu.HasAnyItem() || menu.IsOnMenu(foodId);
        if (onMenu && inv != null && inv.GetFoodCount(foodId) > 0)
            return registry?.Foods?.Get(foodId);
    }

    // 폴백: 첫 번째 가능한 음식 (재고 무시)
    return registry?.Foods?.Get(m_PossibleOrderIds[0]);
}
```

- [ ] **Step 4: Check compilation in Unity**

`read_console` — no compile errors.

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/Restaurant/DailyMenuData.cs \
        Assets/Scripts/Core/Managers/PlayerDataManager.cs \
        Assets/Scripts/Restaurant/CustomerAI.cs
git commit -m "feat: add DailyMenuData and wire CustomerAI.PickOrder to respect daily menu"
```

---

## Task 3: CustomerAI satisfaction + RestaurantTable IsDirty

**Files:**
- Modify: `Assets/Scripts/Restaurant/CustomerAI.cs`
- Modify: `Assets/Scripts/Restaurant/RestaurantTable.cs`

- [ ] **Step 1: Add satisfaction tracking to CustomerAI**

In `Assets/Scripts/Restaurant/CustomerAI.cs`:

Add fields after existing fields:

```csharp
int   m_Satisfaction      = 50;   // 0~100
float m_PatienceDecayMult = 1.0f; // 더러운 테이블 시 1.5f
```

Add public property after `IsWaiting`:
```csharp
public int Satisfaction => m_Satisfaction;
```

Add helper method before `TickEntering`:
```csharp
void AddSatisfaction(int delta)
    => m_Satisfaction = Mathf.Clamp(m_Satisfaction + delta, 0, 100);
```

In `EnterSeated()`, after `m_Anim.SetFloat(s_HashSpeed, 0f);` add dirty-table check:
```csharp
// 더러운 테이블 착석 페널티
if (m_Table != null && m_Table.IsDirty)
{
    AddSatisfaction(-5);
    m_PatienceDecayMult = 1.5f;
    DebugUtil.Log("[Customer] 더러운 테이블 착석 — 만족도 -5, 인내심 감소 1.5×");
}
```

In `TickWaiting()`, change timer decrement to:
```csharp
m_PatienceTimer -= Time.deltaTime * m_PatienceDecayMult;
```

In `Serve()`, before `m_State = State.Served;` add timing + grade satisfaction:
```csharp
// 서빙 타이밍 보너스/패널티
float patienceRatio = m_PatienceTimer / m_Patience;
if (patienceRatio >= 0.5f)
    AddSatisfaction(+10);   // 빠른 서빙
else
    AddSatisfaction(-10);   // 느린 서빙

// 음식 등급 보너스
switch (grade)
{
    case FoodGrade.Perfect:   AddSatisfaction(+10); break;
    case FoodGrade.Legendary: AddSatisfaction(+15); break;
}
// 정확한 음식 서빙 기본 보너스
AddSatisfaction(+20);
```

Replace `EatAndPay()` tip logic (keep payment calc, replace tip block):
```csharp
IEnumerator EatAndPay(FoodData food, FoodGrade grade)
{
    m_Anim.SetTrigger(s_HashEat);
    yield return new WaitForSeconds(m_EatDuration);

    float gradeMult = grade switch
    {
        FoodGrade.Good      => food.GoodMultiplier,
        FoodGrade.Perfect   => food.PerfectMultiplier,
        FoodGrade.Legendary => food.LegendaryMultiplier,
        _                   => 1f,
    };

    float shopMult    = PlayerDataManager.Instance?.Upgrades.ShopTipMultiplier ?? 1f;
    var   recipe      = DataRegistry.Instance?.GetRecipeByFoodId(food.Id);
    float masteryMult = recipe != null
        ? (PlayerDataManager.Instance?.Mastery?.GetPriceMultiplier(recipe.Id) ?? 1f)
        : 1f;
    int pay = Mathf.RoundToInt(food.BasePrice * gradeMult * shopMult * masteryMult);
    NetworkManager.Instance?.RequestEarnGold(pay);

    // 만족도 기반 팁
    int tip = CalculateTip(pay);
    if (tip > 0)
    {
        NetworkManager.Instance?.RequestEarnGold(tip);
        if (m_Satisfaction >= 100)
            GameHUD.Instance?.ShowNotification("완벽한 서비스!", 2f);
    }

    DebugUtil.Log($"[Customer] {food.DisplayName} [{grade}] 식사 완료. 지불: {pay}G, 팁: {tip}G, 만족도: {m_Satisfaction}");

    // DayManager 일일 통계 기록
    DayManager.Instance?.RecordServing(pay, tip, m_Satisfaction);

    StartCoroutine(LeaveRoutine(paid: true));
}
```

Add `CalculateTip` method:
```csharp
int CalculateTip(int basePayment)
{
    if (m_Satisfaction < 70)  return 0;
    if (m_Satisfaction < 85)  return Mathf.RoundToInt(basePayment * 0.10f);
    return Mathf.RoundToInt(basePayment * 0.20f);
}
```

In `LeaveRoutine()`, before `m_State = State.Leaving;` in the `paid: false` path:
```csharp
IEnumerator LeaveRoutine(bool paid)
{
    if (!paid)
    {
        // 인내심 만료 만족도 패널티
        AddSatisfaction(-30);
        DayManager.Instance?.RecordServing(0, 0, m_Satisfaction);
        DebugUtil.Log($"[Customer] 인내심 만료 퇴장. 만족도: {m_Satisfaction}");
    }

    m_State = State.Leaving;
    m_Table.Vacate();
    m_Table.SetDirty(true);    // 퇴장 후 테이블 오염
    m_Anim.SetTrigger(s_HashLeave);

    Vector2 exit = m_ExitPoint;
    while (Vector2.Distance(transform.position, exit) > 0.15f)
    {
        Vector2 dir = (exit - (Vector2)transform.position).normalized;
        m_Rb.linearVelocity = dir * m_MoveSpeed;
        m_Anim.SetFloat(s_HashSpeed, m_MoveSpeed);
        yield return null;
    }

    m_State = State.Done;
    OnGuestFinished?.Invoke();
    Destroy(gameObject);
}
```

- [ ] **Step 2: Add IsDirty + cleaning interaction to RestaurantTable**

Replace `Assets/Scripts/Restaurant/RestaurantTable.cs` entirely:

```csharp
using System.Collections;
using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Restaurant
{
    /// <summary>
    /// 식당 테이블 1개. 착석 여부 + 청결 상태를 관리한다.
    /// 손님 퇴장 시 IsDirty=true. 플레이어 E키로 1.5초 청소 가능.
    /// </summary>
    public class RestaurantTable : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Transform   m_SeatPoint;
        [SerializeField] GameObject  m_DirtyOverlay;   // 더러운 비주얼 오버레이

        [Header("Settings")]
        [SerializeField] float m_CleanDuration = 1.5f;

        public bool       IsOccupied { get; private set; }
        public bool       IsDirty    { get; private set; }
        public CustomerAI Occupant   { get; private set; }

        public Transform SeatPoint => m_SeatPoint != null ? m_SeatPoint : transform;

        bool m_PlayerNearby;
        bool m_IsCleaning;

        // ── 착석 / 퇴장 ───────────────────────────────────────────────

        public void Occupy(CustomerAI customer)
        {
            IsOccupied = true;
            Occupant   = customer;
        }

        public void Vacate()
        {
            IsOccupied = false;
            Occupant   = null;
        }

        // ── 청결 상태 ─────────────────────────────────────────────────

        public void SetDirty(bool dirty)
        {
            IsDirty = dirty;
            if (m_DirtyOverlay != null)
                m_DirtyOverlay.SetActive(dirty);
        }

        // ── 플레이어 근접 청소 상호작용 ────────────────────────────────

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            m_PlayerNearby = true;
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract += TryClean;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            m_PlayerNearby = false;
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract -= TryClean;
        }

        void OnDisable()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract -= TryClean;
        }

        void TryClean()
        {
            if (!IsDirty || IsOccupied || m_IsCleaning) return;
            StartCoroutine(CleanRoutine());
        }

        IEnumerator CleanRoutine()
        {
            m_IsCleaning = true;
            DebugUtil.Log("[Table] 청소 중...");
            yield return new WaitForSeconds(m_CleanDuration);
            SetDirty(false);
            m_IsCleaning = false;
            DebugUtil.Log("[Table] 청소 완료.");
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_SeatPoint == null)
                DebugUtil.LogWarning("[RestaurantTable] SeatPoint 미연결.", this);
            if (m_DirtyOverlay == null)
                DebugUtil.LogWarning("[RestaurantTable] DirtyOverlay 미연결.", this);
        }
#endif
    }
}
```

- [ ] **Step 3: Check compilation + verify**

`read_console` — no errors.

Play-test in RestaurantScene:
1. 손님 퇴장 시 테이블에 DirtyOverlay 활성화 확인
2. E키 → 1.5초 후 DirtyOverlay 비활성화 확인
3. 더러운 테이블에 손님 착석 → Console에서 만족도 -5 로그 확인

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/Restaurant/CustomerAI.cs \
        Assets/Scripts/Restaurant/RestaurantTable.cs
git commit -m "feat: add satisfaction tracking to CustomerAI and IsDirty cleaning to RestaurantTable"
```

---

## Task 4: DayManager — stats tracking + OnAllGuestsLeft + CompleteDay

**Files:**
- Modify: `Assets/Scripts/Core/Managers/DayManager.cs`

- [ ] **Step 1: Replace DayManager.cs with extended version**

Replace `Assets/Scripts/Core/Managers/DayManager.cs`:

```csharp
using MonsterKitchen.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonsterKitchen.Data;
using MonsterKitchen.Restaurant;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 날 카운터 + 식당 영업 오픈/마감.
    /// GlobalController 가 new DayManager(this) 로 생성하고 Init() 를 호출한다.
    /// </summary>
    public class DayManager
    {
        public static DayManager Instance { get; private set; }

        readonly MonoBehaviour m_Runner;

        Transform         m_GuestSpawnPoint;
        CustomerAI        m_CustomerPrefab;
        RestaurantTable[] m_Tables;

        const float TIME_BETWEEN_GUESTS = 8f;

        public int  CurrentDay { get; private set; }
        public bool IsOpen     { get; private set; }

        public event Action<int> OnDayStarted;
        public event Action<int> OnDayEnded;
        /// <summary>모든 손님이 퇴장 완료 시 발행. SettlementUI 표시 트리거.</summary>
        public event Action      OnAllGuestsLeft;

        // ── 일일 통계 ────────────────────────────────────────────────
        int        m_DayRevenue;
        int        m_DayTips;
        int        m_GuestsServed;
        int        m_GuestsArrived;
        int        m_GuestsForToday;
        List<int>  m_SatisfactionScores = new();

        public int   DayRevenue   => m_DayRevenue;
        public int   DayTips      => m_DayTips;
        public int   GuestsServed => m_GuestsServed;
        public int   GuestsArrived => m_GuestsArrived;
        public float AvgSatisfaction => m_SatisfactionScores.Count > 0
            ? (float)m_SatisfactionScores.Average() : 50f;

        int m_GuestsFinished;

        public DayManager(MonoBehaviour runner) => m_Runner = runner;

        public void Init()
        {
            Instance   = this;
            CurrentDay = 1;
        }

        public void AdvanceToNextDay()
        {
            CurrentDay++;
            DebugUtil.Log($"[DayManager] Day {CurrentDay} 시작.");
        }

        public void StartDay()
        {
            if (IsOpen) return;
            IsOpen           = true;
            m_GuestsFinished = 0;
            m_GuestsArrived  = 0;
            m_GuestsForToday = GetGuestCountForDay();
            ResetDailyStats();
            OnDayStarted?.Invoke(CurrentDay);
            DebugUtil.Log($"[DayManager] Day {CurrentDay} 영업 시작! (손님 {m_GuestsForToday}명)");
            m_Runner.StartCoroutine(SpawnGuestsRoutine());
        }

        // ── 일일 통계 기록 ───────────────────────────────────────────

        /// <summary>CustomerAI 가 서빙 완료 또는 인내심 만료 시 호출.</summary>
        public void RecordServing(int payment, int tip, int satisfaction)
        {
            m_DayRevenue   += payment;
            m_DayTips      += tip;
            if (payment > 0) m_GuestsServed++;
            m_SatisfactionScores.Add(satisfaction);
        }

        void ResetDailyStats()
        {
            m_DayRevenue   = 0;
            m_DayTips      = 0;
            m_GuestsServed = 0;
            m_SatisfactionScores.Clear();
        }

        // ── 손님 수 계산 (명성 기반) ─────────────────────────────────

        int GetGuestCountForDay()
            => PlayerDataManager.Instance?.Fame?.MaxGuestsPerDay() ?? 3;

        // ── 스폰 ────────────────────────────────────────────────────

        IEnumerator SpawnGuestsRoutine()
        {
            while (m_GuestsArrived < m_GuestsForToday)
            {
                yield return new WaitForSeconds(m_GuestsArrived == 0 ? 1f : TIME_BETWEEN_GUESTS);

                var table = FindFreeTable();
                if (table == null) { yield return new WaitForSeconds(2f); continue; }

                SpawnGuest(table);
                m_GuestsArrived++;
            }
        }

        void SpawnGuest(RestaurantTable table)
        {
            if (m_CustomerPrefab == null || m_GuestSpawnPoint == null) return;
            var go = UnityEngine.Object.Instantiate(m_CustomerPrefab,
                m_GuestSpawnPoint.position, Quaternion.identity);
            go.gameObject.SetActive(true);
            go.Init(table);
            go.OnGuestFinished += HandleGuestFinished;
        }

        void HandleGuestFinished()
        {
            m_GuestsFinished++;
            if (m_GuestsFinished >= m_GuestsForToday)
                OnAllGuestsLeft?.Invoke();
        }

        // ── 영업 종료 ────────────────────────────────────────────────

        /// <summary>SettlementUI "다음 날로" 버튼이 호출한다.</summary>
        public void CompleteDay()
        {
            m_Runner.StartCoroutine(EndDayRoutine());
        }

        IEnumerator EndDayRoutine()
        {
            yield return new WaitForSeconds(1f);
            IsOpen = false;
            OnDayEnded?.Invoke(CurrentDay);
            DebugUtil.Log($"[DayManager] Day {CurrentDay} 영업 마감.");

            yield return new WaitForSeconds(0.5f);
            if (PhaseManager.Instance != null)
                PhaseManager.Instance.EndDay();
            else
                SceneLoader.Instance?.LoadScene("ManagementScene");
        }

        RestaurantTable FindFreeTable()
        {
            if (m_Tables == null) return null;
            foreach (var t in m_Tables)
                if (t != null && !t.IsOccupied) return t;
            return null;
        }

        public void SetTables(RestaurantTable[] t) => m_Tables = t;

        public void RestoreDay(int day) { if (day > 0) CurrentDay = day; }

        public void SetRestaurantConfig(Transform spawnPoint, CustomerAI prefab, RestaurantTable[] tables)
        {
            m_GuestSpawnPoint = spawnPoint;
            m_CustomerPrefab  = prefab;
            m_Tables          = tables;
        }
    }
}
```

- [ ] **Step 2: Add `using System.Linq;` to DayManager if not already present**

Check that `using System.Linq;` is in the using block.

- [ ] **Step 3: Check compilation in Unity**

`read_console` — no errors.

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/Core/Managers/DayManager.cs
git commit -m "feat: add DayManager daily stats tracking, OnAllGuestsLeft event, CompleteDay(), fame-based guest count"
```

---

## Task 5: MenuSetupUI — UXML + USS + CS

**Files:**
- Create: `Assets/UI/MenuSetupPanel.uxml`
- Create: `Assets/UI/MenuSetupPanel.uss`
- Create: `Assets/Scripts/UI/Restaurant/MenuSetupUI.cs`

- [ ] **Step 1: Create MenuSetupPanel.uxml**

Create `Assets/UI/MenuSetupPanel.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <ui:Style src="project://database/Assets/UI/MenuSetupPanel.uss" />

    <ui:VisualElement name="menu-setup-overlay" class="menu-setup-overlay">
        <ui:VisualElement name="menu-setup-panel" class="menu-setup-panel">

            <ui:Label text="오늘의 메뉴 구성" class="menu-setup-title" />

            <!-- 메뉴 슬롯 영역 (코드에서 동적 생성) -->
            <ui:VisualElement name="menu-slots-container" class="menu-slots-container" />

            <!-- 보유 음식 목록 -->
            <ui:Label text="보유 음식" class="menu-setup-section-title" />
            <ui:ScrollView name="menu-food-scroll" class="menu-food-scroll"
                           horizontal-scroller-visibility="Hidden">
                <ui:VisualElement name="menu-food-grid" class="menu-food-grid" />
            </ui:ScrollView>

            <!-- 영업 시작 버튼 -->
            <ui:Button name="menu-start-btn" text="영업 시작" class="menu-start-btn" />

        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Create MenuSetupPanel.uss**

Create `Assets/UI/MenuSetupPanel.uss`:

```css
.menu-setup-overlay {
    flex-grow: 1;
    background-color: rgba(0, 0, 0, 0.65);
    align-items: center;
    justify-content: center;
}

.menu-setup-panel {
    width: 500px;
    background-color: rgb(28, 22, 18);
    border-radius: 12px;
    border-width: 2px;
    border-color: rgb(90, 70, 45);
    padding: 24px;
    flex-direction: column;
}

.menu-setup-title {
    font-size: 24px;
    -unity-font-style: bold;
    color: rgb(240, 210, 140);
    -unity-text-align: middle-center;
    margin-bottom: 16px;
}

.menu-setup-section-title {
    font-size: 14px;
    color: rgb(180, 150, 100);
    margin-top: 12px;
    margin-bottom: 6px;
}

.menu-slots-container {
    flex-direction: column;
}

.menu-slot-row {
    flex-direction: row;
    align-items: center;
    background-color: rgb(40, 32, 24);
    border-radius: 8px;
    padding: 10px 12px;
    margin-bottom: 8px;
}

.menu-slot-label {
    font-size: 15px;
    color: rgb(220, 200, 160);
    flex-grow: 1;
}

.menu-slot-price-label {
    font-size: 14px;
    color: rgb(255, 210, 80);
    width: 70px;
    -unity-text-align: middle-right;
}

.menu-slot-clear-btn {
    font-size: 13px;
    color: rgb(200, 100, 80);
    background-color: rgba(0,0,0,0);
    border-width: 0;
    width: 28px;
    -unity-text-align: middle-center;
    margin-left: 6px;
}

.menu-slot-empty-label {
    font-size: 14px;
    color: rgb(120, 100, 80);
    font-style: italic;
    flex-grow: 1;
}

.menu-food-scroll {
    max-height: 160px;
}

.menu-food-grid {
    flex-direction: row;
    flex-wrap: wrap;
}

.menu-food-item {
    width: 100px;
    background-color: rgb(40, 32, 24);
    border-radius: 8px;
    padding: 8px;
    margin: 4px;
    align-items: center;
    border-width: 1px;
    border-color: rgb(70, 55, 35);
}

.menu-food-item:hover {
    border-color: rgb(240, 210, 140);
}

.menu-food-name {
    font-size: 12px;
    color: rgb(220, 200, 160);
    -unity-text-align: middle-center;
    white-space: normal;
}

.menu-food-count {
    font-size: 11px;
    color: rgb(160, 140, 100);
    -unity-text-align: middle-center;
}

.menu-start-btn {
    margin-top: 16px;
    height: 44px;
    font-size: 18px;
    -unity-font-style: bold;
    color: rgb(30, 20, 10);
    background-color: rgb(220, 170, 60);
    border-radius: 8px;
    border-width: 0;
}

.menu-start-btn:hover {
    background-color: rgb(240, 190, 80);
}

.menu-start-btn:disabled {
    background-color: rgb(80, 65, 45);
    color: rgb(120, 100, 70);
}
```

- [ ] **Step 3: Create MenuSetupUI.cs**

Create `Assets/Scripts/UI/Restaurant/MenuSetupUI.cs`:

```csharp
using System;
using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  MenuSetupUI — 영업 전 메뉴 구성 패널
    //
    //  ▶ RestaurantSceneController 가 OnInit 시 열고 OnStartDay 콜백을 주입.
    //  ▶ "영업 시작" 버튼 클릭 → OnStartDay() 호출 → DayManager.StartDay().
    // ====================================================================

    public class MenuSetupUI : UIPanel
    {
        public Action OnStartDay;   // RestaurantSceneController 주입

        VisualElement m_SlotsContainer;
        VisualElement m_FoodGrid;
        Button        m_StartBtn;

        // ── UIPanel 훅 ────────────────────────────────────────────────

        protected override void OnFirstOpen()
        {
            m_SlotsContainer = Root?.Q<VisualElement>("menu-slots-container");
            m_FoodGrid       = Root?.Q<VisualElement>("menu-food-grid");
            m_StartBtn       = Root?.Q<Button>("menu-start-btn");

            m_StartBtn?.RegisterCallback<ClickEvent>(_ => HandleStartDay());
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Refresh();
        }

        // ── 갱신 ─────────────────────────────────────────────────────

        void Refresh()
        {
            BuildSlots();
            BuildFoodGrid();
            UpdateStartButton();
        }

        // ── 메뉴 슬롯 ────────────────────────────────────────────────

        void BuildSlots()
        {
            m_SlotsContainer?.Clear();
            var menu = PlayerDataManager.Instance?.DailyMenu;
            if (menu == null) return;

            for (int i = 0; i < menu.SlotCount; i++)
            {
                int idx  = i;
                var slot = menu.Slots[idx];
                var row  = new VisualElement();
                row.AddToClassList("menu-slot-row");

                if (slot.IsEmpty)
                {
                    var emptyLabel = new Label($"슬롯 {idx + 1} — 비어있음");
                    emptyLabel.AddToClassList("menu-slot-empty-label");
                    row.Add(emptyLabel);
                }
                else
                {
                    var food = DataRegistry.Instance?.Foods?.Get(slot.FoodId);
                    var nameLabel = new Label(food?.DisplayName ?? $"ID:{slot.FoodId}");
                    nameLabel.AddToClassList("menu-slot-label");
                    row.Add(nameLabel);

                    var priceLabel = new Label($"{slot.CustomPrice}G");
                    priceLabel.AddToClassList("menu-slot-price-label");
                    row.Add(priceLabel);

                    var clearBtn = new Button(() => { menu.ClearSlot(idx); Refresh(); });
                    clearBtn.text = "✕";
                    clearBtn.AddToClassList("menu-slot-clear-btn");
                    row.Add(clearBtn);
                }

                m_SlotsContainer?.Add(row);
            }
        }

        // ── 음식 목록 ─────────────────────────────────────────────────

        void BuildFoodGrid()
        {
            m_FoodGrid?.Clear();
            var inv  = PlayerDataManager.Instance?.Inventory;
            var menu = PlayerDataManager.Instance?.DailyMenu;
            if (inv == null || menu == null) return;

            foreach (var kv in inv.AllFoods)
            {
                if (kv.Value <= 0) continue;
                uint foodId = kv.Key;
                if (menu.IsOnMenu(foodId)) continue;    // 이미 메뉴에 있음

                var food = DataRegistry.Instance?.Foods?.Get(foodId);
                if (food == null) continue;

                var item = new VisualElement();
                item.AddToClassList("menu-food-item");

                var nameLabel = new Label(food.DisplayName);
                nameLabel.AddToClassList("menu-food-name");
                item.Add(nameLabel);

                var countLabel = new Label($"×{kv.Value}");
                countLabel.AddToClassList("menu-food-count");
                item.Add(countLabel);

                // 클릭 → 빈 슬롯에 배치 (기본가로)
                item.RegisterCallback<ClickEvent>(_ =>
                {
                    int emptyIdx = menu.FirstEmptySlotIndex();
                    if (emptyIdx < 0) return;
                    menu.SetSlot(emptyIdx, foodId, food.BasePrice);
                    Refresh();
                });

                m_FoodGrid?.Add(item);
            }
        }

        // ── 영업 시작 버튼 ────────────────────────────────────────────

        void UpdateStartButton()
        {
            if (m_StartBtn == null) return;
            bool canStart = PlayerDataManager.Instance?.DailyMenu?.HasAnyItem() ?? false;
            m_StartBtn.SetEnabled(canStart);
        }

        void HandleStartDay()
        {
            UIManager.Instance?.Close(PanelId);
            OnStartDay?.Invoke();
        }
    }
}
```

- [ ] **Step 4: Check compilation in Unity**

`read_console` — no compile errors.

- [ ] **Step 5: Commit**

```
git add Assets/UI/MenuSetupPanel.uxml \
        Assets/UI/MenuSetupPanel.uss \
        Assets/Scripts/UI/Restaurant/MenuSetupUI.cs
git commit -m "feat: add MenuSetupUI with daily menu slot configuration"
```

---

## Task 6: SettlementUI — UXML + USS + CS

**Files:**
- Create: `Assets/UI/SettlementPanel.uxml`
- Create: `Assets/UI/SettlementPanel.uss`
- Create: `Assets/Scripts/UI/Restaurant/SettlementUI.cs`

- [ ] **Step 1: Create SettlementPanel.uxml**

Create `Assets/UI/SettlementPanel.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <ui:Style src="project://database/Assets/UI/SettlementPanel.uss" />

    <ui:VisualElement name="settlement-overlay" class="settlement-overlay">
        <ui:VisualElement name="settlement-panel" class="settlement-panel">

            <ui:Label name="settlement-title" text="영업 결산" class="settlement-title" />
            <ui:Label name="settlement-day"   text="Day 1"   class="settlement-day" />

            <ui:VisualElement class="settlement-divider" />

            <ui:Label name="settlement-guests"       text="" class="settlement-row" />
            <ui:Label name="settlement-revenue"      text="" class="settlement-row" />
            <ui:Label name="settlement-satisfaction" text="" class="settlement-row" />
            <ui:Label name="settlement-fame"         text="" class="settlement-row settlement-fame" />

            <ui:VisualElement class="settlement-divider" />

            <ui:Button name="settlement-next-btn" text="다음 날로" class="settlement-next-btn" />

        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Create SettlementPanel.uss**

Create `Assets/UI/SettlementPanel.uss`:

```css
.settlement-overlay {
    flex-grow: 1;
    background-color: rgba(0, 0, 0, 0.75);
    align-items: center;
    justify-content: center;
}

.settlement-panel {
    width: 420px;
    background-color: rgb(22, 18, 14);
    border-radius: 14px;
    border-width: 2px;
    border-color: rgb(100, 80, 50);
    padding: 28px;
    flex-direction: column;
    align-items: stretch;
}

.settlement-title {
    font-size: 26px;
    -unity-font-style: bold;
    color: rgb(255, 220, 100);
    -unity-text-align: middle-center;
    margin-bottom: 4px;
}

.settlement-day {
    font-size: 16px;
    color: rgb(180, 160, 110);
    -unity-text-align: middle-center;
    margin-bottom: 14px;
}

.settlement-divider {
    height: 1px;
    background-color: rgb(70, 55, 35);
    margin-top: 8px;
    margin-bottom: 8px;
}

.settlement-row {
    font-size: 16px;
    color: rgb(220, 200, 160);
    margin-top: 6px;
    padding-left: 8px;
}

.settlement-fame {
    color: rgb(120, 200, 255);
    -unity-font-style: bold;
}

.settlement-next-btn {
    margin-top: 20px;
    height: 46px;
    font-size: 18px;
    -unity-font-style: bold;
    color: rgb(30, 20, 10);
    background-color: rgb(220, 170, 60);
    border-radius: 8px;
    border-width: 0;
}

.settlement-next-btn:hover {
    background-color: rgb(240, 190, 80);
}
```

- [ ] **Step 3: Create SettlementUI.cs**

Create `Assets/Scripts/UI/Restaurant/SettlementUI.cs`:

```csharp
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  SettlementUI — 영업 결산 오버레이
    //
    //  ▶ RestaurantSceneController 가 DayManager.OnAllGuestsLeft 에서 호출.
    //  ▶ 명성 계산 + 적용 후 결과 표시.
    //  ▶ "다음 날로" 버튼 → DayManager.CompleteDay().
    // ====================================================================

    public class SettlementUI : UIPanel
    {
        Label m_DayLabel;
        Label m_GuestsLabel;
        Label m_RevenueLabel;
        Label m_SatisfactionLabel;
        Label m_FameLabel;

        // ── UIPanel 훅 ────────────────────────────────────────────────

        protected override void OnFirstOpen()
        {
            m_DayLabel          = Root?.Q<Label>("settlement-day");
            m_GuestsLabel       = Root?.Q<Label>("settlement-guests");
            m_RevenueLabel      = Root?.Q<Label>("settlement-revenue");
            m_SatisfactionLabel = Root?.Q<Label>("settlement-satisfaction");
            m_FameLabel         = Root?.Q<Label>("settlement-fame");

            Root?.Q<Button>("settlement-next-btn")
                ?.RegisterCallback<ClickEvent>(_ => HandleNextDay());
        }

        // ── 표시 ─────────────────────────────────────────────────────

        public override void OnOpen()
        {
            base.OnOpen();
            PopulateStats();
        }

        void PopulateStats()
        {
            var day  = DayManager.Instance;
            var fame = PlayerDataManager.Instance?.Fame;
            if (day == null) return;

            // 명성 계산 + 적용
            int fameDelta = fame?.GetDailyFame(day.AvgSatisfaction) ?? 0;
            if (fameDelta != 0)
                fame?.AddFame(fameDelta);

            if (m_DayLabel != null)
                m_DayLabel.text = $"Day {day.CurrentDay}";

            if (m_GuestsLabel != null)
                m_GuestsLabel.text = $"👥 손님   {day.GuestsServed} / {day.GuestsArrived}명";

            int totalRevenue = day.DayRevenue + day.DayTips;
            if (m_RevenueLabel != null)
                m_RevenueLabel.text = $"💰 매출   {totalRevenue} G" +
                    $"  (요리 {day.DayRevenue} + 팁 {day.DayTips})";

            int satStars = SatisfactionToStars(day.AvgSatisfaction);
            string starStr = BuildStars(satStars);
            if (m_SatisfactionLabel != null)
                m_SatisfactionLabel.text =
                    $"⭐ 평균 만족도   {day.AvgSatisfaction:F0}점  {starStr}";

            string fameSign = fameDelta >= 0 ? "+" : "";
            if (m_FameLabel != null)
                m_FameLabel.text =
                    $"🏅 명성   {fameSign}{fameDelta}  (누적 {fame?.TotalFame ?? 0})";
        }

        // ── "다음 날로" ──────────────────────────────────────────────

        void HandleNextDay()
        {
            UIManager.Instance?.Close(PanelId);
            DayManager.Instance?.CompleteDay();
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        static int SatisfactionToStars(float sat)
        {
            if (sat >= 90f) return 5;
            if (sat >= 75f) return 4;
            if (sat >= 60f) return 3;
            if (sat >= 40f) return 2;
            return 1;
        }

        static string BuildStars(int count)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 5; i++) sb.Append(i < count ? "★" : "☆");
            return sb.ToString();
        }
    }
}
```

- [ ] **Step 4: Check compilation in Unity**

`read_console` — no compile errors.

- [ ] **Step 5: Commit**

```
git add Assets/UI/SettlementPanel.uxml \
        Assets/UI/SettlementPanel.uss \
        Assets/Scripts/UI/Restaurant/SettlementUI.cs
git commit -m "feat: add SettlementUI with daily revenue/satisfaction/fame summary"
```

---

## Task 7: RestaurantSceneController integration + HUD.prefab wiring

**Files:**
- Modify: `Assets/Scripts/Restaurant/RestaurantSceneController.cs`
- Modify (Inspector): `Assets/Prefabs/UI/HUD.prefab` — MenuSetupUI + SettlementUI 컴포넌트 추가

- [ ] **Step 1: Update RestaurantSceneController.cs**

Replace `Assets/Scripts/Restaurant/RestaurantSceneController.cs`:

```csharp
// ====================================================================
//  RestaurantSceneController — RestaurantScene 컨트롤러
//
//  ▶ 상태 흐름
//    Init    : DayManager 설정 + 메뉴 구성 UI 열기
//    Menu    : MenuSetupUI에서 음식 선택 → "영업 시작" 클릭
//    Running : DayManager.StartDay() → 손님 서빙
//    End     : DayManager.OnAllGuestsLeft → SettlementUI 표시
//    Done    : SettlementUI "다음 날로" → DayManager.CompleteDay()
// ====================================================================

using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.UI;
using UnityEngine;

namespace MonsterKitchen.Restaurant
{
    [DisallowMultipleComponent]
    public class RestaurantSceneController : SceneControllerBase
    {
        [SerializeField] Transform         m_GuestSpawnPoint;
        [SerializeField] RestaurantTable[] m_Tables;

        protected override void OnInit()
        {
            var day = DayManager.Instance;
            if (day == null)
            {
                DebugUtil.LogError("[RestaurantSceneController] DayManager 없음 — StartScene 부터 시작하세요.", this);
                return;
            }

            // 1. 씬 로컬 레퍼런스 주입
            var customerPrefab = AssetLoadManager.Instance?.Load<CustomerAI>(AssetKeys.PREFAB_CUSTOMER);
            if (customerPrefab == null)
                DebugUtil.LogError("[RestaurantSceneController] Customer 프리팹 로드 실패. 키: " + AssetKeys.PREFAB_CUSTOMER, this);

            day.SetRestaurantConfig(m_GuestSpawnPoint, customerPrefab, m_Tables);

            // 2. 플레이어 위치 재조정
            GlobalController.Instance?.Player?.RepositionInScene();

            // 3. 일일 메뉴 초기화 (명성 기반 슬롯 수)
            int slotCount = PlayerDataManager.Instance?.Fame?.MenuSlotCount() ?? 3;
            PlayerDataManager.Instance?.DailyMenu?.Reset(slotCount);

            // 4. OnAllGuestsLeft 이벤트 구독 → 정산 UI 표시
            day.OnAllGuestsLeft += HandleAllGuestsLeft;

            // 5. 메뉴 구성 UI 열기 (이전: StartDay() 직접 호출)
            var menuUI = UIManager.Instance?.GetPanel<MenuSetupUI>("MenuSetupUI");
            if (menuUI != null)
            {
                menuUI.OnStartDay = () => day.StartDay();
                UIManager.Instance?.Open("MenuSetupUI");
            }
            else
            {
                DebugUtil.LogWarning("[RestaurantSceneController] MenuSetupUI 없음 — 직접 StartDay 호출.");
                day.StartDay();
            }

            CompleteInit();
        }

        void HandleAllGuestsLeft()
        {
            DayManager.Instance.OnAllGuestsLeft -= HandleAllGuestsLeft;
            UIManager.Instance?.Open("SettlementUI");
        }

        void OnDestroy()
        {
            if (DayManager.Instance != null)
                DayManager.Instance.OnAllGuestsLeft -= HandleAllGuestsLeft;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_GuestSpawnPoint == null)
                DebugUtil.LogWarning("[RestaurantSceneController] GuestSpawnPoint 미연결.", this);
            if (m_Tables == null || m_Tables.Length == 0)
                DebugUtil.LogWarning("[RestaurantSceneController] Tables 비어있음.", this);
        }
#endif
    }
}
```

- [ ] **Step 2: Check compilation in Unity**

`read_console` — no compile errors. Wait for `isCompiling: false`.

- [ ] **Step 3: Add MenuSetupUI + SettlementUI to HUD.prefab via MCP**

MCP 명령 순서:

**a) HUD.prefab에 MenuSetupUI GameObject 추가:**
```
manage_prefabs: open HUD.prefab
manage_gameobject: create child "MenuSetupUI" under HUD root
manage_components: add UIDocument to MenuSetupUI, set sourceAsset = MenuSetupPanel.uxml
manage_components: add MenuSetupUI (script) to MenuSetupUI
  - m_Layer = Panel (20)
  - m_IsPopup = false
  - m_ToggleKey = None
```

**b) HUD.prefab에 SettlementUI GameObject 추가:**
```
manage_prefabs: open HUD.prefab
manage_gameobject: create child "SettlementUI" under HUD root
manage_components: add UIDocument to SettlementUI, set sourceAsset = SettlementPanel.uxml
manage_components: add SettlementUI (script) to SettlementUI
  - m_Layer = Overlay (100)
  - m_IsPopup = false
  - m_ToggleKey = None
```

- [ ] **Step 4: Verify RestaurantScene flow in play mode**

1. ManagementScene → RestaurantScene 진입 확인
2. **메뉴 구성 패널 자동 열림** 확인
3. 보유 음식 목록에서 클릭 → 슬롯 배치 확인
4. "영업 시작" 버튼 클릭 → 패널 닫힘 + 손님 스폰 확인
5. 손님 전원 퇴장 → **정산 패널 자동 표시** 확인
6. 정산 패널에 매출/만족도/명성 수치 확인
7. "다음 날로" 클릭 → ManagementScene 귀환 확인

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/Restaurant/RestaurantSceneController.cs \
        Assets/Prefabs/UI/HUD.prefab
git commit -m "feat: wire RestaurantSceneController to MenuSetupUI and SettlementUI flow"
```

---

## Task 8: ServerDBManager — TotalFame 저장 연동 확인

**Files:**
- Modify: `Assets/Scripts/Core/Managers/ServerDBManager.cs`

- [ ] **Step 1: Add TotalFame to save logic**

Open `Assets/Scripts/Core/Managers/ServerDBManager.cs`. Find the save block where `data.Gold = ...` is set. Add:

```csharp
data.TotalFame = PlayerDataManager.Instance.Fame.Save();
```

- [ ] **Step 2: Verify save/load cycle**

1. RestaurantScene 영업 완료 → 명성 획득
2. 게임 종료 후 재시작 → PlayerDataManager.Fame.TotalFame 값 유지 확인

- [ ] **Step 3: Commit**

```
git add Assets/Scripts/Core/Managers/ServerDBManager.cs
git commit -m "feat: persist TotalFame to ServerSaveData on save"
```

---

## Module 8 완료 검증

- [ ] 모듈 8 테스트 기준 11개 항목 전체 확인 (spec `2026-06-04-restaurant-management-design.md` §테스트 기준)
- [ ] EditMode 테스트 전체 통과

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" \
  -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" \
  -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit
```

- [ ] `docs/MODULES.md` — 8-2, 8-8, 8-9, 8-10, 8-11 항목 ✅ 완료로 업데이트
- [ ] `WORK_IN_PROGRESS.md` 업데이트
