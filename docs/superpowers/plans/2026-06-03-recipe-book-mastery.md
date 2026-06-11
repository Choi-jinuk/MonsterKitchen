# Recipe Book + Cooking Mastery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Recipe Book UI (7-6) — globally accessible via Tab, auto-unlocks on ingredient possession — and per-recipe Cooking Mastery (7-7) — Lv1~5 based on cook count, affects food sale price.

**Architecture:** Pure-C# `PlayerCookingMasteryData` follows existing `PlayerUpgradeData` pattern. `RecipeBookUI : UIPanel` dynamically loaded via UIData.csv + AssetManifest (same as CookingUI). Price multiplier injected in `CustomerAI.EatAndPay`. Save uses `List<CookCountEntry>` because `JsonUtility` cannot serialize `Dictionary<uint,int>`.

**Tech Stack:** Unity 6, UIToolkit (UXML/USS), NUnit (EditMode tests), JsonUtility, MonsterKitchen namespace pattern.

---

## File Map

| Action | File |
|---|---|
| **Create** | `Assets/Scripts/Data/Player/PlayerCookingMasteryData.cs` |
| **Create** | `Assets/Tests/EditMode/PlayerCookingMasteryDataTests.cs` |
| **Create** | `Assets/Scripts/UI/HUD/RecipeBookUI.cs` |
| **Create** | `Assets/UI/RecipeBookPanel.uxml` |
| **Create** | `Assets/UI/RecipeBookPanel.uss` |
| **Modify** | `Assets/Scripts/Data/Server/ServerSaveData.cs` — add `CookCountEntry` + `RecipeCookCounts` |
| **Modify** | `Assets/Scripts/Core/Managers/PlayerDataManager.cs` — add `Mastery` property + load |
| **Modify** | `Assets/Scripts/Core/Managers/ServerDBManager.cs` — save/load mastery |
| **Modify** | `Assets/Scripts/Data/Table/DataRegistry.cs` — add `GetRecipeByFoodId()` |
| **Modify** | `Assets/Scripts/Cooking/CookingStation.cs` — `RecordCook` after success + levelup toast |
| **Modify** | `Assets/Scripts/Restaurant/CustomerAI.cs` — mastery price multiplier |
| **Modify** | `Assets/Data/CSV/UIData.csv` — add RecipeBookUI row |
| **MCP** | Create `RecipeBookUI` prefab, wire UIDocument → `RecipeBookPanel.uxml`, add to AssetManifest |

---

## Task 1: PlayerCookingMasteryData — pure C# class

**Files:**
- Create: `Assets/Scripts/Data/Player/PlayerCookingMasteryData.cs`
- Create: `Assets/Tests/EditMode/PlayerCookingMasteryDataTests.cs`

- [ ] **Step 1.1: Write the failing test**

