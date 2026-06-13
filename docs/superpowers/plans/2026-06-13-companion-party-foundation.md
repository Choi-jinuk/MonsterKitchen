# 동료 파티 시스템 (첫 슬라이스) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 플레이어 동료 최대 2명을 AI로 던전에 동반 배치한다. 동료는 공용 flock 으로 리더를 추종하고 적을 둘러싸며 공격하며, 파티 구성은 ManagementScene UI 에서 선택해 ServerSaveData 에 저장한다.

**Architecture:** 동료 = 기존 `PlayerController` + 전용 companion BT(Follow/Engage). 이동/전투는 PlayerController 의 기존 공개 API(`SetBtMoveDir`/`FindNearestEnemy`/`ExecuteAutoAttack`) 재사용, 회피·둘러싸기는 `MonsterKitchen.AI.FlockSteering`/`FlockManager` 공용 레이어. `CompanionManager` 가 던전 진입(리더 배치 직후) 시 SpawnManager 패턴으로 스폰. 파티는 PlayerDataManager(읽기)+NetworkManager(변경)+ServerSaveData(영속).

**Tech Stack:** Unity 6, C# (MonoBehaviour + BT + 순수 헬퍼), UIToolkit, NUnit, 기존 NavAgent/Flock.

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Data/Server/ServerSaveData.cs` | `PartyCompanionIds` 필드 |
| `Core/Managers/PlayerDataManager.cs` | 파티 런타임 상태 + ApplyParty + LoadFrom |
| `Core/Managers/ServerDBManager.cs` | BuildSaveData 에 파티 저장 |
| `Core/Managers/NetworkManager.cs` | RequestSetParty + 검증 |
| `Player/PlayerController.cs` | IFlockAgent, IsCompanion/Leader, Init btAddressOverride |
| `AI/Companion/CompanionStateDecision.cs` | 신규 — Engage/Follow 결정 순수 헬퍼 |
| `AI/BehaviorTree/Player/BTAction_CompanionFollow.cs` | 신규 |
| `AI/BehaviorTree/Player/BTAction_CompanionEngage.cs` | 신규 |
| `Core/Managers/CompanionManager.cs` | 신규 — 던전 동료 스폰/디스폰 |
| `Dungeon/DungeonMapController.cs` | OnInit → SpawnParty 훅 |
| `UI/Party/PartySelectPanel.cs` (+UXML/USS) | 신규 파티 편성 UI |
| `Data/CSV/Players.csv` | PLR_004/005 |
| BTAsset `bt/companion/9001` + AssetManifest | 신규 (Editor 자동화) |
| `Assets/Tests/EditMode/PartyDataTests.cs` | 저장/검증/결정 테스트 |
| `Assets/Tests/PlayMode/CompanionSpawnTests.cs` | 추종·비겹침 테스트 |

---

## Task 1: ServerSaveData 파티 필드 (TDD)

**Files:**
- Modify: `Assets/Scripts/Data/Server/ServerSaveData.cs`
- Test: `Assets/Tests/EditMode/PartyDataTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using MonsterKitchen.Data.Server;

namespace MonsterKitchen.Tests
{
    public class PartyDataTests
    {
        [Test]
        public void ServerSaveData_PartyCompanionIds_DefaultsEmptyNonNull()
        {
            var s = new ServerSaveData();
            Assert.IsNotNull(s.PartyCompanionIds);
            Assert.AreEqual(0, s.PartyCompanionIds.Count);
        }

