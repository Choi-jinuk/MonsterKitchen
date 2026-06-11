# Dungeon Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace linear multi-room dungeon with a single open map — continuous monster respawn, weight-based DungeonBag temporary inventory, UIToolkit minimap, and always-active DungeonExit with confirmation popup.

**Architecture:** Four new files (DungeonMapController, DungeonSpawnZone, DungeonBag, DungeonBagUI) replace four deleted ones (DungeonSceneController, DungeonRoom, DungeonDoor, MonsterRespawnManager). ItemDrop routes pickups through DungeonBag.TryAdd(). GameHUD gets real minimap rendering and bag-weight HUD bar. All tested before scene rewiring.

**Tech Stack:** Unity 6, UIToolkit (VisualElement programmatic minimap), New Input System (Keyboard.current), Health.OnDeath event, NavGrid, DataRegistry, AssetLoadManager.

---

## File Map

| File | Change |
|---|---|
| `Assets/Scripts/Data/Table/GameEnums.cs` | Add `BagCapacity` to `PlayerUpgradeType` |
| `Assets/Scripts/Data/Player/PlayerUpgradeData.cs` | Add BagCapacityLevel + cost + capacity property |
| `Assets/Scripts/Data/Server/ServerSaveData.cs` | Add `BagCapacityLevel` field |
| `Assets/Scripts/Core/Managers/ServerDBManager.cs` | BuildSaveData + LoadLevels |
| `Assets/Scripts/Core/Managers/PlayerDataManager.cs` | LoadFrom |
| `Assets/Scripts/Core/Managers/NetworkManager.cs` | RequestUpgrade BagCapacity case |
| `Assets/Scripts/Data/Table/IngredientTable.cs` | Add `Weight` int field |
| `Assets/Data/CSV/Ingredients.csv` | Add `Weight` column (all = 1) |
| `Assets/Scripts/Dungeon/DungeonBag.cs` | **NEW** — pure C# session bag |
| `Assets/Tests/EditMode/DungeonBagTests.cs` | **NEW** — EditMode tests |
| `Assets/Scripts/Dungeon/DungeonSpawnZone.cs` | **NEW** — zone with auto-respawn |
| `Assets/Scripts/Dungeon/DungeonMapController.cs` | **NEW** — replaces DungeonSceneController |
| `Assets/Scripts/Dungeon/DungeonExit.cs` | Rework: E-key + DungeonBag check + popup |
| `Assets/Scripts/Combat/ItemDrop.cs` | Route through DungeonBag.TryAdd |
| `Assets/Scripts/UI/HUD/DungeonBagUI.cs` | **NEW** — UIPanel, B-key bag panel |
| `Assets/UI/DungeonBagPanel.uxml` | **NEW** |
| `Assets/UI/DungeonBagPanel.uss` | **NEW** |
| `Assets/UI/HUD.uxml` | Add minimap-root, bag-weight-bar, notification-label |
| `Assets/UI/HUD.uss` | Add minimap + bag bar CSS |
| `Assets/Scripts/UI/HUD/GameHUD.cs` | FindAndBindMinimap real impl + bag bar + toast |
| `Assets/Scripts/Dungeon/DungeonSceneController.cs` | **DELETE** |
| `Assets/Scripts/Dungeon/DungeonRoom.cs` | **DELETE** |
| `Assets/Scripts/Dungeon/DungeonDoor.cs` | **DELETE** |
| `Assets/Scripts/Enemy/MonsterRespawnManager.cs` | **DELETE** |
| `Assets/Scenes/DungeonScene.unity` | Rewire: remove old components, place new |

---

## Task 1: Data Foundations

**Files:**
- Modify: `Assets/Scripts/Data/Table/GameEnums.cs`
- Modify: `Assets/Scripts/Data/Player/PlayerUpgradeData.cs`
- Modify: `Assets/Scripts/Data/Server/ServerSaveData.cs`
- Modify: `Assets/Scripts/Core/Managers/ServerDBManager.cs`
- Modify: `Assets/Scripts/Core/Managers/PlayerDataManager.cs`
- Modify: `Assets/Scripts/Core/Managers/NetworkManager.cs`
- Modify: `Assets/Scripts/Data/Table/IngredientTable.cs`
- Modify: `Assets/Data/CSV/Ingredients.csv`

- [ ] **Step 1: Add BagCapacity to PlayerUpgradeType enum**

In `Assets/Scripts/Data/Table/GameEnums.cs`, change:
```csharp
public enum PlayerUpgradeType
{
    ToolDamage, ToolRange, ToolCooldown,
    ShopSeats,  ShopTip,
}
```
to:
```csharp
public enum PlayerUpgradeType
{
    ToolDamage, ToolRange, ToolCooldown,
    ShopSeats,  ShopTip,
    BagCapacity,
}
```

- [ ] **Step 2: Add BagCapacityLevel to PlayerUpgradeData**

In `Assets/Scripts/Data/Player/PlayerUpgradeData.cs`, add these members in the appropriate sections:

After the existing const block, add:
```csharp
const int BAG_UPGRADE_COST_BASE = 90;
```

After `int m_ShopTipLevel;`, add:
```csharp
int m_BagCapacityLevel;
```

After `public event Action OnShopUpgraded;`, add:
```csharp
public event Action OnBagUpgraded;
```

After `public int ShopTipUpgradeCost => ...`, add:
```csharp
public int   BagCapacityLevel    => m_BagCapacityLevel;
public int   BagCurrentCapacity  => 10 + m_BagCapacityLevel * 5;   // Lv0=10, Lv1=15, ..., Lv4=30
public int   BagUpgradeCost      => (m_BagCapacityLevel + 1) * BAG_UPGRADE_COST_BASE;
```

In `SetLevel`, add a new `case` before the closing `}`:
```csharp
case PlayerUpgradeType.BagCapacity:
    m_BagCapacityLevel = newLevel;
    OnBagUpgraded?.Invoke();
    DebugUtil.Log($"[BagUpgrade] 가방 Lv{newLevel} → {BagCurrentCapacity}kg");
    break;
```

Change `LoadLevels` signature and body:
```csharp
public void LoadLevels(int toolDamage, int toolRange, int toolCooldown,
                       int shopSeat, int shopTip, int bagCapacity = 0)
{
    m_ToolDamageLevel   = toolDamage;
    m_ToolRangeLevel    = toolRange;
    m_ToolCooldownLevel = toolCooldown;
    m_ShopSeatLevel     = shopSeat;
    m_ShopTipLevel      = shopTip;
    m_BagCapacityLevel  = bagCapacity;
}
```

- [ ] **Step 3: Add BagCapacityLevel to ServerSaveData**

In `Assets/Scripts/Data/Server/ServerSaveData.cs`, after `public int ShopTipLevel;` add:
```csharp
public int BagCapacityLevel;
```

- [ ] **Step 4: Update ServerDBManager.BuildSaveData**

In `Assets/Scripts/Core/Managers/ServerDBManager.cs`, in `BuildSaveData()`, after `ShopTipLevel = upg?.ShopTipLevel ?? 0,` add:
```csharp
BagCapacityLevel  = upg?.BagCapacityLevel  ?? 0,
```

- [ ] **Step 5: Update PlayerDataManager.LoadFrom**

In `Assets/Scripts/Core/Managers/PlayerDataManager.cs`, in `LoadFrom`, change:
```csharp
Upgrades.LoadLevels(
    save.ToolDamageLevel, save.ToolRangeLevel, save.ToolCooldownLevel,
    save.ShopSeatLevel,   save.ShopTipLevel);
```
to:
```csharp
Upgrades.LoadLevels(
    save.ToolDamageLevel, save.ToolRangeLevel, save.ToolCooldownLevel,
    save.ShopSeatLevel,   save.ShopTipLevel,   save.BagCapacityLevel);
```

- [ ] **Step 6: Update NetworkManager.RequestUpgrade BagCapacity case**

In `Assets/Scripts/Core/Managers/NetworkManager.cs`, in `RequestUpgrade`, update the `cost` switch:
```csharp
int cost = type switch
{
    PlayerUpgradeType.ToolDamage   => upg.ToolDamageUpgradeCost,
    PlayerUpgradeType.ToolRange    => upg.ToolRangeUpgradeCost,
    PlayerUpgradeType.ToolCooldown => upg.ToolCooldownUpgradeCost,
    PlayerUpgradeType.ShopSeats    => upg.ShopSeatUpgradeCost,
    PlayerUpgradeType.ShopTip      => upg.ShopTipUpgradeCost,
    PlayerUpgradeType.BagCapacity  => upg.BagUpgradeCost,
    _                              => int.MaxValue,
};
```

