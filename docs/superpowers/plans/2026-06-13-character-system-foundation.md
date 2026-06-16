# 캐릭터 시스템 기반 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 다중 캐릭터 보유 데이터 토대 — 보유/저장/조작 캐릭터 전환/클래스 제한 무기 장착 검증.

**Architecture:** 기존 4-레이어 패턴 연장. `PlayerCharData` CSV 컬럼 추가(클래스·태생 성급·작업 스탯), `PlayerRosterData` 신설(PlayerDataManager 서브 데이터), `ServerSaveData` v3(`CharacterEntry` 리스트 + v2 마이그레이션), `NetworkManager` Request 3종(Server Stub 패턴).

**Tech Stack:** Unity 6 (6000.4.2f1), 순수 C# 데이터 클래스, NUnit (EditMode/PlayMode), CSV→SO 파이프라인 (DataManagerWindow).

**스펙:** `docs/superpowers/specs/2026-06-13-character-system-foundation-design.md`

**커밋 규칙:** 이 프로젝트는 사용자 명시 요청 시에만 커밋한다 (CLAUDE.md 작업 규칙 8). Task별 커밋 단계 없음 — 모듈 완료 후 사용자에게 커밋 여부 확인.

**테스트 실행:** MCP `run_tests` (EditMode/PlayMode). CLI 폴백:
```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit
```

**기준 카운트:** EditMode 99 PASS / PlayMode 5 PASS + 1 Skip (2026-06-12 기준).

---

### Task 1: PlayerCharData 확장 + Players.csv 시드 + CSV 무결성 테스트

**Files:**
- Modify: `Assets/Scripts/Data/Table/PlayerCharTable.cs`
- Modify: `Assets/Data/CSV/Players.csv`
- Modify: `Assets/Editor/DataPipeline/DataManagerWindow.cs` (HEADERS 배열, `// 11: Players` 줄)
- Test: `Assets/Tests/EditMode/PlayerCharsCsvTests.cs` (신규)

- [ ] **Step 1: CSV 무결성 테스트 작성 (RED)**

`Assets/Tests/EditMode/PlayerCharsCsvTests.cs` 신규:

```csharp
// ====================================================================
//  PlayerCharsCsvTests — Players.csv (캐릭터 시스템 기반 컬럼) 무결성
// ====================================================================

using System.IO;
using System.Linq;
using NUnit.Framework;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class PlayerCharsCsvTests
    {
        const string CsvPath = "Assets/Data/CSV/Players.csv";

        static string[] Header() =>
            File.ReadLines(CsvPath)
                .First(l => !string.IsNullOrWhiteSpace(l)
                         && !l.StartsWith(";") && !l.StartsWith("#TYPE"))
                .Split(',');

        static string[][] DataRows() =>
            File.ReadLines(CsvPath)
                .Where(l => !string.IsNullOrWhiteSpace(l)
                         && !l.StartsWith(";") && !l.StartsWith("#TYPE"))
                .Skip(1)
                .Select(l => l.Split(','))
                .ToArray();

        static int Col(string name)
        {
            int idx = System.Array.IndexOf(Header(), name);
            Assert.GreaterOrEqual(idx, 0, $"컬럼 없음: {name}");
            return idx;
        }

        [Test]
        public void NatalStars_InRange1To3()
        {
            int col = Col("NatalStars");
            foreach (var row in DataRows())
            {
                int stars = int.Parse(row[col]);
                Assert.That(stars, Is.InRange(1, 3), $"{row[0]}: NatalStars={stars}");
            }
        }

        [Test]
        public void CharClass_IsValidWeaponType()
        {
            int col = Col("CharClass");
            foreach (var row in DataRows())
                Assert.IsTrue(System.Enum.TryParse<WeaponType>(row[col], out _),
                    $"{row[0]}: CharClass='{row[col]}' 무효");
        }

        [Test]
        public void WorkSpeeds_ArePositive()
        {
            int cook  = Col("CookSpeed");
            int serve = Col("ServeSpeed");
            foreach (var row in DataRows())
            {
                Assert.Greater(float.Parse(row[cook],
                    System.Globalization.CultureInfo.InvariantCulture), 0f, $"{row[0]} CookSpeed");
                Assert.Greater(float.Parse(row[serve],
                    System.Globalization.CultureInfo.InvariantCulture), 0f, $"{row[0]} ServeSpeed");
            }
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

MCP `run_tests` (mode=EditMode, filter=PlayerCharsCsvTests).
Expected: 3 FAIL — "컬럼 없음: NatalStars" 등 (컬럼 미존재).

- [ ] **Step 3: PlayerCharData 필드 추가**

`Assets/Scripts/Data/Table/PlayerCharTable.cs` — `GaugeOnKill` 필드 뒤에 추가:

```csharp
        [Header("클래스/희귀도")]
        [Tooltip("클래스 = 장착 가능 무기 타입 (1:1). 이 타입 무기만 장착 가능.")]
        public WeaponType CharClass;

        [Tooltip("태생 성급 1~3. 뽑기 풀 배정·획득 시 초기 성급.")]
        [Range(1, 3)]
        public int NatalStars = 1;

        [Header("작업 스탯 — 근무지 배치 모듈(후속)에서 적용")]
        [Tooltip("주방 배치 시 요리 속도 배율 기준 (1.0 = 표준).")]
        public float CookSpeed = 1f;

        [Tooltip("식당 배치 시 서빙/보조 속도 배율 기준 (1.0 = 표준).")]
        public float ServeSpeed = 1f;