        [Test]
        public void ServerSaveData_PartyCompanionIds_HoldsValues()
        {
            var s = new ServerSaveData();
            s.PartyCompanionIds.Add(9002);
            s.PartyCompanionIds.Add(9003);
            Assert.AreEqual(new List<uint> { 9002, 9003 }, s.PartyCompanionIds);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform EditMode -testFilter "MonsterKitchen.Tests.PartyDataTests" -testResults results.xml -batchmode`
> 주의: `-quit` 금지 (테스트 실행 전 종료됨). Editor 가 열려 있으면 락 충돌 — 닫고 실행.
Expected: 컴파일 실패 (PartyCompanionIds 없음).

- [ ] **Step 3: 필드 추가**

`ServerSaveData.cs` 의 `SelectedCharId` 줄 아래에 추가:
```csharp
        public uint   SelectedCharId = 9001;
        public List<uint> PartyCompanionIds = new();   // AI 동료 (max 2)
```
> 파일 상단에 `using System.Collections.Generic;` 가 이미 있는지 확인(다른 List 필드 존재 → 있음).

- [ ] **Step 4: 통과 확인**

Run: 위 EditMode 명령.
Expected: 2 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Data/Server/ServerSaveData.cs Assets/Tests/EditMode/PartyDataTests.cs
git commit -m "feat(save): add PartyCompanionIds to ServerSaveData"
```

---

## Task 2: PlayerDataManager 파티 상태 + 영속

**Files:**
- Modify: `Assets/Scripts/Core/Managers/PlayerDataManager.cs`
- Modify: `Assets/Scripts/Core/Managers/ServerDBManager.cs:107-113`

- [ ] **Step 1: PlayerDataManager 파티 상태 추가**

`SelectedCharId` 프로퍼티 아래에 추가:
```csharp
        public uint SelectedCharId { get; set; } = 9001;

        // AI 동료 파티 (max 2). 읽기 직접 허용, 변경은 NetworkManager.RequestSetParty 경유.
        public List<uint> PartyCompanionIds { get; private set; } = new();

        public void ApplyParty(IEnumerable<uint> ids)
        {
            PartyCompanionIds.Clear();
            if (ids != null) PartyCompanionIds.AddRange(ids);
        }
```
> 파일 상단 `using System.Collections.Generic;` 확인(없으면 추가).

- [ ] **Step 2: LoadFrom 에 파티 로드 추가**

`LoadFrom` 메서드의 `SelectedCharId = save.SelectedCharId;` 줄 아래에:
```csharp
            SelectedCharId = save.SelectedCharId;
            ApplyParty(save.PartyCompanionIds);
```

- [ ] **Step 3: ServerDBManager.BuildSaveData 에 파티 저장 추가**

`ServerDBManager.cs` 의 `BuildSaveData()` 내 `new ServerSaveData { ... SelectedCharId = pm?.SelectedCharId ?? 9001, ... }` 객체 초기화에 추가:
```csharp
                SelectedCharId    = pm?.SelectedCharId ?? 9001,
                PartyCompanionIds = pm != null ? new List<uint>(pm.PartyCompanionIds) : new List<uint>(),
```
> `ServerDBManager.cs` 상단 `using System.Collections.Generic;` 확인.

- [ ] **Step 4: 컴파일 확인**

Editor 닫고 EditMode 전체 실행(회귀):
Run: `... -runTests -testPlatform EditMode -testResults results.xml -batchmode`
Expected: 전체 PASS (기존 SaveDataRoundTrip 포함).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Core/Managers/PlayerDataManager.cs Assets/Scripts/Core/Managers/ServerDBManager.cs
git commit -m "feat(save): persist party companion ids via PlayerDataManager/ServerDB"
```

---

## Task 3: NetworkManager.RequestSetParty + 검증 (TDD)

**Files:**
- Modify: `Assets/Scripts/Core/Managers/NetworkManager.cs`
- Test: `Assets/Tests/EditMode/PartyDataTests.cs`

검증 로직을 테스트 가능하도록 순수 static 함수로 분리한 뒤 NetworkManager 가 호출한다.

- [ ] **Step 1: 실패 테스트 추가 (PartyDataTests.cs)**

```csharp
        [Test]
        public void SanitizeParty_CapsAtTwo_ExcludesLeader_Distinct()
        {
            // 리더 9001, 후보 [9001, 9002, 9002, 9003, 9004]
            var result = MonsterKitchen.Core.NetworkManager.SanitizeParty(
                leaderId: 9001,
                requested: new uint[] { 9001, 9002, 9002, 9003, 9004 },
                isValidId: id => id >= 9002 && id <= 9005);

            Assert.AreEqual(2, result.Count, "최대 2명");
            Assert.IsFalse(result.Contains(9001u), "리더 제외");
            Assert.AreEqual(new List<uint> { 9002, 9003 }, result, "중복 제거 + 순서 유지");
        }

        [Test]
        public void SanitizeParty_DropsInvalidIds()
        {
            var result = MonsterKitchen.Core.NetworkManager.SanitizeParty(
                leaderId: 9001,
                requested: new uint[] { 9999, 9002 },
                isValidId: id => id == 9002);
            Assert.AreEqual(new List<uint> { 9002 }, result);
        }
```

- [ ] **Step 2: 실패 확인**

Run: `... -testFilter "MonsterKitchen.Tests.PartyDataTests" ...`
Expected: 컴파일 실패 (SanitizeParty 없음).

- [ ] **Step 3: NetworkManager 에 SanitizeParty + RequestSetParty 추가**

`NetworkManager.cs` 클래스 본문에 추가(`using System;` `using System.Collections.Generic;` `using MonsterKitchen.Data;` 확인):
```csharp
        // ── 파티 편성 ────────────────────────────────────────────────────

        /// <summary>요청 파티를 정제: 리더 제외, 중복 제거, 무효 ID 드롭, 최대 2명.</summary>
        public static List<uint> SanitizeParty(uint leaderId, IReadOnlyList<uint> requested, Func<uint, bool> isValidId)
        {
            var result = new List<uint>(2);
            if (requested == null) return result;
            foreach (var id in requested)
            {
                if (result.Count >= 2)        break;
                if (id == leaderId)           continue;
                if (result.Contains(id))      continue;
                if (isValidId != null && !isValidId(id)) continue;
                result.Add(id);
            }
            return result;
        }

        public void RequestSetParty(IReadOnlyList<uint> ids, Action onResult = null)
        {
            var pm    = PlayerDataManager.Instance;
            var chars = MonsterKitchen.Data.DataRegistry.Instance?.PlayerChars;
            uint leader = pm?.SelectedCharId ?? 9001;

            var clean = SanitizeParty(leader, ids,
                id => chars != null && chars.Get(id) != null);

            pm?.ApplyParty(clean);
            GlobalController.Instance?.SaveSched.MarkDirty();
            onResult?.Invoke();
        }
```

- [ ] **Step 4: 통과 확인**

Run: `... -testFilter "MonsterKitchen.Tests.PartyDataTests" ...`
Expected: 전체 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Core/Managers/NetworkManager.cs Assets/Tests/EditMode/PartyDataTests.cs
git commit -m "feat(net): RequestSetParty with validation (cap 2, exclude leader, valid ids)"
```

---

## Task 4: PlayerController — IFlockAgent + 동료 역할

**Files:**
- Modify: `Assets/Scripts/Player/PlayerController.cs`

- [ ] **Step 1: 클래스 선언 + using 변경**

상단 using 에 `using MonsterKitchen.AI;` 추가(없으면). 클래스 선언:
```csharp
    public class PlayerController : MonoBehaviour, IBTBlackboardInitializer, IFlockAgent
```

- [ ] **Step 2: 동료 역할 필드 + IFlockAgent 구현 추가**

공개 API 영역(예: `Rb` 프로퍼티 근처)에 추가:
```csharp
        // ── 동료(컴패니언) 역할 ─────────────────────────────────────────
        public bool      IsCompanion { get; private set; }
        public Transform Leader      { get; private set; }

        // ── IFlockAgent (리더·동료 공통, 단일 그리드) ───────────────────
        public Vector2   FlockPosition  => transform.position;
        public Transform FlockTransform => transform;
```

- [ ] **Step 3: 등록/해제 — OnEnable/OnDisable**

PlayerController 에 OnEnable/OnDisable 이 있으면 거기에, 없으면 추가(기존 InputManager 구독 로직 보존):
```csharp
        void OnEnable()
        {
            // (기존 InputManager 구독 등이 있으면 유지)
            FlockManager.GetOrCreate().Register(this);
        }

        void OnDisable()
        {
            FlockManager.Instance?.Unregister(this);
        }
```
> PlayerController 에 이미 OnEnable/OnDisable 이 있으면 본문에 Register/Unregister 줄만 추가. 중복 메서드 만들지 말 것.

- [ ] **Step 4: Init 에 BT 주소 override 파라미터 추가 + 동료 초기화 진입점**

기존 `public void Init(PlayerCharData data)` 시그니처를 확장:
```csharp
        public void Init(PlayerCharData data, string btAddressOverride = null)
        {
            // ... (기존 본문 유지) ...
            // BTRunner.SetAsset 부분에서 사용할 주소:
            //   string btKey = string.IsNullOrEmpty(btAddressOverride) ? data.BtAssetAddress : btAddressOverride;
            // 기존 data.BtAssetAddress 사용처를 btKey 로 교체.
        }

        /// <summary>동료로 초기화. SetActive(true) 전에 호출해야 BTRunner.Start 가 companion BT 로 시작된다.</summary>
        public void InitAsCompanion(PlayerCharData data, Transform leader, string companionBtAddress)
        {
            IsCompanion = true;
            Leader      = leader;
            Init(data, companionBtAddress);
        }
```
> 기존 Init 본문에서 `data.BtAssetAddress` 로 BTAsset 로드하던 부분을 `btAddressOverride ?? data.BtAssetAddress` 기준으로 바꾼다. 리더 경로(override=null)는 동작 불변.

- [ ] **Step 5: InitializeBlackboard 에 동료 키 주입**

`InitializeBlackboard(BTBlackboard bb)` 메서드 끝에 동료 전용 키 설정 추가(리더는 영향 없음):
```csharp
            // 동료: companion BT 가 읽는 키. InitAsCompanion 이 SetActive 전에 호출되므로
            // IsCompanion/Leader 는 이 콜백 시점에 이미 세팅돼 있다.
            if (IsCompanion)
            {
                bb.Set("Leader",         Leader);
                bb.Set("FollowDistance", 1.5f);
                bb.Set("DetectRange",    6f);
            }
```
> `BTBlackboard.Set` / `bb.Set` 시그니처는 기존 InitializeBlackboard 내 사용 패턴과 동일.

- [ ] **Step 6: 컴파일 확인**

Editor 닫고 EditMode 전체 실행.
Expected: 전체 PASS (회귀 없음). PlayerController 컴파일 에러 0.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Player/PlayerController.cs
git commit -m "feat(player): IFlockAgent + companion role (InitAsCompanion, BT override, bb keys)"
```

---

## Task 5: CompanionStateDecision 순수 헬퍼 (TDD)

**Files:**
- Create: `Assets/Scripts/AI/Companion/CompanionStateDecision.cs`
- Test: `Assets/Tests/EditMode/PartyDataTests.cs`

- [ ] **Step 1: 실패 테스트 추가**

```csharp
        [Test]
        public void CompanionState_EnemyWithinDetect_Engage()
        {
            var st = MonsterKitchen.AI.Companion.CompanionStateDecision.Decide(
                hasEnemy: true, enemyDist: 4f, detectRange: 6f);
            Assert.AreEqual(MonsterKitchen.AI.Companion.CompanionState.Engage, st);
        }

        [Test]
        public void CompanionState_NoEnemy_Follow()
        {
            var st = MonsterKitchen.AI.Companion.CompanionStateDecision.Decide(
                hasEnemy: false, enemyDist: 0f, detectRange: 6f);
            Assert.AreEqual(MonsterKitchen.AI.Companion.CompanionState.Follow, st);
        }

        [Test]
        public void CompanionState_EnemyBeyondDetect_Follow()
        {
            var st = MonsterKitchen.AI.Companion.CompanionStateDecision.Decide(
                hasEnemy: true, enemyDist: 9f, detectRange: 6f);
            Assert.AreEqual(MonsterKitchen.AI.Companion.CompanionState.Follow, st);
        }
```

- [ ] **Step 2: 실패 확인**

Run: `... -testFilter "MonsterKitchen.Tests.PartyDataTests" ...`
Expected: 컴파일 실패.

- [ ] **Step 3: 구현**

```csharp
namespace MonsterKitchen.AI.Companion
{
    public enum CompanionState { Follow, Engage }

    // ====================================================================
    //  CompanionStateDecision — 동료 행동 상태 결정 (순수 함수)
    // ====================================================================
    public static class CompanionStateDecision
    {
        /// <summary>적이 감지 범위 내면 Engage, 아니면 Follow.</summary>
        public static CompanionState Decide(bool hasEnemy, float enemyDist, float detectRange)
        {
            return (hasEnemy && enemyDist <= detectRange)
                ? CompanionState.Engage
                : CompanionState.Follow;
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `... -testFilter "MonsterKitchen.Tests.PartyDataTests" ...`
Expected: 전체 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/AI/Companion/CompanionStateDecision.cs Assets/Tests/EditMode/PartyDataTests.cs
git commit -m "feat(ai): CompanionStateDecision (engage vs follow)"
```

---

## Task 6: BTAction_CompanionFollow

**Files:**
- Create: `Assets/Scripts/AI/BehaviorTree/Player/BTAction_CompanionFollow.cs`

리더를 flock 으로 추종, FollowDistance 내면 정지.

- [ ] **Step 1: 작성**

```csharp
using MonsterKitchen.AI;
using MonsterKitchen.Player;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Player
{
    // ====================================================================
    //  BTAction_CompanionFollow — 리더 추종 (flock chase + separation)
    //
    //  blackboard: Leader(Transform), FollowDistance(float)
    // ====================================================================
    [BTNode("Companion/Action/Follow")]
    public class BTAction_CompanionFollow : BTNode
    {
        class State
        {
            public PlayerController Pc;
            public Vector2[]        Buffer;
        }

        protected override void OnEnter(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Pc == null)     s.Pc     = ctx.Owner.GetComponent<PlayerController>();
            if (s.Buffer == null) s.Buffer = new Vector2[16];
        }

        protected override BTStatus Execute(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Pc == null) return BTStatus.Failure;

            if (!ctx.Blackboard.TryGet<Transform>("Leader", out var leader) || leader == null)
                return BTStatus.Failure;

            float follow = ctx.Blackboard.Get<float>("FollowDistance");
            Vector2 self = ctx.Owner.transform.position;
            Vector2 toLeader = (Vector2)leader.position - self;
            float   dist = toLeader.magnitude;

            if (dist <= follow)
            {
                s.Pc.SetBtMoveDir(Vector2.zero);
                return BTStatus.Success;
            }

            var w   = FlockWeights.Default;
            var mgr = FlockManager.Instance;
            int n   = mgr != null ? mgr.QueryNeighbors(self, w.SepRadius, ctx.Owner.transform, s.Buffer) : 0;

            Vector2 vel = FlockSteering.ComputeChase(self, toLeader.normalized, s.Buffer, n,
                                                     moveSpeed: 1f, w);
            s.Pc.SetBtMoveDir(vel.normalized);   // PlayerController 가 속도/물리 적용
            return BTStatus.Running;
        }

        protected override void OnExit(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            s.Pc?.SetBtMoveDir(Vector2.zero);
        }

#if UNITY_EDITOR
        public override string DebugLabel => "→ CompanionFollow";
#endif
    }
}
```
> `moveSpeed:1f` 는 방향 산출용(결과를 normalized 로 SetBtMoveDir 전달, 실제 속도는 PlayerController.Stats). BTNode/BTContext/BTStatus API 는 기존 `BTAction_PlayerMove` 와 동일 패턴.

- [ ] **Step 2: 컴파일 확인**

Editor 닫고 EditMode 실행(컴파일 확인).
Expected: 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/AI/BehaviorTree/Player/BTAction_CompanionFollow.cs
git commit -m "feat(ai): BTAction_CompanionFollow (flock chase to leader)"
```

---

## Task 7: BTAction_CompanionEngage

**Files:**
- Create: `Assets/Scripts/AI/BehaviorTree/Player/BTAction_CompanionEngage.cs`

근처 적 탐지 → 접근/둘러싸기 + 공격. 적 없으면 Failure(→Follow).

- [ ] **Step 1: 작성**

```csharp
using MonsterKitchen.AI;
using MonsterKitchen.AI.Companion;
using MonsterKitchen.Player;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Player
{
    // ====================================================================
    //  BTAction_CompanionEngage — 적 둘러싸며 공격
    //
    //  blackboard: DetectRange(float), EngageRange(float)
    //  적 없음 → Failure (Follow 로 폴백)
    // ====================================================================
    [BTNode("Companion/Action/Engage")]
    public class BTAction_CompanionEngage : BTNode
    {
        const float RingRadius = 1.2f;

        class State
        {
            public PlayerController Pc;
            public Vector2[]        Buffer;
        }

        protected override void OnEnter(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Pc == null)     s.Pc     = ctx.Owner.GetComponent<PlayerController>();
            if (s.Buffer == null) s.Buffer = new Vector2[16];
        }

        protected override BTStatus Execute(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Pc == null) return BTStatus.Failure;

            float detect = ctx.Blackboard.Get<float>("DetectRange");
            var   enemy  = s.Pc.FindNearestEnemy(detect);

            var state = CompanionStateDecision.Decide(
                hasEnemy: enemy != null,
                enemyDist: enemy != null ? Vector2.Distance(ctx.Owner.transform.position, enemy.position) : 0f,
                detectRange: detect);

            if (state != CompanionState.Engage || enemy == null)
                return BTStatus.Failure;   // Follow 로

            Vector2 self = ctx.Owner.transform.position;
            float   dist = Vector2.Distance(self, enemy.position);

            var w   = FlockWeights.Default;
            var mgr = FlockManager.Instance;
            int n   = mgr != null ? mgr.QueryNeighbors(self, w.SepRadius, ctx.Owner.transform, s.Buffer) : 0;

            Vector2 vel = dist > RingRadius
                ? FlockSteering.ComputeChase(self, ((Vector2)enemy.position - self).normalized, s.Buffer, n, 1f, w)
                : FlockSteering.ComputeEncircle(self, enemy.position, RingRadius, s.Buffer, n, 1f, w);

            s.Pc.SetBtMoveDir(vel.normalized);
            s.Pc.ExecuteAutoAttack(enemy);   // 공격 타이밍/쿨다운은 내부 처리
            return BTStatus.Running;
        }

        protected override void OnExit(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            s.Pc?.SetBtMoveDir(Vector2.zero);
        }

#if UNITY_EDITOR
        public override string DebugLabel => "⚔ CompanionEngage";
#endif
    }
}
```
> `FindNearestEnemy` / `ExecuteAutoAttack` 는 PlayerController 기존 공개 메서드. `ExecuteAutoAttack` 가 자체 쿨다운/사거리 처리.

- [ ] **Step 2: 컴파일 확인**

Editor 닫고 EditMode 실행.
Expected: 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/AI/BehaviorTree/Player/BTAction_CompanionEngage.cs
git commit -m "feat(ai): BTAction_CompanionEngage (encircle + attack nearest enemy)"
```

---

## Task 8: CompanionManager (스폰/디스폰 — 타이밍 핵심)

**Files:**
- Create: `Assets/Scripts/Core/Managers/CompanionManager.cs`

- [ ] **Step 1: 작성**

```csharp
using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Player;
using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  CompanionManager — 던전 동료 스폰/디스폰 (싱글톤, 던전 한정)
    //
    //  ▶ 호출 시점: DungeonMapController.OnInit() 에서 리더 리포지션 직후 SpawnParty().
    //    선행: DataRegistry 로드, 던전 NavGrid 존재, 리더(PlayerManager.Player) 배치 완료.
    //  ▶ SpawnManager 패턴: InstantiateDisabled → InitAsCompanion → SetActive.
    // ====================================================================
    public sealed class CompanionManager : MonoBehaviour
    {
        public static CompanionManager Instance { get; private set; }

        const string CompanionBtAddress = "bt/companion/9001";

        readonly List<GameObject> m_Spawned = new();

        public static CompanionManager GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[CompanionManager]");
            return Instance = go.AddComponent<CompanionManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>현재 파티 동료를 리더 주변에 스폰. 리더 배치 직후 호출.</summary>
        public void SpawnParty()
        {
            DespawnAll();

            var leaderPc = PlayerManager.Instance?.Player;
            if (leaderPc == null)
            {
                DebugUtil.LogError("[CompanionManager] 리더(Player)가 없어 동료 스폰 불가.", this);
                return;
            }

            var ids   = PlayerDataManager.Instance?.PartyCompanionIds;
            if (ids == null || ids.Count == 0) return;

            var prefab = AssetLoadManager.Instance?.Load<GameObject>("prefab/player/9001");
            if (prefab == null)
            {
                DebugUtil.LogError("[CompanionManager] 동료 프리팹 로드 실패: prefab/player/9001", this);
                return;
            }

            Vector3 leaderPos = leaderPc.transform.position;
            for (int i = 0; i < ids.Count; i++)
            {
                var data = DataRegistry.Instance?.PlayerChars?.Get(ids[i]);
                if (data == null)
                {
                    DebugUtil.LogError($"[CompanionManager] PlayerChar({ids[i]}) 데이터 없음 — 스킵.", this);
                    continue;
                }

                Vector3 offset = OffsetFor(i, ids.Count);
                var go = InstantiateDisabled(prefab, leaderPos + offset);

                var pc = go.GetComponent<PlayerController>();
                if (pc == null)
                {
                    DebugUtil.LogError("[CompanionManager] 프리팹에 PlayerController 없음.", this);
                    Destroy(go);
                    continue;
                }

                // SetActive(true) 전에 동료/리더/BT 주입 (BTRunner.Start 가 companion BT 로 시작)
                pc.InitAsCompanion(data, leaderPc.transform, CompanionBtAddress);
                go.SetActive(true);

                m_Spawned.Add(go);
            }
        }

        public void DespawnAll()
        {
            for (int i = 0; i < m_Spawned.Count; i++)
                if (m_Spawned[i] != null) Destroy(m_Spawned[i]);
            m_Spawned.Clear();
        }

        // 리더 뒤쪽 반원에 분산 (겹침 스폰 방지)
        static Vector3 OffsetFor(int index, int count)
        {
            float spread = 1.5f;
            float t      = count <= 1 ? 0.5f : (float)index / (count - 1);
            float angle  = Mathf.Lerp(200f, 340f, t) * Mathf.Deg2Rad;  // 뒤쪽 호
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spread;
        }

        static GameObject InstantiateDisabled(GameObject prefab, Vector3 position)
        {
            bool wasActive = prefab.activeSelf;
            prefab.SetActive(false);
            var instance = Instantiate(prefab, position, Quaternion.identity);
            prefab.SetActive(wasActive);
            return instance;
        }
    }
}
```
> `AssetLoadManager.Instance.Load<GameObject>("prefab/player/9001")` 로 리더와 동일 프리팹 사용(BT 만 companion 으로 override). `DebugUtil`/`PlayerManager`/`DataRegistry`/`AssetLoadManager` 는 기존 타입.

- [ ] **Step 2: 컴파일 확인**

Editor 닫고 EditMode 실행.
Expected: 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Core/Managers/CompanionManager.cs
git commit -m "feat(core): CompanionManager spawns party near leader on dungeon entry"
```

---

## Task 9: DungeonMapController 스폰 훅

**Files:**
- Modify: `Assets/Scripts/Dungeon/DungeonMapController.cs` (OnInit 내 RepositionInScene 직후)

- [ ] **Step 1: OnInit 에 SpawnParty 추가**

`DungeonMapController.OnInit()` 에서 `PlayerManager.Instance.RepositionInScene(...)`(리더 배치) 를 호출하는 줄 **직후** 에 추가:
```csharp
            // 리더 배치 직후 — 동료 스폰 (선행: NavGrid 존재 + 리더 위치 확정)
            MonsterKitchen.Core.CompanionManager.GetOrCreate().SpawnParty();
```
> RepositionInScene 호출 위치를 먼저 확인하고 그 바로 다음 줄에 삽입. NavGrid 초기화가 OnInit 내에서 리포지션보다 앞이라면 순서 OK; 아니라면 NavGrid 준비 이후로 배치.

- [ ] **Step 2: 컴파일 확인**

Editor 닫고 EditMode 실행.
Expected: 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Dungeon/DungeonMapController.cs
git commit -m "feat(dungeon): spawn companion party after leader reposition"
```

---

## Task 10: PartySelectPanel UI (UIToolkit)

**Files:**
- Create: `Assets/Scripts/UI/Party/PartySelectPanel.cs`
- Create: `Assets/UI/Party/PartySelectPanel.uxml`
- Create: `Assets/UI/Party/PartySelectPanel.uss`

> 기존 UIToolkit 패널(예: RewardPopup / GameHUD)의 UXML/USS 구조와 로드 방식을 먼저 확인해 동일 패턴을 따른다.

- [ ] **Step 1: UXML 작성 (`PartySelectPanel.uxml`)**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
  <ui:VisualElement name="root" class="party-root">
    <ui:Label name="title" text="파티 편성" class="party-title"/>
    <ui:Label name="leader-label" class="party-leader"/>
    <ui:Label name="count-label" class="party-count"/>
    <ui:ScrollView name="roster" class="party-roster"/>
    <ui:VisualElement class="party-buttons">
      <ui:Button name="save-btn" text="저장"/>
      <ui:Button name="close-btn" text="닫기"/>
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: USS 작성 (`PartySelectPanel.uss`)**

```css
.party-root { width: 520px; padding: 16px; background-color: rgba(20,20,28,0.95); }
.party-title { font-size: 22px; -unity-font-style: bold; color: white; }
.party-leader { color: rgb(200,200,120); margin-top: 6px; }
.party-count { color: rgb(180,180,180); margin: 6px 0; }
.party-roster { flex-direction: row; flex-wrap: wrap; height: 200px; }
.card { width: 110px; height: 130px; margin: 6px; background-color: rgba(50,50,70,0.9); }
.card.selected { background-color: rgba(80,120,200,0.95); }
.party-buttons { flex-direction: row; justify-content: center; margin-top: 10px; }
```

- [ ] **Step 3: 컴포넌트 작성 (`PartySelectPanel.cs`)**

```csharp
using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  PartySelectPanel — ManagementScene 파티 편성 UI
    //
    //  로스터(리더 제외)에서 동료 최대 2명 토글 → RequestSetParty 저장.
    // ====================================================================
    [RequireComponent(typeof(UIDocument))]
    public class PartySelectPanel : MonoBehaviour
    {
        const int MaxCompanions = 2;

        readonly List<uint> m_Selected = new();
        Label  m_CountLabel;
        ScrollView m_Roster;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            m_CountLabel = root.Q<Label>("count-label");
            m_Roster     = root.Q<ScrollView>("roster");

            var pm = PlayerDataManager.Instance;
            uint leaderId = pm?.SelectedCharId ?? 9001;

            // 현재 파티 프리셋
            m_Selected.Clear();
            if (pm != null) m_Selected.AddRange(pm.PartyCompanionIds);

            var leaderData = DataRegistry.Instance?.PlayerChars?.Get(leaderId);
            root.Q<Label>("leader-label").text =
                $"리더: {(leaderData != null ? leaderData.DisplayName : leaderId.ToString())} (고정)";

            BuildRoster(leaderId);
            UpdateCount();

            root.Q<Button>("save-btn").clicked  += OnSave;
            root.Q<Button>("close-btn").clicked += () => gameObject.SetActive(false);
        }

        void BuildRoster(uint leaderId)
        {
            m_Roster.Clear();
            var chars = DataRegistry.Instance?.PlayerChars;
            if (chars == null) return;

            foreach (var data in chars.All)
            {
                if (data.Id == leaderId) continue;
                uint id   = data.Id;
                var  card = new Button { text = $"{data.DisplayName}\n★{data.NatalStars} {data.CharClass}" };
                card.AddToClassList("card");
                if (m_Selected.Contains(id)) card.AddToClassList("selected");
                card.clicked += () => ToggleCard(id, card);
                m_Roster.Add(card);
            }
        }

        void ToggleCard(uint id, Button card)
        {
            if (m_Selected.Contains(id))
            {
                m_Selected.Remove(id);
                card.RemoveFromClassList("selected");
            }
            else
            {
                if (m_Selected.Count >= MaxCompanions) return;   // 상한 막기
                m_Selected.Add(id);
                card.AddToClassList("selected");
            }
            UpdateCount();
        }

        void UpdateCount() => m_CountLabel.text = $"동료 선택 ({m_Selected.Count}/{MaxCompanions})";

        void OnSave()
        {
            NetworkManager.Instance?.RequestSetParty(new List<uint>(m_Selected));
            gameObject.SetActive(false);
        }
    }
}
```

- [ ] **Step 4: 컴파일 확인**

Editor 닫고 EditMode 실행.
Expected: 에러 없음. (UXML/USS 는 Editor import 시 .meta 생성)

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/UI/Party/ Assets/UI/Party/
git commit -m "feat(ui): PartySelectPanel for choosing companions"
```

---

## Task 11: ManagementScene 오픈 트리거 + 씬 배치

**Files:**
- Editor 작업: ManagementScene 에 PartySelectPanel UIDocument 배치 + 오픈 버튼 연결

- [ ] **Step 1: 씬 배치 (Editor)**

ManagementScene 열고:
- `PartySelectPanel` 컴포넌트를 가진 UIDocument GameObject 추가(PanelSettings = 기존 HUD 와 동일 에셋), `PartySelectPanel.uxml` 연결. 기본 비활성(SetActive false).
- ManagementScene UI(또는 던전 포털 옆)에 "파티 편성" 버튼 추가 → 클릭 시 패널 `SetActive(true)`. 기존 ManagementSceneController 또는 간단한 오프너 컴포넌트로 연결.
- `manage_scene(action: save)` 또는 Editor 저장.

- [ ] **Step 2: 검증**

Play → ManagementScene 에서 버튼 클릭 → 패널 표시 → 카드 토글(최대 2) → 저장 → 패널 닫힘. Console 에러 0.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scenes/ManagementScene.unity
git commit -m "feat(ui): wire PartySelectPanel into ManagementScene"
```

---

## Task 12: 신규 캐릭터 + Companion BT 에셋 (Editor 자동화)

**Files:**
- Modify: `Assets/Data/CSV/Players.csv`
- Editor 자동화: companion BT 에셋 + AssetManifest + SO sync

- [ ] **Step 1: PLR_004/005 CSV 행 추가**

`Players.csv` 마지막 행 뒤(PLR_003 다음)에:
```
PLR_004,9004,훈련병 전사,prefab/player/9001,bt/player/9001,120,9,4.8,2,None,0,SGD_010,,SGD_020,100,15,25,Sword,1,1.0,1.0
PLR_005,9005,수습 사제,prefab/player/9001,bt/player/9001,90,8,5.2,0,None,0,SGD_010,,SGD_020,100,15,25,Staff,2,0.9,1.1
```

- [ ] **Step 2: Companion BT 자동화 메서드 추가 (FlockAutomation 패턴)**

`Assets/Editor/CompanionAutomation.cs` 신규:
```csharp
using MonsterKitchen.Core;
using UnityEditor;
using UnityEngine;

namespace MonsterKitchen.Editor
{
    public static class CompanionAutomation
    {
        const string PlayerBtPath  = "Assets/Data/BehaviorTrees/PlayerBT.asset";
        const string CompanionBt   = "Assets/Data/BehaviorTrees/CompanionBT.asset";
        const string ManifestPath  = "Assets/Data/AssetManifest.asset";