Update the `newLevel` switch:
```csharp
int newLevel = type switch
{
    PlayerUpgradeType.ToolDamage   => upg.ToolDamageLevel   + 1,
    PlayerUpgradeType.ToolRange    => upg.ToolRangeLevel    + 1,
    PlayerUpgradeType.ToolCooldown => upg.ToolCooldownLevel + 1,
    PlayerUpgradeType.ShopSeats    => upg.ShopSeatLevel     + 1,
    PlayerUpgradeType.ShopTip      => upg.ShopTipLevel      + 1,
    PlayerUpgradeType.BagCapacity  => upg.BagCapacityLevel  + 1,
    _                              => 0,
};
```

- [ ] **Step 7: Add Weight field to IngredientData**

In `Assets/Scripts/Data/Table/IngredientTable.cs`, inside the `IngredientData` class, add after `public string SpriteAddress;`:
```csharp
[Header("Dungeon Bag")]
[Tooltip("던전 가방 무게. 기본값 1.")]
public int Weight = 1;
```

- [ ] **Step 8: Add Weight column to Ingredients.csv**

In `Assets/Data/CSV/Ingredients.csv`, change:
```
#TYPE,string,uint,string,string,AttributeType,RarityType,IngredientState,string
_key,Id,NameKey,DescKey,Attribute,Rarity,DefaultState,SourceMonsterIds
ING_001,2001,,,Poison,Common,Raw,1001
ING_002,2002,,,Fire,Common,Raw,1002
ING_003,2003,,,None,Common,Raw,1003
ING_004,2004,,,None,Uncommon,Raw,1003
ING_005,2005,,,Poison,Uncommon,Raw,1004
ING_006,2006,,,Poison,Uncommon,Raw,1004
ING_007,2007,,,Earth,Rare,Raw,1005
ING_008,2008,,,Earth,Rare,Raw,1005
```
to:
```
#TYPE,string,uint,string,string,AttributeType,RarityType,IngredientState,string,int
_key,Id,NameKey,DescKey,Attribute,Rarity,DefaultState,SourceMonsterIds,Weight
ING_001,2001,,,Poison,Common,Raw,1001,1
ING_002,2002,,,Fire,Common,Raw,1002,1
ING_003,2003,,,None,Common,Raw,1003,1
ING_004,2004,,,None,Uncommon,Raw,1003,2
ING_005,2005,,,Poison,Uncommon,Raw,1004,2
ING_006,2006,,,Poison,Uncommon,Raw,1004,2
ING_007,2007,,,Earth,Rare,Raw,1005,3
ING_008,2008,,,Earth,Rare,Raw,1005,3
```

- [ ] **Step 9: Sync CSV in Unity Editor**

In Unity Editor, open `Window → DataManager` and click `Sync All`. Verify `TableData.asset` shows IngredientData entries with Weight values.

- [ ] **Step 10: Verify compilation**

Use `read_console` MCP tool. Expect zero errors. If errors appear, fix before continuing.

- [ ] **Step 11: Commit**

```bash
git add Assets/Scripts/Data/Table/GameEnums.cs
git add Assets/Scripts/Data/Player/PlayerUpgradeData.cs
git add Assets/Scripts/Data/Server/ServerSaveData.cs
git add Assets/Scripts/Core/Managers/ServerDBManager.cs
git add Assets/Scripts/Core/Managers/PlayerDataManager.cs
git add Assets/Scripts/Core/Managers/NetworkManager.cs
git add Assets/Scripts/Data/Table/IngredientTable.cs
git add Assets/Data/CSV/Ingredients.csv
git add Assets/Data/SO/TableData.asset
git commit -m "feat: add BagCapacity upgrade type and IngredientData.Weight field"
```

---

## Task 2: DungeonBag (TDD)

**Files:**
- Create: `Assets/Scripts/Dungeon/DungeonBag.cs`
- Create: `Assets/Tests/EditMode/DungeonBagTests.cs`

- [ ] **Step 1: Write failing tests first**

Create `Assets/Tests/EditMode/DungeonBagTests.cs`:

```csharp
using NUnit.Framework;
using MonsterKitchen.Dungeon;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class DungeonBagTests
    {
        DungeonBag m_Bag;

        [SetUp]
        public void SetUp()
        {
            m_Bag = new DungeonBag(10);   // maxWeight=10
        }

        [TearDown]
        public void TearDown()
        {
            DungeonBag.ClearCurrent();
        }

        // ── TryAdd ───────────────────────────────────────────────────

        [Test]
        public void TryAdd_UnderCapacity_ReturnsTrue()
        {
            bool result = m_Bag.TryAdd(2001u, 2, IngredientQuality.I, 1);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryAdd_ExactCapacity_ReturnsTrue()
        {
            bool result = m_Bag.TryAdd(2001u, 10, IngredientQuality.I, 1);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryAdd_ExceedsCapacity_ReturnsFalse()
        {
            bool result = m_Bag.TryAdd(2001u, 11, IngredientQuality.I, 1);
            Assert.IsFalse(result);
        }

        [Test]
        public void TryAdd_AccumulatesWeight()
        {
            m_Bag.TryAdd(2001u, 3, IngredientQuality.I, 2);   // +6
            Assert.AreEqual(6, m_Bag.CurrentWeight);
        }

        [Test]
        public void TryAdd_SecondAddWouldExceedCapacity_ReturnsFalse()
        {
            m_Bag.TryAdd(2001u, 4, IngredientQuality.I, 2);   // +8, total=8
            bool result = m_Bag.TryAdd(2002u, 2, IngredientQuality.I, 2); // +4 → 12 > 10
            Assert.IsFalse(result);
        }

        [Test]
        public void TryAdd_SameIngredient_MergesQty()
        {
            m_Bag.TryAdd(2001u, 2, IngredientQuality.I, 1);
            m_Bag.TryAdd(2001u, 3, IngredientQuality.I, 1);
            m_Bag.Contents.TryGetValue(2001u, out var entry);
            Assert.AreEqual(5, entry.qty);
        }

        [Test]
        public void TryAdd_SameIngredient_KeepsBestQuality()
        {
            m_Bag.TryAdd(2001u, 1, IngredientQuality.I,   1);
            m_Bag.TryAdd(2001u, 1, IngredientQuality.III, 1);
            m_Bag.Contents.TryGetValue(2001u, out var entry);
            Assert.AreEqual(IngredientQuality.III, entry.quality);
        }

        // ── IsFull ───────────────────────────────────────────────────

        [Test]
        public void IsFull_WhenAtMaxWeight_True()
        {
            m_Bag.TryAdd(2001u, 10, IngredientQuality.I, 1);
            Assert.IsTrue(m_Bag.IsFull);
        }

        [Test]
        public void IsFull_WhenUnderMaxWeight_False()
        {
            m_Bag.TryAdd(2001u, 5, IngredientQuality.I, 1);
            Assert.IsFalse(m_Bag.IsFull);
        }

        // ── Remove ───────────────────────────────────────────────────

        [Test]
        public void Remove_DecreasesWeightAndQty()
        {
            m_Bag.TryAdd(2001u, 4, IngredientQuality.I, 2);   // weight=8
            m_Bag.Remove(2001u, 2);                             // remove 2 → weight=4
            Assert.AreEqual(4, m_Bag.CurrentWeight);
            m_Bag.Contents.TryGetValue(2001u, out var entry);
            Assert.AreEqual(2, entry.qty);
        }

        [Test]
        public void Remove_AllQty_RemovesKey()
        {
            m_Bag.TryAdd(2001u, 3, IngredientQuality.I, 1);
            m_Bag.Remove(2001u, 3);
            Assert.IsFalse(m_Bag.Contents.ContainsKey(2001u));
            Assert.AreEqual(0, m_Bag.CurrentWeight);
        }

        [Test]
        public void Remove_MoreThanQty_ClampsToActualQty()
        {
            m_Bag.TryAdd(2001u, 2, IngredientQuality.I, 1);
            m_Bag.Remove(2001u, 99);
            Assert.IsFalse(m_Bag.Contents.ContainsKey(2001u));
            Assert.AreEqual(0, m_Bag.CurrentWeight);
        }

        // ── Static Current ───────────────────────────────────────────

        [Test]
        public void Constructor_SetsCurrent()
        {
            Assert.AreSame(m_Bag, DungeonBag.Current);
        }

        [Test]
        public void ClearCurrent_SetsNull()
        {
            DungeonBag.ClearCurrent();
            Assert.IsNull(DungeonBag.Current);
        }

        // ── OnBagChanged event ────────────────────────────────────────

        [Test]
        public void TryAdd_Success_FiresOnBagChanged()
        {
            bool fired = false;
            m_Bag.OnBagChanged += () => fired = true;
            m_Bag.TryAdd(2001u, 1, IngredientQuality.I, 1);
            Assert.IsTrue(fired);
        }

        [Test]
        public void TryAdd_Fail_DoesNotFireOnBagChanged()
        {
            bool fired = false;
            m_Bag.OnBagChanged += () => fired = true;
            m_Bag.TryAdd(2001u, 100, IngredientQuality.I, 1);   // exceeds
            Assert.IsFalse(fired);
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compile error (DungeonBag doesn't exist yet)**

Use `run_tests` MCP tool with testPlatform=EditMode. Expected: compile error "DungeonBag not found".

- [ ] **Step 3: Create DungeonBag.cs**

Create `Assets/Scripts/Dungeon/DungeonBag.cs`:

```csharp
using System;
using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonBag — 던전 세션 전용 임시 재료 컨테이너 (순수 C# 클래스)
    //
    //  ▶ 무게 기반. 최대 무게 초과 시 TryAdd 거부.
    //  ▶ DungeonMapController 가 생성하고 Current 에 등록.
    //  ▶ FlushToInventory() 호출 시 NetworkManager 경유 인벤토리 이전 후 초기화.
    // ====================================================================

    public class DungeonBag
    {
        public static DungeonBag Current { get; private set; }

        readonly Dictionary<uint, (int qty, IngredientQuality quality)> m_Contents        = new();
        readonly Dictionary<uint, int>                                   m_WeightPerUnit   = new();

        int m_CurrentWeight;
        int m_MaxWeight;

        public int  CurrentWeight => m_CurrentWeight;
        public int  MaxWeight     => m_MaxWeight;
        public bool IsFull        => m_CurrentWeight >= m_MaxWeight;

        public IReadOnlyDictionary<uint, (int qty, IngredientQuality quality)> Contents => m_Contents;

        public event Action OnBagChanged;

        // ── 생성 ─────────────────────────────────────────────────────

        public DungeonBag(int maxWeight)
        {
            m_MaxWeight = maxWeight;
            Current     = this;
        }

        public static void ClearCurrent()
        {
            Current = null;
        }

        // ── 조작 API ─────────────────────────────────────────────────

        /// <summary>
        /// 재료 추가 시도. 무게 초과 시 false 반환 (부분 추가 없음).
        /// weightPerUnit: 단위 무게 (IngredientData.Weight 에서 전달).
        /// 같은 재료 재추가 시 더 높은 품질 유지.
        /// </summary>
        public bool TryAdd(uint ingredientId, int qty, IngredientQuality quality, int weightPerUnit)
        {
            int addWeight = weightPerUnit * qty;
            if (m_CurrentWeight + addWeight > m_MaxWeight) return false;

            if (m_Contents.TryGetValue(ingredientId, out var existing))
            {
                var bestQuality = (IngredientQuality)System.Math.Max((int)existing.quality, (int)quality);
                m_Contents[ingredientId] = (existing.qty + qty, bestQuality);
            }
            else
            {
                m_Contents[ingredientId]      = (qty, quality);
                m_WeightPerUnit[ingredientId] = weightPerUnit;
            }

            m_CurrentWeight += addWeight;
            OnBagChanged?.Invoke();
            return true;
        }

        /// <summary>특정 재료 qty 개 제거 (버리기). qty > 보유량이면 전량 제거.</summary>
        public void Remove(uint ingredientId, int qty)
        {
            if (!m_Contents.TryGetValue(ingredientId, out var existing)) return;

            int weightPerUnit = m_WeightPerUnit.TryGetValue(ingredientId, out int w) ? w : 1;
            int removeQty     = System.Math.Min(qty, existing.qty);
            int newQty        = existing.qty - removeQty;

            if (newQty <= 0)
            {
                m_Contents.Remove(ingredientId);
                m_WeightPerUnit.Remove(ingredientId);
            }
            else
            {
                m_Contents[ingredientId] = (newQty, existing.quality);
            }

            m_CurrentWeight -= weightPerUnit * removeQty;
            if (m_CurrentWeight < 0) m_CurrentWeight = 0;
            OnBagChanged?.Invoke();
        }

        /// <summary>귀환 시 전체 내용물을 NetworkManager 경유 인벤토리로 이전 후 클리어.</summary>
        public void FlushToInventory()
        {
            foreach (var kv in m_Contents)
                NetworkManager.Instance?.RequestAddIngredient(kv.Key, kv.Value.qty, kv.Value.quality);

            m_Contents.Clear();
            m_WeightPerUnit.Clear();
            m_CurrentWeight = 0;
            OnBagChanged?.Invoke();
            DebugUtil.Log("[DungeonBag] FlushToInventory 완료. 가방 초기화.");
        }
    }
}
```

- [ ] **Step 4: Run tests — expect all pass**

Use `run_tests` MCP with EditMode. Filter: `DungeonBagTests`. Expected: all 15 tests PASS.

- [ ] **Step 5: Verify compilation**

Use `read_console` MCP. Expect zero errors.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Dungeon/DungeonBag.cs
git add Assets/Tests/EditMode/DungeonBagTests.cs
git commit -m "feat: add DungeonBag with TDD — weight-based dungeon session bag"
```

---

## Task 3: DungeonSpawnZone

**Files:**
- Create: `Assets/Scripts/Dungeon/DungeonSpawnZone.cs`

- [ ] **Step 1: Create DungeonSpawnZone.cs**

Create `Assets/Scripts/Dungeon/DungeonSpawnZone.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using MonsterKitchen.Combat;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Enemy;
using MonsterKitchen.Navigation;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonSpawnZone — 단일 스폰 구역. 자동 리스폰 관리.
    //
    //  ▶ Init(player) 호출 → SpawnAsync (프레임 분산) → 몬스터 Health.OnDeath 구독
    //  ▶ 몬스터 사망 → RespawnAfterDelay → 동일 위치·테이블에서 재스폰
    //  ▶ HasAliveMonsters / ZoneCenter : 미니맵 마커용
    // ====================================================================

    [DisallowMultipleComponent]
    public class DungeonSpawnZone : MonoBehaviour
    {
        const int SpawnPerFrame = 3;

        [Header("Spawn 설정")]
        [SerializeField] uint        m_SpawnTableId;
        [SerializeField] Transform[] m_SpawnPoints;
        [SerializeField] float       m_RespawnDelay = 8f;

        Transform m_Player;
        int       m_AliveCount;

        public bool    HasAliveMonsters => m_AliveCount > 0;
        public Vector3 ZoneCenter       => transform.position;

        // ── 공개 API ─────────────────────────────────────────────────

        public void Init(Transform player)
        {
            m_Player = player;
            StartCoroutine(SpawnAsync());
        }

        // ── 내부: 초기 스폰 (프레임 분산) ────────────────────────────

        IEnumerator SpawnAsync()
        {
            yield return null;   // 1프레임 대기 — 씬 활성화 직후 스파이크 방지

            if (m_SpawnTableId == 0) yield break;

            var table = DataRegistry.Instance?.DungeonSpawnTables?.Get(m_SpawnTableId);
            if (table == null)
            {
                DebugUtil.LogWarning(
                    $"[DungeonSpawnZone] spawnTableId={m_SpawnTableId} 를 찾을 수 없습니다.", this);
                yield break;
            }

            int pointIndex       = 0;
            int spawnedThisFrame = 0;

            foreach (var entry in table.Monsters)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    Vector3 pos = PickPoint(ref pointIndex, entry.SpawnRadius);
                    SpawnMonster(entry.MonsterId, pos);

                    spawnedThisFrame++;
                    if (spawnedThisFrame >= SpawnPerFrame)
                    {
                        spawnedThisFrame = 0;
                        yield return null;
                    }
                }
            }
        }

        // ── 단일 몬스터 스폰 ─────────────────────────────────────────

        void SpawnMonster(uint monsterId, Vector3 pos)
        {
            var data = DataRegistry.Instance?.Monsters?.Get(monsterId);
            if (data == null || string.IsNullOrEmpty(data.PrefabAddress))
            {
                DebugUtil.LogWarning(
                    $"[DungeonSpawnZone] monsterId={monsterId} 데이터 또는 prefabAddress 없음.", this);
                return;
            }

            var prefab = AssetLoadManager.Instance?.Load<MonsterBase>(data.PrefabAddress);
            if (prefab == null)
            {
                DebugUtil.LogWarning(
                    $"[DungeonSpawnZone] prefabAddress='{data.PrefabAddress}' 로드 실패.", this);
                return;
            }

            // InstantiateDisabled → Init → SetActive (SpawnManager 패턴 준수)
            bool wasActive = prefab.gameObject.activeSelf;
            prefab.gameObject.SetActive(false);
            var inst = Instantiate(prefab, pos, Quaternion.identity);
            prefab.gameObject.SetActive(wasActive);

            inst.Init(data, m_Player, pos);
            inst.gameObject.SetActive(true);

            // 사망 이벤트 구독
            var health = inst.GetComponent<Health>();
            if (health != null)
            {
                uint   capturedId  = monsterId;
                Vector3 capturedPos = pos;
                health.OnDeath += _ => OnMonsterDied(capturedId, capturedPos);
            }

            m_AliveCount++;
        }

        // ── 사망 처리 ─────────────────────────────────────────────────

        void OnMonsterDied(uint monsterId, Vector3 pos)
        {
            m_AliveCount = Mathf.Max(0, m_AliveCount - 1);
            StartCoroutine(RespawnAfterDelay(monsterId, pos));
        }

        IEnumerator RespawnAfterDelay(uint monsterId, Vector3 pos)
        {
            yield return new WaitForSeconds(m_RespawnDelay);
            if (this == null || !gameObject.activeSelf) yield break;   // 씬 전환 방어
            SpawnMonster(monsterId, pos);
        }

        // ── 스폰 포인트 선택 ─────────────────────────────────────────

        Vector3 PickPoint(ref int index, float radius)
        {
            Vector3 center = Vector3.zero;
            if (m_SpawnPoints != null && m_SpawnPoints.Length > 0)
            {
                var pt = m_SpawnPoints[index % m_SpawnPoints.Length];
                index++;
                center = pt != null ? pt.position : Vector3.zero;
            }

            if (NavGrid.Instance != null)
            {
                Vector2 candidate = (Vector2)center + RandomUtil.InCircle(radius);
                Vector2 valid     = NavAgent.IsValid(candidate)
                    ? candidate
                    : NavAgent.GetNearestValid(candidate);
                return new Vector3(valid.x, valid.y, center.z);
            }

            return center + RandomUtil.InCircle3D(radius);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_SpawnTableId == 0)
                DebugUtil.LogWarning($"[DungeonSpawnZone] SpawnTableId 가 0 입니다.", this);
        }
#endif
    }
}
```

