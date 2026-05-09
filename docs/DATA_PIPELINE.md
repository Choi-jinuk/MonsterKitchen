# Data Pipeline — 몬스터 키친 (가제)

> 최종 수정: 2026-04-15

---

## 1. 개요 — 양방향 동기화 구조

```
┌─────────────────────────────────────────────────────────────┐
│                    데이터 동기화 흐름                         │
│                                                             │
│   Excel (.xlsx) ◄──────────────────────────────────────┐   │
│        ↕  (EPPlus)                                      │   │
│   CSV (.csv)    ◄──────────────────────────────────┐   │   │
│        ↕  (CsvParser)                              │   │   │
│   ScriptableObject (.asset)                        │   │   │
│        ↕                                           │   │   │
│   ┌─────────────────────────────────┐              │   │   │
│   │   DataManagerWindow (Editor)    │──────────────┘───┘   │
│   │   조회 · 추가 · 삭제 · 시각화   │  변경 시 3곳 자동 반영 │
│   └─────────────────────────────────┘                       │
└─────────────────────────────────────────────────────────────┘
```

**진리의 원천(Source of Truth)**: CSV
- CSV가 마스터 데이터. SO는 CSV에서 생성, Excel도 CSV 기준으로 갱신
- Unity Editor 툴에서 변경 → CSV 저장 → SO 재생성 → Excel 갱신 (순서대로)

---

## 2. CSV 포맷 규칙 (확정)

### 2.1 행 규칙

| 패턴 | 처리 |
|---|---|
| `;` 로 시작하는 행 | **주석** — 파서가 무시, Excel에도 유지 |
| `#TYPE` 행 | 타입 힌트 — 파서가 캐스팅에 사용, 임포터 필수 행 |
| 빈 첫 컬럼 행 | 건너뜀 |

### 2.2 컬럼 규칙

| 패턴 | 처리 |
|---|---|
| `_` 로 시작하는 컬럼명 | **무시** — 파서/임포터가 읽지 않음 (기획 메모용) |
| 일반 컬럼 | 정상 파싱 |

```csv
; 박쥐 몬스터 데이터 — 1티어 기본 몬스터
id,name_kr,hp,attack,attribute,_memo,_작업자
#TYPE,string,string,int,float,AttributeType,string,string
MON_001,박쥐,80,12.5,Wind,1티어 기본 몬스터,김기획
; MON_002는 아직 미구현
```
→ `_memo`, `_작업자` 컬럼은 파싱 무시, CSV/Excel에는 유지됨

### 2.3 데이터 타입 표기

| 타입 | #TYPE 표기 | CSV 값 예시 |
|---|---|---|
| 문자열 | `string` | `박쥐` |
| 정수 | `int` | `80` |
| 실수 | `float` | `12.5` |
| 불리언 | `bool` | `true` / `false` |
| Enum | Enum 클래스명 | `Fire` / `Wind` |
| 문자열 배열 | `string[]` | `ING_001\|ING_002\|ING_003` |
| 정수 배열 | `int[]` | `1\|2\|3` |
| 실수 배열 | `float[]` | `0.8\|0.3\|0.5` |
| 복합 구조체 | `struct:StructName` | `key:value;key:value` |

---

## 3. 폴더 구조