        [MenuItem("MonsterKitchen/Companion/Run Asset Automation")]
        public static void Run()
        {
            // 1. 데이터 동기화 (PLR_004/005)
            DataManagerWindow.SyncAllSO();

            // 2. CompanionBT 에셋 — PlayerBT 복제 후 트리를 Selector[Engage, Follow] 로 교체
            //    (자동 트리 구성이 어려우면 빈 BTAsset 생성 후 Editor 에서 수동 노드 배치 안내)
            if (AssetDatabase.LoadMainAssetAtPath(CompanionBt) == null)
            {
                AssetDatabase.CopyAsset(PlayerBtPath, CompanionBt);
                Debug.Log($"[CompanionAutomation] CompanionBT 생성: {CompanionBt} — 트리를 Selector[Companion/Action/Engage, Companion/Action/Follow] 로 수동 구성 필요");
            }

            // 3. AssetManifest 등록: bt/companion/9001 → CompanionBT
            RegisterManifest("bt/companion/9001", CompanionBt);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CompanionAutomation] 완료 ✓");
        }

        static void RegisterManifest(string key, string assetPath)
        {
            var manifest = AssetDatabase.LoadMainAssetAtPath(ManifestPath);
            var asset    = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (manifest == null || asset == null)
            {
                Debug.LogError($"[CompanionAutomation] 매니페스트/에셋 로드 실패: {key}");
                return;
            }
            var so      = new SerializedObject(manifest);
            var entries = so.FindProperty("m_Entries");
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Key").stringValue == key)
                    return; // 이미 존재
            int idx = entries.arraySize;
            entries.InsertArrayElementAtIndex(idx);
            var e = entries.GetArrayElementAtIndex(idx);
            e.FindPropertyRelative("Key").stringValue            = key;
            e.FindPropertyRelative("Asset").objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manifest);
            Debug.Log($"[CompanionAutomation] 매니페스트 등록: {key}");
        }
    }
}
```

- [ ] **Step 3: 자동화 실행 (batchmode, Editor 닫고)**

Run:
```
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -batchmode -quit -executeMethod MonsterKitchen.Editor.CompanionAutomation.Run -logFile companion_auto.log
```
Expected: 로그에 "완료 ✓", CompanionBT.asset 생성, manifest bt/companion/9001 등록.

- [ ] **Step 4: CompanionBT 트리 구성 (Editor, 수동)**

Editor 에서 `CompanionBT.asset` 열고 루트를 `Selector` → 자식 `[Companion/Action/Engage, Companion/Action/Follow]` 순으로 구성(BTAsset 에디터 사용). 저장.
> PlayerBT 복제본이라 기존 노드가 있으면 제거 후 위 2개로 교체.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Data/CSV/Players.csv Assets/Data/SO/TableData.asset Assets/Editor/CompanionAutomation.cs Assets/Data/BehaviorTrees/CompanionBT.asset Assets/Data/AssetManifest.asset
git commit -m "feat(data): PLR_004/005 + companion BT asset + manifest"
```