- [ ] **Step 2: Check compilation**

Use `read_console` MCP. Expect zero errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Dungeon/DungeonSpawnZone.cs
git commit -m "feat: add DungeonSpawnZone with automatic respawn"
```

---

## Task 4: DungeonMapController

**Files:**
- Create: `Assets/Scripts/Dungeon/DungeonMapController.cs`

- [ ] **Step 1: Create DungeonMapController.cs**

Create `Assets/Scripts/Dungeon/DungeonMapController.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Navigation;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonMapController — DungeonScene 총괄. SceneControllerBase 상속.
    //
    //  ▶ 역할
    //    DungeonSpawnZone[] 초기화 + DungeonBag 생성 + DungeonExit 활성화
    //    + NavGrid.Bake() + CameraConfiner.Refresh() + 미니맵 데이터 제공
    //
    //  ▶ 미니맵
    //    m_MinimapSprite: Inspector 연결 (던전 탑뷰 이미지)
    //    m_WorldBounds  : Inspector 연결 (맵 월드 영역)
    //    GameHUD 가 이 값을 읽어 UIToolkit 미니맵 렌더링에 사용.
    // ====================================================================

    [DisallowMultipleComponent]
    public class DungeonMapController : SceneControllerBase
    {
        public static DungeonMapController Instance { get; private set; }

        [Header("스폰 존 — Inspector 직접 연결")]
        [SerializeField] DungeonSpawnZone[] m_SpawnZones;

        [Header("출구 — Inspector 직접 연결 (항상 활성 배치)")]
        [SerializeField] DungeonExit m_Exit;

        [Header("카메라 컨파이너 — Inspector 직접 연결")]
        [SerializeField] DungeonCameraConfiner m_CameraConfiner;

        [Header("미니맵")]
        [SerializeField] Sprite m_MinimapSprite;
        [SerializeField] Rect   m_WorldBounds;

        public Sprite                 MinimapSprite => m_MinimapSprite;
        public Rect                   WorldBounds   => m_WorldBounds;
        public IReadOnlyList<DungeonSpawnZone> SpawnZones  => m_SpawnZones;

        // ── Unity ────────────────────────────────────────────────────

        void Awake()
        {
            Instance = this;
            SetupLayerCollisions();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                DungeonBag.ClearCurrent();
            }
        }

        // ── SceneControllerBase ──────────────────────────────────────

        protected override void OnInit()
        {
            GlobalController.Instance?.Player?.RepositionInScene();
            StartCoroutine(InitAsync());
        }

        IEnumerator InitAsync()
        {
            yield return null;   // 1프레임 대기

            // DungeonBag 초기화
            int maxWeight = PlayerDataManager.Instance?.Upgrades?.BagCurrentCapacity ?? 10;
            _ = new DungeonBag(maxWeight);   // DungeonBag.Current 자동 설정

            // NavGrid 베이크
            NavGrid.Instance?.Bake();

            // 카메라 경계 갱신
            m_CameraConfiner?.Refresh();

            // 스폰 존 초기화
            var player = GetPlayer();
            if (player != null)
            {
                foreach (var zone in m_SpawnZones)
                {
                    if (zone != null) zone.Init(player);
                }
            }
            else
            {
                DebugUtil.LogWarning("[DungeonMapController] 플레이어를 찾을 수 없습니다.");
            }

            // 출구 활성화 (항상)
            if (m_Exit != null)
                m_Exit.gameObject.SetActive(true);
            else
                DebugUtil.LogWarning("[DungeonMapController] DungeonExit 가 연결되지 않았습니다.", this);

            CompleteInit();
            DebugUtil.Log($"[DungeonMapController] 초기화 완료. 스폰 존: {m_SpawnZones?.Length ?? 0}개. 가방 용량: {maxWeight}");
        }

        // ── 플레이어 참조 ────────────────────────────────────────────

        Transform m_CachedPlayer;

        Transform GetPlayer()
        {
            if (m_CachedPlayer != null) return m_CachedPlayer;
            var player = PlayerManager.Instance?.Player;
            if (player != null)
            {
                m_CachedPlayer = player.transform;
                return m_CachedPlayer;
            }
            DebugUtil.LogWarning("[DungeonMapController] PlayerManager.Player 없음.");
            return null;
        }

        // ── 레이어 충돌 설정 ─────────────────────────────────────────

        static void SetupLayerCollisions()
        {
            int enemy  = LayerMask.NameToLayer("Enemy");
            int player = LayerMask.NameToLayer("Player");
            if (enemy < 0)
            {
                DebugUtil.LogWarning("[DungeonMapController] 'Enemy' 레이어 없음.");
                return;
            }
            Physics2D.IgnoreLayerCollision(enemy, enemy, true);
            if (player >= 0)
                Physics2D.IgnoreLayerCollision(player, enemy, true);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_SpawnZones == null || m_SpawnZones.Length == 0)
                DebugUtil.LogWarning("[DungeonMapController] SpawnZones 가 비어있습니다.");
            if (m_Exit == null)
                DebugUtil.LogWarning("[DungeonMapController] DungeonExit 가 연결되지 않았습니다.");
        }
#endif
    }
}
```

- [ ] **Step 2: Check compilation**

Use `read_console` MCP. Expect zero errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Dungeon/DungeonMapController.cs
git commit -m "feat: add DungeonMapController — single open map controller"
```

---

## Task 5: DungeonExit Rework

**Files:**
- Modify: `Assets/Scripts/Dungeon/DungeonExit.cs`

- [ ] **Step 1: Rewrite DungeonExit.cs**

Replace the entire content of `Assets/Scripts/Dungeon/DungeonExit.cs` with:

```csharp
using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonExit — 던전 출구. 항상 활성. E키 상호작용.
    //
    //  ▶ 플레이어가 Trigger 진입 → InteractionPrompt 표시
    //  ▶ E키 누름:
    //    - 가방 가득 찼거나 비어 있음 → 즉시 귀환
    //    - 가방 여유 있음 → 확인 팝업 표시
    //  ▶ 귀환: DungeonBag.FlushToInventory() → SceneLoader.LoadScene("ManagementScene")
    // ====================================================================

    public class DungeonExit : MonoBehaviour
    {
        bool m_PlayerInRange;
        bool m_PopupShown;

        // 팝업 오버레이 — 코드로 생성 (별도 UXML 불필요)
        VisualElement m_PopupRoot;

        // ── Unity ────────────────────────────────────────────────────

        void OnTriggerEnter2D(UnityEngine.Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            m_PlayerInRange = true;
        }

        void OnTriggerExit2D(UnityEngine.Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            m_PlayerInRange = false;
            HidePopup();
        }

        void Update()
        {
            if (!m_PlayerInRange || m_PopupShown) return;
            if (!Keyboard.current[Key.E].wasPressedThisFrame) return;

            var bag = DungeonBag.Current;

            // 가방이 없거나, 비어있거나, 가득 찼으면 즉시 귀환
            if (bag == null || bag.CurrentWeight == 0 || bag.IsFull)
            {
                DoReturn();
                return;
            }

            // 가방에 여유 있음 → 확인 팝업
            ShowConfirmPopup(bag.CurrentWeight, bag.MaxWeight);
        }

        // ── 귀환 ─────────────────────────────────────────────────────

        void DoReturn()
        {
            HidePopup();
            DungeonBag.Current?.FlushToInventory();
            DebugUtil.Log("[DungeonExit] 귀환 → ManagementScene");
            SceneLoader.Instance?.LoadScene("ManagementScene");
        }

        // ── 확인 팝업 (코드 생성) ─────────────────────────────────────

        void ShowConfirmPopup(int current, int max)
        {
            m_PopupShown = true;

            // GameHUD UIDocument 에 오버레이 추가
            var hud = GameHUD.Instance;
            if (hud == null) { DoReturn(); return; }

            var hudRoot = hud.GetComponent<UnityEngine.UIElements.UIDocument>()
                            ?.rootVisualElement;
            if (hudRoot == null) { DoReturn(); return; }

            m_PopupRoot = new VisualElement();
            m_PopupRoot.style.position          = Position.Absolute;
            m_PopupRoot.style.top               = 0;
            m_PopupRoot.style.left              = 0;
            m_PopupRoot.style.right             = 0;
            m_PopupRoot.style.bottom            = 0;
            m_PopupRoot.style.backgroundColor   = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
            m_PopupRoot.style.alignItems        = Align.Center;
            m_PopupRoot.style.justifyContent    = Justify.Center;

            var box = new VisualElement();
            box.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.16f, 0.97f));
            box.style.borderTopLeftRadius     = 12;
            box.style.borderTopRightRadius    = 12;
            box.style.borderBottomLeftRadius  = 12;
            box.style.borderBottomRightRadius = 12;
            box.style.paddingTop    = 24;
            box.style.paddingBottom = 24;
            box.style.paddingLeft   = 32;
            box.style.paddingRight  = 32;
            box.style.minWidth      = 320;
            box.style.alignItems    = Align.Center;

            var msg = new Label($"가방에 여유가 있습니다. ({current}/{max})\n귀환하시겠습니까?");
            msg.style.fontSize           = 18;
            msg.style.color              = new StyleColor(Color.white);
            msg.style.whiteSpace         = WhiteSpace.Normal;
            msg.style.unityTextAlign     = TextAnchor.MiddleCenter;
            msg.style.marginBottom       = 20;
            box.Add(msg);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.Center;

            var btnReturn = new Button(() => DoReturn())
            {
                text = "귀 환"
            };
            btnReturn.style.width           = 100;
            btnReturn.style.height          = 36;
            btnReturn.style.marginRight     = 12;
            btnReturn.style.fontSize        = 16;
            btnReturn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.55f, 0.85f));
            btnReturn.style.color           = new StyleColor(Color.white);
            btnReturn.style.borderTopLeftRadius     = 6;
            btnReturn.style.borderTopRightRadius    = 6;
            btnReturn.style.borderBottomLeftRadius  = 6;
            btnReturn.style.borderBottomRightRadius = 6;

            var btnCancel = new Button(() => HidePopup())
            {
                text = "취 소"
            };
            btnCancel.style.width           = 100;
            btnCancel.style.height          = 36;
            btnCancel.style.fontSize        = 16;
            btnCancel.style.backgroundColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
            btnCancel.style.color           = new StyleColor(Color.white);
            btnCancel.style.borderTopLeftRadius     = 6;
            btnCancel.style.borderTopRightRadius    = 6;
            btnCancel.style.borderBottomLeftRadius  = 6;
            btnCancel.style.borderBottomRightRadius = 6;

            btnRow.Add(btnReturn);
            btnRow.Add(btnCancel);
            box.Add(btnRow);
            m_PopupRoot.Add(box);
            hudRoot.Add(m_PopupRoot);
        }

        void HidePopup()
        {
            m_PopupShown = false;
            m_PopupRoot?.RemoveFromHierarchy();
            m_PopupRoot = null;
        }
    }
}
```

Note: `GameHUD` must be referenced. Add `using MonsterKitchen.UI;` at the top of the file.

Final import block:
```csharp
using MonsterKitchen.Core;
using MonsterKitchen.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
```

- [ ] **Step 2: Check compilation**

Use `read_console` MCP. Expect zero errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Dungeon/DungeonExit.cs
git commit -m "feat: rework DungeonExit — always active, E-key interaction, bag capacity popup"
```

---

## Task 6: ItemDrop → DungeonBag

**Files:**
- Modify: `Assets/Scripts/Combat/ItemDrop.cs`

- [ ] **Step 1: Update ItemDrop.OnTriggerEnter2D**

Replace the `OnTriggerEnter2D` method in `Assets/Scripts/Combat/ItemDrop.cs`:

```csharp
void OnTriggerEnter2D(Collider2D other)
{
    if (m_PickedUp) return;
    if (!other.CompareTag("Player")) return;

    // 던전 씬: DungeonBag 경유
    if (DungeonBag.Current != null)
    {
        var data       = DataRegistry.Instance?.Ingredients?.Get(m_IngredientId);
        int weightUnit = data?.Weight ?? 1;

        if (DungeonBag.Current.TryAdd(m_IngredientId, m_Qty, m_Quality, weightUnit))
        {
            m_PickedUp = true;
            Destroy(gameObject);
        }
        else
        {
            GameHUD.Instance?.ShowNotification("가방이 가득 찼습니다!", 2f);
        }
        return;
    }

    // 던전 외 씬 폴백 (정상 경로에선 발생 안 함)
    m_PickedUp = true;
    NetworkManager.Instance?.RequestAddIngredient(m_IngredientId, m_Qty, m_Quality);
    Destroy(gameObject);
}
```

Add `using MonsterKitchen.Dungeon;` and `using MonsterKitchen.UI;` to the using block.

- [ ] **Step 2: Check compilation**

`read_console` — will see `GameHUD.ShowNotification` not found. This is expected; it will be added in Task 9. Verify no other errors.

Note: If the compiler errors block build, temporarily comment out `GameHUD.Instance?.ShowNotification(...)` and uncomment after Task 9.

- [ ] **Step 3: Commit (after Task 9 makes ShowNotification compile)**

This file will be committed together with GameHUD changes in Task 9.

---

## Task 7: DungeonBagUI

**Files:**
- Create: `Assets/UI/DungeonBagPanel.uxml`
- Create: `Assets/UI/DungeonBagPanel.uss`
- Create: `Assets/Scripts/UI/HUD/DungeonBagUI.cs`

- [ ] **Step 1: Create DungeonBagPanel.uxml**

Create `Assets/UI/DungeonBagPanel.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <Style src="project://database/Assets/UI/DungeonBagPanel.uss"/>
    <ui:VisualElement name="bag-overlay" class="bag-overlay">
        <ui:VisualElement name="bag-panel" class="bag-panel">
            <ui:Label name="bag-title" text="던전 가방" class="bag-title"/>
            <ui:Label name="bag-weight-text" text="무게: 0/10" class="bag-weight-text"/>
            <ui:ScrollView name="bag-item-list" class="bag-item-list"/>
            <ui:Button name="bag-close-btn" text="닫기 [B]" class="bag-close-btn"/>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Create DungeonBagPanel.uss**

Create `Assets/UI/DungeonBagPanel.uss`:

```css
.bag-overlay {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    background-color: rgba(0, 0, 0, 0.55);
    align-items: center;
    justify-content: center;
}

.bag-panel {
    background-color: rgba(18, 18, 22, 0.97);
    border-radius: 12px;
    padding: 24px 28px;
    min-width: 300px;
    max-height: 480px;
    align-items: stretch;
    border-width: 1px;
    border-color: rgba(255, 255, 255, 0.12);
}

.bag-title {
    font-size: 22px;
    -unity-font-style: bold;
    color: rgb(255, 215, 60);
    -unity-text-align: upper-center;
    margin-bottom: 4px;
}

.bag-weight-text {
    font-size: 14px;
    color: rgb(200, 200, 200);
    -unity-text-align: upper-center;
    margin-bottom: 12px;
}

.bag-weight-text--full {
    color: rgb(255, 90, 90);
}

.bag-item-list {
    flex-grow: 1;
    min-height: 100px;
    max-height: 300px;
}

.bag-slot {
    flex-direction: row;
    align-items: center;
    padding: 6px 4px;
    border-bottom-width: 1px;
    border-color: rgba(255, 255, 255, 0.08);
}

.bag-slot-icon {
    width: 36px;
    height: 36px;
    background-color: rgba(255, 255, 255, 0.08);
    border-radius: 6px;
    -unity-background-scale-mode: scale-to-fit;
    margin-right: 10px;
}

.bag-slot-info {
    flex-grow: 1;
}

.bag-slot-name {
    font-size: 14px;
    color: rgb(220, 220, 220);
}

.bag-slot-detail {
    font-size: 12px;
    color: rgb(140, 140, 140);
}

.bag-discard-btn {
    width: 48px;
    height: 28px;
    font-size: 12px;
    background-color: rgba(180, 50, 50, 0.8);
    color: rgb(255, 200, 200);
    border-radius: 5px;
    border-width: 0;
}

.bag-close-btn {
    margin-top: 12px;
    height: 36px;
    font-size: 15px;
    background-color: rgba(60, 60, 80, 0.9);
    color: rgb(200, 200, 200);
    border-radius: 8px;
    border-width: 0;
}
```