```csharp
// Assets/Tests/EditMode/PlayerCookingMasteryDataTests.cs
using NUnit.Framework;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class PlayerCookingMasteryDataTests
    {
        PlayerCookingMasteryData m_Mastery;

        [SetUp]
        public void SetUp() => m_Mastery = new PlayerCookingMasteryData();

        // ── Level thresholds ─────────────────────────────────────────

        [Test]
        public void GetLevel_ZeroCooks_ReturnsOne()
            => Assert.AreEqual(1, m_Mastery.GetLevel(3001u));

        [Test]
        public void GetLevel_FiveCooks_ReturnsTwo()
        {
            for (int i = 0; i < 5; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(2, m_Mastery.GetLevel(3001u));
        }

        [Test]
        public void GetLevel_FifteenCooks_ReturnsThree()
        {
            for (int i = 0; i < 15; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(3, m_Mastery.GetLevel(3001u));
        }

        [Test]
        public void GetLevel_ThirtyCooks_ReturnsFour()
        {
            for (int i = 0; i < 30; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(4, m_Mastery.GetLevel(3001u));
        }

        [Test]
        public void GetLevel_FiftyCooks_ReturnsFive()
        {
            for (int i = 0; i < 50; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(5, m_Mastery.GetLevel(3001u));
        }

        [Test]
        public void GetLevel_OverFifty_CapsAtFive()
        {
            for (int i = 0; i < 100; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(5, m_Mastery.GetLevel(3001u));
        }

        // ── Price multiplier ─────────────────────────────────────────

        [Test]
        public void GetPriceMultiplier_Lv1_Returns1f()
            => Assert.AreEqual(1.0f, m_Mastery.GetPriceMultiplier(3001u), 0.001f);

        [Test]
        public void GetPriceMultiplier_Lv5_Returns1Point5f()
        {
            for (int i = 0; i < 50; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(1.5f, m_Mastery.GetPriceMultiplier(3001u), 0.001f);
        }

        // ── Independence ─────────────────────────────────────────────

        [Test]
        public void RecordCook_DifferentRecipes_IndependentCounts()
        {
            for (int i = 0; i < 5; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(2, m_Mastery.GetLevel(3001u));
            Assert.AreEqual(1, m_Mastery.GetLevel(3002u));
        }

        // ── OnLevelUp event ──────────────────────────────────────────

        [Test]
        public void RecordCook_WhenLevelUp_FiresOnLevelUp()
        {
            uint firedId  = 0;
            int  firedLvl = 0;
            m_Mastery.OnLevelUp += (id, lv) => { firedId = id; firedLvl = lv; };

            for (int i = 0; i < 5; i++) m_Mastery.RecordCook(3001u);

            Assert.AreEqual(3001u, firedId);
            Assert.AreEqual(2,     firedLvl);
        }

        [Test]
        public void RecordCook_NoLevelChange_DoesNotFireOnLevelUp()
        {
            bool fired = false;
            m_Mastery.OnLevelUp += (_, __) => fired = true;
            m_Mastery.RecordCook(3001u);   // 1 cook — still Lv1
            Assert.IsFalse(fired);
        }

        // ── GetCookCount ─────────────────────────────────────────────

        [Test]
        public void GetCookCount_AfterThreeCooks_ReturnsThree()
        {
            for (int i = 0; i < 3; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(3, m_Mastery.GetCookCount(3001u));
        }

        // ── LoadCounts ───────────────────────────────────────────────

        [Test]
        public void LoadCounts_RestoresLevelCorrectly()
        {
            var data = new System.Collections.Generic.Dictionary<uint, int>
                { [3001u] = 15 };
            m_Mastery.LoadCounts(data);
            Assert.AreEqual(3, m_Mastery.GetLevel(3001u));
        }
    }
}
```

- [ ] **Step 1.2: Run test — expect compile failure (class missing)**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" \
  -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" \
  -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit
```

Expected: compile error — `PlayerCookingMasteryData` not found.

- [ ] **Step 1.3: Implement PlayerCookingMasteryData**

```csharp
// Assets/Scripts/Data/Player/PlayerCookingMasteryData.cs
using System;
using System.Collections.Generic;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  PlayerCookingMasteryData — 레시피별 요리 숙련도 관리
    //
    //  ▶ 역할
    //    레시피별 요리 횟수를 추적하고 Lv1~5 숙련도와 판매가 보정치를 제공.
    //    PlayerDataManager 가 생성하고 Init() 을 호출한다.
    //
    //  ▶ 레벨 임계값
    //    Lv1=0회, Lv2=5회, Lv3=15회, Lv4=30회, Lv5=50회
    //
    //  ▶ 판매가 보정
    //    Lv1=×1.0, Lv2=×1.1, Lv3=×1.2, Lv4=×1.3, Lv5=×1.5
    // ====================================================================

    public class PlayerCookingMasteryData
    {
        static readonly int[]   s_Thresholds   = { 0, 5, 15, 30, 50 };
        static readonly float[] s_Multipliers  = { 1.0f, 1.1f, 1.2f, 1.3f, 1.5f };

        readonly Dictionary<uint, int> m_CookCounts = new();

        // ── 이벤트 ───────────────────────────────────────────────────

        /// <summary>레벨업 시 발행. (recipeId, newLevel)</summary>
        public event Action<uint, int> OnLevelUp;

        // ── 조회 API ─────────────────────────────────────────────────

        /// <summary>레시피 숙련도 레벨 (1~5).</summary>
        public int GetLevel(uint recipeId)
        {
            int count = GetCookCount(recipeId);
            int level = 1;
            for (int i = s_Thresholds.Length - 1; i >= 1; i--)
            {
                if (count >= s_Thresholds[i])
                {
                    level = i + 1;
                    break;
                }
            }
            return level;
        }

        /// <summary>레시피 판매가 배율 (1.0~1.5).</summary>
        public float GetPriceMultiplier(uint recipeId)
            => s_Multipliers[GetLevel(recipeId) - 1];

        /// <summary>레시피 총 요리 횟수.</summary>
        public int GetCookCount(uint recipeId)
            => m_CookCounts.TryGetValue(recipeId, out int c) ? c : 0;

        /// <summary>전체 카운트 읽기 전용 뷰 (저장용).</summary>
        public IReadOnlyDictionary<uint, int> CookCounts => m_CookCounts;

        // ── 변경 API ─────────────────────────────────────────────────

        /// <summary>요리 완료 시 호출. 레벨업 시 OnLevelUp 이벤트 발행.</summary>
        public void RecordCook(uint recipeId)
        {
            int prevLevel = GetLevel(recipeId);

            m_CookCounts.TryGetValue(recipeId, out int current);
            m_CookCounts[recipeId] = current + 1;

            int newLevel = GetLevel(recipeId);
            if (newLevel > prevLevel)
                OnLevelUp?.Invoke(recipeId, newLevel);
        }

        // ── 초기화 / 로드 ────────────────────────────────────────────

        public void Init() { }

        /// <summary>ServerDBManager 가 저장 파일 복원 시 호출한다.</summary>
        public void LoadCounts(Dictionary<uint, int> data)
        {
            m_CookCounts.Clear();
            if (data == null) return;
            foreach (var kv in data)
                m_CookCounts[kv.Key] = kv.Value;
        }
    }
}
```

- [ ] **Step 1.4: Run tests — expect all pass**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" \
  -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" \
  -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit
```

Expected: `PlayerCookingMasteryDataTests` — all 11 tests PASS.

- [ ] **Step 1.5: Check console for compile errors**

Use `read_console(types=["error"])` — expect 0 errors.

- [ ] **Step 1.6: Commit**

```bash
git add Assets/Scripts/Data/Player/PlayerCookingMasteryData.cs \
        Assets/Tests/EditMode/PlayerCookingMasteryDataTests.cs
git commit -m "feat: add PlayerCookingMasteryData — per-recipe cook count → Lv1-5, price multiplier"
```

---

## Task 2: ServerSaveData + PlayerDataManager 확장

**Files:**
- Modify: `Assets/Scripts/Data/Server/ServerSaveData.cs`
- Modify: `Assets/Scripts/Core/Managers/PlayerDataManager.cs`

- [ ] **Step 2.1: ServerSaveData — CookCountEntry + RecipeCookCounts 추가**

`ServerSaveData.cs` 하단에 추가:

```csharp
// ── 요리 마스터리 (List 직렬화 — JsonUtility는 Dictionary 불가) ────
public List<CookCountEntry> RecipeCookCounts = new();
```

파일 하단 `FoodEntry` 아래에 새 클래스 추가:

```csharp
[Serializable]
public class CookCountEntry
{
    public uint RecipeId;
    public int  Count;
}
```

- [ ] **Step 2.2: PlayerDataManager — Mastery 프로퍼티 + Init + LoadFrom 확장**

`PlayerDataManager.cs`:

```csharp
// 프로퍼티 블록에 추가:
public PlayerCookingMasteryData Mastery { get; private set; }

// Init() 내부에 추가:
Mastery = new PlayerCookingMasteryData();
Mastery.Init();

// LoadFrom() 내부 마지막에 추가:
if (save.RecipeCookCounts != null)
{
    var dict = new Dictionary<uint, int>();
    foreach (var e in save.RecipeCookCounts)
        dict[e.RecipeId] = e.Count;
    Mastery.LoadCounts(dict);
}
```