---

## Task 13: PlayMode 동료 스폰/추종 검증

**Files:**
- Create: `Assets/Tests/PlayMode/CompanionSpawnTests.cs`

CompanionManager 와 IFlockAgent 비겹침을 씬 비의존 더미로 검증(전체 던전 플로우는 시각 검증으로).

- [ ] **Step 1: 테스트 작성**

```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MonsterKitchen.AI;

namespace MonsterKitchen.Tests.PlayMode
{
    public class CompanionSpawnTests
    {
        sealed class Agent : MonoBehaviour, IFlockAgent
        {
            public Vector2   FlockPosition  => transform.position;
            public Transform FlockTransform => transform;
        }

        [UnityTest]
        public IEnumerator Companions_SeparateAndStayNearLeader()
        {
            var mgr    = FlockManager.GetOrCreate();
            var leader = new GameObject("leader");
            leader.transform.position = Vector3.zero;
            var la = leader.AddComponent<Agent>();
            mgr.Register(la);

            var comps = new List<Agent>();
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject($"comp{i}");
                go.transform.position = new Vector2(0.1f * i, 0.1f);
                var a = go.AddComponent<Agent>();
                mgr.Register(a);
                comps.Add(a);
            }

            var buffer = new Vector2[16];
            float t = 0f;
            while (t < 1.5f)
            {
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                foreach (var a in comps)
                {
                    Vector2 self     = a.FlockPosition;
                    Vector2 toLeader = (Vector2)leader.transform.position - self;
                    int n   = mgr.QueryNeighbors(self, 0.9f, a.FlockTransform, buffer);
                    Vector2 vel = FlockSteering.ComputeChase(self, toLeader.normalized, buffer, n, 2f, FlockWeights.Default);
                    a.transform.position += (Vector3)(vel * Time.fixedDeltaTime);
                }
            }

            // 동료끼리 비겹침
            float d = Vector2.Distance(comps[0].FlockPosition, comps[1].FlockPosition);
            // cleanup
            mgr.Unregister(la); Object.Destroy(leader.gameObject);
            foreach (var a in comps) { mgr.Unregister(a); Object.Destroy(a.gameObject); }
            Object.Destroy(mgr.gameObject);
            yield return null;

            Assert.Greater(d, 0.3f, "동료끼리 겹치지 않아야 함");
        }
    }
}
```