- [ ] **Step 3: Create DungeonBagUI.cs**

Create `Assets/Scripts/UI/HUD/DungeonBagUI.cs`:

```csharp
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Dungeon;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  DungeonBagUI — 던전 가방 패널. UIPanel(isPopup=true), B키 토글.
    //
    //  ▶ DungeonBag.Current 에서 내용물 조회.
    //  ▶ 버리기 버튼 → DungeonBag.Remove() → 즉시 리스트 갱신.
    //  ▶ DungeonScene 에만 씬 직접 배치 → B키 등록은 UIPanel 이 자동 처리.
    // ====================================================================

    public class DungeonBagUI : UIPanel
    {
        Label         m_WeightText;
        ScrollView    m_ItemList;

        // ── UIPanel 훅 ────────────────────────────────────────────────

        protected override void OnFirstOpen()
        {
            m_WeightText = Root?.Q<Label>("bag-weight-text");
            m_ItemList   = Root?.Q<ScrollView>("bag-item-list");

            Root?.Q<Button>("bag-close-btn")
                ?.RegisterCallback<ClickEvent>(_ => UIManager.Instance?.Close(PanelId));
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Refresh();

            // 가방 변경 시 실시간 갱신
            if (DungeonBag.Current != null)
                DungeonBag.Current.OnBagChanged += Refresh;
        }

        public override void OnClose()
        {
            if (DungeonBag.Current != null)
                DungeonBag.Current.OnBagChanged -= Refresh;

            base.OnClose();
        }

        // ── 리스트 갱신 ──────────────────────────────────────────────

        void Refresh()
        {
            var bag = DungeonBag.Current;
            if (bag == null) return;

            // 무게 텍스트
            if (m_WeightText != null)
            {
                m_WeightText.text = $"무게: {bag.CurrentWeight} / {bag.MaxWeight}";
                bool isFull = bag.IsFull;
                if (isFull)
                    m_WeightText.AddToClassList("bag-weight-text--full");
                else
                    m_WeightText.RemoveFromClassList("bag-weight-text--full");
            }

            // 슬롯 목록 재빌드
            m_ItemList?.Clear();
            foreach (var kv in bag.Contents)
            {
                uint              id      = kv.Key;
                int               qty     = kv.Value.qty;
                IngredientQuality quality = kv.Value.quality;

                var data = DataRegistry.Instance?.Ingredients?.Get(id);
                var slot = BuildSlot(id, qty, quality, data);
                m_ItemList?.Add(slot);
            }
        }

        VisualElement BuildSlot(uint id, int qty, IngredientQuality quality, IngredientData data)
        {
            var slot = new VisualElement();
            slot.AddToClassList("bag-slot");

            // 아이콘
            var icon = new VisualElement();
            icon.AddToClassList("bag-slot-icon");
            if (data != null && !string.IsNullOrEmpty(data.SpriteAddress))
            {
                var sprite = AssetLoadManager.Instance?.Load<Sprite>(data.SpriteAddress);
                if (sprite != null)
                    icon.style.backgroundImage = new StyleBackground(sprite);
            }
            slot.Add(icon);

            // 정보
            var info = new VisualElement();
            info.AddToClassList("bag-slot-info");

            var nameLabel = new Label(data?.DisplayName ?? $"ID:{id}");
            nameLabel.AddToClassList("bag-slot-name");
            info.Add(nameLabel);

            string qualityStr = quality == IngredientQuality.III ? "최상" :
                                quality == IngredientQuality.II  ? "상"   : "보통";
            var detailLabel = new Label($"x{qty}  품질:{qualityStr}");
            detailLabel.AddToClassList("bag-slot-detail");
            info.Add(detailLabel);

            slot.Add(info);

            // 버리기 버튼
            var discard = new Button();
            discard.text = "버리기";
            discard.AddToClassList("bag-discard-btn");
            discard.RegisterCallback<ClickEvent>(_ =>
            {
                DungeonBag.Current?.Remove(id, qty);
                // OnBagChanged 이벤트가 Refresh를 호출하므로 별도 갱신 불필요
            });
            slot.Add(discard);

            return slot;
        }
    }
}
```

- [ ] **Step 4: Check compilation**

Use `read_console` MCP. Expect zero errors.

- [ ] **Step 5: Commit**

```bash
git add Assets/UI/DungeonBagPanel.uxml
git add Assets/UI/DungeonBagPanel.uss
git add Assets/Scripts/UI/HUD/DungeonBagUI.cs
git commit -m "feat: add DungeonBagUI — B-key panel with discard mechanic"
```

---

## Task 8: HUD.uxml + HUD.uss

**Files:**
- Modify: `Assets/UI/HUD.uxml`
- Modify: `Assets/UI/HUD.uss`

- [ ] **Step 1: Add minimap-root, bag-weight-bar, and notification-label to HUD.uxml**

In `Assets/UI/HUD.uxml`, before the closing `</ui:UXML>` tag (after `</ui:VisualElement>` of skill-bar), add:

```xml
    <ui:VisualElement name="minimap-root" class="minimap-root" style="display: none;">
        <ui:VisualElement name="minimap-bg" class="minimap-bg"/>
        <ui:VisualElement name="minimap-player-dot" class="minimap-player-dot"/>
    </ui:VisualElement>
    <ui:VisualElement name="bag-weight-bar" class="bag-weight-bar" style="display: none;">
        <ui:Label name="bag-weight-label" text="0/10" class="bag-weight-label"/>
        <ui:VisualElement name="bag-weight-fill-bg" class="bag-weight-fill-bg">
            <ui:VisualElement name="bag-weight-fill" class="bag-weight-fill"/>
        </ui:VisualElement>
    </ui:VisualElement>
    <ui:Label name="notification-label" text="" class="notification-label" style="display: none;"/>
```

- [ ] **Step 2: Add CSS to HUD.uss**

Append to `Assets/UI/HUD.uss`:

```css
/* ══════════════════════════════════════════════════
   미니맵 (던전 씬 전용 — 기본 display:none)
   ══════════════════════════════════════════════════ */

.minimap-root {
    position: absolute;
    bottom: 100px;
    right: 20px;
    width: 120px;
    height: 120px;
    background-color: rgba(0, 0, 0, 0.75);
    border-radius: 6px;
    border-width: 1.5px;
    border-color: rgba(255, 255, 255, 0.18);
    overflow: hidden;
}

.minimap-bg {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    -unity-background-scale-mode: scale-to-fit;
    opacity: 0.85;
}

.minimap-player-dot {
    position: absolute;
    width: 8px;
    height: 8px;
    border-radius: 4px;
    background-color: rgb(255, 255, 255);
    /* left/top controlled by GameHUD.Update() */
}

.minimap-spawn-marker {
    position: absolute;
    width: 10px;
    height: 10px;
    /* triangular via border trick */
    border-top-left-radius: 0;
    border-top-right-radius: 0;
    border-bottom-left-radius: 5px;
    border-bottom-right-radius: 5px;
    background-color: rgb(220, 60, 60);   /* 기본: 빨강(몬스터 생존) */
}

.minimap-spawn-marker--inactive {
    background-color: rgb(120, 120, 120);   /* 회색: 리스폰 대기 중 */
}

/* ══════════════════════════════════════════════════
   가방 무게 바 (던전 씬 전용)
   ══════════════════════════════════════════════════ */

.bag-weight-bar {
    position: absolute;
    bottom: 230px;
    right: 20px;
    width: 120px;
    background-color: rgba(0, 0, 0, 0.7);
    border-radius: 6px;
    border-width: 1px;
    border-color: rgba(255, 255, 255, 0.15);
    padding: 6px 8px;
    align-items: stretch;
}

.bag-weight-label {
    font-size: 12px;
    color: rgb(200, 200, 200);
    -unity-text-align: upper-center;
    margin-bottom: 4px;
}

.bag-weight-label--full {
    color: rgb(255, 90, 90);
}

.bag-weight-fill-bg {
    height: 8px;
    background-color: rgba(0, 0, 0, 0.5);
    border-radius: 4px;
    overflow: hidden;
}

.bag-weight-fill {
    height: 100%;
    width: 0%;
    background-color: rgb(80, 200, 100);
    border-radius: 4px;
    transition-property: width;
    transition-duration: 0.15s;
}

.bag-weight-fill--full {
    background-color: rgb(220, 60, 60);
}

/* ══════════════════════════════════════════════════
   알림 토스트 (가방 가득 등)
   ══════════════════════════════════════════════════ */

.notification-label {
    position: absolute;
    top: 80px;
    left: 0;
    right: 0;
    font-size: 18px;
    -unity-font-style: bold;
    color: rgb(255, 200, 80);
    -unity-text-align: upper-center;
}
```

