# Ingredient Quality & Cooking Grade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 던전 처치 방식(속성 매칭·CC·무기 등급)으로 재료 품질(I/II/III)을 결정하고, 요리 등급(FoodGrade)과 식당 팁에 연결한다.

**Architecture:** `KillQualityEvaluator`(순수 C# static)가 처치 시 품질 점수를 계산 → `ItemDrop`에 품질 기록 → `PlayerInventoryData`가 품질별 카운트를 side-dict로 관리 → `CookingStation`이 레시피 재료 품질 평균으로 FoodGrade 도출 → `CustomerAI`가 Perfect 서빙 시 팁 지급.

**Tech Stack:** Unity 6 C#, NUnit EditMode 테스트, UIToolkit(인벤토리 배지).

---

## 파일 구조

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Data/Table/GameEnums.cs` | `IngredientQuality` enum 추가 |
| `Assets/Scripts/Combat/CrowdControlComponent.cs` | `CCType` 에 `Sleep`, `Hypnosis` 추가 + IsImmune |
| `Assets/Scripts/Data/Table/WeaponTable.cs` | `WeaponData.Tier` 필드 추가 |
| `Assets/Data/CSV/Weapons.csv` | `Tier` 컬럼 추가 |
| `Assets/Scripts/Combat/KillQualityEvaluator.cs` | **신규** — 순수 static 계산기 |
| `Assets/Tests/EditMode/KillQualityEvaluatorTests.cs` | **신규** — 7개 테스트 |
| `Assets/Scripts/Combat/ItemDrop.cs` | `Quality` 필드 + `Init` 오버로드 |
| `Assets/Scripts/Combat/DropResolver.cs` | `HandleDeath` 에서 품질 계산 후 ItemDrop 에 전달 |
| `Assets/Scripts/Data/Player/PlayerInventoryData.cs` | 품질별 side-dict + `AddIngredientWithQuality` + `CalculateCookingGrade` + `ConsumeIngredientQuality` |
| `Assets/Scripts/Core/Managers/NetworkManager.cs` | `RequestAddIngredient` quality 오버로드 + `RequestCook` 품질 소모 |
| `Assets/Scripts/Cooking/CookingStation.cs` | `CookRecipe(recipe)` — grade 파라미터 제거, 품질에서 도출 |
| `Assets/Scripts/Cooking/CookingUI.cs` | `TryCook` 호출 시 grade 파라미터 제거 |
| `Assets/Scripts/Restaurant/CustomerAI.cs` | Perfect 서빙 시 30% 팁 지급 |
| `Assets/Scripts/UI/HUD/InventoryUI.cs` | 재료 슬롯에 품질 배지 추가 |

---

## Task 1: 기반 타입 — IngredientQuality enum + CCType + WeaponData.Tier

**Files:**
- Modify: `Assets/Scripts/Data/Table/GameEnums.cs`
- Modify: `Assets/Scripts/Combat/CrowdControlComponent.cs`
- Modify: `Assets/Scripts/Data/Table/WeaponTable.cs`
- Modify: `Assets/Data/CSV/Weapons.csv`

- [ ] **Step 1: GameEnums.cs 에 IngredientQuality enum 추가**

`FoodGrade` 열거 직후에 추가:

```csharp
    public enum IngredientQuality
    {
        I   = 1,   // Normal — 기본 처치
        II  = 2,   // Good   — 속성 매칭 또는 CC 처치
        III = 3,   // Perfect — 복수 조건 충족
    }
```

- [ ] **Step 2: CrowdControlComponent.cs 에서 CCType 에 Sleep·Hypnosis 추가**

파일 상단에서 `CCType` 열거체를 찾아 아래처럼 수정한다. (`CCType` 는 동일 파일에 없고 별도 정의되어 있을 수 있다 — `grep -r "enum CCType"` 로 파일 확인 후 수정.)

현재 CCType:
```csharp
public enum CCType { None, Knockback, Stun, PullIn }
```

수정 후:
```csharp
public enum CCType
{
    None      = 0,
    Knockback = 1,
    Stun      = 2,
    PullIn    = 3,
    Sleep     = 4,   // 수면 — 처치 시 재료 품질 +1
    Hypnosis  = 5,   // 최면 — 처치 시 재료 품질 +1
}
```

- [ ] **Step 3: CrowdControlComponent.IsImmune 에 Sleep·Hypnosis 케이스 추가**

`IsImmune` switch 문에 두 케이스 추가:

```csharp
bool IsImmune(CCType type)
{
    var mb = GetComponent<MonsterBase>();
    if (mb?.Data == null) return false;

    var data = mb.Data;
    return type switch
    {
        CCType.Knockback => data.ImmuneToKnockback,
        CCType.Stun      => data.ImmuneToStun,
        CCType.PullIn    => data.ImmuneToPullIn,
        CCType.Sleep     => false,   // 현재 면역 없음 — 향후 MonsterData 확장 시 추가
        CCType.Hypnosis  => false,
        _                => false,
    };
}
```

- [ ] **Step 4: WeaponTable.cs WeaponData 에 Tier 필드 추가**

`WeaponData` 클래스 `[Header("Durability")]` 섹션 위에 삽입:

```csharp
    [Header("Tier")]
    [Tooltip("무기 등급 (1~5). KillQualityEvaluator 의 weaponTier 로 사용된다.")]
    [Min(1)]
    public int Tier = 1;
```

- [ ] **Step 5: Weapons.csv 에 Tier 컬럼 추가**

`#TYPE` 행과 `_key` 행, 데이터 행 모두 수정:

```csv
; 무기 데이터
; id: uint (WPN_001=7001 ~)
; abils: "AbilType:value|..." 형식 (예: Attack:10.0|CritRate:0.05)
; normalAttackGroupId: SkillGroupData.skillGroupId (string)
; tier: int (1=기본, 2=중급, 3=고급, 4=희귀, 5=전설)
#TYPE,string,uint,string,string,string,int,string,string,string,string,int
_key,Id,WeaponName,WeaponType,Abils,MaxDurability,DecayMode,NormalAttackGroupId,WeaponSpriteAddress,WeaponAnimAddress,Tier
WPN_001,7001,낡은 검,Sword,Attack:10,0,None,SGD_001,,,1
WPN_002,7002,단단한 도끼,Axe,Attack:18|Defense:3,0,None,SGD_002,,,2
WPN_003,7003,쌍검,DualSword,Attack:8|AttackSpeed:0.2,0,None,SGD_003,,,2
```

- [ ] **Step 6: Unity Editor 에서 DataManagerWindow 로 Weapons SO 재동기화**

Unity Editor → Tools → DataManager → Weapons Sync.
컴파일 에러 없는지 확인: MCP `read_console`.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Data/Table/GameEnums.cs
git add Assets/Scripts/Combat/CrowdControlComponent.cs
git add Assets/Scripts/Data/Table/WeaponTable.cs
git add Assets/Data/CSV/Weapons.csv
git commit -m "feat: add IngredientQuality enum, CCType Sleep/Hypnosis, WeaponData.Tier"
```

---

## Task 2: KillQualityEvaluator — TDD

**Files:**
- Create: `Assets/Tests/EditMode/KillQualityEvaluatorTests.cs`
- Create: `Assets/Scripts/Combat/KillQualityEvaluator.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
// Assets/Tests/EditMode/KillQualityEvaluatorTests.cs
using NUnit.Framework;
using MonsterKitchen;
using MonsterKitchen.Combat;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class KillQualityEvaluatorTests
    {
        // monsterRarity: RarityType enum int (0=Common..4=Legendary)
        // recommended = ceil(rarity / 2f): 0→0, 1→1, 2→1, 3→2, 4→2

        [Test]
        public void Evaluate_NoFactors_ReturnsGradeI()
        {
            // weaponTier=1, recommended=ceil(2/2)=1 → gap=0 → +1점
            // but no attribute match, no CC → total=1 → Grade I
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Rare,      // 2 → recommended=1
                monsterAttribute: AttributeType.Fire,
                killAttribute:    AttributeType.None,        // 속성 미스
                hadSoftCC:        false,
                weaponTier:       0);                        // gap=-1 → 0점

            Assert.AreEqual(IngredientQuality.I, result);
        }

        [Test]
        public void Evaluate_AttributeMatchOnly_ReturnsGradeI()
        {
            // attribute +1, no CC, weapon gap<0 → total=1 → Grade I
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Common,    // 0 → recommended=0
                monsterAttribute: AttributeType.Water,
                killAttribute:    AttributeType.Water,       // 속성 매칭 +1
                hadSoftCC:        false,
                weaponTier:       -1);                       // gap=-1 → 0점

            Assert.AreEqual(IngredientQuality.I, result);
        }

        [Test]
        public void Evaluate_AttributeMatchAndCC_ReturnsGradeII()
        {
            // attribute +1, CC +1, weapon gap<0 → total=2 → Grade II
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Epic,      // 3 → recommended=2
                monsterAttribute: AttributeType.Fire,
                killAttribute:    AttributeType.Fire,        // +1
                hadSoftCC:        true,                      // +1
                weaponTier:       0);                        // gap=-2 → 0점

            Assert.AreEqual(IngredientQuality.II, result);
        }

        [Test]
        public void Evaluate_AllThreeFactors_ReturnsGradeIII()
        {
            // attribute +1, CC +1, weapon gap=0 → +1 → total=3 → Grade III
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Common,    // 0 → recommended=0
                monsterAttribute: AttributeType.Wind,
                killAttribute:    AttributeType.Wind,        // +1
                hadSoftCC:        true,                      // +1
                weaponTier:       0);                        // gap=0 → +1

            Assert.AreEqual(IngredientQuality.III, result);
        }

        [Test]
        public void Evaluate_HighWeaponTierAlone_ReturnsGradeIII()
        {
            // no attribute, no CC, weapon gap≥2 → +3 → capped to 3 → Grade III
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Common,    // 0 → recommended=0
                monsterAttribute: AttributeType.None,
                killAttribute:    AttributeType.None,
                hadSoftCC:        false,
                weaponTier:       3);                        // gap=3 → +3

            Assert.AreEqual(IngredientQuality.III, result);
        }

        [Test]
        public void Evaluate_WeaponTierGap1_Returns2WeaponPoints()
        {
            // no attribute, no CC, weapon gap=1 → +2 → total=2 → Grade II
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Common,    // recommended=0
                monsterAttribute: AttributeType.None,
                killAttribute:    AttributeType.None,
                hadSoftCC:        false,
                weaponTier:       1);                        // gap=1 → +2

            Assert.AreEqual(IngredientQuality.II, result);
        }

        [Test]
        public void Evaluate_TotalCappedAt3()
        {
            // attribute +1, CC +1, weapon gap=2 → +3 → sum=5, capped to 3 → Grade III
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Common,    // recommended=0
                monsterAttribute: AttributeType.Fire,
                killAttribute:    AttributeType.Fire,        // +1
                hadSoftCC:        true,                      // +1
                weaponTier:       2);                        // gap=2 → +3

            Assert.AreEqual(IngredientQuality.III, result);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" \
  -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" \
  -runTests -testPlatform EditMode \
  -testResults "C:/Users/CHOI/AppData/LocalLow/DefaultCompany/FantasyTycoon/TestResults.xml" \
  -batchmode -quit