```
Assets/
├── Data/
│   ├── CSV/                              # CSV 마스터 (git 추적)
│   │   ├── Monsters.csv
│   │   ├── Ingredients.csv
│   │   ├── Recipes.csv
│   │   ├── Foods.csv
│   │   ├── DropTables.csv
│   │   ├── Characters.csv
│   │   └── Buffs.csv
│   │
│   └── ScriptableObjects/               # 생성된 SO 에셋 (git 추적)
│       ├── Monsters/
│       ├── Ingredients/
│       ├── Recipes/
│       ├── DropTables/
│       └── Characters/
│
├── Editor/
│   └── DataPipeline/
│       ├── DataManagerWindow.cs          # 통합 데이터 관리 Editor 창
│       ├── Tabs/
│       │   ├── DataBrowserTab.cs         # 테이블 뷰, 검색, 필터
│       │   ├── DataEditorTab.cs          # 상세 편집 폼
│       │   └── StatisticsTab.cs          # 통계/시각화
│       ├── Sync/
│       │   ├── SyncCoordinator.cs        # 변경 감지 → CSV/SO/Excel 순차 동기화
│       │   ├── CsvReadWriter.cs          # CSV 읽기/쓰기 (주석·무시컬럼 보존)
│       │   ├── ScriptableObjectSync.cs   # SO 생성/갱신/삭제
│       │   └── ExcelSync.cs              # Excel 읽기/쓰기 (EPPlus)
│       ├── Core/
│       │   ├── CsvParser.cs              # CSV → Dictionary 파싱
│       │   ├── DataSchema.cs             # 스키마 정의 (컬럼, 타입, 검증 규칙)
│       │   └── DataValidator.cs          # 저장 전 유효성 검사
│       └── Importers/                    # 타입별 SO 임포터
│           ├── MonsterImporter.cs
│           ├── IngredientImporter.cs
│           ├── RecipeImporter.cs
│           ├── DropTableImporter.cs
│           └── CharacterImporter.cs
│
└── RawData/                              # Excel 원본 (git LFS 권장)
    └── MonsterKitchen_GameData.xlsx
```

---

## 4. DataManagerWindow — Editor 툴 상세 설계

메뉴 경로: `MonsterKitchen → Data Manager`

### 4.1 전체 레이아웃

```
┌──────────────────────────────────────────────────────────────────┐
│  Monster Kitchen — Data Manager                         [?] [⚙]  │
├──────────────────────────────────────────────────────────────────┤
│  [Monsters ▼] [Ingredients] [Recipes] [DropTables] [Characters]  │  ← 데이터 타입 탭
├──────────────────────────────────────────────────────────────────┤
│  [Browser] [Statistics]                                          │  ← 기능 탭
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  🔍 [검색어 입력        ]  [속성 ▼] [티어 ▼]  [+ 추가] [🗑삭제]  │
│                                                                  │
│  ┌──┬──────────┬──────┬────────┬──────────┬────────┐           │
│  │☐ │ ID       │ 이름 │  HP    │ 속성     │ 티어   │           │
│  ├──┼──────────┼──────┼────────┼──────────┼────────┤           │
│  │☐ │ MON_001  │ 박쥐 │  80    │ Wind     │ 1      │  ←클릭    │
│  │☑ │ MON_002  │슬라임│ 120    │ Water    │ 1      │  ←선택됨  │
│  │☐ │ MON_003  │화염도│ 200    │ Fire     │ 2      │           │
│  └──┴──────────┴──────┴────────┴──────────┴────────┘           │
│  총 3개 | 선택 1개                    [◀ 1/1 ▶]  페이지당[50 ▼] │
│                                                                  │
├──────────────────────────────────────────────────────────────────┤
│  [MON_002 — 슬라임] ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │  ← 하단 상세 편집 패널
│  id          [MON_002          ]    name_kr   [슬라임       ]   │
│  hp          [120              ]    attack    [8.0          ]   │
│  defense     [8.0              ]    attribute [Water      ▼]   │
│  weak_attr   [Magic           ▼]    tier      [1            ]   │
│  drop_table  [DRP_002          ]    _memo     [기본 슬라임   ]  │
│                               [취소]  [저장 & 동기화]           │
├──────────────────────────────────────────────────────────────────┤
│  ✅ 마지막 동기화: 2026-04-15 14:23  CSV ✓  SO ✓  Excel ✓      │
└──────────────────────────────────────────────────────────────────┘
```

### 4.2 Browser 탭 — 기능 명세

**테이블 뷰**
- 컬럼 너비 조절 가능 (드래그)
- 컬럼 헤더 클릭 → 오름/내림 정렬
- `_` 접두사 컬럼은 회색으로 표시 (숨기기 토글 가능)