- [ ] **Step 2.3: Check console — 0 compile errors**

- [ ] **Step 2.4: Commit**

```bash
git add Assets/Scripts/Data/Server/ServerSaveData.cs \
        Assets/Scripts/Core/Managers/PlayerDataManager.cs
git commit -m "feat: add RecipeCookCounts to ServerSaveData, Mastery property to PlayerDataManager"
```

---

## Task 3: ServerDBManager — 마스터리 저장/로드

**Files:**
- Modify: `Assets/Scripts/Core/Managers/ServerDBManager.cs`

- [ ] **Step 3.1: BuildSaveData — 마스터리 직렬화**

`BuildSaveData()` 내부, `return save;` 직전에 추가:

```csharp
var mastery = pm?.Mastery;
if (mastery != null)
{
    foreach (var kv in mastery.CookCounts)
        save.RecipeCookCounts.Add(new CookCountEntry { RecipeId = kv.Key, Count = kv.Value });
}
```

- [ ] **Step 3.2: Check console — 0 compile errors**

- [ ] **Step 3.3: Commit**

```bash
git add Assets/Scripts/Core/Managers/ServerDBManager.cs
git commit -m "feat: serialize/deserialize cooking mastery cook counts in ServerDBManager"
```

---

## Task 4: DataRegistry — GetRecipeByFoodId

**Files:**
- Modify: `Assets/Scripts/Data/Table/DataRegistry.cs`

- [ ] **Step 4.1: 역조회 메서드 추가**

`DataRegistry.cs` 의 편의 조회 영역 하단에 추가:

```csharp
/// <summary>
/// foodId 를 결과물로 갖는 레시피를 반환한다.
/// 레시피 수가 적어 선형 탐색(O(n)) 허용.
/// 없으면 null.
/// </summary>
public RecipeData GetRecipeByFoodId(uint foodId)
{
    if (m_Table?.Recipes == null) return null;
    foreach (var recipe in m_Table.Recipes.All)
    {
        if (recipe.ResultFoodId == foodId) return recipe;
    }
    return null;
}
```

- [ ] **Step 4.2: Check console — 0 compile errors**

- [ ] **Step 4.3: Commit**

```bash
git add Assets/Scripts/Data/Table/DataRegistry.cs
git commit -m "feat: add DataRegistry.GetRecipeByFoodId — reverse lookup by result food ID"
```

---

## Task 5: CookingStation — RecordCook + 레벨업 토스트

**Files:**
- Modify: `Assets/Scripts/Cooking/CookingStation.cs`

- [ ] **Step 5.1: CookRoutine 성공 블록에 RecordCook 추가**

`CookRoutine` 내부 `if (cookSuccess)` 블록:

```csharp
if (cookSuccess)
{
    // 마스터리 기록
    PlayerDataManager.Instance?.Mastery?.RecordCook(recipe.Id);

    if (m_CookCompleteVFX != null)
        m_CookCompleteVFX.Play();

    DebugUtil.Log($"[CookingStation] 완성! {resultFood?.DisplayName ?? "???"} [{grade}] → FoodInventory 추가");
    OnCookComplete?.Invoke(recipe, resultFood);
    ShowCookRewardPopup(resultFood);
}
```

- [ ] **Step 5.2: 레벨업 구독 — Awake/OnDestroy 또는 CookRoutine 내 1회 등록**

`CookingStation` 클래스에 추가:

```csharp
void OnEnable()
{
    if (PlayerDataManager.Instance?.Mastery != null)
        PlayerDataManager.Instance.Mastery.OnLevelUp += OnMasteryLevelUp;
}

void OnDisable()
{
    if (PlayerDataManager.Instance?.Mastery != null)
        PlayerDataManager.Instance.Mastery.OnLevelUp -= OnMasteryLevelUp;
}

void OnMasteryLevelUp(uint recipeId, int newLevel)
{
    var recipe = DataRegistry.Instance?.Recipes?.Get(recipeId);
    string name = recipe?.DisplayName ?? $"레시피({recipeId})";
    GameHUD.Instance?.ShowNotification($"{name} 마스터리 Lv{newLevel} 달성!", 2.5f);
}
```