```

- [ ] **Step 4: Players.csv 컬럼 + 시드 캐릭터 추가**

`Assets/Data/CSV/Players.csv` 전체 교체:

```csv
; 플레이어 캐릭터 데이터
; id: uint (PLR_001 = 9001)
; prefabAddress: 프리팹 AssetManifest 키 (빈칸이면 prefab/player/{id} 자동 생성)
;   → AssetManifest 에 "prefab/player/9001" 키로 PlayerController 프리팹을 등록해야 함
; btAssetAddress: BehaviorTree 에셋 AssetManifest 키 (빈칸이면 Inspector 폴백 또는 BT 미사용)
;   → AssetManifest 에 "bt/player/9001" 키로 BTAsset SO 를 등록해야 함
; defaultWeaponId: 시작 무기 uint ID (0이면 무기 없이 시작)
; skillGroupId1/2: 슬롯 1·2 SkillGroupData.skillGroupId 문자열 (빈칸이면 없음)
; ultimateSkillGroupId: 궁극기 SkillGroupData.skillGroupId 문자열
; maxUltimateGauge: 궁극기 게이지 최대치
; gaugeOnHit/Kill: 피격/처치 시 게이지 증가량
; charClass: 클래스 = 장착 가능 WeaponType (1:1)
; natalStars: 태생 성급 1~3
; cookSpeed/serveSpeed: 작업 스탯 배율 기준 (1.0 = 표준) — 근무지 배치 모듈에서 적용
; PLR_002/003: 테스트 캐릭터 — 전용 아트 미존재, 9001 프리팹/BT 재사용 (Bow/Staff 무기는 무기 모듈에서 추가)
#TYPE,string,uint,string,string,string,int,int,float,int,AttributeType,uint,string,string,string,float,float,float,WeaponType,int,float,float
_key,Id,DisplayName,PrefabAddress,BtAssetAddress,BaseMaxHp,BaseAttack,BaseMoveSpeed,BaseDefense,AttackAttribute,DefaultWeaponId,SkillGroupId1,SkillGroupId2,UltimateSkillGroupId,MaxUltimateGauge,GaugeOnHit,GaugeOnKill,CharClass,NatalStars,CookSpeed,ServeSpeed
PLR_001,9001,기본 플레이어,,bt/player/9001,100,10,5.0,0,None,7001,SGD_010,,SGD_020,100,15,25,Sword,1,1.0,1.0
PLR_002,9002,견습 궁수,prefab/player/9001,bt/player/9001,80,12,5.5,0,None,0,SGD_010,,SGD_020,100,15,25,Bow,2,1.2,0.8
PLR_003,9003,견습 마법사,prefab/player/9001,bt/player/9001,70,14,4.5,0,None,0,SGD_010,,SGD_020,100,15,25,Staff,3,0.8,1.2
```

주의: 9002/9003 은 `PrefabAddress` 를 명시한다 — 비우면 `PlayerMapper` 가 `prefab/player/9002` 를 자동 생성하는데 AssetManifest 에 없는 키라 로드가 실패한다.

- [ ] **Step 5: DataManagerWindow HEADERS 갱신**

`Assets/Editor/DataPipeline/DataManagerWindow.cs` — `// 11: Players` 의 배열을 교체
(기존 누락이던 `BtAssetAddress` 포함 + 신규 4컬럼):

```csharp
            // 11: Players
            new[] { "_key","Id","DisplayName","PrefabAddress","BtAssetAddress","BaseMaxHp","BaseAttack","BaseMoveSpeed","BaseDefense","AttackAttribute","DefaultWeaponId","SkillGroupId1","SkillGroupId2","UltimateSkillGroupId","MaxUltimateGauge","GaugeOnHit","GaugeOnKill","CharClass","NatalStars","CookSpeed","ServeSpeed" },
```

- [ ] **Step 6: 컴파일 확인 + SO 동기화**

1. MCP `read_console` — 컴파일 에러 0 확인 (`isCompiling: false` 대기)
2. MCP `execute_menu_item`: `MonsterKitchen/Sync All SO`
3. `read_console` — Players 3건 동기화 로그 확인, 에러 0

- [ ] **Step 7: 테스트 실행 — 통과 확인 (GREEN)**

MCP `run_tests` (mode=EditMode, filter=PlayerCharsCsvTests).
Expected: 3 PASS.

---

### Task 2: PlayerRosterData + CharacterState (TDD)

**Files:**
- Create: `Assets/Scripts/Data/Player/PlayerRosterData.cs`
- Test: `Assets/Tests/EditMode/PlayerRosterTests.cs` (신규)

- [ ] **Step 1: 테스트 작성 (RED)**

`Assets/Tests/EditMode/PlayerRosterTests.cs` 신규:

```csharp
// ====================================================================
//  PlayerRosterTests — 보유 캐릭터 로스터 (획득/중복/장착/라운드트립)
// ====================================================================

using System.Linq;
using NUnit.Framework;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class PlayerRosterTests
    {
        PlayerRosterData m_Roster;

        [SetUp]
        public void SetUp()
        {
            m_Roster = new PlayerRosterData();
            m_Roster.Init();
        }

        [Test]
        public void AddCharacter_SetsInitialState()
        {
            m_Roster.AddCharacter(9001u, 2, 7001u);

            Assert.IsTrue(m_Roster.Owns(9001u));
            var st = m_Roster.Get(9001u);
            Assert.AreEqual(1,     st.Level);
            Assert.AreEqual(2,     st.Stars);
            Assert.AreEqual(7001u, st.EquippedWeaponId);
        }

        [Test]
        public void AddCharacter_Duplicate_IsIgnored()
        {
            m_Roster.AddCharacter(9001u, 1, 7001u);
            m_Roster.Get(9001u).Level = 5;

            m_Roster.AddCharacter(9001u, 3, 7002u);   // 중복 — 무시

            var st = m_Roster.Get(9001u);
            Assert.AreEqual(5,     st.Level);
            Assert.AreEqual(1,     st.Stars);
            Assert.AreEqual(7001u, st.EquippedWeaponId);
        }

        [Test]
        public void Get_Unowned_ReturnsNull()
        {
            Assert.IsNull(m_Roster.Get(9999u));
        }

        [Test]
        public void SetEquippedWeapon_OwnedCharacter_Updates()
        {
            m_Roster.AddCharacter(9001u, 1, 7001u);

            bool changed = false;
            m_Roster.OnCharacterChanged += _ => changed = true;
            m_Roster.SetEquippedWeapon(9001u, 7002u);

            Assert.AreEqual(7002u, m_Roster.Get(9001u).EquippedWeaponId);
            Assert.IsTrue(changed);
        }

        [Test]
        public void SetEquippedWeapon_UnownedCharacter_IsIgnored()
        {
            m_Roster.SetEquippedWeapon(9999u, 7001u);
            Assert.IsFalse(m_Roster.Owns(9999u));
        }

        [Test]
        public void OnCharacterAdded_Fires()
        {
            uint fired = 0;
            m_Roster.OnCharacterAdded += id => fired = id;
            m_Roster.AddCharacter(9001u, 1, 0u);
            Assert.AreEqual(9001u, fired);
        }

        [Test]
        public void SaveLoad_RoundTrip_Preserves()
        {
            m_Roster.AddCharacter(9001u, 1, 7001u);
            m_Roster.AddCharacter(9002u, 3, 0u);
            m_Roster.Get(9002u).Level = 7;

            var entries = m_Roster.AllEntries().ToList();

            var restored = new PlayerRosterData();
            restored.Init();
            restored.LoadCharacters(entries);

            Assert.AreEqual(2,     restored.AllCharacters.Count);
            Assert.AreEqual(7,     restored.Get(9002u).Level);
            Assert.AreEqual(3,     restored.Get(9002u).Stars);
            Assert.AreEqual(7001u, restored.Get(9001u).EquippedWeaponId);
        }

        [Test]
        public void LoadCharacters_ClampsInvalidValues()
        {
            m_Roster.LoadCharacters(new[] { (9001u, 0, 99, 0u) });

            var st = m_Roster.Get(9001u);
            Assert.AreEqual(1,                        st.Level);
            Assert.AreEqual(PlayerRosterData.MaxStars, st.Stars);
        }
    }
}
```

- [ ] **Step 2: 컴파일 실패 확인 (RED)**

MCP `read_console` — `PlayerRosterData` 미정의 컴파일 에러 확인 (CS0246).

- [ ] **Step 3: PlayerRosterData 구현**

`Assets/Scripts/Data/Player/PlayerRosterData.cs` 신규:

```csharp
using System;
using System.Collections.Generic;
using MonsterKitchen.Core;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  PlayerRosterData — 보유 캐릭터 로스터 상태 관리
    //
    //  ▶ 역할
    //    보유 캐릭터(charId → CharacterState) 보관 + 이벤트 발행.
    //    PlayerDataManager 가 생성하고 Init() 을 호출한다.
    //
    //  ▶ 접근 방법 (PlayerDataManager 경유만 허용)
    //    PlayerDataManager.Instance.Roster.AllCharacters
    //    PlayerDataManager.Instance.Roster.OnCharacterAdded
    //
    //  ▶ 변경 방법 (NetworkManager 경유만 허용)
    //    NetworkManager.Instance.RequestAddCharacter(id)
    //    NetworkManager.Instance.RequestEquipWeapon(charId, weaponId)
    //    → 콜백 → PlayerDataManager.Apply*() → 이 클래스의 내부 메서드 호출
    //
    //  ▶ 성장 미적용 (캐릭터 시스템 기반 스코프)
    //    Level/Stars 는 저장만 한다. 스탯 공식·승급 실행은 성장 모듈에서.
    // ====================================================================

    /// <summary>보유 캐릭터 1체의 런타임 상태.</summary>
    public class CharacterState
    {
        public uint CharId;
        public int  Level = 1;
        public int  Stars;             // 태생값으로 시작, 승급 +1, 최대 MaxStars
        public uint EquippedWeaponId;  // 획득 시 DefaultWeaponId 로 초기화. 0 = 무기 없음
    }

    public class PlayerRosterData
    {
        /// <summary>승급 포함 성급 상한.</summary>
        public const int MaxStars = 10;

        readonly Dictionary<uint, CharacterState> m_Characters = new();

        /// <summary>캐릭터 획득 시 발행. (charId)</summary>
        public event Action<uint> OnCharacterAdded;

        /// <summary>레벨/성급/무기 변경 시 발행. (charId)</summary>
        public event Action<uint> OnCharacterChanged;

        // ── 생명주기 ─────────────────────────────────────────────────

        public void Init() { }

        // ── 읽기 전용 공개 API ────────────────────────────────────────

        public IReadOnlyDictionary<uint, CharacterState> AllCharacters => m_Characters;

        public bool Owns(uint charId) => m_Characters.ContainsKey(charId);

        /// <summary>보유 캐릭터 상태. 미보유 시 null.</summary>
        public CharacterState Get(uint charId) =>
            m_Characters.TryGetValue(charId, out var st) ? st : null;

        // ── Apply 메서드 — PlayerDataManager 만 호출 ─────────────────

        /// <summary>캐릭터를 획득한다. 이미 보유 시 무시(로그).</summary>
        public void AddCharacter(uint charId, int natalStars, uint defaultWeaponId)
        {
            if (m_Characters.ContainsKey(charId))
            {
                DebugUtil.Log($"[Roster] 이미 보유한 캐릭터 id:{charId} — 무시");
                return;
            }

            m_Characters[charId] = new CharacterState
            {
                CharId           = charId,
                Level            = 1,
                Stars            = Math.Clamp(natalStars, 1, MaxStars),
                EquippedWeaponId = defaultWeaponId,
            };
            OnCharacterAdded?.Invoke(charId);
            DebugUtil.Log($"[Roster] 캐릭터 획득 id:{charId} (★{m_Characters[charId].Stars})");
        }

        /// <summary>무기를 장착한다. 미보유 캐릭터면 무시. 클래스 검증은 NetworkManager 책임.</summary>
        public void SetEquippedWeapon(uint charId, uint weaponId)
        {
            if (!m_Characters.TryGetValue(charId, out var st))
            {
                DebugUtil.LogWarning($"[Roster] 미보유 캐릭터 무기 장착 시도 id:{charId} — 무시");
                return;
            }
            st.EquippedWeaponId = weaponId;
            OnCharacterChanged?.Invoke(charId);
        }

        // ── 세이브/로드 — ServerDBManager 전용 ──────────────────────

        /// <summary>저장 데이터 복원. 기존 데이터를 덮어쓴다. 범위 밖 값은 클램프.</summary>
        public void LoadCharacters(IEnumerable<(uint id, int level, int stars, uint weaponId)> entries)
        {
            m_Characters.Clear();
            foreach (var (id, level, stars, weaponId) in entries)
            {
                m_Characters[id] = new CharacterState
                {
                    CharId           = id,
                    Level            = Math.Max(1, level),
                    Stars            = Math.Clamp(stars, 1, MaxStars),
                    EquippedWeaponId = weaponId,
                };
            }
        }

        /// <summary>저장용 열거. ServerDBManager 전용.</summary>
        public IEnumerable<(uint id, int level, int stars, uint weaponId)> AllEntries()
        {
            foreach (var kv in m_Characters)
                yield return (kv.Key, kv.Value.Level, kv.Value.Stars, kv.Value.EquippedWeaponId);
        }
    }
}
```

- [ ] **Step 4: 테스트 실행 — 통과 확인 (GREEN)**

MCP `run_tests` (mode=EditMode, filter=PlayerRosterTests).
Expected: 8 PASS.

---

### Task 3: ServerSaveData v3 + PlayerDataManager 통합 + ServerDBManager

**Files:**
- Modify: `Assets/Scripts/Data/Server/ServerSaveData.cs`
- Modify: `Assets/Scripts/Core/Managers/PlayerDataManager.cs`
- Modify: `Assets/Scripts/Core/Managers/ServerDBManager.cs`
- Test: `Assets/Tests/EditMode/PlayerDataManagerRosterTests.cs` (신규)

- [ ] **Step 1: 테스트 작성 (RED)**

`Assets/Tests/EditMode/PlayerDataManagerRosterTests.cs` 신규:

```csharp
// ====================================================================
//  PlayerDataManagerRosterTests — 로스터 로드 / v2 마이그레이션 / 선택 폴백
//
//  ▶ EditMode 전제: DataRegistry 미초기화 → EnsureDefaultCharacter 는
//    폴백 경로(Stars=1, Weapon=0)로 동작한다. 실데이터 경로는 PlayMode 검증.
// ====================================================================

using NUnit.Framework;
using MonsterKitchen.Core;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class PlayerDataManagerRosterTests
    {
        PlayerDataManager m_Pm;

        [SetUp]
        public void SetUp()
        {
            m_Pm = new PlayerDataManager();
            m_Pm.Init();
        }

        [Test]
        public void LoadFrom_V2Save_NoCharacters_EnsuresDefault()
        {
            var save = new ServerSaveData { Version = 2, SelectedCharId = 9001 };

            m_Pm.LoadFrom(save);

            Assert.AreEqual(1, m_Pm.Roster.AllCharacters.Count);
            Assert.IsTrue(m_Pm.Roster.Owns(9001u));
            Assert.AreEqual(1, m_Pm.Roster.Get(9001u).Level);
        }

        [Test]
        public void LoadFrom_V3Save_RestoresCharacters()
        {
            var save = new ServerSaveData
            {
                Version        = 3,
                SelectedCharId = 9002,
                Characters     =
                {
                    new CharacterEntry { Id = 9001, Level = 3, Stars = 2, EquippedWeaponId = 7001 },
                    new CharacterEntry { Id = 9002, Level = 1, Stars = 3, EquippedWeaponId = 0 },
                },
            };

            m_Pm.LoadFrom(save);

            Assert.AreEqual(2,     m_Pm.Roster.AllCharacters.Count);
            Assert.AreEqual(3,     m_Pm.Roster.Get(9001u).Level);
            Assert.AreEqual(7001u, m_Pm.Roster.Get(9001u).EquippedWeaponId);
            Assert.AreEqual(9002u, m_Pm.SelectedCharId);
        }

        [Test]
        public void LoadFrom_SelectedCharNotOwned_FallsBackToFirstOwned()
        {
            var save = new ServerSaveData
            {
                Version        = 3,
                SelectedCharId = 9999,
                Characters     =
                {
                    new CharacterEntry { Id = 9001, Level = 1, Stars = 1, EquippedWeaponId = 7001 },
                },
            };

            m_Pm.LoadFrom(save);

            Assert.AreEqual(9001u, m_Pm.SelectedCharId);
        }
    }
}
```