- [ ] **Step 3: Check HUD.uxml is valid**

Open Unity Editor. In the Inspector click on `HUD.uxml`. Verify no red errors in the UIToolkit debugger.

- [ ] **Step 4: Commit**

```bash
git add Assets/UI/HUD.uxml
git add Assets/UI/HUD.uss
git commit -m "feat: add minimap-root, bag-weight-bar, and notification-label to HUD"
```

---

## Task 9: GameHUD Minimap + Bag Bar + Toast

**Files:**
- Modify: `Assets/Scripts/UI/HUD/GameHUD.cs`

- [ ] **Step 1: Add new fields to GameHUD**

After the `m_MinimapRoot` field declaration, add:

```csharp
// ── 미니맵 요소 ───────────────────────────────────────────────
VisualElement m_MinimapBg;
VisualElement m_MinimapPlayerDot;
readonly System.Collections.Generic.List<VisualElement> m_SpawnMarkers = new();

// ── 가방 무게 바 ──────────────────────────────────────────────
VisualElement m_BagWeightBar;
Label         m_BagWeightLabel;
VisualElement m_BagWeightFill;

// ── 알림 토스트 ───────────────────────────────────────────────
Label         m_NotificationLabel;
Coroutine     m_NotificationCoroutine;

// ── 플레이어 Transform (미니맵 도트용) ───────────────────────
Transform     m_PlayerTransform;
```

- [ ] **Step 2: Cache new elements in Start()**

In `Start()`, after the `m_MinimapRoot = root.Q<VisualElement>("minimap-root");` line, add:

```csharp
m_MinimapBg        = root.Q<VisualElement>("minimap-bg");
m_MinimapPlayerDot = root.Q<VisualElement>("minimap-player-dot");
m_BagWeightBar     = root.Q<VisualElement>("bag-weight-bar");
m_BagWeightLabel   = root.Q<Label>("bag-weight-label");
m_BagWeightFill    = root.Q<VisualElement>("bag-weight-fill");
m_NotificationLabel = root.Q<Label>("notification-label");
```

- [ ] **Step 3: Add Update() to GameHUD**

Add a new `Update()` method:

```csharp
void Update()
{
    UpdateMinimapDot();
    UpdateSpawnMarkers();
}

void UpdateMinimapDot()
{
    if (m_MinimapPlayerDot == null || m_PlayerTransform == null) return;
    var ctrl = DungeonMapController.Instance;
    if (ctrl == null) return;

    Rect bounds = ctrl.WorldBounds;
    if (bounds.width <= 0 || bounds.height <= 0) return;

    Vector3 pos = m_PlayerTransform.position;
    float uvX = (pos.x - bounds.xMin) / bounds.width;
    float uvY = (pos.y - bounds.yMin) / bounds.height;

    float mapW = m_MinimapRoot?.resolvedStyle.width  ?? 120f;
    float mapH = m_MinimapRoot?.resolvedStyle.height ?? 120f;

    m_MinimapPlayerDot.style.left = uvX * mapW - 4f;
    m_MinimapPlayerDot.style.top  = (1f - uvY) * mapH - 4f;
}

void UpdateSpawnMarkers()
{
    var ctrl = DungeonMapController.Instance;
    if (ctrl == null || m_SpawnMarkers.Count != ctrl.SpawnZones.Count) return;

    for (int i = 0; i < m_SpawnMarkers.Count; i++)
    {
        var zone   = ctrl.SpawnZones[i];
        var marker = m_SpawnMarkers[i];
        if (zone == null || marker == null) continue;

        bool alive = zone.HasAliveMonsters;
        if (alive)
        {
            marker.RemoveFromClassList("minimap-spawn-marker--inactive");
        }
        else
        {
            if (!marker.ClassListContains("minimap-spawn-marker--inactive"))
                marker.AddToClassList("minimap-spawn-marker--inactive");
        }
    }
}
```

Add `using MonsterKitchen.Dungeon;` to the using block.

- [ ] **Step 4: Implement FindAndBindMinimap()**

Replace the existing `FindAndBindMinimap()` stub with:

```csharp
void FindAndBindMinimap()
{
    if (m_MinimapRoot == null) return;

    var ctrl = DungeonMapController.Instance;
    if (ctrl == null)
    {
        m_MinimapRoot.style.display = DisplayStyle.None;
        return;
    }

    // 배경 스프라이트 설정
    if (m_MinimapBg != null && ctrl.MinimapSprite != null)
        m_MinimapBg.style.backgroundImage = new StyleBackground(ctrl.MinimapSprite);

    // 스폰 존 마커 생성
    m_SpawnMarkers.Clear();
    foreach (var zone in ctrl.SpawnZones)
    {
        if (zone == null) continue;

        Rect  bounds = ctrl.WorldBounds;
        float mapW   = 120f;
        float mapH   = 120f;

        Vector3 wp  = zone.ZoneCenter;
        float uvX   = bounds.width  > 0 ? (wp.x - bounds.xMin) / bounds.width  : 0.5f;
        float uvY   = bounds.height > 0 ? (wp.y - bounds.yMin) / bounds.height : 0.5f;

        var marker = new VisualElement();
        marker.AddToClassList("minimap-spawn-marker");
        marker.style.left = uvX * mapW - 5f;
        marker.style.top  = (1f - uvY) * mapH - 5f;

        m_MinimapRoot.Add(marker);
        m_SpawnMarkers.Add(marker);
    }

    m_MinimapRoot.style.display = DisplayStyle.Flex;

    // 가방 바 활성화 + 이벤트 구독
    if (m_BagWeightBar != null && DungeonBag.Current != null)
    {
        m_BagWeightBar.style.display = DisplayStyle.Flex;
        DungeonBag.Current.OnBagChanged += UpdateBagBar;
        UpdateBagBar();
    }
}
```

- [ ] **Step 5: Implement UnbindMinimap()**

Replace the existing `UnbindMinimap()` with:

```csharp
void UnbindMinimap()
{
    if (DungeonBag.Current != null)
        DungeonBag.Current.OnBagChanged -= UpdateBagBar;

    m_SpawnMarkers.Clear();

    if (m_MinimapRoot != null)
    {
        m_MinimapRoot.Clear();
        m_MinimapRoot.style.display = DisplayStyle.None;
    }

    if (m_BagWeightBar != null)
        m_BagWeightBar.style.display = DisplayStyle.None;
}
```

- [ ] **Step 6: Add UpdateBagBar() and ShowNotification() methods**

Add these methods in a new section after `UnbindMinimap()`:

```csharp
// ================================================================
//  가방 무게 바
// ================================================================

void UpdateBagBar()
{
    var bag = DungeonBag.Current;
    if (bag == null || m_BagWeightLabel == null || m_BagWeightFill == null) return;

    m_BagWeightLabel.text = $"{bag.CurrentWeight}/{bag.MaxWeight}";

    float ratio = bag.MaxWeight > 0 ? (float)bag.CurrentWeight / bag.MaxWeight : 0f;
    m_BagWeightFill.style.width = Length.Percent(ratio * 100f);

    bool full = bag.IsFull;

    if (full)
    {
        m_BagWeightLabel.AddToClassList("bag-weight-label--full");
        m_BagWeightFill.AddToClassList("bag-weight-fill--full");
    }
    else
    {
        m_BagWeightLabel.RemoveFromClassList("bag-weight-label--full");
        m_BagWeightFill.RemoveFromClassList("bag-weight-fill--full");
    }
}

// ================================================================
//  알림 토스트
// ================================================================

/// <summary>HUD 중앙에 단기 알림 텍스트를 표시한다.</summary>
public void ShowNotification(string message, float duration = 2f)
{
    if (m_NotificationLabel == null) return;
    if (m_NotificationCoroutine != null) StopCoroutine(m_NotificationCoroutine);
    m_NotificationCoroutine = StartCoroutine(NotificationRoutine(message, duration));
}

System.Collections.IEnumerator NotificationRoutine(string message, float duration)
{
    m_NotificationLabel.text            = message;
    m_NotificationLabel.style.display   = DisplayStyle.Flex;
    yield return new WaitForSeconds(duration);
    m_NotificationLabel.style.display   = DisplayStyle.None;
    m_NotificationLabel.text            = "";
    m_NotificationCoroutine             = null;
}
```