`using MonsterKitchen.UI;` 추가 (파일 상단 using 목록에 없으면).

- [ ] **Step 5.3: Check console — 0 compile errors**

- [ ] **Step 5.4: Commit**

```bash
git add Assets/Scripts/Cooking/CookingStation.cs
git commit -m "feat: record cooking mastery on cook success, show levelup toast"
```

---

## Task 6: CustomerAI — 마스터리 판매가 보정

**Files:**
- Modify: `Assets/Scripts/Restaurant/CustomerAI.cs`

- [ ] **Step 6.1: EatAndPay — 마스터리 배율 추가**

`EatAndPay` 코루틴 내부 `int pay =` 계산 부분을 교체:

```csharp
// 기존:
// float shopMult = PlayerDataManager.Instance?.Upgrades.ShopTipMultiplier ?? 1f;
// int   pay      = Mathf.RoundToInt(food.BasePrice * gradeMult * shopMult);

// 교체:
float shopMult    = PlayerDataManager.Instance?.Upgrades.ShopTipMultiplier ?? 1f;
var   recipe      = DataRegistry.Instance?.GetRecipeByFoodId(food.Id);
float masteryMult = recipe != null
    ? (PlayerDataManager.Instance?.Mastery?.GetPriceMultiplier(recipe.Id) ?? 1f)
    : 1f;
int   pay = Mathf.RoundToInt(food.BasePrice * gradeMult * shopMult * masteryMult);
```

로그도 갱신:

```csharp
DebugUtil.Log($"[Customer] {food.DisplayName} [{grade}] 식사 완료. 지불: {pay}G " +
          $"(base {food.BasePrice} × grade {gradeMult:F2} × shop {shopMult:F2} × mastery {masteryMult:F2})");
```

- [ ] **Step 6.2: Check console — 0 compile errors**

- [ ] **Step 6.3: Commit**

```bash
git add Assets/Scripts/Restaurant/CustomerAI.cs
git commit -m "feat: apply cooking mastery price multiplier in CustomerAI.EatAndPay"
```

---

## Task 7: RecipeBookPanel UXML + USS

**Files:**
- Create: `Assets/UI/RecipeBookPanel.uxml`
- Create: `Assets/UI/RecipeBookPanel.uss`

- [ ] **Step 7.1: RecipeBookPanel.uxml 작성**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <Style src="project://database/Assets/UI/RecipeBookPanel.uss"/>
    <ui:VisualElement name="recipe-book-overlay" class="recipe-book-overlay">
        <ui:VisualElement name="recipe-book-panel" class="recipe-book-panel">
            <ui:VisualElement name="recipe-book-header" class="recipe-book-header">
                <ui:Label name="recipe-book-title" text="레시피 도감" class="recipe-book-title"/>
                <ui:Button name="recipe-book-close-btn" text="✕" class="recipe-book-close-btn"/>
            </ui:VisualElement>
            <ui:ScrollView name="recipe-card-list" class="recipe-card-list"/>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 7.2: RecipeBookPanel.uss 작성**

