# 레시피 도감 + 요리 마스터리 설계 문서

> 최종 갱신: 2026-06-04 (마스터리 보너스 Dave the Diver 방식으로 확장)

---

## Goal

모듈 7 잔여 기능 구현.  
레시피 도감(7-6): 전 씬 상시 열람, 재료 보유 시 자동 해금, 미해금 ???표시.  
요리 마스터리(7-7): 레시피별 요리 횟수 → Lv1~5, 다단계 보너스 (Dave the Diver 방식).

---

## 섹션 1: 데이터 레이어

### PlayerCookingMasteryData (순수 C# 클래스)

```csharp
public class PlayerCookingMasteryData
{
    Dictionary<uint, int> m_CookCounts;   // recipeId → 총 요리 횟수

    public int   GetLevel(uint recipeId);               // 1~5
    public float GetPriceMultiplier(uint recipeId);     // 1.0~1.5
    public float GetSpeedMultiplier(uint recipeId);     // 1.0~0.7 (빠를수록 작음)
    public float GetIngredientSaveChance(uint recipeId);// 0.0~0.20
    public float GetGradeUpChance(uint recipeId);       // 0.0~0.30
    public void  RecordCook(uint recipeId);             // 카운트 증가, 레벨업 이벤트
    public void  LoadCounts(Dictionary<uint, int> data);
    public IReadOnlyDictionary<uint, int> CookCounts { get; }

    public event Action<uint, int> OnLevelUp;  // (recipeId, newLevel)
}
```

**레벨 임계값 및 보너스 (Dave the Diver 참조):**

| 레벨 | 누적 요리 횟수 | 판매가 보정 | 조리 속도   | 재료 절약 확률 | 등급 상향 확률 |
|------|--------------|------------|------------|--------------|--------------|
| Lv1  | 0            | ×1.0       | ×1.0       | 0%           | 0%           |
| Lv2  | 5            | ×1.1       | ×0.85      | 0%           | 0%           |
| Lv3  | 15           | ×1.2       | ×0.85      | 20%          | 0%           |
| Lv4  | 30           | ×1.3       | ×0.85      | 20%          | 0%           |
| Lv5  | 50           | ×1.5       | ×0.70      | 20%          | 30%          |

**보너스 설명:**
- **조리 속도**: `CookingStation.cookDuration × speedMultiplier`
- **재료 절약**: 요리 시 재료 1개를 소비하지 않을 확률 (랜덤 1개 선택)
- **등급 상향**: `FoodGrade` 산출 결과를 1단계 상향할 확률 (Legendary 초과 없음)

### PlayerDataManager 확장

```csharp
public PlayerCookingMasteryData Mastery { get; }
```

### ServerSaveData 확장

```csharp
public Dictionary<uint, int> RecipeCookCounts = new();
```

### DataRegistry 확장

```csharp
// RecipeData.ResultFoodId 기준 역조회 (레시피 수 적어 O(n) 허용)
public RecipeData? GetRecipeByFoodId(uint foodId);
```

---

## 섹션 2: 레시피 도감 UI

### 컴포넌트

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/UI/HUD/RecipeBookUI.cs` | UIPanel 상속, 도감 로직 |
| `Assets/UI/RecipeBookPanel.uxml` | 레이아웃 |
| `Assets/UI/RecipeBookPanel.uss` | 스타일 |

### UIPanel 설정

- `m_IsPopup = true`
- `m_ToggleKey = Tab`
- `m_Layer = UILayer.Panel`
- HUD.prefab에 UIDocument + RecipeBookUI 컴포넌트 추가

### 해금 조건

On Open 시 전체 RecipeTable 순회:

```
해금 = PlayerInventoryData.AllIngredients 에 해당 레시피 재료 중 1종 이상 보유
       OR Mastery.CookCounts[recipeId] > 0
