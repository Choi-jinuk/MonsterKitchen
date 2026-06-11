# Cooking Grade Judgment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Auto-calculate `FoodGrade` (Normal/Good/Perfect/Legendary) from ingredient rarity and default state when a recipe is cooked.

**Architecture:** Pure static `CookingGradeCalculator` class computes weighted average of (rarity × state multiplier) across recipe ingredients. `CookingUI` calls the calculator on recipe match, caches the result, displays it in the recipe label, and passes it to `CookingStation.CookRecipe`. Thresholds and multipliers live in `GameConfig` SO for inspector tuning.

**Tech Stack:** Unity 6, C#, UIToolkit, Unity Test Framework (NUnit EditMode)

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Data/Table/GameConfig.cs` | Modify | Add 6 cooking grade fields (multipliers + thresholds) |
| `Assets/Scripts/Cooking/CookingGradeCalculator.cs` | Create | Pure static grade calculation logic |
| `Assets/Tests/EditMode/CookingGradeCalculatorTests.cs` | Create | NUnit tests for grade formula |
| `Assets/Tests/EditMode/Tests.EditMode.asmdef` | Create | EditMode test assembly definition |
| `Assets/Scripts/Cooking/CookingUI.cs` | Modify | Cache grade on match, show in label, pass to CookRecipe |

---

## Task 1: Add cooking grade fields to GameConfig

**Files:**
- Modify: `Assets/Scripts/Data/Table/GameConfig.cs`

- [ ] **Step 1: Add the 6 cooking grade fields after the existing `TiltAngleDeg` field**

Open `Assets/Scripts/Data/Table/GameConfig.cs`. After the `OnValidate` block (around line 55), add:

```csharp
        // ── Cooking — Grade ───────────────────────────────────────────
        [Header("Cooking — Grade")]
        [Tooltip("Raw 재료 상태 배율")]
        public float GradeMultiplierRaw      = 1.0f;
        [Tooltip("Cooked 재료 상태 배율")]
        public float GradeMultiplierCooked   = 1.2f;
        [Tooltip("Spoiled 재료 상태 배율")]
        public float GradeMultiplierSpoiled  = 0.5f;

        [Tooltip("이 값 이상이면 Good 등급")]
        public float GradeThresholdGood      = 1.0f;
        [Tooltip("이 값 이상이면 Perfect 등급")]
        public float GradeThresholdPerfect   = 2.5f;
        [Tooltip("이 값 이상이면 Legendary 등급")]
        public float GradeThresholdLegendary = 4.0f;
```

Insert this block between `#if UNITY_EDITOR ... #endif` and the closing `}` of the class.

- [ ] **Step 2: Check Unity console for compile errors**

In Unity MCP: call `read_console`. Expected: 0 errors.

---

## Task 2: Create CookingGradeCalculator

**Files:**
- Create: `Assets/Scripts/Cooking/CookingGradeCalculator.cs`

- [ ] **Step 1: Create the file**