```css
.recipe-book-overlay {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    background-color: rgba(0, 0, 0, 0.55);
    align-items: center;
    justify-content: center;
}

.recipe-book-panel {
    background-color: rgba(18, 18, 22, 0.97);
    border-radius: 14px;
    padding: 20px;
    width: 480px;
    max-height: 70%;
    min-height: 200px;
}

.recipe-book-header {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 12px;
}

.recipe-book-title {
    font-size: 20px;
    color: rgb(255, 220, 100);
    -unity-font-style: bold;
}

.recipe-book-close-btn {
    width: 32px;
    height: 32px;
    font-size: 16px;
    background-color: rgba(80, 80, 80, 0.8);
    color: white;
    border-radius: 6px;
    border-width: 0;
}

.recipe-card-list {
    flex-grow: 1;
}

/* ── Recipe Card ────────────────────────────────────────── */

.recipe-card {
    background-color: rgba(35, 35, 45, 0.95);
    border-radius: 8px;
    padding: 10px 14px;
    margin-bottom: 8px;
    flex-direction: row;
    align-items: center;
}

.recipe-card--locked {
    opacity: 0.45;
}

.recipe-card-icon {
    width: 48px;
    height: 48px;
    border-radius: 6px;
    background-color: rgba(60, 60, 70, 1);
    margin-right: 12px;
    flex-shrink: 0;
}

.recipe-card-info {
    flex-grow: 1;
}

.recipe-card-name {
    font-size: 15px;
    color: rgb(230, 230, 230);
    margin-bottom: 3px;
}

.recipe-card-result {
    font-size: 12px;
    color: rgb(160, 200, 255);
    margin-bottom: 3px;
}

.recipe-card-detail {
    font-size: 11px;
    color: rgb(160, 160, 160);
}

.recipe-card-mastery {
    font-size: 13px;
    color: rgb(255, 200, 50);
    margin-left: 10px;
    flex-shrink: 0;
    align-items: flex-end;
}

.recipe-card-stars {
    font-size: 14px;
    color: rgb(255, 200, 50);
}

.recipe-card-cook-count {
    font-size: 10px;
    color: rgb(140, 140, 140);
}
```

- [ ] **Step 7.3: Check console — 0 compile/import errors**

- [ ] **Step 7.4: Commit**

```bash
git add Assets/UI/RecipeBookPanel.uxml Assets/UI/RecipeBookPanel.uss \
        Assets/UI/RecipeBookPanel.uxml.meta Assets/UI/RecipeBookPanel.uss.meta
git commit -m "feat: add RecipeBookPanel UXML + USS layout"
```

---

## Task 8: RecipeBookUI.cs

**Files:**
- Create: `Assets/Scripts/UI/HUD/RecipeBookUI.cs`

- [ ] **Step 8.1: RecipeBookUI 구현**