```

Expected: FAIL with "KillQualityEvaluator not found"

- [ ] **Step 3: KillQualityEvaluator 구현**

```csharp
// Assets/Scripts/Combat/KillQualityEvaluator.cs
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    // ====================================================================
    //  KillQualityEvaluator — 처치 조건 → 재료 품질 계산기
    //
    //  ▶ 점수 계산
    //    속성 매칭 : killAttribute == monsterAttribute (None 제외) → +1
    //    CC 처치   : hadSoftCC(Sleep·Hypnosis) → +1
    //    무기 등급 : gap = weaponTier - ceil(monsterRarity / 2)
    //               gap < 0 → 0, gap == 0 → +1, gap == 1 → +2, gap ≥ 2 → +3
    //    total = min(3, 합산)
    //
    //  ▶ 품질 매핑
    //    0-1점 → IngredientQuality.I
    //    2점   → IngredientQuality.II
    //    3점   → IngredientQuality.III
    // ====================================================================

    public static class KillQualityEvaluator
    {
        /// <summary>
        /// 처치 조건으로 재료 품질을 결정한다.
        /// </summary>
        /// <param name="monsterRarity">MonsterData.Rarity 를 int 캐스트한 값 (0=Common … 4=Legendary)</param>
        /// <param name="monsterAttribute">몬스터 속성</param>
        /// <param name="killAttribute">처치에 사용된 속성</param>
        /// <param name="hadSoftCC">처치 시점에 Sleep 또는 Hypnosis CC 가 걸려 있었는지</param>
        /// <param name="weaponTier">장착 무기 Tier (WeaponData.Tier)</param>
        public static IngredientQuality Evaluate(
            int           monsterRarity,
            AttributeType monsterAttribute,
            AttributeType killAttribute,
            bool          hadSoftCC,
            int           weaponTier)
        {
            int score = 0;

            // 속성 매칭
            if (monsterAttribute != AttributeType.None && killAttribute == monsterAttribute)
                score += 1;

            // CC 처치
            if (hadSoftCC)
                score += 1;

            // 무기 등급
            int recommended = Mathf.CeilToInt(monsterRarity / 2f);
            int gap         = weaponTier - recommended;
            score += gap < 0 ? 0 : gap == 0 ? 1 : gap == 1 ? 2 : 3;

            score = Mathf.Min(3, score);

            return score switch
            {
                3 => IngredientQuality.III,
                2 => IngredientQuality.II,
                _ => IngredientQuality.I,
            };
        }
    }
}
```

- [ ] **Step 4: 테스트 실행 — 7개 모두 통과 확인**

위와 동일한 명령 실행. Expected: 전체 테스트 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Combat/KillQualityEvaluator.cs
git add Assets/Tests/EditMode/KillQualityEvaluatorTests.cs
git commit -m "feat: add KillQualityEvaluator with 7 passing tests"
```