미해금 = 위 조건 불충족
```

### 카드 레이아웃 (해금)

```
┌─────────────────────────────────────────────────────┐
│ [음식 아이콘]  슬라임 스튜                          │
│ 재료: 🍄×2  🐌×1  💧×1                            │
│ 결과: 슬라임 스튜  ★★★☆☆ Lv3  15회              │
│ 보너스: 조리속도-15%  재료절약20%                   │
└─────────────────────────────────────────────────────┘
```

### 카드 레이아웃 (미해금)

```
┌─────────────────────────────────────────────────────┐
│ [?]  ???                                            │
│ 재료: 🍄×?  ?×?  ?×?   (회색)                     │
│ 결과: ???  ★☆☆☆☆ Lv0  0회                        │
└─────────────────────────────────────────────────────┘
```

---

## 섹션 3: 연동 포인트

### 요리 완료 → 마스터리 기록

`CookingStation` 요리 성공 콜백:

```csharp
PlayerDataManager.Instance.Mastery.RecordCook(recipe.Id);
```

레벨업 이벤트 → `GameHUD.ShowNotification($"{recipe.DisplayName} 마스터리 Lv{newLevel} 달성!")` (2초 토스트)

### 조리 속도 보정

`CookingStation.StartCooking()`:

```csharp
float speedMult = PlayerDataManager.Instance.Mastery.GetSpeedMultiplier(recipe.Id);
float duration  = recipe.CookTimeSeconds * speedMult;
```

### 재료 절약

`CookingStation` 요리 성공 직후, 재료 차감 전:

```csharp
float saveChance = PlayerDataManager.Instance.Mastery.GetIngredientSaveChance(recipe.Id);
if (saveChance > 0f && Random.value < saveChance)
{
    // recipe.Ingredients 중 랜덤 1개 제외하고 차감
}
```

### 등급 상향

`CookingGradeCalculator.Calculate()` 결과 반환 전:

```csharp
float gradeUpChance = PlayerDataManager.Instance.Mastery.GetGradeUpChance(recipe.Id);
if (gradeUpChance > 0f && Random.value < gradeUpChance)
    grade = (FoodGrade)Mathf.Min((int)grade + 1, (int)FoodGrade.Legendary);
```

### 판매가 보정

`ServingSystem` 서빙 처리:

```csharp
var recipe   = DataRegistry.Instance.GetRecipeByFoodId(foodId);
float mult   = recipe != null
    ? PlayerDataManager.Instance.Mastery.GetPriceMultiplier(recipe.Id)
    : 1f;
int finalPrice = Mathf.RoundToInt(basePrice * mult);
```

### 저장/로드

**Save (`ServerDBManager.Save`):**
```csharp
data.RecipeCookCounts = PlayerDataManager.Instance.Mastery.CookCounts
    .ToDictionary(kv => kv.Key, kv => kv.Value);
```

**Load (`ServerDBManager.Load`):**
```csharp
PlayerDataManager.Instance.Mastery.LoadCounts(data.RecipeCookCounts);
```

---

## 파일 변경 요약

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Data/Player/PlayerCookingMasteryData.cs` | 신규 (또는 기존 확장) |
| `Assets/Scripts/UI/HUD/RecipeBookUI.cs` | 신규 |
| `Assets/UI/RecipeBookPanel.uxml` | 신규 |
| `Assets/UI/RecipeBookPanel.uss` | 신규 |
| `Assets/Scripts/Core/Managers/PlayerDataManager.cs` | `Mastery` 프로퍼티 추가 |
| `Assets/Scripts/Data/Server/ServerSaveData.cs` | `RecipeCookCounts` 필드 추가 |
| `Assets/Scripts/Data/Table/DataRegistry.cs` | `GetRecipeByFoodId()` 추가 |
| `Assets/Scripts/Cooking/CookingStation.cs` | 속도/절약/등급 보정 추가 |
| `Assets/Scripts/Restaurant/ServingSystem.cs` | 판매가 보정 적용 |
| `Assets/Prefabs/UI/HUD.prefab` | RecipeBookUI 컴포넌트 추가 |

---

## 테스트 기준 (모듈 7 완료 조건)

1. Tab키 → 레시피 도감 열림 (전 씬 공통)
2. 재료 미보유 + 요리 이력 없음 → ??? 표시
3. 재료 보유 → 해금 → 이름/결과/아이콘 + 보너스 표시
4. 요리 5회 → Lv2 달성 토스트 + 도감 ★★☆☆☆ + 조리 속도 -15% 적용
5. 요리 15회 → Lv3 → 재료 절약 20% 발동 확인
6. 요리 50회 → Lv5 → 등급 상향 30% 발동 확인
7. Lv2 음식 서빙 → 판매가 ×1.1 적용 확인
8. 저장 → 재시작 → 마스터리 레벨 유지