**검색 & 필터**
- 텍스트 검색: ID, 이름, 설명 전체 텍스트 검색
- 드롭다운 필터: Enum 타입 컬럼 필터 (속성, 등급, 티어 등)
- 범위 필터: int/float 컬럼 범위 필터 (HP 50~200 등)

**행 추가**
- `[+ 추가]` 버튼 → 빈 행 생성, ID 자동 채번 (마지막 ID + 1)
- 하단 편집 패널에서 즉시 편집 가능

**행 삭제**
- 체크박스 선택 후 `[🗑 삭제]` → 확인 다이얼로그 → 삭제 후 즉시 동기화

**인라인 편집**
- 셀 더블클릭 → 인라인 편집 (단순 텍스트/숫자 필드)
- 복잡한 필드 (배열, Enum) → 하단 패널에서 편집

### 4.3 Statistics 탭 — 시각화

```
┌──────────────────────────────────────────────────────────────────┐
│  Statistics — Monsters                                           │
├─────────────────────────┬────────────────────────────────────────┤
│  요약                   │  속성 분포                            │
│  총 몬스터: 24개        │  ████████ Fire (8)                    │
│  티어 1: 10개           │  ██████   Water (6)                   │
│  티어 2: 8개            │  █████    Wind (5)                    │
│  티어 3: 6개            │  ████     Earth (4)                   │
│                         │  █        Magic (1)                   │
├─────────────────────────┼────────────────────────────────────────┤
│  HP 분포 (히스토그램)   │  드롭 테이블 연결 현황                │
│  0~100   ██████ (12)   │  ✅ 연결됨: 22개                      │
│  100~200 ████   (8)    │  ⚠️  미연결: 2개  [바로가기]           │
│  200~300 ██     (4)    │                                        │
├─────────────────────────┴────────────────────────────────────────┤
│  [CSV 내보내기]  [Excel 열기]  [레포트 생성]                     │
└──────────────────────────────────────────────────────────────────┘
```

**지원 차트 타입** (Unity IMGUI 또는 UI Toolkit 기반)
- 바 차트: Enum 분포, 티어별 개수
- 히스토그램: HP/공격력 수치 분포
- 파이 차트: 속성/등급 비율
- 데이터 무결성 리포트: 미연결 참조, 빈 필수 필드, 중복 ID

---

## 5. 동기화 로직 — SyncCoordinator

### 5.1 변경 발생 시 처리 순서

```
[편집 저장]
    │
    ▼
1. DataValidator.Validate(row)
    ├─ 실패: 오류 표시, 저장 중단
    └─ 성공: 계속
    │
    ▼
2. CsvReadWriter.Write(csvPath, rows)
    ├─ 주석 행(;) 위치 보존
    ├─ _컬럼 데이터 보존
    └─ UTF-8 BOM 인코딩 (Excel 한글 호환)
    │
    ▼
3. ScriptableObjectSync.Sync(changedRows)
    ├─ ID로 기존 .asset 검색
    ├─ 존재: 필드 업데이트 → AssetDatabase.SaveAssetIfDirty()
    └─ 없음: CreateInstance() → AssetDatabase.CreateAsset()
    │
    ▼
4. ExcelSync.Write(xlsxPath, sheetName, rows)
    ├─ EPPlus로 해당 시트만 업데이트
    ├─ 기존 셀 서식/색상 유지
    └─ _컬럼 보존
    │
    ▼
5. 상태바 갱신: "✅ 동기화 완료 14:23"
```

### 5.2 삭제 처리

```
[행 삭제 확인]
    │
    ▼
1. CsvReadWriter.DeleteRow(id)        → CSV에서 행 제거
2. ScriptableObjectSync.Delete(id)    → .asset 파일 삭제 (AssetDatabase.DeleteAsset)
3. ExcelSync.DeleteRow(id)            → Excel 시트에서 행 제거
4. DataRegistry 캐시 무효화
```

### 5.3 주석/무시 컬럼 보존 전략

CSV 파일을 쓸 때 원본 파일의 메타데이터를 보존:
- `;` 시작 행: 원래 위치(행 번호) 그대로 유지
- `_` 접두사 컬럼: 값 그대로 유지, 파싱만 건너뜀
- `#TYPE` 행: 항상 두 번째 행 위치 유지