---

## Task 3: ItemDrop + DropResolver — 처치 → 품질 → 드롭

**Files:**
- Modify: `Assets/Scripts/Combat/ItemDrop.cs`
- Modify: `Assets/Scripts/Combat/DropResolver.cs`

- [ ] **Step 1: ItemDrop 에 Quality 필드 및 Init 오버로드 추가**

```csharp
// Assets/Scripts/Combat/ItemDrop.cs
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    /// <summary>
    /// 씬에 놓인 드롭 아이템. 플레이어가 접촉하면 인벤토리에 추가된다.
    /// </summary>
    public class ItemDrop : MonoBehaviour
    {
        [SerializeField] SpriteRenderer m_SpriteRenderer;
        [SerializeField] float          m_PickupRadius = 0.35f;

        uint             m_IngredientId;
        int              m_Qty;
        IngredientQuality m_Quality = IngredientQuality.I;
        bool             m_PickedUp;

        /// <summary>품질 없는 초기화 (MVP 폴백, 품질 I 적용).</summary>
        public void Init(uint ingredientId, int qty)
            => Init(ingredientId, qty, IngredientQuality.I);

        /// <summary>품질 포함 초기화.</summary>
        public void Init(uint ingredientId, int qty, IngredientQuality quality)
        {
            m_IngredientId = ingredientId;
            m_Qty          = qty;
            m_Quality      = quality;

            if (m_SpriteRenderer != null)
            {
                var data   = DataRegistry.Instance?.Ingredients?.Get(ingredientId);
                var sprite = AssetLoadManager.Instance?.Load<Sprite>(data?.SpriteAddress);
                if (sprite != null) m_SpriteRenderer.sprite = sprite;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (m_PickedUp) return;
            if (!other.CompareTag("Player")) return;

            m_PickedUp = true;
            NetworkManager.Instance?.RequestAddIngredient(m_IngredientId, m_Qty, m_Quality);
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_PickupRadius);
        }
    }
}
```