```csharp
using MonsterKitchen.Data;

namespace MonsterKitchen.Cooking
{
    // ====================================================================
    //  CookingGradeCalculator — 레시피 재료로부터 FoodGrade 를 계산한다.
    //
    //  ▶ 공식
    //    각 재료 슬롯: score += RarityValue × StateMultiplier × Quantity
    //    weightedAvg = score / totalQty
    //    thresholds (GameConfig): Good / Perfect / Legendary
    //
    //  ▶ 테스트 가능 분리
    //    GradeFromAverage — 순수 함수 (float 만 사용)
    //    Calculate        — DataRegistry/GameConfig 조회 후 GradeFromAverage 위임
    // ====================================================================

    public static class CookingGradeCalculator
    {
        /// <summary>
        /// 레시피의 재료 구성으로부터 FoodGrade 를 계산한다.
        /// DataRegistry 또는 GameConfig 가 없으면 Normal 반환.
        /// </summary>
        public static FoodGrade Calculate(RecipeData recipe)
        {
            if (recipe?.Ingredients == null || recipe.Ingredients.Length == 0)
                return FoodGrade.Normal;

            var config   = GameConfig.Current;
            var registry = DataRegistry.Instance;
            if (config == null || registry == null) return FoodGrade.Normal;

            float totalScore = 0f;
            int   totalQty   = 0;

            foreach (var slot in recipe.Ingredients)
            {
                var data = registry.Ingredients?.Get(slot.IngredientId);
                if (data == null || slot.Quantity <= 0) continue;

                float stateMult = data.DefaultState switch
                {
                    IngredientState.Cooked  => config.GradeMultiplierCooked,
                    IngredientState.Spoiled => config.GradeMultiplierSpoiled,
                    _                       => config.GradeMultiplierRaw,
                };

                totalScore += (int)data.Rarity * stateMult * slot.Quantity;
                totalQty   += slot.Quantity;
            }

            if (totalQty == 0) return FoodGrade.Normal;

            return GradeFromAverage(
                totalScore / totalQty,
                config.GradeThresholdGood,
                config.GradeThresholdPerfect,
                config.GradeThresholdLegendary);
        }

        /// <summary>
        /// 순수 함수 — 가중 평균 점수와 threshold 값으로 FoodGrade 반환.
        /// 테스트에서 직접 호출한다.
        /// </summary>
        public static FoodGrade GradeFromAverage(
            float avg,
            float thresholdGood,
            float thresholdPerfect,
            float thresholdLegendary)
        {
            if (avg >= thresholdLegendary) return FoodGrade.Legendary;
            if (avg >= thresholdPerfect)   return FoodGrade.Perfect;
            if (avg >= thresholdGood)       return FoodGrade.Good;
            return FoodGrade.Normal;
        }
    }
}
```

- [ ] **Step 2: Check Unity console for compile errors**

Call `read_console`. Expected: 0 errors.

---

## Task 3: EditMode tests for CookingGradeCalculator

**Files:**
- Create: `Assets/Tests/EditMode/Tests.EditMode.asmdef`
- Create: `Assets/Tests/EditMode/CookingGradeCalculatorTests.cs`

- [ ] **Step 1: Create test assembly definition**

Create `Assets/Tests/EditMode/Tests.EditMode.asmdef`:

```json
{
    "name": "Tests.EditMode",
    "references": [],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false,
    "optionalUnityReferences": [
        "TestAssemblies"
    ]
}
```

- [ ] **Step 2: Create test file**

Create `Assets/Tests/EditMode/CookingGradeCalculatorTests.cs`:

```csharp
using NUnit.Framework;
using MonsterKitchen.Cooking;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests.EditMode
{
    // ====================================================================
    //  CookingGradeCalculatorTests — GradeFromAverage 순수 함수 테스트
    //
    //  thresholds: Good=1.0, Perfect=2.5, Legendary=4.0
    // ====================================================================
    public class CookingGradeCalculatorTests
    {
        const float Good      = 1.0f;
        const float Perfect   = 2.5f;
        const float Legendary = 4.0f;

        [Test]
        public void GradeFromAverage_Zero_ReturnsNormal()
        {
            var result = CookingGradeCalculator.GradeFromAverage(0f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Normal, result);
        }

        [Test]
        public void GradeFromAverage_BelowGood_ReturnsNormal()
        {
            var result = CookingGradeCalculator.GradeFromAverage(0.99f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Normal, result);
        }

        [Test]
        public void GradeFromAverage_AtGoodThreshold_ReturnsGood()
        {
            var result = CookingGradeCalculator.GradeFromAverage(1.0f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Good, result);
        }

        [Test]
        public void GradeFromAverage_BetweenGoodAndPerfect_ReturnsGood()
        {
            var result = CookingGradeCalculator.GradeFromAverage(2.0f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Good, result);
        }

        [Test]
        public void GradeFromAverage_AtPerfectThreshold_ReturnsPerfect()
        {
            var result = CookingGradeCalculator.GradeFromAverage(2.5f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Perfect, result);
        }

        [Test]
        public void GradeFromAverage_AtLegendaryThreshold_ReturnsLegendary()
        {
            var result = CookingGradeCalculator.GradeFromAverage(4.0f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Legendary, result);
        }

        [Test]
        public void GradeFromAverage_AboveLegendary_ReturnsLegendary()
        {
            var result = CookingGradeCalculator.GradeFromAverage(10f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Legendary, result);
        }
    }
}
```

- [ ] **Step 3: Run EditMode tests**

Run via CLI:
```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit
```