```csharp
// Assets/Scripts/UI/HUD/RecipeBookUI.cs
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  RecipeBookUI — 레시피 도감 패널. UIPanel(isPopup=true), Tab 키 토글.
    //
    //  ▶ 해금 조건
    //    PlayerInventoryData.GetIngredientCount(id) > 0 (레시피 재료 1종 이상 보유)
    //    OR Mastery.GetCookCount(recipeId) > 0 (한 번이라도 요리)
    //
    //  ▶ 카드 표시
    //    해금: 이름 + 재료 목록 + 결과 음식 + 마스터리 ★ + 요리 횟수
    //    미해금: ??? + 회색 처리
    // ====================================================================

    public class RecipeBookUI : UIPanel
    {
        ScrollView m_CardList;

        // ── UIPanel 훅 ────────────────────────────────────────────────

        protected override void OnFirstOpen()
        {
            m_CardList = Root?.Q<ScrollView>("recipe-card-list");
            Root?.Q<Button>("recipe-book-close-btn")
                ?.RegisterCallback<ClickEvent>(_ => UIManager.Instance?.Close(PanelId));
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Refresh();
        }

        // ── 카드 목록 갱신 ────────────────────────────────────────────

        void Refresh()
        {
            m_CardList?.Clear();

            var recipes = DataRegistry.Instance?.Recipes?.All;
            if (recipes == null) return;

            foreach (var recipe in recipes)
            {
                bool unlocked = IsUnlocked(recipe);
                var card = BuildCard(recipe, unlocked);
                m_CardList?.Add(card);
            }
        }

        bool IsUnlocked(RecipeData recipe)
        {
            var inv     = PlayerDataManager.Instance?.Inventory;
            var mastery = PlayerDataManager.Instance?.Mastery;

            // 한 번이라도 요리한 레시피 → 해금
            if (mastery != null && mastery.GetCookCount(recipe.Id) > 0) return true;

            // 재료 1종 이상 보유 → 해금
            if (inv != null && recipe.Ingredients != null)
            {
                foreach (var ing in recipe.Ingredients)
                {
                    if (ing.IngredientId != 0u && inv.GetIngredientCount(ing.IngredientId) > 0)
                        return true;
                }
            }

            return false;
        }

        // ── 카드 빌더 ─────────────────────────────────────────────────

        VisualElement BuildCard(RecipeData recipe, bool unlocked)
        {
            var card = new VisualElement();
            card.AddToClassList("recipe-card");
            if (!unlocked) card.AddToClassList("recipe-card--locked");

            // 결과 음식 아이콘
            var icon = new VisualElement();
            icon.AddToClassList("recipe-card-icon");
            if (unlocked)
            {
                var foodData = DataRegistry.Instance?.Foods?.Get(recipe.ResultFoodId);
                if (foodData != null && !string.IsNullOrEmpty(foodData.SpriteAddress))
                {
                    var sprite = AssetLoadManager.Instance?.Load<Sprite>(foodData.SpriteAddress);
                    if (sprite != null)
                        icon.style.backgroundImage = new StyleBackground(sprite);
                }
            }
            card.Add(icon);

            // 정보 컬럼
            var info = new VisualElement();
            info.AddToClassList("recipe-card-info");

            var nameLabel = new Label(unlocked ? recipe.DisplayName : "???");
            nameLabel.AddToClassList("recipe-card-name");
            info.Add(nameLabel);

            // 재료 목록 (해금: ID → 이름, 미해금: ? × ?)
            string ingredientStr = BuildIngredientString(recipe, unlocked);
            var ingLabel = new Label(ingredientStr);
            ingLabel.AddToClassList("recipe-card-detail");
            info.Add(ingLabel);

            // 결과 음식
            string resultStr = unlocked
                ? (DataRegistry.Instance?.Foods?.Get(recipe.ResultFoodId)?.DisplayName ?? "???")
                : "???";
            var resultLabel = new Label($"결과: {resultStr}");
            resultLabel.AddToClassList("recipe-card-result");
            info.Add(resultLabel);

            card.Add(info);

            // 마스터리 (우측)
            var masteryCol = new VisualElement();
            masteryCol.AddToClassList("recipe-card-mastery");

            int level      = PlayerDataManager.Instance?.Mastery?.GetLevel(recipe.Id) ?? 1;
            int cookCount  = PlayerDataManager.Instance?.Mastery?.GetCookCount(recipe.Id) ?? 0;
            string stars   = BuildStars(level);

            var starsLabel = new Label(stars);
            starsLabel.AddToClassList("recipe-card-stars");
            masteryCol.Add(starsLabel);

            var countLabel = new Label($"Lv{level}  {cookCount}회");
            countLabel.AddToClassList("recipe-card-cook-count");
            masteryCol.Add(countLabel);

            card.Add(masteryCol);

            return card;
        }

        string BuildIngredientString(RecipeData recipe, bool unlocked)
        {
            if (recipe.Ingredients == null || recipe.Ingredients.Length == 0)
                return "재료: 없음";

            var sb = new System.Text.StringBuilder("재료: ");
            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                if (i > 0) sb.Append("  ");

                var ing = recipe.Ingredients[i];
                if (!unlocked || ing.IngredientId == 0u)
                {
                    sb.Append($"?×?");
                }
                else
                {
                    var data = DataRegistry.Instance?.Ingredients?.Get(ing.IngredientId);
                    string name = data?.DisplayName ?? $"ID:{ing.IngredientId}";
                    sb.Append($"{name}×{ing.Quantity}");
                }
            }
            return sb.ToString();
        }

        static string BuildStars(int level)
        {
            const string Filled  = "★";
            const string Empty   = "☆";
            return Filled.PadRight(level, '★') + Empty.PadRight(5 - level, '☆');
        }
    }
}
```

> **Note on BuildStars:** `PadRight` won't work for multi-byte chars. Replace with:
> ```csharp
> static string BuildStars(int level)
> {
>     var sb = new System.Text.StringBuilder();
>     for (int i = 0; i < 5; i++) sb.Append(i < level ? "★" : "☆");
>     return sb.ToString();
> }
> ```

- [ ] **Step 8.2: Check console — 0 compile errors**

- [ ] **Step 8.3: Commit**