- [ ] **Step 2: DropResolver 에서 품질 계산 후 ItemDrop 에 전달**

`DropResolver.HandleDeath` 는 기존 `Action<AttributeType>` 시그니처를 유지하면서, 내부에서 플레이어 무기 티어와 몬스터 CC 상태를 조회한다.

```csharp
// Assets/Scripts/Combat/DropResolver.cs
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    /// <summary>
    /// 몬스터 사망 시 드롭 테이블을 굴려 씬에 ItemDrop 을 스폰한다.
    /// 처치 조건(속성·CC·무기 등급)으로 재료 품질을 결정한다.
    /// </summary>
    public class DropResolver : MonoBehaviour
    {
        [SerializeField] GameObject m_ItemDropPrefab;

        Health               m_Health;
        MonsterBase          m_Monster;
        CrowdControlComponent m_CC;

        void Awake()
        {
            m_Health  = GetComponent<Health>();
            m_Monster = GetComponent<MonsterBase>();
            m_CC      = GetComponent<CrowdControlComponent>();
        }

        void OnEnable()
        {
            if (m_Health != null)
                m_Health.OnDeath += HandleDeath;
        }

        void OnDisable()
        {
            if (m_Health != null)
                m_Health.OnDeath -= HandleDeath;
        }

        void HandleDeath(AttributeType killAttribute)
        {
            uint dropTableId = m_Monster?.Data?.DropTableId ?? 0u;
            if (dropTableId == 0u) return;

            var dropTable = DataRegistry.Instance?.DropTables?.Get(dropTableId);
            if (dropTable == null) return;

            // ── 품질 계산 ────────────────────────────────────────────
            bool hadSoftCC = m_CC != null &&
                             (m_CC.CurrentCC == CCType.Sleep || m_CC.CurrentCC == CCType.Hypnosis);

            int weaponTier = PlayerManager.Instance?.Stats?.EquippedWeapon?.Tier ?? 1;

            var quality = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)(m_Monster.Data?.Rarity ?? RarityType.Common),
                monsterAttribute: m_Monster.Data?.Attribute ?? AttributeType.None,
                killAttribute:    killAttribute,
                hadSoftCC:        hadSoftCC,
                weaponTier:       weaponTier);
            // ─────────────────────────────────────────────────────────

            var drops = dropTable.Roll();
            foreach (var (ingredientId, qty) in drops)
            {
                if (ingredientId == 0u || qty <= 0) continue;
                SpawnDrop(ingredientId, qty, quality);
            }
        }

        void SpawnDrop(uint ingredientId, int qty, IngredientQuality quality)
        {
            if (m_ItemDropPrefab == null)
            {
                NetworkManager.Instance?.RequestAddIngredient(ingredientId, qty, quality);
                return;
            }

            Vector2 offset = RandomUtil.InCircle(0.4f);
            var go = Instantiate(m_ItemDropPrefab,
                                 (Vector2)transform.position + offset,
                                 Quaternion.identity);

            var drop = go.GetComponent<ItemDrop>();
            drop?.Init(ingredientId, qty, quality);
        }
    }
}
```