Expected: All 7 `CookingGradeCalculatorTests` tests PASS.

---

## Task 4: Integrate grade into CookingUI

**Files:**
- Modify: `Assets/Scripts/Cooking/CookingUI.cs`

- [ ] **Step 1: Add `m_CurrentGrade` field**

In `CookingUI.cs`, add `m_CurrentGrade` field to the slot state section (after `m_Matched`):

```csharp
        // ── 슬롯 상태 ────────────────────────────────────────────────
        readonly List<uint> m_SlotIds = new();
        CookingStation      m_Station;
        RecipeData          m_Matched;
        FoodGrade           m_CurrentGrade;
```

- [ ] **Step 2: Update `RefreshRecipeMatch()` to calculate and display grade**

Replace the matched branch in `RefreshRecipeMatch()`:

```csharp
        void RefreshRecipeMatch()
        {
            if (m_RecipeLabel == null || m_CookBtn == null) return;

            m_Matched = m_SlotIds.Count > 0
                ? RecipeMatcher.FindSlotMatch(DataRegistry.Instance?.Recipes?.All, m_SlotIds)
                : null;

            if (m_Matched != null)
            {
                m_CurrentGrade    = CookingGradeCalculator.Calculate(m_Matched);
                string gradeSuffix = $"  [{m_CurrentGrade}]";
                m_RecipeLabel.text = LocaleManager.Get("UI_COOKING_RECIPE_PREFIX")
                                     + m_Matched.DisplayName + gradeSuffix;
                m_RecipeLabel.RemoveFromClassList("cook-recipe-matched");
                m_RecipeLabel.AddToClassList("cook-recipe-matched");
                m_CookBtn.RemoveFromClassList("cook-btn-disabled");
                m_CookBtn.AddToClassList("cook-btn-enabled");
            }
            else
            {
                m_CurrentGrade = FoodGrade.Normal;
                m_RecipeLabel.text = m_SlotIds.Count > 0
                    ? LocaleManager.Get("UI_COOKING_NO_MATCH")
                    : LocaleManager.Get("UI_COOKING_SELECT");
                m_RecipeLabel.RemoveFromClassList("cook-recipe-matched");
                m_CookBtn.RemoveFromClassList("cook-btn-enabled");
                m_CookBtn.AddToClassList("cook-btn-disabled");
            }
        }
```

- [ ] **Step 3: Update `TryCook()` to pass grade**

Replace `TryCook()`:

```csharp
        void TryCook()
        {
            if (m_Matched == null || m_Station == null) return;

            if (!RecipeMatcher.CanCook(m_Matched, PlayerDataManager.Instance?.Inventory))
            {
                m_RecipeLabel.text = "재료가 부족합니다!";
                return;
            }

            m_Station.CookRecipe(m_Matched, m_CurrentGrade);
            UIManager.Instance?.Close(PanelId);
        }
```

- [ ] **Step 4: Check Unity console for compile errors**

Call `read_console`. Expected: 0 errors.

---

## Task 5: Final verification

- [ ] **Step 1: Run EditMode tests again to confirm nothing regressed**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit
```

Expected: All tests PASS.

- [ ] **Step 2: Verify GameConfig.asset Inspector**

In Unity Editor → `Assets/Data/GameConfig.asset` in Inspector. Confirm the `Cooking — Grade` header with 6 fields appears with correct defaults.

- [ ] **Step 3: Manual smoke test**

Enter Play mode in KitchenScene. Open CookingStation. Add ingredients. Confirm:
- Recipe label shows grade suffix e.g. `슬라임 수프  [Good]`
- Grade changes when different rarity ingredients are added
- Cook button triggers cook with that grade

---

## Self-Review Notes

- `RecipeIngredient.Quantity` used throughout (matches `RecipeTable.cs` field name)
- `DataRegistry.Instance.Ingredients.Get(id)` — matches `DataRegistry.cs` line 40
- `GameConfig.Current` — static accessor, matches `GameConfig.cs` line 37
- `CookingStation.CookRecipe(RecipeData, FoodGrade)` — signature already exists in `CookingStation.cs` line 63
- `GradeFromAverage` is `public static` so test assembly can access it without additional asmdef references
- No UXML modifications needed — grade appended to existing `m_RecipeLabel`