- [ ] **Step 2: 컴파일 실패 확인 (RED)**

MCP `read_console` — `Roster` / `CharacterEntry` 미정의 에러 확인.

- [ ] **Step 3: ServerSaveData v3 스키마**

`Assets/Scripts/Data/Server/ServerSaveData.cs`:

`Version` 필드와 주석 갱신 + `Characters` 리스트 추가:

```csharp
        public int    Version       = 3;   // v3: Characters(보유 캐릭터) 추가
```

`RecipeCookCounts` 필드 아래에 추가:

```csharp
        // ── 보유 캐릭터 (v3 추가 — v2 이하 세이브는 빈 리스트 → 기본 캐릭터 자동 등록) ──
        public List<CharacterEntry> Characters = new();
```

파일 하단 `CookCountEntry` 클래스 뒤에 추가:

```csharp
    /// <summary>보유 캐릭터 1체. (Id 당 1엔트리)</summary>
    [Serializable]
    public class CharacterEntry
    {
        public uint Id;
        public int  Level;
        public int  Stars;
        public uint EquippedWeaponId;
    }
```

- [ ] **Step 4: PlayerDataManager 통합**

`Assets/Scripts/Core/Managers/PlayerDataManager.cs`:

서브 데이터 프로퍼티 블록에 추가:

```csharp
        public PlayerRosterData         Roster    { get; private set; }
```

`Init()` 에 생성/초기화 추가 (`DailyMenu = new DailyMenuData();` 뒤, `Coin.Init();` 그룹에):

```csharp
            Roster    = new PlayerRosterData();
```
```csharp
            Roster.Init();
```

Apply 블록에 추가 (`ApplyUpgradeLevel` 뒤):

```csharp
        /// <summary>캐릭터를 로스터에 추가한다.</summary>
        public void ApplyAddCharacter(uint charId, int natalStars, uint defaultWeaponId) =>
            Roster.AddCharacter(charId, natalStars, defaultWeaponId);

        /// <summary>조작 캐릭터를 변경한다. 보유 검증은 NetworkManager 책임.</summary>
        public void ApplySelectCharacter(uint charId) => SelectedCharId = charId;

        /// <summary>캐릭터 장착 무기를 변경한다. 클래스 검증은 NetworkManager 책임.</summary>
        public void ApplyEquipWeapon(uint charId, uint weaponId) =>
            Roster.SetEquippedWeapon(charId, weaponId);
```

`LoadFrom()` — `Fame.Load(save.TotalFame);` 뒤에 추가:

```csharp
            Roster.LoadCharacters(CharEntries(save.Characters));
            EnsureDefaultCharacter();
```

`LoadFrom()` 뒤에 신규 메서드:

```csharp
        /// <summary>
        /// 로스터 빈 상태(새 게임·v2 이하 세이브) → 기본 캐릭터 자동 등록.
        /// SelectedCharId 미보유 → 첫 보유 캐릭터로 폴백.
        /// ServerDBManager.Load() 의 새 게임 경로에서도 호출된다.
        /// </summary>
        public void EnsureDefaultCharacter()
        {
            if (Roster.AllCharacters.Count == 0)
            {
                var charData = DataRegistry.Instance?.PlayerChars?.Get(SelectedCharId);
                int  stars   = charData?.NatalStars      ?? 1;
                uint weapon  = charData?.DefaultWeaponId ?? 0u;
                Roster.AddCharacter(SelectedCharId, stars, weapon);
                DebugUtil.Log($"[PlayerDataManager] 기본 캐릭터 자동 등록 id:{SelectedCharId} (마이그레이션/새 게임)");
            }

            if (!Roster.Owns(SelectedCharId))
            {
                foreach (var kv in Roster.AllCharacters) { SelectedCharId = kv.Key; break; }
                DebugUtil.LogWarning($"[PlayerDataManager] SelectedCharId 미보유 → id:{SelectedCharId} 폴백");
            }
        }
```

내부 헬퍼 블록에 추가:

```csharp
        static IEnumerable<(uint id, int level, int stars, uint weaponId)> CharEntries(
            List<CharacterEntry> list)
        {
            if (list == null) yield break;
            foreach (var e in list) yield return (e.Id, e.Level, e.Stars, e.EquippedWeaponId);
        }
```

- [ ] **Step 5: ServerDBManager 저장/로드**

`Assets/Scripts/Core/Managers/ServerDBManager.cs`:

버전 상수 교체:

```csharp
        const int    CurrentVersion = 3;   // v3: Characters(보유 캐릭터) 추가
```

`Load()` — 새 게임/실패 경로 모두 기본 캐릭터 보장:

```csharp
            if (!File.Exists(SavePath))
            {
                DebugUtil.Log("[ServerDB] 저장 파일 없음 — 새 게임 시작.");
                PlayerDataManager.Instance?.EnsureDefaultCharacter();
                return;
            }
```

catch 블록에도 추가:

```csharp
            catch (Exception e)
            {
                DebugUtil.LogError(StringUtil.Format("[ServerDB] 로드 실패 — 새 게임으로 진행합니다. 오류: {0}", e.Message));
                PlayerDataManager.Instance?.EnsureDefaultCharacter();
            }
```