> **NOTE:** `PlayerManager.Instance?.Stats?.EquippedWeapon` 접근 경로를 확인해야 한다. `PlayerManager` 가 `PlayerStats` 를 가지고 있고 `PlayerStats.EquippedWeapon` 이 `WeaponData` 를 반환하는지 `grep -r "EquippedWeapon"` 로 확인 후 실제 경로로 수정한다.

- [ ] **Step 3: 컴파일 확인**

MCP `read_console` — 에러 없으면 통과.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Combat/ItemDrop.cs
git add Assets/Scripts/Combat/DropResolver.cs
git commit -m "feat: ItemDrop Quality field, DropResolver evaluates kill quality"
```

---

## Task 4: PlayerInventoryData — 품질 스토리지

**Files:**
- Modify: `Assets/Scripts/Data/Player/PlayerInventoryData.cs`
- Create: `Assets/Tests/EditMode/IngredientQualityStorageTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
// Assets/Tests/EditMode/IngredientQualityStorageTests.cs
using NUnit.Framework;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class IngredientQualityStorageTests
    {
        PlayerInventoryData m_Inv;

        [SetUp]
        public void SetUp()
        {
            m_Inv = new PlayerInventoryData();
            m_Inv.Init();
        }

        [Test]
        public void AddIngredientWithQuality_TracksQualityCount()
        {
            m_Inv.AddIngredientWithQuality(1001u, 2, IngredientQuality.III);
            Assert.AreEqual(2, m_Inv.GetIngredientCount(1001u, IngredientQuality.III));
        }

        [Test]
        public void AddIngredientWithQuality_TotalCountCorrect()
        {
            m_Inv.AddIngredientWithQuality(1001u, 2, IngredientQuality.II);
            m_Inv.AddIngredientWithQuality(1001u, 3, IngredientQuality.I);
            Assert.AreEqual(5, m_Inv.GetIngredientCount(1001u));
        }

        [Test]
        public void ConsumeIngredientQuality_RemovesFromHighestFirst()
        {
            m_Inv.AddIngredientWithQuality(1001u, 1, IngredientQuality.III);
            m_Inv.AddIngredientWithQuality(1001u, 2, IngredientQuality.I);
            m_Inv.ConsumeIngredientQuality(1001u, 1);
            Assert.AreEqual(0, m_Inv.GetIngredientCount(1001u, IngredientQuality.III));
            Assert.AreEqual(2, m_Inv.GetIngredientCount(1001u, IngredientQuality.I));
        }

        [Test]
        public void GetBestQuality_ReturnsHighestAvailable()
        {
            m_Inv.AddIngredientWithQuality(1001u, 1, IngredientQuality.II);
            m_Inv.AddIngredientWithQuality(1001u, 1, IngredientQuality.I);
            Assert.AreEqual(IngredientQuality.II, m_Inv.GetBestQuality(1001u));
        }

        [Test]
        public void GetBestQuality_NoQualityTracked_ReturnsGradeI()
        {
            // 품질 정보 없음 (기존 데이터 호환)
            Assert.AreEqual(IngredientQuality.I, m_Inv.GetBestQuality(9999u));
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

위의 EditMode 테스트 실행 커맨드로 실행. Expected: FAIL.

- [ ] **Step 3: PlayerInventoryData 에 품질 스토리지 추가**

기존 필드 선언 다음에 추가:

```csharp
// 품질별 카운트 (에페머럴 — 세이브/로드 시 quality I 로 복원)
readonly Dictionary<uint, Dictionary<IngredientQuality, int>> m_IngredientQualities = new();
```

새 공개 메서드들 추가 (`// ── Apply 메서드` 섹션 아래):

```csharp
// ── 품질 스토리지 API ─────────────────────────────────────────

/// <summary>재료 추가 시 품질 카운트를 증가시킨다.</summary>
public void AddIngredientWithQuality(uint id, int qty, IngredientQuality quality)
{
    if (qty <= 0) return;
    if (!m_IngredientQualities.TryGetValue(id, out var qDict))
    {
        qDict = new Dictionary<IngredientQuality, int>();
        m_IngredientQualities[id] = qDict;
    }
    qDict.TryGetValue(quality, out int cur);
    qDict[quality] = cur + qty;
}

/// <summary>품질별 재료 수량. 품질 정보 없으면 0.</summary>
public int GetIngredientCount(uint id, IngredientQuality quality)
{
    if (!m_IngredientQualities.TryGetValue(id, out var qDict)) return 0;
    qDict.TryGetValue(quality, out int c);
    return c;
}

/// <summary>해당 재료의 최고 품질. 품질 정보 없으면 I.</summary>
public IngredientQuality GetBestQuality(uint id)
{
    if (!m_IngredientQualities.TryGetValue(id, out var qDict)) return IngredientQuality.I;
    if (qDict.TryGetValue(IngredientQuality.III, out int c3) && c3 > 0) return IngredientQuality.III;
    if (qDict.TryGetValue(IngredientQuality.II,  out int c2) && c2 > 0) return IngredientQuality.II;
    return IngredientQuality.I;
}

/// <summary>요리 시 재료를 품질 높은 것부터 qty 개 소모한다.</summary>
public void ConsumeIngredientQuality(uint id, int qty)
{
    if (!m_IngredientQualities.TryGetValue(id, out var qDict)) return;
    foreach (var q in new[] { IngredientQuality.III, IngredientQuality.II, IngredientQuality.I })
    {
        if (qty <= 0) break;
        if (!qDict.TryGetValue(q, out int c)) continue;
        int take = System.Math.Min(c, qty);
        qDict[q] = c - take;
        qty -= take;
        if (qDict[q] == 0) qDict.Remove(q);
    }
    if (qDict.Count == 0) m_IngredientQualities.Remove(id);
}

/// <summary>레시피에 필요한 재료 품질 평균 → FoodGrade 도출.</summary>
public FoodGrade CalculateCookingGrade(RecipeData recipe)
{
    if (recipe == null) return FoodGrade.Normal;

    // 레시피 재료 중복 제거 (동일 id 여러 슬롯 있어도 1회만 평균)
    var seenIds = new System.Collections.Generic.HashSet<uint>();
    float total = 0f;
    int   count = 0;

    foreach (var req in recipe.Ingredients)
    {
        if (req.IngredientId == 0u) continue;
        if (!seenIds.Add(req.IngredientId)) continue;

        total += (int)GetBestQuality(req.IngredientId);
        count++;
    }

    if (count == 0) return FoodGrade.Normal;

    float avg = total / count;
    return avg >= 2.5f ? FoodGrade.Perfect
         : avg >= 1.5f ? FoodGrade.Good
         :               FoodGrade.Normal;
}
```

- [ ] **Step 4: 테스트 실행 — 5개 모두 통과 확인**

Expected: PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Data/Player/PlayerInventoryData.cs
git add Assets/Tests/EditMode/IngredientQualityStorageTests.cs
git commit -m "feat: PlayerInventoryData quality-aware storage (side dict)"
```

---

## Task 5: NetworkManager — 품질 경로 추가

**Files:**
- Modify: `Assets/Scripts/Core/Managers/NetworkManager.cs`

- [ ] **Step 1: RequestAddIngredient 품질 오버로드 추가**

기존 `RequestAddIngredient` 메서드 바로 아래에 추가:

```csharp
/// <summary>
/// 품질 포함 재료 추가를 서버에 요청한다.
/// 총 수량 업데이트 + 품질 카운트 업데이트.
/// </summary>
public void RequestAddIngredient(uint ingredientId, int qty, IngredientQuality quality,
                                  Action<uint, int> onResult = null)
{
    if (qty <= 0) return;

    // ── [SERVER STUB START] ──────────────────────────────────
    int cur    = PlayerDataManager.Instance.Inventory.GetIngredientCount(ingredientId);
    int newQty = cur + qty;
    // ── [SERVER STUB END] ────────────────────────────────────

    PlayerDataManager.Instance.ApplyIngredient(ingredientId, newQty);
    PlayerDataManager.Instance.Inventory.AddIngredientWithQuality(ingredientId, qty, quality);
    GlobalController.Instance?.SaveSched.MarkDirty();
    onResult?.Invoke(ingredientId, newQty);
}
```

- [ ] **Step 2: RequestCook 에 품질 소모 추가**

기존 `RequestCook` 의 `foreach (var kv in ingredientChanges)` Apply 블록 이후에 추가:

```csharp
// 기존 코드:
foreach (var kv in ingredientChanges)
    PlayerDataManager.Instance.ApplyIngredient(kv.Key, kv.Value);

// 추가할 코드 (바로 아래):
foreach (var kv in reqCounts)
    PlayerDataManager.Instance.Inventory.ConsumeIngredientQuality(kv.Key, kv.Value);
```

- [ ] **Step 3: 컴파일 확인**

MCP `read_console` — 에러 없으면 통과.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Core/Managers/NetworkManager.cs
git commit -m "feat: NetworkManager RequestAddIngredient quality overload, RequestCook consumes quality"
```

---

## Task 6: CookingStation + CookingUI — 품질 → FoodGrade

**Files:**
- Modify: `Assets/Scripts/Cooking/CookingStation.cs`
- Modify: `Assets/Scripts/Cooking/CookingUI.cs`

- [ ] **Step 1: CookingStation.CookRecipe 에서 grade 파라미터 제거, 품질 도출로 변경**

기존:
```csharp
public void CookRecipe(RecipeData recipe, FoodGrade grade = FoodGrade.Normal)
{
    if (m_IsCooking || recipe == null) return;
    StartCoroutine(CookRoutine(recipe, grade));
}
```

변경 후:
```csharp
public void CookRecipe(RecipeData recipe)
{
    if (m_IsCooking || recipe == null) return;

    // 요리 전 인벤토리 품질로 등급 결정 (소모 전 미리 계산)
    FoodGrade grade = PlayerDataManager.Instance?.Inventory
                          .CalculateCookingGrade(recipe) ?? FoodGrade.Normal;

    StartCoroutine(CookRoutine(recipe, grade));
}
```

- [ ] **Step 2: CookingUI.TryCook 에서 grade 파라미터 제거**

기존:
```csharp
m_Station.CookRecipe(m_Matched, m_CurrentGrade);
```

변경 후:
```csharp
m_Station.CookRecipe(m_Matched);
```

- [ ] **Step 3: 컴파일 확인**

MCP `read_console`. `m_CurrentGrade` 가 다른 곳에서도 쓰이는지 확인 — 쓰이지 않으면 해당 필드와 관련 코드도 제거.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Cooking/CookingStation.cs
git add Assets/Scripts/Cooking/CookingUI.cs
git commit -m "feat: CookingStation derives FoodGrade from ingredient quality"
```

---

## Task 7: CustomerAI — Perfect 서빙 시 팁

**Files:**
- Modify: `Assets/Scripts/Restaurant/CustomerAI.cs`

- [ ] **Step 1: EatAndPay 에 팁 로직 추가**

기존 `EatAndPay` 코루틴의 `NetworkManager.Instance?.RequestEarnGold(pay);` 바로 아래에 추가:

```csharp
// Perfect 음식 → 30% 확률 팁
if (grade == FoodGrade.Perfect && UnityEngine.Random.value < 0.3f)
{
    int tip = Mathf.RoundToInt(food.BasePrice * 0.5f * shopMult);
    NetworkManager.Instance?.RequestEarnGold(tip);
    DebugUtil.Log($"[Customer] 팁 지급: {tip}G (Perfect 서빙 보너스)");
}
```

- [ ] **Step 2: 컴파일 확인**

MCP `read_console`.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Restaurant/CustomerAI.cs
git commit -m "feat: CustomerAI 30% tip on Perfect food serving"
```

---

## Task 8: InventoryUI — 재료 품질 배지

**Files:**
- Modify: `Assets/Scripts/UI/HUD/InventoryUI.cs`

- [ ] **Step 1: BuildIngredients 에 품질 배지 추가**

`BuildIngredients` 메서드에서 `Slot` 을 생성하는 부분을 품질 배지 포함 버전으로 교체:

```csharp
void BuildIngredients()
{
    if (m_IngGrid == null) return;
    m_IngGrid.Clear();

    var all = PlayerDataManager.Instance?.Inventory.AllIngredients;
    if (all == null || all.Count == 0)
    {
        m_IngGrid.Add(EmptyMsg("재료가 없습니다."));
        return;
    }

    var inv      = PlayerDataManager.Instance.Inventory;
    var registry = DataRegistry.Instance;
    var loader   = AssetLoadManager.Instance;
    foreach (var kv in all)
    {
        var d       = registry?.Ingredients?.Get(kv.Key);
        var sprite  = loader?.Load<Sprite>(d?.SpriteAddress);
        var quality = inv.GetBestQuality(kv.Key);
        m_IngGrid.Add(IngredientSlot(sprite, kv.Value.ToString(), d?.DisplayName ?? CommonString.Unknown, quality));
    }
}
```

- [ ] **Step 2: IngredientSlot 헬퍼 메서드 추가**

기존 `Slot` 메서드 아래에 추가:

```csharp
static VisualElement IngredientSlot(Sprite sprite, string count, string name, IngredientQuality quality)
{
    var wrap = new VisualElement();
    wrap.style.alignItems = Align.Center;

    var slot = new VisualElement();
    slot.AddToClassList("inv-slot");

    var icon = new VisualElement();
    icon.AddToClassList("inv-slot-icon");
    if (sprite != null) icon.style.backgroundImage = Background.FromSprite(sprite);
    slot.Add(icon);

    var countLbl = new Label(count);
    countLbl.AddToClassList("inv-slot-count");
    slot.Add(countLbl);

    // 품질 배지
    var badge = new Label(quality switch
    {
        IngredientQuality.III => "III",
        IngredientQuality.II  => "II",
        _                     => "I",
    });
    badge.AddToClassList("inv-quality-badge");
    badge.style.color = quality switch
    {
        IngredientQuality.III => new UnityEngine.Color(1f,  0.84f, 0f),    // 금색
        IngredientQuality.II  => new UnityEngine.Color(0.4f, 0.6f, 1f),   // 파란색
        _                     => new UnityEngine.Color(0.6f, 0.6f, 0.6f), // 회색
    };
    slot.Add(badge);

    var nameLbl = new Label(name);
    nameLbl.AddToClassList("inv-slot-name");

    wrap.Add(slot);
    wrap.Add(nameLbl);
    return wrap;
}
```

- [ ] **Step 3: InventoryPanel.uss 에 inv-quality-badge 스타일 추가**

`Assets/UI/InventoryPanel.uss` 파일 맨 끝에 추가:

```css
/* ── 재료 품질 배지 ─────────────────────────────── */
.inv-quality-badge {
    position: absolute;
    top: 2px;
    left: 3px;
    font-size: 9px;
    -unity-font-style: bold;
}
```

- [ ] **Step 4: 컴파일 확인**

MCP `read_console`.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/UI/HUD/InventoryUI.cs
git add Assets/UI/InventoryPanel.uss
git commit -m "feat: InventoryUI quality badge (I/II/III) on ingredient slots"
```

---

## Task 9: 최종 검증

- [ ] **Step 1: EditMode 테스트 전체 실행**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" \
  -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" \
  -runTests -testPlatform EditMode \
  -testResults "C:/Users/CHOI/AppData/LocalLow/DefaultCompany/FantasyTycoon/TestResults.xml" \
  -batchmode -quit
```

Expected: 전체 PASS (기존 13개 + 신규 12개 = 25개)

- [ ] **Step 2: MODULES.md 업데이트**

`docs/MODULES.md` 에서:
```
| 7-4 | 요리 등급 판정 ... | ❌ |
```
→
```
| 7-4 | 요리 등급 판정 (속성 매칭·CC 처치·무기 등급 → 재료 품질 I/II/III → FoodGrade) | ✅ |
```

- [ ] **Step 3: 최종 커밋**

```bash
git add docs/MODULES.md
git commit -m "docs: mark module 7-4 complete"
```