- [ ] **Step 2: 실행 (Editor 닫고)**

Run: `... -runTests -testPlatform PlayMode -testFilter "MonsterKitchen.Tests.PlayMode.CompanionSpawnTests" -testResults results.xml -batchmode`
Expected: PASS.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Tests/PlayMode/CompanionSpawnTests.cs
git commit -m "test(ai): companion flock separation playmode smoke"
```

---

## Task 14: 시각 검증 (수동)

- [ ] **Step 1: 전체 회귀**

Editor 닫고 EditMode + PlayMode 전체 실행 → 전부 PASS 확인.

- [ ] **Step 2: 던전 플레이 검증**

1. ManagementScene → 파티 편성 → 동료 2명 선택 → 저장.
2. 던전 포털 진입 → 동료 2명이 리더 주변에 스폰되는지(겹침 없이).
3. 이동 시 동료가 리더를 추종, 서로/리더와 안 겹침.
4. 적 접근 시 동료가 적으로 전환해 둘러싸며 공격.
5. 다른 씬 전환 시 동료 디스폰.

- [ ] **Step 3: WIP 업데이트 + 커밋**

`WORK_IN_PROGRESS.md` 동료 파티 슬라이스 완료 체크.
```bash
git add WORK_IN_PROGRESS.md
git commit -m "docs: WIP companion party slice complete"
```

---

## 완료 기준

- [ ] EditMode 전체 PASS (Party 저장/검증/결정 + 회귀).
- [ ] PlayMode 동료 비겹침 PASS.
- [ ] 던전 시각 검증: 동료 추종 + 적 둘러싸기 + 디스폰.
- [ ] 파티 선택이 ServerSaveData 에 저장/로드.