- [ ] **Step 7: Store player Transform in FindAndBindPlayer()**

In `FindAndBindPlayer()`, after `var pc = go != null ? go.GetComponent<PlayerController>() : null;`, add:

```csharp
m_PlayerTransform = pc != null ? pc.transform : null;
```

And in `UnbindPlayer()`, add:
```csharp
m_PlayerTransform = null;
```

- [ ] **Step 8: Check compilation**

Use `read_console` MCP. Expect zero errors. All `ShowNotification` references from ItemDrop and DungeonExit should now compile.

- [ ] **Step 9: Commit**

```bash
git add Assets/Scripts/UI/HUD/GameHUD.cs
git add Assets/Scripts/Combat/ItemDrop.cs
git commit -m "feat: GameHUD minimap binding, bag weight bar, and notification toast"
```

---

## Task 10: Delete Old Files

**Files:**
- Delete: `Assets/Scripts/Dungeon/DungeonSceneController.cs`
- Delete: `Assets/Scripts/Dungeon/DungeonRoom.cs`
- Delete: `Assets/Scripts/Dungeon/DungeonDoor.cs`
- Delete: `Assets/Scripts/Enemy/MonsterRespawnManager.cs`

- [ ] **Step 1: Delete via MCP manage_script**

Use `manage_script` MCP with `action: "delete"` for each file:
1. `Scripts/Dungeon/DungeonSceneController.cs`
2. `Scripts/Dungeon/DungeonRoom.cs`
3. `Scripts/Dungeon/DungeonDoor.cs`
4. `Scripts/Enemy/MonsterRespawnManager.cs`

- [ ] **Step 2: Check compilation**

Use `read_console` MCP. Expect zero errors. If errors appear (references in DungeonSceneController were cleaned up), fix them.

Note: `DungeonSceneController` had a `new MonsterRespawnManager(this)` call in `Awake()`. This is deleted so there are no dangling references. Any remaining `using` in other files that reference deleted types must be removed.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor: delete DungeonSceneController, DungeonRoom, DungeonDoor, MonsterRespawnManager"
```

---

## Task 11: DungeonScene Rewiring

**Files:**
- Modify: `Assets/Scenes/DungeonScene.unity` (via Unity MCP tools)

- [ ] **Step 1: Read current scene state**

Use `manage_scene(action: "info")` to list current GameObjects in DungeonScene.

- [ ] **Step 2: Remove old controller**

Find the GameObject with `DungeonSceneController` component. Remove it:
```
manage_gameobject(action: "delete", name: "<DungeonController object name>")
```

- [ ] **Step 3: Create DungeonMapController GameObject**

```
manage_gameobject(action: "create", name: "DungeonMapController")
manage_components(action: "add", gameObjectName: "DungeonMapController", componentType: "MonsterKitchen.Dungeon.DungeonMapController")
```

- [ ] **Step 4: Create DungeonSpawnZone GameObjects**

Create at least one spawn zone (adjust count to match your dungeon layout):

```
manage_gameobject(action: "create", name: "SpawnZone_A")
manage_components(action: "add", gameObjectName: "SpawnZone_A", componentType: "MonsterKitchen.Dungeon.DungeonSpawnZone")
```

Set `spawnTableId` on each zone to the existing DungeonSpawnTable ID (e.g., 5001 — check TableData.asset for the actual ID).

- [ ] **Step 5: Ensure DungeonExit exists and has collider**

Find or create the `DungeonExit` GameObject:
```
manage_gameobject(action: "find", name: "DungeonExit")
```

Ensure it has:
- `DungeonExit` component
- `BoxCollider2D` with `isTrigger = true`

- [ ] **Step 6: Wire Inspector references on DungeonMapController**

Use `manage_components` to set references:
- `m_SpawnZones`: array of all SpawnZone GameObjects
- `m_Exit`: the DungeonExit GameObject
- `m_CameraConfiner`: the existing DungeonCameraConfiner (if present)
- `m_MinimapSprite`: assign a dungeon top-view sprite (or leave null for now)
- `m_WorldBounds`: set to match the dungeon map extents (e.g., x=-20, y=-20, width=40, height=40)

- [ ] **Step 7: Create DungeonBagUI GameObject**

```
manage_gameobject(action: "create", name: "DungeonBagUI")
manage_components(action: "add", gameObjectName: "DungeonBagUI", componentType: "UnityEngine.UIElements.UIDocument")
manage_components(action: "add", gameObjectName: "DungeonBagUI", componentType: "MonsterKitchen.UI.DungeonBagUI")
```

Set UIDocument `sourceAsset` to `DungeonBagPanel.uxml`. Set `sortingOrder = 20`.
Set DungeonBagUI `m_Layer = Panel`, `m_IsPopup = true`, `m_ToggleKey = B`.

- [ ] **Step 8: Save scene**

```
manage_scene(action: "save")
```

- [ ] **Step 9: Enter Play Mode and verify**

Use `manage_editor(action: "set_play_mode_state", playModeState: "Playing")`.

Verify via `read_console`:
1. `[DungeonMapController] 초기화 완료` log appears
2. Monsters spawn (no spawn errors)
3. No compilation errors

- [ ] **Step 10: Exit Play Mode**

```
manage_editor(action: "set_play_mode_state", playModeState: "Stopped")
```

- [ ] **Step 11: Commit**

```bash
git add Assets/Scenes/DungeonScene.unity
git commit -m "feat: rewire DungeonScene — DungeonMapController + SpawnZones + DungeonBagUI"
```

---

## Self-Review

**Spec coverage check:**

| Spec Requirement | Task |
|---|---|
| DungeonMapController (SceneControllerBase 상속) | Task 4 |
| DungeonSpawnZone (respawnDelay, Health.OnDeath) | Task 3 |
| DungeonBag (TryAdd / Remove / FlushToInventory) | Task 2 |
| DungeonExit 항상 활성 | Task 4 (DungeonMapController.InitAsync activates it) |
| DungeonExit 가방 미달 확인 팝업 | Task 5 |
| DungeonBagUI B키 토글 + 버리기 | Task 7 |
| IngredientData.Weight | Task 1 |
| PlayerUpgradeData.BagCapacity | Task 1 |
| ItemDrop → DungeonBag.TryAdd | Task 6 |
| 가방 가득 알림 (2초) | Task 9 (ShowNotification) + Task 6 |
| 미니맵 플레이어 도트 | Task 9 (UpdateMinimapDot) |
| 미니맵 스폰 존 마커 (빨강/회색) | Task 9 (UpdateSpawnMarkers) |
| HUD bag-weight-bar | Task 8 + 9 |
| 삭제: DungeonSceneController · DungeonRoom · DungeonDoor · MonsterRespawnManager | Task 10 |
| DungeonScene 재배선 | Task 11 |

**Type consistency check:**
- `DungeonBag.TryAdd(uint, int, IngredientQuality, int)` — used in ItemDrop Task 6 ✓
- `DungeonBag.Current` static — used in DungeonExit Task 5, ItemDrop Task 6, DungeonBagUI Task 7, GameHUD Task 9 ✓
- `DungeonBag.ClearCurrent()` — called in DungeonMapController.OnDestroy Task 4 ✓
- `DungeonMapController.Instance` — used in GameHUD Task 9 ✓
- `DungeonMapController.WorldBounds` (Rect) — used in GameHUD Task 9 ✓
- `DungeonMapController.SpawnZones` (IReadOnlyList<DungeonSpawnZone>) — used in GameHUD Task 9 ✓
- `DungeonSpawnZone.HasAliveMonsters` — used in GameHUD Task 9 ✓
- `DungeonSpawnZone.ZoneCenter` — used in GameHUD Task 9 ✓
- `GameHUD.ShowNotification(string, float)` — used in ItemDrop Task 6 ✓
- `PlayerUpgradeData.BagCurrentCapacity` — used in DungeonMapController.InitAsync Task 4 ✓
- `IngredientData.Weight` — used in ItemDrop Task 6 ✓
- `DungeonBag.OnBagChanged` (event Action) — subscribed in DungeonBagUI Task 7, GameHUD Task 9 ✓