`BuildSaveData()` — `save.TotalFame = ...` 줄 앞에 추가:

```csharp
            var roster = pm?.Roster;
            if (roster != null)
            {
                foreach (var (id, level, stars, weaponId) in roster.AllEntries())
                    save.Characters.Add(new CharacterEntry
                        { Id = id, Level = level, Stars = stars, EquippedWeaponId = weaponId });
            }
```

- [ ] **Step 6: 테스트 실행 — 통과 확인 (GREEN)**

MCP `run_tests` (mode=EditMode, filter=PlayerDataManagerRosterTests).
Expected: 3 PASS. (PlayerRosterTests 8 PASS 유지 확인 — filter=PlayerRoster 로 묶어 11 PASS.)

---

### Task 4: NetworkManager Request 3종

**Files:**
- Modify: `Assets/Scripts/Core/Managers/NetworkManager.cs`
- Test: `Assets/Tests/EditMode/NetworkRosterTests.cs` (신규)

- [ ] **Step 1: 테스트 작성 (RED)**

`Assets/Tests/EditMode/NetworkRosterTests.cs` 신규:

```csharp
// ====================================================================
//  NetworkRosterTests — 캐릭터 Request 검증 (EditMode 범위)
//
//  ▶ EditMode 전제: DataRegistry 미초기화 가능 → 테이블 의존 검증
//    (RequestAddCharacter 성공·RequestEquipWeapon 클래스 체크)은 PlayMode 에서.
//    여기서는 보유 검증·미정의 ID 거부만 다룬다.
// ====================================================================

using NUnit.Framework;
using MonsterKitchen.Core;

namespace MonsterKitchen.Tests
{
    public class NetworkRosterTests
    {
        PlayerDataManager m_Pm;
        NetworkManager    m_Net;

        [SetUp]
        public void SetUp()
        {
            m_Pm  = new PlayerDataManager();
            m_Pm.Init();
            m_Net = new NetworkManager();
            m_Net.Init();
        }

        [Test]
        public void RequestSelectCharacter_Unowned_Fails()
        {
            bool? result = null;
            m_Net.RequestSelectCharacter(9001u, ok => result = ok);
            Assert.IsFalse(result);
        }

        [Test]
        public void RequestSelectCharacter_Owned_Succeeds()
        {
            m_Pm.Roster.AddCharacter(9002u, 1, 0u);   // 테스트 시드 — 직접 주입

            bool? result = null;
            m_Net.RequestSelectCharacter(9002u, ok => result = ok);

            Assert.IsTrue(result);
            Assert.AreEqual(9002u, m_Pm.SelectedCharId);
        }

        [Test]
        public void RequestAddCharacter_UndefinedId_Fails()
        {
            // 65000: 어떤 테이블에도 정의되지 않은 ID — DataRegistry 유무와 무관하게 거부
            bool? result = null;
            m_Net.RequestAddCharacter(65000u, ok => result = ok);
            Assert.IsFalse(result);
        }

        [Test]
        public void RequestEquipWeapon_UnownedCharacter_Fails()
        {
            bool? result = null;
            m_Net.RequestEquipWeapon(65000u, 7001u, ok => result = ok);
            Assert.IsFalse(result);
        }
    }
}
```

- [ ] **Step 2: 컴파일 실패 확인 (RED)**

MCP `read_console` — `RequestSelectCharacter` 미정의 에러 확인.

- [ ] **Step 3: NetworkManager Request 구현**

`Assets/Scripts/Core/Managers/NetworkManager.cs` — `RequestUpgrade` 메서드 뒤에 추가:

```csharp
        // ================================================================
        //  캐릭터 로스터
        // ================================================================

        /// <summary>
        /// 캐릭터 획득을 서버에 요청한다.
        /// 미정의 캐릭터·이미 보유 시 onResult(false).
        /// (중복 뽑기 → 승급 재료 변환은 뽑기 모듈에서 이 경로 위에 구현)
        /// </summary>
        public void RequestAddCharacter(uint charId, Action<bool> onResult = null)
        {
            var roster = PlayerDataManager.Instance.Roster;

            // ── [SERVER STUB START] ──────────────────────────────────
            var charData = DataRegistry.Instance?.PlayerChars?.Get(charId);
            if (charData == null || roster.Owns(charId))
            {
                DebugUtil.Log(StringUtil.Format("[Network] AddCharacter 실패 — id:{0} (미정의 또는 보유 중)", charId));
                onResult?.Invoke(false);
                return;
            }
            // ── [SERVER STUB END] ────────────────────────────────────

            PlayerDataManager.Instance.ApplyAddCharacter(charId, charData.NatalStars, charData.DefaultWeaponId);
            DebugUtil.Log(StringUtil.Format("[Network] AddCharacter id:{0} ★{1}", charId, charData.NatalStars));
            GlobalController.Instance?.SaveSched.MarkDirty();
            onResult?.Invoke(true);
        }

        /// <summary>조작 캐릭터 변경을 서버에 요청한다. 미보유 캐릭터면 onResult(false).</summary>
        public void RequestSelectCharacter(uint charId, Action<bool> onResult = null)
        {
            // ── [SERVER STUB START] ──────────────────────────────────
            if (!PlayerDataManager.Instance.Roster.Owns(charId))
            {
                DebugUtil.Log(StringUtil.Format("[Network] SelectCharacter 실패 — 미보유 id:{0}", charId));
                onResult?.Invoke(false);
                return;
            }
            // ── [SERVER STUB END] ────────────────────────────────────

            PlayerDataManager.Instance.ApplySelectCharacter(charId);
            DebugUtil.Log(StringUtil.Format("[Network] SelectCharacter id:{0}", charId));
            GlobalController.Instance?.SaveSched.MarkDirty();
            onResult?.Invoke(true);
        }

        /// <summary>
        /// 캐릭터 무기 장착을 서버에 요청한다.
        /// 미보유·미정의·클래스 불일치(weapon.WeaponType != CharClass) 시 onResult(false).
        /// </summary>
        public void RequestEquipWeapon(uint charId, uint weaponId, Action<bool> onResult = null)
        {
            var roster = PlayerDataManager.Instance.Roster;

            // ── [SERVER STUB START] ──────────────────────────────────
            var charData = DataRegistry.Instance?.PlayerChars?.Get(charId);
            var weapon   = DataRegistry.Instance?.Weapons?.Get(weaponId);
            if (charData == null || weapon == null || !roster.Owns(charId)
                || weapon.WeaponType != charData.CharClass)
            {
                DebugUtil.Log(StringUtil.Format("[Network] EquipWeapon 실패 — char:{0} weapon:{1} (미보유/미정의/클래스 불일치)", charId, weaponId));
                onResult?.Invoke(false);
                return;
            }
            // ── [SERVER STUB END] ────────────────────────────────────

            PlayerDataManager.Instance.ApplyEquipWeapon(charId, weaponId);
            DebugUtil.Log(StringUtil.Format("[Network] EquipWeapon char:{0} weapon:{1}", charId, weaponId));
            GlobalController.Instance?.SaveSched.MarkDirty();
            onResult?.Invoke(true);
        }
```

