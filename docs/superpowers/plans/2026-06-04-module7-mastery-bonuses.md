# Module 7 — Mastery Bonuses + RecipeBook Bonus Display Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Dave the Diver–style mastery bonuses (cooking speed, ingredient save, grade up) to `PlayerCookingMasteryData` and display them in `RecipeBookUI`.

**Architecture:** Extend `PlayerCookingMasteryData` with 3 static bonus tables (speed / ingredient-save / grade-up) keyed by level index. `CookingStation` reads these at cook time. `RecipeBookUI` displays active bonuses per card.

**Tech Stack:** C# (pure class, no Unity deps for data), Unity UI Toolkit (RecipeBookUI), NUnit (EditMode tests).

**Spec:** `docs/superpowers/specs/2026-06-03-recipe-book-mastery-design.md`

---

## File Map

| Action | File |
|---|---|
| Modify | `Assets/Scripts/Data/Player/PlayerCookingMasteryData.cs` |
| Modify | `Assets/Tests/EditMode/PlayerCookingMasteryDataTests.cs` |
| Modify | `Assets/Scripts/Cooking/CookingStation.cs` |
| Modify | `Assets/Scripts/UI/HUD/RecipeBookUI.cs` |

---

## Task 1: Add bonus lookup methods to PlayerCookingMasteryData

**Files:**
- Modify: `Assets/Scripts/Data/Player/PlayerCookingMasteryData.cs`
- Modify: `Assets/Tests/EditMode/PlayerCookingMasteryDataTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `Assets/Tests/EditMode/PlayerCookingMasteryDataTests.cs` inside the class, after existing tests:

```csharp
// ── Speed multiplier ─────────────────────────────────────────

[Test]
public void GetSpeedMultiplier_Lv1_Returns1f()
    => Assert.AreEqual(1.0f, m_Mastery.GetSpeedMultiplier(3001u), 0.001f);

[Test]
public void GetSpeedMultiplier_Lv2_Returns0Point85f()
{
    for (int i = 0; i < 5; i++) m_Mastery.RecordCook(3001u);
    Assert.AreEqual(0.85f, m_Mastery.GetSpeedMultiplier(3001u), 0.001f);
}

[Test]
public void GetSpeedMultiplier_Lv5_Returns0Point70f()
{
    for (int i = 0; i < 50; i++) m_Mastery.RecordCook(3001u);
    Assert.AreEqual(0.70f, m_Mastery.GetSpeedMultiplier(3001u), 0.001f);
}

// ── Ingredient save chance ───────────────────────────────────

[Test]
public void GetIngredientSaveChance_Lv1_Returns0f()
    => Assert.AreEqual(0f, m_Mastery.GetIngredientSaveChance(3001u), 0.001f);

[Test]
public void GetIngredientSaveChance_Lv3_Returns0Point2f()
{
    for (int i = 0; i < 15; i++) m_Mastery.RecordCook(3001u);
    Assert.AreEqual(0.20f, m_Mastery.GetIngredientSaveChance(3001u), 0.001f);
}

// ── Grade up chance ──────────────────────────────────────────

[Test]
public void GetGradeUpChance_Lv4_Returns0f()
{
    for (int i = 0; i < 30; i++) m_Mastery.RecordCook(3001u);
    Assert.AreEqual(0f, m_Mastery.GetGradeUpChance(3001u), 0.001f);
}