---

## 6. Excel 동기화 — EPPlus 설정

### 6.1 패키지 설치

EPPlus는 Unity Package Manager의 NuGet 브리지 또는 DLL 직접 추가:

```
Assets/Plugins/EPPlus/
├── EPPlus.dll                 # EPPlus 7.x (LGPLv2.1)
└── EPPlus.Interfaces.dll
```

> **라이선스**: EPPlus 6+ 는 상용 프로젝트에 유료 라이선스 필요.
> 무료 대안: **NPOI** (Apache 2.0) 또는 **ClosedXML** (MIT) 고려.

### 6.2 Excel 시트 구조 규칙

- 파일 1개에 시트 N개 (Monsters, Ingredients, Recipes ...)
- 시트명 = CSV 파일명과 동일 (`Monsters`, `Ingredients` ...)
- 1행: 컬럼 헤더 (배경색 #4472C4, 흰색 텍스트)
- 2행: `#TYPE` 힌트 (배경색 #D9D9D9)
- `;` 주석 행: 배경색 #E2EFDA (연두색)
- `_` 컬럼: 배경색 #FFF2CC (연노랑)

---

## 7. ScriptableObject 데이터 클래스

### 7.1 MonsterData.cs
```csharp
[CreateAssetMenu(menuName = "MonsterKitchen/Monster")]
public class MonsterData : ScriptableObject
{
    public string id;
    public string nameKr;
    public int hp;
    public float attack;
    public float defense;
    public AttributeType attribute;
    public AttributeType weakAttribute;
    public int tier;
    public string dropTableId;
    public string description;
}
```

### 7.2 IngredientData.cs
```csharp
[CreateAssetMenu(menuName = "MonsterKitchen/Ingredient")]
public class IngredientData : ScriptableObject
{
    public string id;
    public string nameKr;
    public IngredientState state;
    public int basePrice;
    public int freshnessMax;
    public string sourceMonsterID;
    public RarityType rarity;
    public string[] tags;
    public string description;
}
```

### 7.3 DropTableData.cs
```csharp
[CreateAssetMenu(menuName = "MonsterKitchen/DropTable")]
public class DropTableData : ScriptableObject
{
    public string id;
    public string monsterID;
    public AttributeType finishAttribute;

    [System.Serializable]
    public struct DropEntry
    {
        public string ingredientId;
        public int minQuantity;
        public int maxQuantity;
        public float chance;
    }

    public DropEntry[] drops;
    public string guaranteedDropId;
}
```

### 7.4 RecipeData.cs
```csharp
[CreateAssetMenu(menuName = "MonsterKitchen/Recipe")]
public class RecipeData : ScriptableObject
{
    public string id;
    public string nameKr;

    [System.Serializable]
    public struct RecipeIngredient
    {
        public string ingredientId;
        public int count;
    }

    public RecipeIngredient[] requiredIngredients;
    public string optionalIngredientId;
    public string resultFoodId;   // → FoodData.id 참조
    public int cookTimeSec;
    public CookingMethod cookingMethod;
    public string description;
}
```

### 7.4-B FoodData.cs
> 완성 요리 템플릿. 버프 효과 정의, 마스터리 레벨별 보정 수치 등 **모든 플레이어 공통 데이터**.
> 마스터리 현재 레벨, 누적 제출 횟수 등 플레이어 개별 상태는 세이브 데이터(JSON)에서 관리.

```csharp
[CreateAssetMenu(menuName = "MonsterKitchen/Food")]
public class FoodData : ScriptableObject
{
    public string id;
    public string nameKr;
    public int basePrice;
    public string buffId;           // → BuffData.id 참조 (없으면 빈 문자열)

    // 마스터리 레벨별 판매가 배율 (index 0 = Lv1, index 4 = Lv5)
    public float[] masteryPriceMultiplier;  // 예: [1.0, 1.2, 1.45, 1.8, 2.5]

    public string description;
}
```

**플레이어 세이브 데이터 측 (JSON, 참고용)**
```json
"foodMastery": {
  "FOOD_001": { "level": 3, "totalCount": 67 },
  "FOOD_002": { "level": 1, "totalCount": 12 }
}
```

### 7.5 CharacterData.cs
```csharp
[CreateAssetMenu(menuName = "MonsterKitchen/Character")]
public class CharacterData : ScriptableObject
{
    public string id;
    public string nameKr;
    public CharacterRole role;      // Warrior / Mage / Rogue / Priest / Cook

    // 기본 스탯 (레벨 1 기준)
    public int baseHp;
    public float baseAttack;
    public float baseDefense;
    public float baseMoveSpeed;

    // 스킬 (액티브 3개 보유, 2개 장착 / 패시브 1개)
    public string[] activeSkillIds;  // 길이 3 고정
    public string passiveSkillId;

    // 스킬 트리는 별도 SkillTreeData SO로 분리 (구현 시 설계)
    public string skillTreeId;

    public string description;
}

public enum CharacterRole
{
    Warrior, Mage, Rogue, Priest, Cook
}
```

> SkillTreeData 상세 설계는 스킬 시스템 구현 시 별도 문서화 예정.

### 7.6 공용 Enum
```csharp
// Assets/Scripts/Data/GameEnums.cs
public enum AttributeType
{
    None, Fire, Electric, Water, Earth, Wind, Ice, Magic, Poison, Capture
}

public enum IngredientState
{
    Raw, Cooked, Frozen, Dried, Poisoned, MagicInfused, Alive
}

public enum RarityType
{
    Common, Uncommon, Rare, Epic, Legendary
}
```

---

## 8. DataRegistry — 런타임 조회

```csharp
// Assets/Scripts/DataAccess/DataRegistry.cs
// Boot 씬에서 초기화, Addressables로 모든 SO 로드 후 Dictionary 구성
public class DataRegistry : MonoBehaviour
{
    public static DataRegistry Instance { get; private set; }

    private Dictionary<string, MonsterData>    _monsters    = new();
    private Dictionary<string, IngredientData> _ingredients = new();
    private Dictionary<string, RecipeData>     _recipes     = new();
    private Dictionary<string, DropTableData>  _dropTables  = new();

    public MonsterData    GetMonster(string id)    => _monsters[id];
    public IngredientData GetIngredient(string id) => _ingredients[id];
    public RecipeData     GetRecipe(string id)     => _recipes[id];

    // 막타 속성으로 드롭 테이블 조회
    public DropTableData  GetDropTable(string monsterId, AttributeType finishAttr)
        => _dropTables[$"{monsterId}_{finishAttr}"];
}
```

---

## 9. ID 명명 규칙

| 타입 | 접두사 | 예시 |
|---|---|---|
| Monster | `MON_` | `MON_001_Bat` |
| Ingredient | `ING_` | `ING_001_BatMeat_Raw` |
| Recipe | `RCP_` | `RCP_001_BatSoup` |
| Drop Table | `DRP_` | `DRP_001_Bat_Fire` |
| Character | `CHR_` | `CHR_001_Warrior` |
| Food (완성 요리) | `FOOD_` | `FOOD_001_BatSoup` |
| Buff | `BUFF_` | `BUFF_001_AttackUp` |

자동 채번: `XXX_NNN` 형식, 마지막 번호 +1 (빈 번호 재사용 안 함)

---

## 10. 버전 관리 전략

| 파일 | git 관리 | 이유 |
|---|---|---|
| `Assets/Data/CSV/*.csv` | ✅ 추적 | 텍스트 diff 가능, 마스터 데이터 |
| `Assets/Data/ScriptableObjects/**/*.asset` | ✅ 추적 | Unity 직렬화 텍스트 모드 |
| `Assets/Editor/DataPipeline/**/*.cs` | ✅ 추적 | 툴 코드 |
| `RawData/*.xlsx` | ⚠️ git LFS | 바이너리, 용량 큼 |
| `Assets/Plugins/EPPlus/` | ✅ 추적 | DLL 버전 고정 |