```bash
git add Assets/Scripts/UI/HUD/RecipeBookUI.cs
git commit -m "feat: add RecipeBookUI — recipe book panel with unlock, mastery stars display"
```

---

## Task 9: UIData.csv + Prefab + AssetManifest 등록

**Files:**
- Modify: `Assets/Data/CSV/UIData.csv`
- MCP: Create prefab + wire UIDocument + AssetManifest 등록

- [ ] **Step 9.1: UIData.csv 행 추가**

```csv
UI_005,9005,RecipeBookUI,레시피 도감,prefab/ui/recipebookui,Tab
```

- [ ] **Step 9.2: DataManager 창에서 SO 동기화**

Unity Editor → `MonsterKitchen → Data Manager → Sync All`.

- [ ] **Step 9.3: RecipeBookUI 프리팹 생성 (MCP)**

```
manage_gameobject(action="create", name="RecipeBookUI")
```

- [ ] **Step 9.4: UIDocument 컴포넌트 추가 + UXML 연결 (MCP)**

```
manage_components(action="add", target="RecipeBookUI", component_type="UIDocument")
manage_components(action="set_property", target="RecipeBookUI", component_type="UIDocument",
    property="visualTreeAsset", value={"path": "Assets/UI/RecipeBookPanel.uxml"})
```

- [ ] **Step 9.5: RecipeBookUI 컴포넌트 추가 + 설정 (MCP)**

```
manage_components(action="add", target="RecipeBookUI", component_type="RecipeBookUI")
manage_components(action="set_property", target="RecipeBookUI", component_type="RecipeBookUI",
    property="m_IsPopup", value=true)
manage_components(action="set_property", target="RecipeBookUI", component_type="RecipeBookUI",
    property="m_Layer", value={"value": 20})
```

> `m_ToggleKey` 는 UIData.csv 의 `ToggleKey=Tab` 으로 `UIManager.RegisterUIDataToggleKeys()` 가 처리하므로 Inspector 설정 불필요.

- [ ] **Step 9.6: 프리팹으로 저장 (MCP)**

```
manage_asset(action="create_prefab",
    gameobject_name="RecipeBookUI",
    prefab_path="Assets/Prefabs/UI/RecipeBookUI.prefab")
```

GameObject는 씬에서 제거 (UIManager 가 동적 로드).

- [ ] **Step 9.7: AssetManifest 등록 (MCP)**

`AssetManifest` SO 에 항목 추가:
- Key: `prefab/ui/recipebookui`
- Asset: `Assets/Prefabs/UI/RecipeBookUI.prefab`

`manage_scriptable_object` 또는 `manage_asset` 으로 AssetManifest에 등록.

- [ ] **Step 9.8: 저장 + console 확인**

`manage_scene(action="save")` 후 `read_console(types=["error"])` — 0 errors.

- [ ] **Step 9.9: Commit**

```bash
git add Assets/Data/CSV/UIData.csv \
        Assets/Prefabs/UI/RecipeBookUI.prefab \
        Assets/Prefabs/UI/RecipeBookUI.prefab.meta \
        Assets/Data/SO/TableData.asset \
        Assets/Data/AssetManifest.asset
git commit -m "feat: register RecipeBookUI prefab in UIData + AssetManifest — Tab key toggle"
```

---

## 테스트 체크리스트 (완료 기준)

1. `PlayerCookingMasteryDataTests` — EditMode 11개 전부 PASS
2. Tab 키 → 레시피 도감 열림 (ManagementScene, KitchenScene, RestaurantScene)
3. 재료 없음 + 요리 이력 없음 → 카드 ??? 표시 + 흐림
4. 재료 보유 or 1회 이상 요리 → 카드 해금 + 이름/재료/결과 표시
5. 요리 5회 → Lv2 달성 HUD 토스트 + 도감 ★★☆☆☆
6. 요리 50회 → Lv5 달성 + 도감 ★★★★★
7. RestaurantScene 서빙 → 로그에 `mastery 1.1x` 배율 반영 확인
8. 저장 → Editor 재시작 → 마스터리 횟수/레벨 유지