[Test]
public void GetGradeUpChance_Lv5_Returns0Point3f()
{
    for (int i = 0; i < 50; i++) m_Mastery.RecordCook(3001u);
    Assert.AreEqual(0.30f, m_Mastery.GetGradeUpChance(3001u), 0.001f);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
Unity CLI EditMode test — expected: CS0117 "PlayerCookingMasteryData does not contain a definition for GetSpeedMultiplier"
```

- [ ] **Step 3: Add bonus tables and methods to PlayerCookingMasteryData**

In `Assets/Scripts/Data/Player/PlayerCookingMasteryData.cs`, add after the existing static arrays:

```csharp
// Lv1 Lv2   Lv3   Lv4   Lv5
static readonly float[] s_SpeedMultipliers      = { 1.0f, 0.85f, 0.85f, 0.85f, 0.70f };
static readonly float[] s_IngredientSaveChances = { 0.0f, 0.0f,  0.20f, 0.20f, 0.20f };
static readonly float[] s_GradeUpChances        = { 0.0f, 0.0f,  0.0f,  0.0f,  0.30f };
```

Add after `GetPriceMultiplier`:

```csharp
/// <summary>조리 시간 배율 (1.0~0.7). CookingStation.CookRoutine 에서 사용.</summary>
public float GetSpeedMultiplier(uint recipeId)
    => s_SpeedMultipliers[GetLevel(recipeId) - 1];

/// <summary>재료 1개 절약 확률 (0~0.20). 요리 성공 후 랜덤 재료 1개 환급.</summary>
public float GetIngredientSaveChance(uint recipeId)
    => s_IngredientSaveChances[GetLevel(recipeId) - 1];

/// <summary>등급 1단계 상향 확률 (0~0.30). Legendary 초과 없음.</summary>
public float GetGradeUpChance(uint recipeId)
    => s_GradeUpChances[GetLevel(recipeId) - 1];
```

- [ ] **Step 4: Run tests to verify they pass**

Run EditMode tests. All new tests should pass. Existing tests must still pass.

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/Data/Player/PlayerCookingMasteryData.cs \
        Assets/Tests/EditMode/PlayerCookingMasteryDataTests.cs
git commit -m "feat: add mastery speed/save/grade bonus methods to PlayerCookingMasteryData"
```

---

## Task 2: Apply mastery bonuses in CookingStation

**Files:**
- Modify: `Assets/Scripts/Cooking/CookingStation.cs`

- [ ] **Step 1: Apply cooking speed bonus in CookRoutine**

In `CookingStation.cs`, replace the `CookRecipe` method body:

```csharp
public void CookRecipe(RecipeData recipe)
{
    if (m_IsCooking || recipe == null) return;

    var mastery = PlayerDataManager.Instance?.Mastery;

    // 요리 전 인벤토리 품질로 등급 결정 (소모 전 미리 계산)
    FoodGrade grade = PlayerDataManager.Instance?.Inventory
                          .CalculateCookingGrade(recipe) ?? FoodGrade.Normal;

    // 마스터리 등급 상향 확률 적용
    float gradeUpChance = mastery?.GetGradeUpChance(recipe.Id) ?? 0f;
    if (gradeUpChance > 0f && UnityEngine.Random.value < gradeUpChance)
    {
        grade = (FoodGrade)Mathf.Min((int)grade + 1, (int)FoodGrade.Legendary);
        DebugUtil.Log($"[CookingStation] 마스터리 등급 상향! → {grade}");
    }

    // 마스터리 조리 속도 적용
    float baseDuration   = recipe.CookTimeSeconds > 0 ? recipe.CookTimeSeconds : m_CookDuration;
    float speedMult      = mastery?.GetSpeedMultiplier(recipe.Id) ?? 1f;
    float finalDuration  = baseDuration * speedMult;

    StartCoroutine(CookRoutine(recipe, grade, finalDuration));
}
```

Replace the `CookRoutine` signature and first yield to accept duration:

```csharp
IEnumerator CookRoutine(RecipeData recipe, FoodGrade grade, float duration)
{
    m_IsCooking = true;
    DebugUtil.Log($"[CookingStation] 조리 시작: {recipe.DisplayName} [{grade}] ({duration:F1}s)");

    yield return new WaitForSeconds(duration);

    // 요리 요청 → 서버(스텁)에서 재료 검증·소모 + 음식 추가
    bool cookSuccess = false;
    FoodData resultFood = null;

    NetworkManager.Instance?.RequestCook(recipe, grade, (success, food) =>
    {
        cookSuccess = success;
        resultFood  = food;
    });

    if (cookSuccess)
    {
        // 마스터리 기록
        PlayerDataManager.Instance?.Mastery?.RecordCook(recipe.Id);

        // 재료 절약 확률 — 성공 시 랜덤 재료 1개 환급
        float saveChance = PlayerDataManager.Instance?.Mastery?.GetIngredientSaveChance(recipe.Id) ?? 0f;
        if (saveChance > 0f && UnityEngine.Random.value < saveChance && recipe.Ingredients?.Length > 0)
        {
            int idx = UnityEngine.Random.Range(0, recipe.Ingredients.Length);
            uint savedId = recipe.Ingredients[idx].IngredientId;
            if (savedId != 0u)
            {
                NetworkManager.Instance?.RequestAddIngredient(savedId, 1);
                DebugUtil.Log($"[CookingStation] 재료 절약! {savedId} 1개 환급");
            }
        }

        if (m_CookCompleteVFX != null)
            m_CookCompleteVFX.Play();

        DebugUtil.Log($"[CookingStation] 완성! {resultFood?.DisplayName ?? "???"} [{grade}] → FoodInventory 추가");
        OnCookComplete?.Invoke(recipe, resultFood);

        ShowCookRewardPopup(resultFood);
    }
    else
    {
        DebugUtil.LogWarning("[CookingStation] 요리 실패 (재료 부족).");
    }

    m_IsCooking = false;
}
```

- [ ] **Step 2: Check compilation in Unity**

Open Unity Editor, check `read_console` for compile errors. Fix if any.

- [ ] **Step 3: Verify in play mode**

1. DungeonScene에서 재료 채집 → KitchenScene 진입
2. 동일 레시피 5회 조리 → Lv2 알림 토스트 확인
3. Lv2에서 조리 시간이 15% 단축된 것 확인 (1.5s → ~1.28s)
4. 동일 레시피 15회 조리 → Lv3 → Console에서 "재료 절약" 로그 가끔 출력 확인

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/Cooking/CookingStation.cs
git commit -m "feat: apply mastery speed/grade-up/ingredient-save bonuses in CookingStation"
```

---

## Task 3: Update RecipeBookUI to display active bonuses

**Files:**
- Modify: `Assets/Scripts/UI/HUD/RecipeBookUI.cs`

- [ ] **Step 1: Add bonus display to BuildCard()**

In `RecipeBookUI.cs`, in `BuildCard()`, add the following block **after** `info.Add(resultLabel)` and **before** `card.Add(info)`:

```csharp
// 마스터리 보너스 표시 (Lv2 이상만)
if (unlocked)
{
    var mastery = PlayerDataManager.Instance?.Mastery;
    int  lv         = mastery?.GetLevel(recipe.Id) ?? 1;
    float speed     = mastery?.GetSpeedMultiplier(recipe.Id) ?? 1f;
    float save      = mastery?.GetIngredientSaveChance(recipe.Id) ?? 0f;
    float gradeUp   = mastery?.GetGradeUpChance(recipe.Id) ?? 0f;

    string bonusStr = BuildBonusString(speed, save, gradeUp);
    if (!string.IsNullOrEmpty(bonusStr))
    {
        var bonusLabel = new Label(bonusStr);
        bonusLabel.AddToClassList("recipe-card-bonus");
        info.Add(bonusLabel);
    }
}
```

- [ ] **Step 2: Add BuildBonusString helper**

Add static helper method to `RecipeBookUI`:

```csharp
static string BuildBonusString(float speedMult, float saveChance, float gradeUpChance)
{
    var parts = new System.Collections.Generic.List<string>();
    if (speedMult < 1f)
        parts.Add($"조리속도 -{Mathf.RoundToInt((1f - speedMult) * 100f)}%");
    if (saveChance > 0f)
        parts.Add($"재료절약 {Mathf.RoundToInt(saveChance * 100f)}%");
    if (gradeUpChance > 0f)
        parts.Add($"등급상향 {Mathf.RoundToInt(gradeUpChance * 100f)}%");
    return parts.Count > 0 ? string.Join("  ", parts) : string.Empty;
}
```

- [ ] **Step 3: Check compilation in Unity**

Check `read_console` for compile errors.

- [ ] **Step 4: Verify in play mode**

1. Tab 키 → RecipeBook 열기
2. 한 번도 요리 안 한 레시피 → 보너스 텍스트 없음 확인
3. 5회 조리 후 (Lv2) → "조리속도 -15%" 텍스트 표시 확인
4. 15회 조리 후 (Lv3) → "조리속도 -15%  재료절약 20%" 표시 확인
5. 50회 조리 후 (Lv5) → "조리속도 -30%  재료절약 20%  등급상향 30%" 표시 확인

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/UI/HUD/RecipeBookUI.cs
git commit -m "feat: show mastery bonus text in RecipeBookUI cards"
```

---

## Module 7 완료 검증

- [ ] Module 7 테스트 기준 전체 통과 확인 (spec 문서 §테스트 기준 8개 항목)
- [ ] EditMode 테스트 전체 통과 확인

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" \
  -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" \
  -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit
```

- [ ] `docs/MODULES.md` — 7-6, 7-7 항목 ✅ 완료로 업데이트
