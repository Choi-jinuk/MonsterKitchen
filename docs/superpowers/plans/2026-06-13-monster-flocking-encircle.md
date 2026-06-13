# 몬스터 군집 이동 & 둘러싸기 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 몬스터 추격 AI를 콜라이더 충돌이 아닌 군집(flocking) 스티어링으로 리워크하고, 플레이어 도착 시 한 점에 멈추지 않고 빈 공간으로 둘러싸게(encircle) 만든다.

**Architecture:** 순수 함수 `FlockSteering`(separation+arrival+tangential)이 desired velocity를 계산하고, `SpatialHashGrid`(순수) + `MonsterFlockManager`(MonoBehaviour 래퍼)가 O(n) 이웃 질의를 제공한다. BT 액션(`MoveToPlayer`/`Attack`)과 이동 컴포넌트(`ContinuousMovement`/`SlimeMovement`)가 이 결과를 소비한다. 콜라이더는 벽 전용.

**Tech Stack:** Unity 6 (6000.4.2f1), C# (MonoBehaviour + 순수 클래스), NUnit (EditMode/PlayMode), 기존 NavAgent A* 네비.

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Enemy/Movement/FlockWeights.cs` | 스티어링 가중치 struct + Default |
| `Assets/Scripts/Enemy/Movement/FlockSteering.cs` | 순수 스티어링 함수 (separation/chase/encircle) |
| `Assets/Scripts/Enemy/Movement/IMonsterFlockAgent.cs` | 군집 에이전트 인터페이스 |
| `Assets/Scripts/Enemy/Movement/SpatialHashGrid.cs` | 순수 공간 해시 그리드 (이웃 질의) |
| `Assets/Scripts/Enemy/MonsterFlockManager.cs` | 그리드 래핑 싱글톤 MonoBehaviour |
| `Assets/Scripts/Enemy/Movement/ContinuousMovement.cs` | 연속 속도 조향 이동 모델 |
| `Assets/Scripts/Enemy/BTMonsterController.cs` | IMonsterFlockAgent 구현, ApplySeparation 교체 |
| `Assets/Scripts/AI/BehaviorTree/Monster/BTAction_MoveToPlayer.cs` | ComputeChase 적용 |
| `Assets/Scripts/AI/BehaviorTree/Monster/BTAction_Attack.cs` | ComputeEncircle 적용, 푸시백 제거 |
| `Assets/Scripts/Enemy/Movement/SlimeMovement.cs` | dash 방향 스티어링 보정 |
| `Assets/Tests/EditMode/FlockSteeringTests.cs` | FlockSteering 단위 테스트 |
| `Assets/Tests/EditMode/SpatialHashGridTests.cs` | SpatialHashGrid 단위 테스트 |
| `Assets/Data/CSV/Monsters.csv` | 검증용 오크 MON_006 엔트리 |
| `Assets/Tests/PlayMode/FlockEncircleSmokeTests.cs` | 다수 스폰 둘러싸기 검증 |

---

## Task 1: FlockWeights struct

**Files:**
- Create: `Assets/Scripts/Enemy/Movement/FlockWeights.cs`

- [ ] **Step 1: 작성**

```csharp
namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  FlockWeights — 군집 스티어링 가중치/반경 설정값
    // ====================================================================
    [System.Serializable]
    public struct FlockWeights
    {
        public float SepWeight;
        public float SeekWeight;
        public float ArrivalWeight;
        public float TangentWeight;
        public float SepRadius;
        public float SlowRadius;
        public float RingBand;

        public static FlockWeights Default => new FlockWeights
        {
            SepWeight     = 2.0f,
            SeekWeight    = 1.0f,
            ArrivalWeight = 1.5f,
            TangentWeight = 1.2f,
            SepRadius     = 0.9f,
            SlowRadius    = 1.0f,
            RingBand      = 1.5f,
        };
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Editor 포커스 후 `read_console` (MCP) 또는 에디터 콘솔에서 컴파일 에러 0 확인.
Expected: 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Enemy/Movement/FlockWeights.cs Assets/Scripts/Enemy/Movement/FlockWeights.cs.meta
git commit -m "feat(enemy): add FlockWeights struct for steering config"
```

---

## Task 2: FlockSteering.Separation (TDD)

**Files:**
- Create: `Assets/Scripts/Enemy/Movement/FlockSteering.cs`
- Test: `Assets/Tests/EditMode/FlockSteeringTests.cs`

이웃은 `Vector2[]` 위치 배열(self 제외, 호출자가 채움)과 유효 개수 `count`로 전달한다. 배열 길이가 아닌 `count`만 순회 → 버퍼 재사용 시 할당 없음.

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using NUnit.Framework;
using UnityEngine;
using MonsterKitchen.Enemy;

namespace MonsterKitchen.Tests
{
    public class FlockSteeringTests
    {
        [Test]
        public void Separation_TwoOverlapping_PushesAway()
        {
            var self      = new Vector2(0f, 0f);
            var neighbors = new[] { new Vector2(0.3f, 0f) };  // 오른쪽에 이웃
            Vector2 sep   = FlockSteering.Separation(self, neighbors, 1, sepRadius: 0.9f, sepWeight: 2f);

            Assert.Less(sep.x, 0f, "이웃 반대(왼쪽)로 밀려야 함");
            Assert.AreEqual(0f, sep.y, 0.001f);
        }

        [Test]
        public void Separation_NoNeighbors_ReturnsZero()
        {
            Vector2 sep = FlockSteering.Separation(Vector2.zero, new Vector2[4], 0, 0.9f, 2f);
            Assert.AreEqual(Vector2.zero, sep);
        }

        [Test]
        public void Separation_NeighborOutsideRadius_Ignored()
        {
            var neighbors = new[] { new Vector2(5f, 0f) };  // 반경 밖
            Vector2 sep   = FlockSteering.Separation(Vector2.zero, neighbors, 1, 0.9f, 2f);
            Assert.AreEqual(Vector2.zero, sep);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform EditMode -testFilter "MonsterKitchen.Tests.FlockSteeringTests" -testResults results.xml -batchmode -quit`
Expected: 컴파일 실패 (FlockSteering 미정의).

- [ ] **Step 3: 최소 구현**

```csharp
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  FlockSteering — 순수 군집 스티어링 함수 (부수효과 없음, 테스트 가능)
    //
    //  ▶ neighbors: self 제외한 이웃 위치 배열. count 만큼만 유효.
    //  ▶ 모든 함수는 desired velocity(또는 force) 를 반환만 한다.
    // ====================================================================
    public static class FlockSteering
    {
        const float Epsilon = 0.0001f;

        /// <summary>역제곱 가중 separation force. 정규화 후 sepWeight 곱.</summary>
        public static Vector2 Separation(
            Vector2 selfPos, Vector2[] neighbors, int count, float sepRadius, float sepWeight)
        {
            Vector2 force      = Vector2.zero;
            float   sqrRadius  = sepRadius * sepRadius;

            for (int i = 0; i < count; i++)
            {
                Vector2 away   = selfPos - neighbors[i];
                float   sqrDst = away.sqrMagnitude;
                if (sqrDst > sqrRadius || sqrDst < Epsilon) continue;

                float dist = Mathf.Sqrt(sqrDst);
                force += (away / dist) / Mathf.Max(sqrDst, Epsilon);  // 역제곱
            }

            if (force.sqrMagnitude < Epsilon) return Vector2.zero;
            return force.normalized * sepWeight;
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: 위 EditMode 테스트 명령.
Expected: 3개 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Enemy/Movement/FlockSteering.cs Assets/Scripts/Enemy/Movement/FlockSteering.cs.meta Assets/Tests/EditMode/FlockSteeringTests.cs Assets/Tests/EditMode/FlockSteeringTests.cs.meta
git commit -m "feat(enemy): add FlockSteering.Separation (inverse-square)"
```

---

## Task 3: FlockSteering.ComputeChase (TDD)

**Files:**
- Modify: `Assets/Scripts/Enemy/Movement/FlockSteering.cs`
- Test: `Assets/Tests/EditMode/FlockSteeringTests.cs`

- [ ] **Step 1: 실패 테스트 추가**

```csharp
        [Test]
        public void ComputeChase_NoNeighbors_FollowsNavDir()
        {
            var w   = FlockWeights.Default;
            var nav = new Vector2(1f, 0f);
            Vector2 v = FlockSteering.ComputeChase(Vector2.zero, nav, new Vector2[4], 0, moveSpeed: 3f, w);

            Assert.Greater(v.x, 0f, "이웃 없으면 nav 방향으로 진행");
            Assert.LessOrEqual(v.magnitude, 3f + 0.001f, "moveSpeed 로 clamp");
        }

        [Test]
        public void ComputeChase_NeighborAhead_StillProgressesButSteers()
        {
            var w         = FlockWeights.Default;
            var nav       = new Vector2(1f, 0f);
            var neighbors = new[] { new Vector2(0.3f, 0.1f) };
            Vector2 v     = FlockSteering.ComputeChase(Vector2.zero, nav, neighbors, 1, 3f, w);

            Assert.LessOrEqual(v.magnitude, 3f + 0.001f);
            Assert.Less(v.y, 0f, "위쪽 이웃에서 멀어지는 성분");
        }
```

- [ ] **Step 2: 실패 확인**

Run: EditMode 테스트.
Expected: 컴파일 실패 (ComputeChase 미정의).

- [ ] **Step 3: 구현 추가 (FlockSteering 내부)**

```csharp
        /// <summary>추격: nav 방향 seek + separation. moveSpeed 로 clamp.</summary>
        public static Vector2 ComputeChase(
            Vector2 selfPos, Vector2 navDir, Vector2[] neighbors, int count,
            float moveSpeed, in FlockWeights w)
        {
            Vector2 seek       = navDir.sqrMagnitude > Epsilon ? navDir.normalized * w.SeekWeight : Vector2.zero;
            Vector2 separation = Separation(selfPos, neighbors, count, w.SepRadius, w.SepWeight);
            Vector2 desired    = seek + separation;
            return Vector2.ClampMagnitude(desired.normalized * moveSpeed, moveSpeed);
        }
```

- [ ] **Step 4: 통과 확인**

Run: EditMode 테스트.
Expected: 전체 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Enemy/Movement/FlockSteering.cs Assets/Tests/EditMode/FlockSteeringTests.cs
git commit -m "feat(enemy): add FlockSteering.ComputeChase (seek + separation)"
```

---

## Task 4: FlockSteering.ComputeEncircle (TDD)

**Files:**
- Modify: `Assets/Scripts/Enemy/Movement/FlockSteering.cs`
- Test: `Assets/Tests/EditMode/FlockSteeringTests.cs`

- [ ] **Step 1: 실패 테스트 추가**

```csharp
        [Test]
        public void ComputeEncircle_InsideRing_PushedOutward()
        {
            var w      = FlockWeights.Default;
            var player = Vector2.zero;
            var self   = new Vector2(0.3f, 0f);     // 링(1.0) 안쪽
            Vector2 v  = FlockSteering.ComputeEncircle(self, player, ringRadius: 1f, new Vector2[4], 0, 3f, w);

            Assert.Greater(v.x, 0f, "링 안쪽이면 바깥(플레이어 반대)으로");
        }

        [Test]
        public void ComputeEncircle_OutsideRing_PulledInward()
        {
            var w      = FlockWeights.Default;
            var player = Vector2.zero;
            var self   = new Vector2(3f, 0f);       // 링 밖
            Vector2 v  = FlockSteering.ComputeEncircle(self, player, 1f, new Vector2[4], 0, 3f, w);

            Assert.Less(v.x, 0f, "링 밖이면 안쪽(플레이어 방향)으로");
        }

        [Test]
        public void ComputeEncircle_OnRingWithCrowdedNeighbor_SlidesTangentially()
        {
            var w         = FlockWeights.Default;
            var player    = Vector2.zero;
            var self      = new Vector2(1f, 0f);                 // 링 위 (오른쪽)
            var neighbors = new[] { new Vector2(1f, 0.4f) };     // 같은 링, 위쪽에 밀집
            Vector2 v     = FlockSteering.ComputeEncircle(self, player, 1f, neighbors, 1, 3f, w);

            Assert.Less(v.y, 0f, "밀집한 위쪽 반대(아래)로 접선 미끄러짐");
            Assert.LessOrEqual(v.magnitude, 3f + 0.001f);
        }
```

- [ ] **Step 2: 실패 확인**

Run: EditMode 테스트.
Expected: 컴파일 실패 (ComputeEncircle 미정의).

- [ ] **Step 3: 구현 추가 (FlockSteering 내부)**

```csharp
        /// <summary>
        /// 둘러싸기: arrival(링 반경 유지) + tangential(빈 각도로 회전) + separation.
        /// </summary>
        public static Vector2 ComputeEncircle(
            Vector2 selfPos, Vector2 playerPos, float ringRadius, Vector2[] neighbors, int count,
            float moveSpeed, in FlockWeights w)
        {
            Vector2 toPlayer = playerPos - selfPos;
            float   dist     = toPlayer.magnitude;
            Vector2 dir      = dist > Epsilon ? toPlayer / dist : Vector2.right;

            // ── arrival: 링 반경 기준 안쪽이면 바깥, 밖이면 안쪽 ──
            float   toRing   = dist - ringRadius;                       // 양수=링 밖
            float   arrAmt   = Mathf.Clamp(toRing / Mathf.Max(w.SlowRadius, Epsilon), -1f, 1f);
            Vector2 arrival  = dir * (arrAmt * w.ArrivalWeight);        // dir=플레이어 방향

            // ── separation ──
            Vector2 separation = Separation(selfPos, neighbors, count, w.SepRadius, w.SepWeight);

            // ── tangential: 링 근처일수록 강하게, 이웃 밀집 반대쪽으로 회전 ──
            Vector2 tangent = new Vector2(-dir.y, dir.x);              // 플레이어 기준 접선
            float   side    = Vector2.Dot(separation, tangent);
            float   sign    = side >= 0f ? 1f : -1f;
            float   ringProx = 1f - Mathf.Clamp01(Mathf.Abs(toRing) / Mathf.Max(w.RingBand, Epsilon));
            Vector2 tangential = tangent * (sign * w.TangentWeight * ringProx);

            Vector2 desired = arrival + separation + tangential;
            if (desired.sqrMagnitude < Epsilon) return Vector2.zero;
            return Vector2.ClampMagnitude(desired.normalized * moveSpeed, moveSpeed);
        }
```

- [ ] **Step 4: 통과 확인**

Run: EditMode 테스트.
Expected: 전체 PASS.

> 참고: tangential 부호는 `Dot(separation, tangent)` 기반. 밀집 이웃이 separation을 밀집 반대 방향으로 만들고, 그 접선 성분 부호로 회전 방향 결정. 위쪽(+y) 밀집 → separation.y<0 → tangent=(0,1) 와 Dot<0 → sign=-1 → tangential.y<0 (아래로). 테스트 일치.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Enemy/Movement/FlockSteering.cs Assets/Tests/EditMode/FlockSteeringTests.cs
git commit -m "feat(enemy): add FlockSteering.ComputeEncircle (arrival + tangential)"
```

---

## Task 5: IMonsterFlockAgent 인터페이스

**Files:**
- Create: `Assets/Scripts/Enemy/Movement/IMonsterFlockAgent.cs`

- [ ] **Step 1: 작성**

```csharp
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    /// <summary>
    /// 군집 매니저에 등록되는 에이전트. 위치 질의용 최소 인터페이스.
    /// </summary>
    public interface IMonsterFlockAgent
    {
        Vector2 FlockPosition { get; }
        Transform FlockTransform { get; }  // self 제외 식별용
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Expected: 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Enemy/Movement/IMonsterFlockAgent.cs Assets/Scripts/Enemy/Movement/IMonsterFlockAgent.cs.meta
git commit -m "feat(enemy): add IMonsterFlockAgent interface"
```

---

## Task 6: SpatialHashGrid (TDD)

**Files:**
- Create: `Assets/Scripts/Enemy/Movement/SpatialHashGrid.cs`
- Test: `Assets/Tests/EditMode/SpatialHashGridTests.cs`

순수 클래스 — 위치만 인덱싱하고 인접 9셀에서 반경 내 이웃 위치를 버퍼에 채운다.

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using NUnit.Framework;
using UnityEngine;
using MonsterKitchen.Enemy;

namespace MonsterKitchen.Tests
{
    public class SpatialHashGridTests
    {
        [Test]
        public void Query_FindsNeighborInSameCell()
        {
            var grid = new SpatialHashGrid(cellSize: 1f);
            grid.Clear();
            grid.Insert(new Vector2(0.1f, 0.1f), id: 1);
            grid.Insert(new Vector2(0.2f, 0.2f), id: 2);

            var buffer = new Vector2[8];
            int count  = grid.Query(new Vector2(0.1f, 0.1f), radius: 0.9f, excludeId: 1, buffer);

            Assert.AreEqual(1, count);
            Assert.AreEqual(new Vector2(0.2f, 0.2f), buffer[0]);
        }

        [Test]
        public void Query_ExcludesSelf()
        {
            var grid = new SpatialHashGrid(1f);
            grid.Clear();
            grid.Insert(Vector2.zero, id: 99);

            var buffer = new Vector2[8];
            int count  = grid.Query(Vector2.zero, 0.9f, excludeId: 99, buffer);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Query_FindsNeighborInAdjacentCell()
        {
            var grid = new SpatialHashGrid(1f);
            grid.Clear();
            grid.Insert(new Vector2(0.9f, 0f), id: 1);   // cell (0,0)
            grid.Insert(new Vector2(1.1f, 0f), id: 2);   // cell (1,0) 인접

            var buffer = new Vector2[8];
            int count  = grid.Query(new Vector2(0.9f, 0f), 0.5f, excludeId: 1, buffer);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Query_RespectsBufferCapacity()
        {
            var grid = new SpatialHashGrid(1f);
            grid.Clear();
            for (int i = 0; i < 10; i++) grid.Insert(new Vector2(0.05f * i, 0f), id: i + 100);

            var buffer = new Vector2[3];
            int count  = grid.Query(Vector2.zero, 0.9f, excludeId: -1, buffer);

            Assert.LessOrEqual(count, 3, "버퍼 용량 초과 금지");
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `... -testFilter "MonsterKitchen.Tests.SpatialHashGridTests" ...`
Expected: 컴파일 실패.

- [ ] **Step 3: 구현**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  SpatialHashGrid — 순수 2D 공간 해시 (이웃 위치 질의)
    //
    //  ▶ Insert 로 위치+id 등록 → Query 로 인접 9셀 반경 내 위치를 버퍼에 채움.
    //  ▶ id 는 self 제외용 (보통 GetInstanceID()).
    //  ▶ 매 프레임 Clear → Insert* → Query* 패턴. 할당은 셀 리스트 재사용으로 최소화.
    // ====================================================================
    public sealed class SpatialHashGrid
    {
        struct Entry { public Vector2 Pos; public int Id; }

        readonly float m_CellSize;
        readonly Dictionary<long, List<Entry>> m_Cells = new Dictionary<long, List<Entry>>(256);
        readonly Stack<List<Entry>>            m_Pool  = new Stack<List<Entry>>();

        public SpatialHashGrid(float cellSize) => m_CellSize = Mathf.Max(0.01f, cellSize);

        static long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

        int CellCoord(float v) => Mathf.FloorToInt(v / m_CellSize);

        public void Clear()
        {
            foreach (var kv in m_Cells) { kv.Value.Clear(); m_Pool.Push(kv.Value); }
            m_Cells.Clear();
        }

        public void Insert(Vector2 pos, int id)
        {
            long key = Key(CellCoord(pos.x), CellCoord(pos.y));
            if (!m_Cells.TryGetValue(key, out var list))
            {
                list = m_Pool.Count > 0 ? m_Pool.Pop() : new List<Entry>(8);
                m_Cells[key] = list;
            }
            list.Add(new Entry { Pos = pos, Id = id });
        }

        /// <summary>반경 내 이웃 위치를 buffer 에 채우고 개수 반환. excludeId 는 제외.</summary>
        public int Query(Vector2 pos, float radius, int excludeId, Vector2[] buffer)
        {
            int   cx        = CellCoord(pos.x);
            int   cy        = CellCoord(pos.y);
            float sqrRadius = radius * radius;
            int   count     = 0;

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!m_Cells.TryGetValue(Key(cx + dx, cy + dy), out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    if (e.Id == excludeId)                       continue;
                    if ((e.Pos - pos).sqrMagnitude > sqrRadius)  continue;
                    if (count >= buffer.Length)                  return count;
                    buffer[count++] = e.Pos;
                }
            }
            return count;
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: SpatialHashGrid 테스트.
Expected: 4개 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Enemy/Movement/SpatialHashGrid.cs Assets/Scripts/Enemy/Movement/SpatialHashGrid.cs.meta Assets/Tests/EditMode/SpatialHashGridTests.cs Assets/Tests/EditMode/SpatialHashGridTests.cs.meta
git commit -m "feat(enemy): add SpatialHashGrid for neighbor queries"
```

---

## Task 7: MonsterFlockManager (싱글톤 래퍼)

**Files:**
- Create: `Assets/Scripts/Enemy/MonsterFlockManager.cs`

MonoBehaviour 래퍼. 첫 Register 시 자동 부트스트랩(lazy). 매 FixedUpdate 그리드 재빌드. (DungeonScene 에만 필요하므로 씬 사전 배치 부담 회피.)

- [ ] **Step 1: 작성**

```csharp
using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  MonsterFlockManager — 군집 이웃 질의 싱글톤
    //
    //  ▶ 등록된 IMonsterFlockAgent 를 매 FixedUpdate SpatialHashGrid 에 재배치.
    //  ▶ QueryNeighbors 로 self 제외 반경 내 이웃 위치를 버퍼에 채운다.
    //  ▶ 첫 Register 시 lazy 부트스트랩 — 씬 사전 배치 불필요.
    // ====================================================================
    public sealed class MonsterFlockManager : MonoBehaviour
    {
        public static MonsterFlockManager Instance { get; private set; }

        const float CellSize = 0.9f;  // = 기본 SepRadius

        readonly List<IMonsterFlockAgent> m_Agents = new List<IMonsterFlockAgent>(128);
        SpatialHashGrid m_Grid;

        public static MonsterFlockManager GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[MonsterFlockManager]");
            return Instance = go.AddComponent<MonsterFlockManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            m_Grid   = new SpatialHashGrid(CellSize);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Register(IMonsterFlockAgent agent)
        {
            if (agent == null || m_Agents.Contains(agent)) return;
            m_Agents.Add(agent);
        }

        public void Unregister(IMonsterFlockAgent agent) => m_Agents.Remove(agent);

        void FixedUpdate()
        {
            if (m_Grid == null) return;
            m_Grid.Clear();
            for (int i = 0; i < m_Agents.Count; i++)
            {
                var a = m_Agents[i];
                if (a?.FlockTransform == null) continue;
                m_Grid.Insert(a.FlockPosition, a.FlockTransform.GetInstanceID());
            }
        }

        /// <summary>self 제외 반경 내 이웃 위치를 buffer 에 채우고 개수 반환.</summary>
        public int QueryNeighbors(Vector2 pos, float radius, Transform self, Vector2[] buffer)
        {
            if (m_Grid == null) return 0;
            int excludeId = self != null ? self.GetInstanceID() : -1;
            return m_Grid.Query(pos, radius, excludeId, buffer);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Expected: 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Enemy/MonsterFlockManager.cs Assets/Scripts/Enemy/MonsterFlockManager.cs.meta
git commit -m "feat(enemy): add MonsterFlockManager singleton (spatial hash wrapper)"
```

---

## Task 8: BTMonsterController — IMonsterFlockAgent 구현 + ApplySeparation 교체

**Files:**
- Modify: `Assets/Scripts/Enemy/BTMonsterController.cs`

기존 `Physics2D.OverlapCircle` 기반 `ApplySeparation` 을 매니저 질의 + `FlockSteering.Separation` 으로 교체. 등록/해제 추가. `m_SepBuffer`(Collider2D[]) 제거, `Vector2[]` 버퍼로 교체.

- [ ] **Step 1: 필드 교체**

`BTMonsterController.cs` 의 Separation 관련 필드 블록을 교체.

기존:
```csharp
        [Header("Separation Steering")]
        [SerializeField] float     m_SeparationRadius = 0.9f;
        [SerializeField] float     m_SeparationWeight = 2.0f;
        [SerializeField] LayerMask m_MonsterLayer;
```
→
```csharp
        [Header("Flock Steering")]
        [SerializeField] FlockWeights m_FlockWeights = FlockWeights.Default;

        public FlockWeights FlockWeights => m_FlockWeights;
```

기존 버퍼:
```csharp
        readonly Collider2D[] m_SepBuffer = new Collider2D[12];
```
→
```csharp
        readonly Vector2[] m_NeighborBuffer = new Vector2[16];
        public Vector2[] NeighborBuffer => m_NeighborBuffer;
```

`m_FlockWeights` 가 `FlockWeights.Default` 로 초기화되도록 `OnValidate` 에서 SepRadius<=0 등 비정상값 clamp:
```csharp
        void OnValidate()
        {
            if (m_FlockWeights.SepRadius <= 0f)  m_FlockWeights = FlockWeights.Default;
        }
```

- [ ] **Step 2: 클래스 선언에 인터페이스 추가**

기존:
```csharp
    public class BTMonsterController : MonsterBase, IBTBlackboardInitializer, IMonsterSeparation
```
→
```csharp
    public class BTMonsterController : MonsterBase, IBTBlackboardInitializer, IMonsterSeparation, IMonsterFlockAgent
```

- [ ] **Step 3: IMonsterFlockAgent 구현 + 등록/해제 추가**

클래스 본문에 추가 (예: 공개 API 영역):
```csharp
        // ── IMonsterFlockAgent ──────────────────────────────────────────
        public Vector2   FlockPosition  => transform.position;
        public Transform FlockTransform => transform;

        protected override void OnEnable()
        {
            base.OnEnable();
            MonsterFlockManager.GetOrCreate().Register(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            MonsterFlockManager.Instance?.Unregister(this);
        }
```

> `MonsterBase.OnEnable/OnDisable` 은 `protected virtual` 이므로 base 호출 필수.

- [ ] **Step 4: ApplySeparation 교체**

기존 메서드 전체:
```csharp
        public Vector2 ApplySeparation(Vector2 desiredDir)
        {
            int count = Physics2D.OverlapCircle(...);
            ... (OverlapCircle + perpendicular slide)
        }
```
→
```csharp
        public Vector2 ApplySeparation(Vector2 desiredDir)
        {
            var mgr = MonsterFlockManager.Instance;
            if (mgr == null) return desiredDir;

            int count = mgr.QueryNeighbors(
                transform.position, m_FlockWeights.SepRadius, transform, m_NeighborBuffer);
            if (count == 0) return desiredDir;

            Vector2 sep = FlockSteering.Separation(
                transform.position, m_NeighborBuffer, count,
                m_FlockWeights.SepRadius, m_FlockWeights.SepWeight);

            Vector2 blended = desiredDir.normalized + sep;
            return blended.sqrMagnitude > 0.0001f ? blended.normalized : desiredDir;
        }
```

- [ ] **Step 5: 컴파일 확인 + 기존 EditMode 회귀**

Unity 콘솔 에러 0. 기존 EditMode 전체 PASS.
Run: `... -testPlatform EditMode -testResults results.xml -batchmode -quit`
Expected: 전체 PASS (회귀 없음).

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Enemy/BTMonsterController.cs
git commit -m "refactor(enemy): replace OverlapCircle separation with flock manager query"
```

---

## Task 9: ContinuousMovement 이동 모델

**Files:**
- Create: `Assets/Scripts/Enemy/Movement/ContinuousMovement.cs`

영상의 footman/orc 형 연속 속도 조향. `TickChase` 에서 `BTAction_MoveToPlayer` 가 위임. 단순 seek+separation 은 `ApplySeparation` 경유, encircle 은 Attack 액션이 직접 구동(Task 11).

- [ ] **Step 1: 작성**

```csharp
using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  ContinuousMovement — 연속 속도 조향 이동 (footman/orc 형)
    //
    //  ▶ TickChase: nav 경로 방향 → ApplySeparation 보정 → StepMove 로 이동.
    //  ▶ 슬라임의 dash 사이클과 달리 매 틱 연속 이동.
    // ====================================================================
    public class ContinuousMovement : MonsterMovementBase
    {
        public override void OnSpawned() { }

        public override void ResetMovement()
        {
            Stop();
            ResetNavState();
        }

        public override void TickPatrol(float dt, Vector2 patrolTarget)
        {
            Vector2 navDir = GetNavDirection(patrolTarget);
            if (navDir == Vector2.zero) { Stop(); return; }
            StepMove(navDir, m_MoveSpeed, dt);
        }

        public override void TickChase(float dt, Vector2 playerPos, float preferredDistance)
        {
            Vector2 navDir = GetNavDirection(playerPos);
            if (navDir == Vector2.zero) { Stop(); return; }

            Vector2 steer = m_Sep != null ? m_Sep.ApplySeparation(navDir) : navDir;
            StepMove(steer, m_MoveSpeed, dt);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Expected: 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Enemy/Movement/ContinuousMovement.cs Assets/Scripts/Enemy/Movement/ContinuousMovement.cs.meta
git commit -m "feat(enemy): add ContinuousMovement model"
```

---

## Task 10: BTAction_MoveToPlayer — ringRadius 도달까지 추격

**Files:**
- Modify: `Assets/Scripts/AI/BehaviorTree/Monster/BTAction_MoveToPlayer.cs:50-57`

`prefDist` 를 ring 도달 판정으로 사용(현 동작 유지). 이동 위임은 그대로 `TickChase`. 변경 핵심은 의미 명확화 — 추가 코드 변경 없음(이미 `TickChase` 위임). 단, 폴백 직선 이동에서 `ApplySeparation` 이 매니저 경유로 동작하는지 확인.

- [ ] **Step 1: 폴백 분기 확인 (변경 없음 검증)**

`BTAction_MoveToPlayer.Execute` 의 폴백(66-75행)에서 `s.Ctrl.ApplySeparation(dir)` 호출이 Task 8 의 새 구현을 타는지 확인. 별도 코드 변경 불필요.

- [ ] **Step 2: 컴파일/회귀 확인**

이 Task는 코드 변경 없음 — Task 8/9 통합으로 동작 변경됨. EditMode 회귀 PASS 확인 후 다음 Task로.

> 변경 없음 Task. 의미 문서화 목적. 실제 추격 이동은 ContinuousMovement.TickChase 가 담당.

---

## Task 11: BTAction_Attack — 둘러싸기(encircle) 적용

**Files:**
- Modify: `Assets/Scripts/AI/BehaviorTree/Monster/BTAction_Attack.cs:59-67`

사거리 내에서 속도 0/푸시백 대신 `FlockSteering.ComputeEncircle` 결과로 이동. 공격하면서 링 주위를 돌며 빈칸 채움.

- [ ] **Step 1: 이동 로직 교체**

기존:
```csharp
            // 너무 붙으면 살짝 밀어내며 공격 유지
            if (s.Ctrl != null)
            {
                float moveSpeed = ctx.Blackboard.Get<float>("MoveSpeed");
                s.Ctrl.Rb.linearVelocity = dist < prefDist * 0.5f
                    ? s.Ctrl.ApplySeparation(-toPlayer) * (moveSpeed * 0.5f)
                    : Vector2.zero;
            }
```
→
```csharp
            // 둘러싸기: 멈추지 않고 링 주위 빈칸을 향해 미끄러지며 공격 유지
            if (s.Ctrl != null)
            {
                float moveSpeed = ctx.Blackboard.Get<float>("MoveSpeed");
                var   mgr       = MonsterKitchen.Enemy.MonsterFlockManager.Instance;
                var   weights   = s.Ctrl.FlockWeights;

                int count = mgr != null
                    ? mgr.QueryNeighbors(ctx.Owner.transform.position, weights.SepRadius,
                                         ctx.Owner.transform, s.Ctrl.NeighborBuffer)
                    : 0;

                Vector2 encircle = MonsterKitchen.Enemy.FlockSteering.ComputeEncircle(
                    ctx.Owner.transform.position, player.position, prefDist,
                    s.Ctrl.NeighborBuffer, count, moveSpeed, weights);

                s.Ctrl.Rb.linearVelocity = encircle;
            }
```

- [ ] **Step 2: 컴파일 확인**

Expected: 에러 없음. `BTMonsterController.FlockWeights` / `.NeighborBuffer` (Task 8 의 공개 프로퍼티) 접근 가능 확인.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/AI/BehaviorTree/Monster/BTAction_Attack.cs
git commit -m "feat(enemy): encircle steering while attacking (no hard stop)"
```

---

## Task 12: SlimeMovement — 조향된 dash

**Files:**
- Modify: `Assets/Scripts/Enemy/Movement/SlimeMovement.cs:148-156`

윈드업 시점 dash 방향을 nav 방향 단독이 아닌 `ApplySeparation` 보정 결과로 잠근다. dash 사이클·벽 반사 로직은 유지. (dash 중 separation 은 기존대로 116-118행에서 이미 적용됨 — Task 8 으로 매니저 경유 동작.)

- [ ] **Step 1: 윈드업 방향 잠금 보정**

기존:
```csharp
                var navDir = GetNavDirection(target);
                if (navDir == Vector2.zero)
                {
                    m_DashCoolTimer = 0.3f; // 경로 미확보 시 짧게 재시도
                    Stop();
                    return;
                }
                m_DashDir         = navDir;
```
→
```csharp
                var navDir = GetNavDirection(target);
                if (navDir == Vector2.zero)
                {
                    m_DashCoolTimer = 0.3f; // 경로 미확보 시 짧게 재시도
                    Stop();
                    return;
                }
                // 군집 보정: 이웃 밀집 시 dash 방향을 빈쪽으로 조향
                m_DashDir         = m_Sep != null ? m_Sep.ApplySeparation(navDir) : navDir;
```

- [ ] **Step 2: 컴파일 확인**

Expected: 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Enemy/Movement/SlimeMovement.cs
git commit -m "feat(enemy): steer slime dash direction via flock separation"
```

---

## Task 13: 검증용 오크 몬스터 (MON_006) — 데이터/프리팹/BT

**Files:**
- Modify: `Assets/Data/CSV/Monsters.csv`
- Create (Editor 작업): 프리팹 `prefab/monster/1006`, BTAsset `bt/monster/1006`, AssetManifest 등록

연속 이동 검증용. 기존 슬라임 프리팹을 복제해 `SlimeMovement` → `ContinuousMovement` 교체.

- [ ] **Step 1: CSV 행 추가**

`Assets/Data/CSV/Monsters.csv` 마지막 행 뒤에 추가:
```
MON_006,1006,,,50,7,3,3,None,Common,5001,SGD_101,false,false,false,prefab/monster/1006,sprite/monster/1006,bt/monster/1006
```
> MoveSpeed=3 (연속 이동이라 슬라임 dash 의 10 보다 낮게). DropTable/SkillGroup 은 기존 재사용.

- [ ] **Step 2: SO 동기화**

Unity 메뉴 → `DataManagerWindow` 열기 → Sync 실행 → `TableData.asset` 에 MON_006 반영 확인.

- [ ] **Step 3: 프리팹 생성**

기존 슬라임 프리팹(`prefab/monster/1001`) 복제 → 이름 `Monster_1006`:
- `SlimeMovement` 컴포넌트 제거 → `ContinuousMovement` 추가.
- `BTMonsterController` 의 `m_FlockWeights` 기본값 확인.
- 몬스터 간 콜라이더 충돌 OFF: Rigidbody2D + Collider2D 는 유지하되 Layer 매트릭스에서 Enemy↔Enemy 충돌 해제(또는 Collider `isTrigger=true`).
- AssetManifest 에 `prefab/monster/1006`, `sprite/monster/1006`, `bt/monster/1006` 키 등록.

- [ ] **Step 4: BTAsset 생성**

`bt/monster/1001` BTAsset 복제 → `bt/monster/1006`. 구조 동일(Selector: Attack / MoveToPlayer / Patrol). AssetManifest 등록.

- [ ] **Step 5: 콜라이더 충돌 매트릭스 확인**

Project Settings → Physics 2D → Layer Collision Matrix: Enemy↔Enemy 체크 해제. 벽(Obstacle)↔Enemy 는 유지.
> 이로써 "콜라이더 겹침 방지 아닌 군집" 요구 충족 — 몬스터 간 물리 충돌 0.

- [ ] **Step 6: 씬 저장 + 커밋**

```bash
git add Assets/Data/CSV/Monsters.csv Assets/Data/SO/TableData.asset
git add Assets/Prefabs Assets/Settings
git commit -m "feat(enemy): add MON_006 orc test monster (continuous movement)"
```
> 프리팹/BTAsset/Manifest 경로는 프로젝트 실제 위치에 맞춰 add.

---

## Task 14: PlayMode 둘러싸기 검증

**Files:**
- Create: `Assets/Tests/PlayMode/FlockEncircleSmokeTests.cs`

다수 오크를 정지 타겟 주위에 스폰 → N초 후 (1) 평균 최근접 이웃거리 ≥ SepRadius*0.8 (겹침 해소), (2) 타겟 주위 각도 분포 검증.

- [ ] **Step 1: 테스트 작성**

```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MonsterKitchen.Enemy;

namespace MonsterKitchen.Tests.PlayMode
{
    public class FlockEncircleSmokeTests
    {
        // 단순 FlockManager + 더미 에이전트로 겹침 해소만 검증 (씬 비의존).
        sealed class DummyAgent : MonoBehaviour, IMonsterFlockAgent
        {
            public Vector2   FlockPosition  => transform.position;
            public Transform FlockTransform => transform;
        }

        [UnityTest]
        public IEnumerator Agents_SeparateOverTime()
        {
            var mgr = MonsterFlockManager.GetOrCreate();

            var agents = new List<DummyAgent>();
            for (int i = 0; i < 20; i++)
            {
                var go = new GameObject($"dummy{i}");
                go.transform.position = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f));
                var a = go.AddComponent<DummyAgent>();
                mgr.Register(a);
                agents.Add(a);
            }

            // separation 적용 루프 (간이): 매 프레임 이웃 반대로 약간 이동
            var buffer = new Vector2[16];
            float t = 0f;
            while (t < 2f)
            {
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                foreach (var a in agents)
                {
                    int count = mgr.QueryNeighbors(a.FlockPosition, 0.9f, a.FlockTransform, buffer);
                    Vector2 sep = FlockSteering.Separation(a.FlockPosition, buffer, count, 0.9f, 2f);
                    a.transform.position += (Vector3)(sep * Time.fixedDeltaTime);
                }
            }

            // 평균 최근접 이웃거리 검증
            float sumMin = 0f;
            foreach (var a in agents)
            {
                float min = float.MaxValue;
                foreach (var b in agents)
                {
                    if (a == b) continue;
                    float d = Vector2.Distance(a.FlockPosition, b.FlockPosition);
                    if (d < min) min = d;
                }
                sumMin += min;
            }
            float avgMin = sumMin / agents.Count;

            // cleanup
            foreach (var a in agents) Object.Destroy(a.gameObject);

            Assert.Greater(avgMin, 0.3f, "2초 후 평균 최근접 이웃거리가 겹침 수준보다 커야 함");
        }
    }
}
```

- [ ] **Step 2: 실행**

Run: `"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform PlayMode -testFilter "MonsterKitchen.Tests.PlayMode.FlockEncircleSmokeTests" -testResults results.xml -batchmode -quit`
Expected: PASS.

- [ ] **Step 3: 수동 시각 검증 (DungeonScene)**

DungeonScene PlayMode → SpawnManager 로 오크 다수 스폰 → 플레이어 추격 시 (1) 겹치지 않고, (2) 한 점에 안 멈추고 둘러싸는지 육안 확인. 영상의 hex 패킹/링 형성과 대조.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Tests/PlayMode/FlockEncircleSmokeTests.cs Assets/Tests/PlayMode/FlockEncircleSmokeTests.cs.meta
git commit -m "test(enemy): PlayMode flock separation smoke test"
```

---

## 완료 기준

- [ ] EditMode 전체 PASS (FlockSteering, SpatialHashGrid + 기존 회귀).
- [ ] PlayMode separation smoke PASS.
- [ ] DungeonScene 수동 검증: 겹침 0 + 둘러싸기 형성 (영상 대조).
- [ ] 몬스터 간 콜라이더 충돌 OFF, 벽 충돌만 유지.
- [ ] `WORK_IN_PROGRESS.md` 완료 체크 업데이트.