- [ ] **Step 4: 테스트 실행 — 통과 확인 (GREEN)**

MCP `run_tests` (mode=EditMode, filter=NetworkRosterTests).
Expected: 4 PASS.

---

### Task 5: PlayerStats/PlayerManager 로스터 연동 + PlayMode 테스트

**Files:**
- Modify: `Assets/Scripts/Player/PlayerStats.cs` (Init 의 기본 무기 장착 블록)
- Modify: `Assets/Scripts/Core/Managers/PlayerManager.cs` (Start 의 캐릭터 ID 결정)
- Test: `Assets/Tests/PlayMode/CharacterRosterPlayTests.cs` (신규)

- [ ] **Step 1: PlayerStats — 로스터 장착 무기 우선**

`Assets/Scripts/Player/PlayerStats.cs` `Init()` — 기존 블록:

```csharp
            // 기본 장착 — 무기·스킬 모두 DataRegistry 를 통해 ID/문자열로 조회
            if (data.DefaultWeaponId != 0)
            {
                var weapon = DataRegistry.Instance?.Weapons?.Get(data.DefaultWeaponId);
                if (weapon != null) EquipWeapon(weapon);
                else DebugUtil.LogWarning($"[PlayerStats] defaultWeaponId={data.DefaultWeaponId} 를 TableData 에서 찾지 못했습니다.");
            }
```

교체:

```csharp
            // 기본 장착 — 로스터 장착 무기 우선, 로스터 미존재 시 DefaultWeaponId 폴백
            uint weaponId    = data.DefaultWeaponId;
            var  rosterState = PlayerDataManager.Instance?.Roster?.Get(data.Id);
            if (rosterState != null)
                weaponId = rosterState.EquippedWeaponId;   // 0 = 무기 없음 (로스터가 권위)
            else
                DebugUtil.LogWarning($"[PlayerStats] 로스터에 캐릭터 id:{data.Id} 없음 — DefaultWeaponId 폴백.");

            if (weaponId != 0)
            {
                var weapon = DataRegistry.Instance?.Weapons?.Get(weaponId);
                if (weapon != null) EquipWeapon(weapon);
                else DebugUtil.LogWarning($"[PlayerStats] weaponId={weaponId} 를 TableData 에서 찾지 못했습니다.");
            }
```

- [ ] **Step 2: PlayerManager — SelectedCharId 사용**

`Assets/Scripts/Core/Managers/PlayerManager.cs` `Start()` — 기존:

```csharp
            m_PlayerData = DataRegistry.Instance?.PlayerChars?.Get(DefaultPlayerId);
            if (m_PlayerData == null)
                DebugUtil.LogError($"[PlayerManager] Players 테이블에서 id={DefaultPlayerId} 를 찾을 수 없습니다. " +
                                   "Players.csv 를 확인하고 Sync All SO 를 실행하세요.");
```

교체:

```csharp
            uint charId = PlayerDataManager.Instance?.SelectedCharId ?? DefaultPlayerId;
            m_PlayerData = DataRegistry.Instance?.PlayerChars?.Get(charId);
            if (m_PlayerData == null)
                DebugUtil.LogError($"[PlayerManager] Players 테이블에서 id={charId} 를 찾을 수 없습니다. " +
                                   "Players.csv 를 확인하고 Sync All SO 를 실행하세요.");
```

(`DefaultPlayerId` static 필드는 폴백으로 유지.)

- [ ] **Step 3: 컴파일 확인**

MCP `read_console` — 에러 0.

- [ ] **Step 4: PlayMode 테스트 작성**

`Assets/Tests/PlayMode/CharacterRosterPlayTests.cs` 신규:

```csharp
// ====================================================================
//  CharacterRosterPlayTests — 부팅 로스터 보장 + 획득/클래스 제한/전환 (실 DataRegistry)
// ====================================================================

using System.Collections;
using NUnit.Framework;
using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MonsterKitchen.Tests.PlayMode
{
    public class CharacterRosterPlayTests
    {
        const float StartupTimeout = 30f;

        static IEnumerator Boot()
        {
            SceneManager.LoadScene(CommonString.SceneStart);
            yield return null;

            float elapsed = 0f;
            var startup = GlobalController.Instance.Startup;
            while (!startup.IsComplete && !startup.IsAborted && elapsed < StartupTimeout)
            { elapsed += Time.deltaTime; yield return null; }
            Assert.IsTrue(startup.IsComplete, "부팅이 선행되어야 한다.");
        }

        [UnityTest]
        public IEnumerator Boot_RosterHasDefaultCharacter()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return Boot();

            var pm = PlayerDataManager.Instance;
            Assert.GreaterOrEqual(pm.Roster.AllCharacters.Count, 1,
                "부팅 후 로스터에 최소 1캐릭터 (새 게임/마이그레이션 보장)");
            Assert.IsTrue(pm.Roster.Owns(pm.SelectedCharId),
                "SelectedCharId 는 보유 캐릭터여야 한다");
        }

        [UnityTest]
        public IEnumerator AddAndEquip_ClassRestriction_Enforced()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return Boot();

            var net  = NetworkManager.Instance;
            var pm   = PlayerDataManager.Instance;
            uint originalSelected = pm.SelectedCharId;

            // 9002(Bow 클래스) 획득 — 이미 보유 상태(이전 세이브)면 dup 검증으로 대체
            bool owned9002Before = pm.Roster.Owns(9002u);
            if (!owned9002Before)
            {
                bool? add = null;
                net.RequestAddCharacter(9002u, ok => add = ok);
                Assert.IsTrue(add, "9002 획득 실패 — Players.csv 시드/Sync 확인");
            }

            bool? dup = null;
            net.RequestAddCharacter(9002u, ok => dup = ok);
            Assert.IsFalse(dup, "중복 획득은 거부되어야 한다");

            // 클래스 불일치: Sword 무기(7001) → Bow 캐릭터(9002)
            bool? wrongClass = null;
            net.RequestEquipWeapon(9002u, 7001u, ok => wrongClass = ok);
            Assert.IsFalse(wrongClass, "클래스 불일치 장착은 거부되어야 한다");

            // 클래스 일치: Sword 무기(7001) → Sword 캐릭터(9001)
            bool? rightClass = null;
            net.RequestEquipWeapon(9001u, 7001u, ok => rightClass = ok);
            Assert.IsTrue(rightClass, "클래스 일치 장착은 성공해야 한다");

            // 조작 캐릭터 전환 + 원복
            bool? sel = null;
            net.RequestSelectCharacter(9002u, ok => sel = ok);
            Assert.IsTrue(sel);
            Assert.AreEqual(9002u, pm.SelectedCharId);

            net.RequestSelectCharacter(originalSelected, null);   // 세이브 오염 방지 원복
        }
    }
}
```

- [ ] **Step 5: PlayMode 테스트 실행**

MCP `run_tests` (mode=PlayMode, filter=CharacterRosterPlayTests).
Expected: 2 PASS.

---

### Task 6: 최종 검증 + 문서

**Files:**
- Modify: `docs/MODULES.md`, `WORK_IN_PROGRESS.md`, `CLAUDE.md` (현재 구현 상태)

- [ ] **Step 1: 전체 EditMode 테스트**

MCP `run_tests` (mode=EditMode, filter 없음).
Expected: 117 PASS (기존 99 + CSV 3 + Roster 8 + PDM 3 + Network 4), 0 FAIL.

- [ ] **Step 2: 전체 PlayMode 테스트**

MCP `run_tests` (mode=PlayMode, filter 없음).
Expected: 7 PASS + 1 Skip (기존 5+1Skip + 신규 2), 0 FAIL.

- [ ] **Step 3: 콘솔/씬 확인**

MCP `read_console` — 컴파일 에러 0. 씬 수정 없음 (이 모듈은 데이터/코드만) — manage_scene save 불필요.

- [ ] **Step 4: 문서 갱신**

1. `docs/MODULES.md` — 캐릭터 시스템 기반 모듈 항목 추가 (보유/저장/전환/클래스 제한, 테스트 카운트)
2. `WORK_IN_PROGRESS.md` — 서브 1 완료 체크 + 처리 내역 기록
3. `CLAUDE.md` 현재 구현 상태 — "캐릭터 로스터: 다중 보유 + 클래스(무기 타입 1:1) 제한 장착 + 세이브 v3" 한 줄 추가

- [ ] **Step 5: 사용자에게 커밋 여부 확인**

"서브프로젝트 1 (캐릭터 시스템 기반) 완료 — 커밋할까요?"

---

## Self-Review 체크 결과

- **스펙 커버리지**: §2 테이블 확장→Task 1, §3 로스터→Task 2, §4 세이브 v3/마이그레이션→Task 3, §5 NetworkManager/PlayerStats 연결→Task 4·5, §6 테스트→Task 1~5 + Task 6 집계, §7 완료 기준→Task 6. 갭 없음.
- **EditMode 한계 명시**: DataRegistry 의존 검증(획득 성공·클래스 체크)은 PlayMode 로 이관 — 테스트 파일 주석에 기록.
- **타입 일관성**: `PlayerRosterData.AllEntries()` 튜플 ↔ `PlayerDataManager.CharEntries()` ↔ `CharacterEntry` 필드명 일치 확인.
