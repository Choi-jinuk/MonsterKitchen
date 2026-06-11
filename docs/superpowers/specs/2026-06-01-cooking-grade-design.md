# Cooking Grade Judgment — Design Spec

> Module 7-4 | Date: 2026-06-01

---

## Overview

Auto-calculate `FoodGrade` from ingredients used in a recipe.
No player timing minigame. Grade reflects what ingredients were used — rarity and inherent state.

---

## Architecture

**New file:** `Assets/Scripts/Cooking/CookingGradeCalculator.cs`
- Pure static class, no MonoBehaviour
- Single public method: `static FoodGrade Calculate(RecipeData recipe)`
- Reads `DataRegistry` for ingredient properties, `GameConfig` for thresholds/multipliers

**Modified:** `Assets/Scripts/Data/Table/GameConfig.cs`
- Add `[Header("Cooking — Grade")]` section with 6 configurable fields

**Modified:** `Assets/Scripts/Cooking/CookingUI.cs`
- Call `CookingGradeCalculator.Calculate(recipe)` when recipe is selected
- Display grade badge in UI before cooking starts
- Pass calculated grade to `CookingStation.CookRecipe(recipe, grade)`

`CookingStation.CookRecipe(RecipeData, FoodGrade)` already accepts grade — no changes needed there.

---

## Grade Formula

```
for each RecipeIngredient slot in recipe.Ingredients:
    ingredientData = DataRegistry.GetIngredient(slot.IngredientId)
    rarityValue    = (int)ingredientData.Rarity          // Common=0 … Legendary=4
    stateMultiplier = GameConfig.multiplier[ingredientData.DefaultState]
    score += rarityValue × stateMultiplier × slot.RequiredQty

weightedAvg = score / sum(slot.RequiredQty for all slots)
```

Grade thresholds (compared against `weightedAvg`):

| weightedAvg | FoodGrade |
|---|---|
| < GradeThresholdGood | Normal |
| < GradeThresholdPerfect | Good |
| < GradeThresholdLegendary | Perfect |
| ≥ GradeThresholdLegendary | Legendary |

---

## GameConfig Changes

```csharp
[Header("Cooking — Grade")]
public float GradeMultiplierRaw      = 1.0f;
public float GradeMultiplierCooked   = 1.2f;
public float GradeMultiplierSpoiled  = 0.5f;
public float GradeThresholdGood      = 1.0f;
public float GradeThresholdPerfect   = 2.5f;
public float GradeThresholdLegendary = 4.0f;
```

Default thresholds rationale (with default multipliers):
- Common Raw (0×1.0=0) → always Normal
- Uncommon Raw (1×1.0=1.0) → Good boundary
- Rare Raw (2×1.0=2.0) → between Good and Perfect
- Epic Raw (3×1.0=3.0) → between Good and Perfect
- Legendary Raw (4×1.0=4.0) → Legendary boundary
- Cooked ingredients push scores slightly higher (+20%)
- Spoiled ingredients drag scores down (-50%)

---

## CookingGradeCalculator — Implementation

```csharp
public static class CookingGradeCalculator
{
    public static FoodGrade Calculate(RecipeData recipe)
    {
        if (recipe == null || recipe.Ingredients == null || recipe.Ingredients.Length == 0)
            return FoodGrade.Normal;

        var config   = GameConfig.Current;
        var registry = DataRegistry.Instance;
        if (config == null || registry == null) return FoodGrade.Normal;

        float totalScore = 0f;
        int   totalQty   = 0;

        foreach (var slot in recipe.Ingredients)
        {
            var data = registry.GetIngredient(slot.IngredientId);
            if (data == null) continue;

            float stateMult = data.DefaultState switch
            {
                IngredientState.Cooked  => config.GradeMultiplierCooked,
                IngredientState.Spoiled => config.GradeMultiplierSpoiled,
                _                       => config.GradeMultiplierRaw,
            };

            totalScore += (int)data.Rarity * stateMult * slot.RequiredQty;
            totalQty   += slot.RequiredQty;
        }

        if (totalQty == 0) return FoodGrade.Normal;

        float avg = totalScore / totalQty;

        if (avg >= config.GradeThresholdLegendary) return FoodGrade.Legendary;
        if (avg >= config.GradeThresholdPerfect)   return FoodGrade.Perfect;
        if (avg >= config.GradeThresholdGood)       return FoodGrade.Good;
        return FoodGrade.Normal;
    }
}
```

---

## CookingUI Integration

When a recipe is selected in the UI:
1. Call `FoodGrade grade = CookingGradeCalculator.Calculate(recipe)`
2. Show grade badge next to recipe name (e.g., colored label: Normal/Good/Perfect/Legendary)
3. On cook button press: `station.CookRecipe(recipe, grade)`

Grade displayed as preview so player can see what grade they will produce before committing.

---

## Files Touched

| File | Type |
|---|---|
| `Assets/Scripts/Cooking/CookingGradeCalculator.cs` | New |
| `Assets/Scripts/Data/Table/GameConfig.cs` | Modified (+6 fields) |
| `Assets/Scripts/Cooking/CookingUI.cs` | Modified (grade preview + pass to station) |

---

## Out of Scope

- Runtime ingredient spoilage (inventory tracks qty only, no per-slot state)
- Cooking upgrade affecting grade (no cooking quality in `PlayerUpgradeData`)
- Grade affects price (handled by `FoodData.GoodMultiplier` etc., already in place)
- Module 7-5 VFX on cook complete (separate task)
