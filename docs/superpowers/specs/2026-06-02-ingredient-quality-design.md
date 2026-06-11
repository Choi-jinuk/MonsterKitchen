# Ingredient Quality & Cooking Grade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 던전 처치 방식(속성 매칭·CC·무기 등급)에 따라 재료 품질(I/II/III)을 결정하고, 이를 요리 등급(FoodGrade)과 식당 팁에 연결한다.

**Architecture:** `KillQualityEvaluator`(순수 C#)가 처치 시 품질을 계산하고 `ItemDrop`에 기록. `PlayerInventoryData`가 품질별 스택을 관리. `CookingStation`이 사용 재료 품질 평균으로 `FoodGrade` 결정. `CustomerAI`가 Perfect 음식 서빙 시 팁 확률 롤.

**Tech Stack:** Unity 6, C#, UIToolkit (인벤토리 배지), 기존 CrowdControlComponent / DropResolver / CookingStation 확장.

---

## 시스템 개요

### 품질 점수 계산 (KillQualityEvaluator)

| 요소 | 조건 | 점수 |
|---|---|---|
| 속성 매칭 | killAttribute == monsterAttribute | +1 |
| CC 처치 | 처치 시 Sleep 또는 Hypnosis CC 활성 | +1 |
| 무기 등급 차이 | weaponTier - ceil(monsterRarity / 2) == 0 | +1 |
| | 차이 == 1 | +2 |
| | 차이 ≥ 2 | +3 |

`total = min(3, 합산)` → 품질: 0-1 = **I**, 2 = **II**, 3 = **III**

### 데이터 흐름

```
몬스터 처치
  DropResolver.HandleDeath(killAttribute, weaponTier, hadSoftCC)
    → KillQualityEvaluator.Evaluate(...) → IngredientQuality
    → ItemDrop 스폰 (quality 포함)
      → PickUp → NetworkManager.RequestAddIngredient(id, qty, quality)
        → PlayerInventoryData: Dictionary<uint, Dictionary<IngredientQuality, int>>

요리
  CookingStation → 사용 재료 품질 평균 점수 계산 → FoodGrade
    → NetworkManager.RequestCook(recipe, grade, callback)

서빙
  CustomerAI: FoodGrade.Perfect → 30% 확률 팁 지급
    → NetworkManager.RequestEarnGold(tipAmount)
```

---

## 데이터 변경

### 신규 타입
- `IngredientQuality` enum (`I = 1, II = 2, III = 3`) — `Assets/Scripts/Data/Table/GameEnums.cs`

### CCType 추가
- `CCType.Sleep`, `CCType.Hypnosis` — `Assets/Scripts/Combat/CCType.cs` (또는 정의 파일)

### WeaponData 변경
- `int Tier` 필드 추가 (기본값 1) — `Assets/Scripts/Data/Table/WeaponTable.cs`
- `Assets/Data/CSV/Weapons.csv` 컬럼 추가

### ItemDrop 변경
- `IngredientQuality Quality` 필드 추가 — `Assets/Scripts/Combat/ItemDrop.cs`
- `Init(uint id, int qty, IngredientQuality quality)` 메서드 업데이트

### PlayerInventoryData 변경
- `m_Ingredients`: `Dictionary<uint, int>` → `Dictionary<uint, Dictionary<IngredientQuality, int>>`
- `GetIngredientCount(uint id)` — 전체 합산 (하위 호환)
- `GetIngredientCount(uint id, IngredientQuality quality)` — 품질별 개수
- `ApplyIngredient(uint id, int newQty)` 유지 (NetworkManager 기존 호환)
- `ApplyIngredient(uint id, IngredientQuality quality, int newQty)` 추가

### NetworkManager 변경
- `RequestAddIngredient(uint id, int qty, IngredientQuality quality)` 오버로드 추가

---

## 신규 클래스

### KillQualityEvaluator
`Assets/Scripts/Combat/KillQualityEvaluator.cs`

```csharp
public static class KillQualityEvaluator
{
    public static IngredientQuality Evaluate(
        int  monsterRarity,
        AttributeType monsterAttribute,
        AttributeType killAttribute,
        bool hadSoftCC,
        int  weaponTier)
    {
        int score = 0;
        if (killAttribute == monsterAttribute && monsterAttribute != AttributeType.None)
            score += 1;
        if (hadSoftCC)
            score += 1;
        int recommended = Mathf.CeilToInt(monsterRarity / 2f);
        int gap = weaponTier - recommended;
        score += gap < 0 ? 0 : gap == 0 ? 1 : gap == 1 ? 2 : 3;
        score = Mathf.Min(3, score);
        return score switch { 3 => IngredientQuality.III, 2 => IngredientQuality.II, _ => IngredientQuality.I };
    }
}
```

---

## DropResolver 변경

`HandleDeath(AttributeType killAttribute)` →
`HandleDeath(AttributeType killAttribute, int weaponTier, bool hadSoftCC)`

호출부(`MonsterBase.Die()` 또는 `Health.OnDeath`): 처치 시점에 `PlayerManager.Instance` 에서 `weaponTier`, 몬스터 `CrowdControlComponent` 에서 `hadSoftCC` 전달.

---

## CookingStation 변경

레시피에 필요한 재료를 소모할 때 품질 정보도 수집:
- 사용 재료 품질 점수 평균 계산
- 0-1점 → `FoodGrade.Normal`, 2점 → `FoodGrade.Good`, 3점 → `FoodGrade.Perfect`
- `NetworkManager.RequestCook(recipe, derivedGrade, callback)` 호출 (기존 서명 유지)

---

## CustomerAI 변경

`EatAndPay()` 에서 서빙된 `FoodGrade` 확인:
- `FoodGrade.Perfect` → `Random.value < 0.3f` 조건으로 팁 지급
- 팁 금액: 기본 음식 가격의 50% (또는 `GameConfig.TipMultiplier` 설정값)
- `NetworkManager.RequestEarnGold(tipAmount)` 호출

---

## UI — 인벤토리 품질 배지

인벤토리 슬롯에 품질 배지 표시:
- Grade I: 회색 `I`
- Grade II: 파란색 `II`  
- Grade III: 금색 `III`

배지는 기존 인벤토리 슬롯 UXML에 `Label` 요소로 추가. 코드에서 `IngredientQuality`에 따라 텍스트·색상 설정.

---

## 테스트

`Assets/Tests/EditMode/KillQualityEvaluatorTests.cs`

- `Evaluate_NoFactors_ReturnsGradeI`
- `Evaluate_AttributeMatchOnly_ReturnsGradeI`
- `Evaluate_AttributeMatchAndCC_ReturnsGradeII`
- `Evaluate_AllThreeFactors_ReturnsGradeIII`
- `Evaluate_HighWeaponTierAlone_ReturnsGradeIII`
- `Evaluate_WeaponTierGap1_Returns2Points`
- `Evaluate_TotalCappedAt3`

---

## 모듈 범위

이 스펙은 모듈 **7-4** 구현. 명성 시스템(8-11)은 별도 스펙으로 처리.
팁은 임시 골드 지급으로 구현 — Fame 연동은 8-11에서 확장.
