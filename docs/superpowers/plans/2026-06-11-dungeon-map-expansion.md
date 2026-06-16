# 던전 맵 확장 구현 계획 (허브 + 3갈래)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 20×16 단일 구역 던전을 60×48 허브+3갈래 깊이 진행형 맵으로 확장 (스펙: `docs/superpowers/specs/2026-06-11-dungeon-map-expansion-design.md`)

**Architecture:** 타일맵 기반 — 에디터 빌더 스크립트가 바닥/벽 타일을 절차 페인트 (NavGrid·TilemapCollider2D가 내비/물리 동시 처리). 신규 컴포넌트 2개 (`DungeonGate` 하드 게이트, `DungeonZoneTrigger` 구역 피드백). 스폰/노드 데이터는 CSV 행 추가만.

**Tech Stack:** Unity 6 URP 2D, Tilemap, UnityMCP (씬 편집), NUnit (EditMode/PlayMode)

**커밋 규칙:** 이 프로젝트는 사용자 명시 요청 시에만 커밋한다. 플랜의 커밋 단계는 생략 — 각 Task 끝의 검증 단계로 대체.

---

## 핵심 전제 (실행 전 확인)

NavGrid 는 타일맵 기반이다 (`NavGrid.cs`: walkable = floor 타일 존재 AND wall 타일 없음).
현재 DungeonScene 의 `DungeonGrid` 아래 Floor/Wall 타일맵 상태를 먼저 확인할 것.
**Task 5 빌더가 타일을 새로 페인트하므로 기존 타일은 전부 지워도 된다.**
기존 `RoomBoundaries` BoxCollider2D 벽은 Wall 타일맵 + TilemapCollider2D 로 대체된다.

## 맵 레이아웃 정의 (셀 좌표, RectInt(xMin, yMin, w, h))

| 구역 | RectInt | 셀 범위 |
|---|---|---|
| 허브 | (-8, -23, 16, 13) | x -8..7, y -23..-11 |
| 숲 통로 | (-13, -17, 5, 5) | x -13..-9, y -17..-13 |
| 숲 갈래 | (-29, -17, 16, 16) | x -29..-14, y -17..-2 |
| 암석 통로 | (8, -17, 5, 5) | x 8..12, y -17..-13 |
| 암석 갈래 | (13, -17, 16, 16) | x 13..28, y -17..-2 |
| 심층 통로 | (-2, -11, 4, 4) | x -2..1, y -11..-8 |
| 심층 바깥 | (-12, -8, 24, 16) | x -12..11, y -8..7 |
| 게이트 통로 | (-2, 8, 4, 3) | x -2..1, y 8..10 |
| 심층 안쪽 | (-12, 11, 24, 12) | x -12..11, y 11..22 |

- 바닥 = 위 전체 union. 벽 = 바닥에 인접한 비바닥 셀 1겹 링.
- **게이트 통로는 바닥 + 벽 타일 동시 페인트** (봉인 상태) — DungeonGate 파괴 시 벽 타일 제거.
- 월드 바운드: `(-30, -24, 60, 48)`.

---

### Task 1: CSV 데이터 추가 + SO 동기화

**Files:**
- Modify: `Assets/Data/CSV/DungeonSpawnTables.csv`
- Modify: `Assets/Data/CSV/ResourceNodes.csv`

- [ ] **Step 1: DungeonSpawnTables.csv 에 4행 추가** (기존 DST_002 행 아래)

```csv
DST_003,6003,1001:4:1.5
DST_004,6004,1002:3:1.5|1003:1:1.0
DST_005,6005,1003:2:1.5|1004:2:1.5
DST_006,6006,1005:1:0.5
```

- [ ] **Step 2: ResourceNodes.csv 에 1행 추가** (RNO_003 아래, 컬럼 순서: _key,Id,NameKey,DisplayName,NodeType,DropIngredientId,DropMin,DropMax,MaxHp,RespawnSeconds,NodeSpriteAddress)

```csv
RNO_004,7004,,희귀 광석,Ore,2007,1,2,80,240,
```

- [ ] **Step 3: SO 동기화** — UnityMCP `execute_menu_item` 으로 `MonsterKitchen/Sync All SO` 실행
- [ ] **Step 4: 검증** — `read_console` 에러 0 + `Assets/Data/SO` 에 6003~6006/7004 반영 확인 (manage_scriptable_object read 또는 콘솔 Sync 로그)

---

### Task 2: DungeonGate — EditMode 테스트 먼저 (TDD)

**Files:**
- Create: `Assets/Tests/EditMode/DungeonGateLogicTests.cs`
- Create: `Assets/Scripts/Dungeon/DungeonGate.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;
using MonsterKitchen.Dungeon;

namespace MonsterKitchen.Tests
{
    public class DungeonGateLogicTests
    {
        [Test]
        public void ComputeGateDamage_ToolLevelBelowRequired_ReturnsZero()
        {
            Assert.AreEqual(0, DungeonGate.ComputeGateDamage(50, toolLevel: 1, requiredLevel: 2));
        }

        [Test]
        public void ComputeGateDamage_ToolLevelMeetsRequired_ReturnsDamage()
        {
            Assert.AreEqual(50, DungeonGate.ComputeGateDamage(50, toolLevel: 2, requiredLevel: 2));
        }

        [Test]
        public void ComputeGateDamage_ToolLevelAboveRequired_ReturnsDamage()
        {
            Assert.AreEqual(50, DungeonGate.ComputeGateDamage(50, toolLevel: 5, requiredLevel: 2));
        }

        [Test]
        public void ComputeGateDamage_ZeroDamageWithValidLevel_ClampsToOne()
        {
            Assert.AreEqual(1, DungeonGate.ComputeGateDamage(0, toolLevel: 2, requiredLevel: 2));
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인** — `run_tests`(EditMode, group `MonsterKitchen.Tests.DungeonGateLogicTests`). 기대: 컴파일 에러 (DungeonGate 미존재)

- [ ] **Step 3: DungeonGate.cs 구현**

```csharp
using MonsterKitchen.Core;
using MonsterKitchen.Navigation;
using MonsterKitchen.UI;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonGate — 심층 보스 구역 하드 게이트
    //
    //  ▶ 도구 공격력 레벨(PlayerUpgradeData.ToolDamageLevel)이
    //    m_RequiredToolLevel 미만이면 데미지 0 + 안내 토스트.
    //  ▶ 내구도 0 → 파괴: 스프라이트/콜라이더 비활성 +
    //    m_WallTilemap 의 m_SealedArea 벽 타일 제거 + NavGrid 재베이크.
    //  ▶ 세션 한정 — 씬 리로드 시 자연 복원 (벽 타일은 씬에 저장된 상태).
    //  ▶ Awake 에서 ResourceNode 레이어 자동 배정 — PlayerController
    //    TryHarvestNode 의 OverlapCircle 에 함께 잡힌다.
    // ====================================================================

    [RequireComponent(typeof(SpriteRenderer))]
    public class DungeonGate : MonoBehaviour
    {
        [Header("Gate 설정")]
        [SerializeField] int m_RequiredToolLevel = 2;
        [SerializeField] int m_MaxHp            = 100;

        [Header("봉인 타일 — Inspector 직접 연결")]
        [SerializeField] Tilemap    m_WallTilemap;
        [SerializeField] BoundsInt  m_SealedArea;   // 게이트 통로 셀 영역

        int  m_CurrentHp;
        bool m_Destroyed;

        public bool IsDestroyed       => m_Destroyed;
        public int  CurrentHp         => m_CurrentHp;
        public int  RequiredToolLevel => m_RequiredToolLevel;

        // ── 순수 로직 (EditMode 테스트 대상) ─────────────────────────

        /// <summary>도구 레벨이 요구치 미만이면 0, 충족하면 최소 1 데미지.</summary>
        public static int ComputeGateDamage(int damage, int toolLevel, int requiredLevel)
        {
            if (toolLevel < requiredLevel) return 0;
            return Mathf.Max(1, damage);
        }

        // ── Mono ─────────────────────────────────────────────────────

        void Awake()
        {
            m_CurrentHp = m_MaxHp;

            int layer = LayerMask.NameToLayer("ResourceNode");
            if (layer >= 0) gameObject.layer = layer;
        }

        // ── 공격 수신 (PlayerController.TryHarvestNode 에서 호출) ────

        public void TakeGateDamage(int damage)
        {
            if (m_Destroyed) return;

            int toolLevel = Core.PlayerDataManager.Instance?.Upgrades?.ToolDamageLevel ?? 0;
            int effective = ComputeGateDamage(damage, toolLevel, m_RequiredToolLevel);

            if (effective <= 0)
            {
                GameHUD.Instance?.ShowNotification(
                    $"단단한 바위다 — 더 강한 도구가 필요하다 (도구 공격력 Lv{m_RequiredToolLevel})", 2.5f);
                return;
            }

            m_CurrentHp -= effective;
            if (m_CurrentHp <= 0) DestroyGate();
        }

        // ── 파괴 ─────────────────────────────────────────────────────

        void DestroyGate()
        {
            m_Destroyed = true;

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            ClearSealedTiles();
            NavGrid.Instance?.Bake();

            GameHUD.Instance?.ShowNotification("길이 열렸다!", 2f);
            DebugUtil.Log("[DungeonGate] 게이트 파괴 — 심층 안쪽 개방.");
        }

        void ClearSealedTiles()
        {
            if (m_WallTilemap == null)
            {
                DebugUtil.LogError("[DungeonGate] m_WallTilemap 미연결 — 봉인 해제 불가.", this);
                return;
            }

            foreach (var cell in m_SealedArea.allPositionsWithin)
                m_WallTilemap.SetTile(cell, null);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_WallTilemap == null)
                DebugUtil.LogWarning("[DungeonGate] m_WallTilemap 미연결.", this);
        }
#endif
    }
}
```

- [ ] **Step 4: 테스트 통과 확인** — `run_tests`(EditMode) 4/4 PASS + `read_console` 컴파일 에러 0

---

### Task 3: PlayerController.TryHarvestNode 게이트 분기

**Files:**
- Modify: `Assets/Scripts/Player/PlayerController.cs` (`TryHarvestNode()` — 약 664행)

- [ ] **Step 1: TryHarvestNode 수정** — ResourceNode 와 DungeonGate 를 한 루프에서 최근접 탐색

기존 메서드의 루프~호출부를 다음으로 교체:

```csharp
        void TryHarvestNode()
        {
            int nodeLayerIdx = LayerMask.NameToLayer("ResourceNode");
            if (nodeLayerIdx < 0) return;
            LayerMask nodeLayer = 1 << nodeLayerIdx;

            float attackRange = GetCurrentAttackRange();
            var nodeFilter = new ContactFilter2D();
            nodeFilter.SetLayerMask(nodeLayer);
            nodeFilter.useTriggers = true;
            int hitCount = Physics2D.OverlapCircle(transform.position, attackRange, nodeFilter, s_OverlapBuffer);

            ResourceNode nearestNode = null;
            DungeonGate  nearestGate = null;
            float        minDist     = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                float d = Vector2.Distance(transform.position, s_OverlapBuffer[i].transform.position);
                if (d >= minDist) continue;

                var node = s_OverlapBuffer[i].GetComponent<ResourceNode>();
                if (node != null && !node.Depleted)
                {
                    minDist = d; nearestNode = node; nearestGate = null;
                    continue;
                }

                var gate = s_OverlapBuffer[i].GetComponent<DungeonGate>();
                if (gate != null && !gate.IsDestroyed)
                {
                    minDist = d; nearestGate = gate; nearestNode = null;
                }
            }
            if (nearestNode == null && nearestGate == null) return;

            Vector3 targetPos = nearestNode != null
                ? nearestNode.transform.position
                : nearestGate.transform.position;

            int dmg = m_Stats != null ? m_Stats.FinalAttack : 10;
            m_AtkTimer  = GetCurrentCooltime();
            m_FacingDir = ((Vector2)(targetPos - transform.position)).normalized;
            m_WeaponSocket?.SetFacingDirection(m_FacingDir);
            m_Anim.SetTrigger(s_HashAttack);

            if (nearestNode != null)
            {
                nearestNode.TakeHarvestDamage(dmg, m_Stats);
                m_Stats?.ConsumeWeaponDurabilityOnHit();
            }
            else
            {
                nearestGate.TakeGateDamage(dmg);
            }
        }
```

- [ ] **Step 2: 검증** — `refresh_unity`(compile) + `read_console` 에러 0 + EditMode 전체 재실행 (기존 86 + 신규 4 = 90 PASS)

---

### Task 4: DungeonZoneTrigger

**Files:**
- Create: `Assets/Scripts/Dungeon/DungeonZoneTrigger.cs`

- [ ] **Step 1: 구현**

```csharp
using System.Collections;
using MonsterKitchen.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonZoneTrigger — 구역 진입 피드백 (토스트 + 조명 보간)
    //
    //  ▶ 구역당 1개, 트리거 BoxCollider2D 로 영역 정의.
    //  ▶ 플레이어 진입: 구역 이름 토스트 + Global Light 2D intensity 보간.
    //  ▶ 같은 구역 연속 재진입은 무시 (s_Current 캐시).
    // ====================================================================

    [RequireComponent(typeof(BoxCollider2D))]
    public class DungeonZoneTrigger : MonoBehaviour
    {
        [Header("구역 설정")]
        [SerializeField] string m_ZoneName = "구역";
        [SerializeField, Range(0.2f, 1.5f)] float m_LightIntensity = 1f;

        [Header("조명 — Inspector 직접 연결")]
        [SerializeField] Light2D m_GlobalLight;

        const float LerpDuration = 0.5f;

        static DungeonZoneTrigger s_Current;
        Coroutine m_LightRoutine;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (s_Current == this) return;
            s_Current = this;

            GameHUD.Instance?.ShowNotification(m_ZoneName, 2f);

            if (m_GlobalLight != null)
            {
                if (m_LightRoutine != null) StopCoroutine(m_LightRoutine);
                m_LightRoutine = StartCoroutine(LerpLight(m_LightIntensity));
            }
        }

        IEnumerator LerpLight(float target)
        {
            float start = m_GlobalLight.intensity;
            float t = 0f;
            while (t < LerpDuration)
            {
                t += Time.deltaTime;
                m_GlobalLight.intensity = Mathf.Lerp(start, target, t / LerpDuration);
                yield return null;
            }
            m_GlobalLight.intensity = target;
        }

        void OnDestroy()
        {
            if (s_Current == this) s_Current = null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col != null && !col.isTrigger)
                Core.DebugUtil.LogWarning("[DungeonZoneTrigger] BoxCollider2D 는 isTrigger 여야 합니다.", this);
        }
#endif
    }
}
```

- [ ] **Step 2: 검증** — `refresh_unity`(compile) + `read_console` 에러 0

---

### Task 5: 에디터 맵 빌더 (타일 페인트)

**Files:**
- Create: `Assets/Editor/DungeonMapBuilder.cs`

- [ ] **Step 1: 빌더 구현** — 메뉴 `MonsterKitchen/Build Dungeon Map`. DungeonScene 이 열린 상태에서 실행. 개발용 타일 2종(바닥 짙은 회색, 벽 갈색)을 생성·저장 후 §맵 레이아웃 정의의 rect 들로 페인트.

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MonsterKitchen.EditorTools
{
    // ====================================================================
    //  DungeonMapBuilder — 던전 확장 맵 절차 페인트 (개발용 플레이스홀더 타일)
    //
    //  ▶ 사용: DungeonScene 을 열고 메뉴 MonsterKitchen → Build Dungeon Map.
    //  ▶ Floor/Wall 타일맵 전체 클리어 후 레이아웃 rect 로 재페인트.
    //  ▶ 아트 타일 준비 시 이 스크립트의 타일 에셋만 교체하면 된다.
    // ====================================================================
    public static class DungeonMapBuilder
    {
        // 스펙 §3 레이아웃 (셀 좌표)
        static readonly RectInt[] FloorAreas =
        {
            new RectInt(-8,  -23, 16, 13),   // 허브
            new RectInt(-13, -17,  5,  5),   // 숲 통로
            new RectInt(-29, -17, 16, 16),   // 숲 갈래
            new RectInt(  8, -17,  5,  5),   // 암석 통로
            new RectInt( 13, -17, 16, 16),   // 암석 갈래
            new RectInt( -2, -11,  4,  4),   // 심층 통로
            new RectInt(-12,  -8, 24, 16),   // 심층 바깥
            new RectInt( -2,   8,  4,  3),   // 게이트 통로
            new RectInt(-12,  11, 24, 12),   // 심층 안쪽
        };

        // 게이트 봉인 영역 — 바닥 페인트 후 벽 타일로 덮는다
        static readonly RectInt GateSeal = new RectInt(-2, 8, 4, 3);

        const string TileDir = "Assets/Data/Tiles";

        [MenuItem("MonsterKitchen/Build Dungeon Map")]
        public static void Build()
        {
            var grid = Object.FindFirstObjectByType<Grid>();
            if (grid == null) { Debug.LogError("[DungeonMapBuilder] Grid 없음 — DungeonScene 을 여세요."); return; }

            Tilemap floor = null, wall = null;
            foreach (var tm in grid.GetComponentsInChildren<Tilemap>(true))
            {
                string n = tm.name.ToLowerInvariant();
                if (n.Contains("wall")) wall = tm;
                else floor ??= tm;
            }
            if (floor == null || wall == null)
            {
                Debug.LogError($"[DungeonMapBuilder] Floor/Wall 타일맵 탐색 실패 (floor:{floor}, wall:{wall})");
                return;
            }

            var floorTile = GetOrCreateTile("DevFloor", new Color(0.22f, 0.22f, 0.26f));
            var wallTile  = GetOrCreateTile("DevWall",  new Color(0.45f, 0.32f, 0.22f));

            floor.ClearAllTiles();
            wall.ClearAllTiles();

            // 1) 바닥
            var floorCells = new HashSet<Vector3Int>();
            foreach (var r in FloorAreas)
                for (int x = r.xMin; x < r.xMax; x++)
                    for (int y = r.yMin; y < r.yMax; y++)
                        floorCells.Add(new Vector3Int(x, y, 0));
            foreach (var c in floorCells) floor.SetTile(c, floorTile);

            // 2) 벽 — 바닥에 인접(8방향)한 비바닥 셀 1겹
            var dirs = new[]
            {
                new Vector3Int( 1, 0, 0), new Vector3Int(-1, 0, 0),
                new Vector3Int( 0, 1, 0), new Vector3Int( 0,-1, 0),
                new Vector3Int( 1, 1, 0), new Vector3Int(-1, 1, 0),
                new Vector3Int( 1,-1, 0), new Vector3Int(-1,-1, 0),
            };
            var wallCells = new HashSet<Vector3Int>();
            foreach (var c in floorCells)
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (!floorCells.Contains(n)) wallCells.Add(n);
                }
            foreach (var c in wallCells) wall.SetTile(c, wallTile);

            // 3) 게이트 봉인 — 통로 위 벽 타일
            for (int x = GateSeal.xMin; x < GateSeal.xMax; x++)
                for (int y = GateSeal.yMin; y < GateSeal.yMax; y++)
                    wall.SetTile(new Vector3Int(x, y, 0), wallTile);

            // 4) Wall 타일맵 물리 — TilemapCollider2D 보장
            if (wall.GetComponent<TilemapCollider2D>() == null)
                wall.gameObject.AddComponent<TilemapCollider2D>();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
            Debug.Log($"[DungeonMapBuilder] 완료 — 바닥 {floorCells.Count}셀, 벽 {wallCells.Count}셀.");
        }

        static TileBase GetOrCreateTile(string name, Color color)
        {
            string tilePath = $"{TileDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(TileDir))
                AssetDatabase.CreateFolder("Assets/Data", "Tiles");

            // 1×1 단색 스프라이트 PNG 생성 (32px, PPU 32 → 1유닛)
            string pngPath = $"{TileDir}/{name}.png";
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var px  = new Color[32 * 32];
            for (int i = 0; i < px.Length; i++) px[i] = color;
            tex.SetPixels(px); tex.Apply();
            System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pngPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.textureType         = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode          = FilterMode.Point;
            importer.SaveAndReimport();

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            tile.colliderType = Tile.ColliderType.Grid;
            AssetDatabase.CreateAsset(tile, tilePath);
            AssetDatabase.SaveAssets();
            return tile;
        }
    }
}
```

(주: 에디터 전용 툴 스크립트 — `FindFirstObjectByType` 은 런타임 금지 규칙의 예외로 둔다. 빌더 1회 실행용.)

- [ ] **Step 2: 검증** — `refresh_unity`(compile) + `read_console` 에러 0
- [ ] **Step 3: DungeonScene 열고 빌더 실행** — `manage_scene`(load DungeonScene) → `execute_menu_item`("MonsterKitchen/Build Dungeon Map") → 콘솔에 "완료 — 바닥 N셀" 로그 확인
- [ ] **Step 4: 씬 저장** — `manage_scene`(save)

---

### Task 6: DungeonScene 오브젝트 재배치 (UnityMCP)

**Files:** `Assets/Scenes/DungeonScene.unity` (MCP 편집)

월드 좌표 = 셀 중심(+0.5). 모든 작업 후 씬 저장.

- [ ] **Step 1: 기존 정리**
  - `RoomBoundaries` GO 삭제 (Wall TilemapCollider2D 로 대체됨)
  - 기존 `SpawnZone1`/`SpawnZone2` 삭제 (새 존으로 교체)
  - 기존 ResourceNode 4개 삭제 (새 배치로 교체)

- [ ] **Step 2: 컨트롤러/공용 갱신**
  - `DungeonMapController.m_WorldBounds = {x:-30, y:-24, w:60, h:48}`
  - `PlayerSpawnPoint` 위치 → `(0.5, -19.5, 0)`
  - `DungeonExit` 위치 → `(4.5, -19.5, 0)` (활성 상태 유지)
  - `Background` 스케일 → 62×50 커버 (스프라이트 1×1 기준 scale 62,50,1)
  - 카메라 컨파이너: `DungeonCameraConfiner` 가 WorldBounds 기반이면 자동, 별도 콜라이더 참조면 새 바운드로 갱신

- [ ] **Step 3: 스폰 존 4개 생성** — 각각 빈 GO + `DungeonSpawnZone`, 자식 SpawnPoint 들

| GO | 위치 | m_SpawnTableId | m_RespawnDelay | SpawnPoints (자식, 로컬 오프셋) |
|---|---|---|---|---|
| SpawnZone_Forest | (-21.5, -9.5) | 6003 | 8 | (-4,3) (4,4) (-3,-4) (4,-3) |
| SpawnZone_Rock | (21.5, -9.5) | 6004 | 8 | (-4,3) (4,-4) (0,0) |
| SpawnZone_DeepOuter | (0.5, 0.5) | 6005 | 8 | (-7,3) (7,3) (-6,-4) (6,-4) |
| SpawnZone_Boss | (0.5, 17.5) | 6006 | 60 | (0,0) |

  - `DungeonMapController.m_SpawnZones` 배열에 4개 연결

- [ ] **Step 4: 자원 노드 11개 생성** — 각각 GO + SpriteRenderer + BoxCollider2D(isTrigger) + `ResourceNode`(m_NodeDataId)

| 이름 | NodeDataId | 위치 | 틴트 |
|---|---|---|---|
| Node_Tree_F1~F4 | 7002 | (-26.5,-4.5) (-17.5,-3.5) (-24.5,-13.5) (-15.5,-14.5) | 초록 |
| Node_Rock_R1~R2 | 7001 | (16.5,-4.5) (26.5,-13.5) | 회색 |
| Node_Ore_R1~R2 | 7003 | (26.5,-3.5) (17.5,-14.5) | 청회색 |
| Node_Ore_D1 | 7003 | (-8.5, 5.5) | 청회색 |
| Node_RareOre_B1~B2 | 7004 | (-7.5, 19.5) (8.5, 19.5) | 보라 |

- [ ] **Step 5: DungeonGate 생성** — GO `DungeonGate` + SpriteRenderer(4×3 스케일, 갈색 틴트) + BoxCollider2D(non-trigger, size 4×3) + `DungeonGate` 컴포넌트
  - 위치 `(0, 9.5, 0)` (게이트 통로 중앙)
  - `m_WallTilemap` = Wall 타일맵 연결
  - `m_SealedArea` = `{x:-2, y:8, z:0, sizeX:4, sizeY:3, sizeZ:1}`

- [ ] **Step 6: DungeonZoneTrigger 5개 생성** — 각각 GO + BoxCollider2D(isTrigger) + 컴포넌트, `m_GlobalLight` = 씬 Global Light 2D 연결

| GO | 위치 | 콜라이더 size | ZoneName | Intensity |
|---|---|---|---|---|
| Zone_Hub | (0,-17) | 14×11 | 허브 — 안전 구역 | 1.0 |
| Zone_Forest | (-21.5,-9.5) | 14×14 | 숲 — 채집지 | 1.0 |
| Zone_Rock | (21.5,-9.5) | 14×14 | 암석 지대 | 0.9 |
| Zone_DeepOuter | (0,0) | 22×14 | 심층부 — 위험! | 0.75 |
| Zone_DeepInner | (0,17) | 22×10 | 최심부 — 매우 위험!! | 0.6 |

- [ ] **Step 7: 씬 저장 + 검증** — `manage_scene`(save) → `read_console` 에러 0

---

### Task 7: PlayMode 스모크 확장

**Files:**
- Modify: `Assets/Tests/PlayMode/SceneFlowSmokeTests.cs`

- [ ] **Step 1: 던전 스모크 테스트 추가** (클래스 끝에)

```csharp
        [UnityTest]
        public IEnumerator DungeonScene_Loads_With4SpawnZones_AndBakedNavGrid()
        {
            LogAssert.ignoreFailingMessages = true;

            // 부팅 → Management (플레이어 스폰 선행)
            SceneManager.LoadScene(CommonString.SceneStart);
            yield return null;
            float elapsed = 0f;
            var startup = GlobalController.Instance.Startup;
            while (!startup.IsComplete && !startup.IsAborted && elapsed < StartupTimeout)
            { elapsed += Time.deltaTime; yield return null; }
            Assert.IsTrue(startup.IsComplete);

            SceneLoader.Instance.LoadScene(CommonString.SceneManagement);
            elapsed = 0f;
            while ((SceneLoader.Instance.IsLoading
                    || SceneManager.GetActiveScene().name != CommonString.SceneManagement)
                   && elapsed < StartupTimeout)
            { elapsed += Time.deltaTime; yield return null; }

            // Dungeon 전환
            SceneLoader.Instance.LoadScene(CommonString.SceneDungeon);
            elapsed = 0f;
            while ((SceneLoader.Instance.IsLoading
                    || SceneManager.GetActiveScene().name != CommonString.SceneDungeon)
                   && elapsed < StartupTimeout)
            { elapsed += Time.deltaTime; yield return null; }

            // 컨트롤러 Init 완료 대기
            elapsed = 0f;
            while ((MonsterKitchen.Dungeon.DungeonMapController.Instance == null
                    || MonsterKitchen.Dungeon.DungeonMapController.Instance.State
                       != SceneControllerBase.SceneState.Running)
                   && elapsed < 10f)
            { elapsed += Time.deltaTime; yield return null; }

            var ctrl = MonsterKitchen.Dungeon.DungeonMapController.Instance;
            Assert.IsNotNull(ctrl, "DungeonMapController 초기화 실패");
            Assert.AreEqual(4, ctrl.SpawnZones.Count, "스폰 존 4개가 연결되어야 한다");
            Assert.IsNotNull(MonsterKitchen.Navigation.NavGrid.Instance);
            Assert.Greater(MonsterKitchen.Navigation.NavGrid.Instance.Width, 50,
                "NavGrid 가 확장 맵(60셀 폭)을 커버해야 한다");
        }
```

- [ ] **Step 2: 실행** — `run_tests`(PlayMode) 3/3 PASS

---

### Task 8: 마무리 검증 + 문서

- [ ] **Step 1: 전체 테스트** — EditMode 90 PASS + PlayMode 3 PASS + `read_console` 에러 0
- [ ] **Step 2: 수동 점검 체크리스트** (사용자 또는 에디터 플레이)
  - 허브 스폰 → 3갈래 이동 가능, 벽 충돌 정상
  - 숲: MON_001×4 + 나무 4 / 암석: MON_002×3+003×1 + 노드 4 / 심층 바깥: 003×2+004×2
  - 게이트: 도구 Lv0~1 공격 → "더 강한 도구" 토스트, Lv2+ → 파괴 → 통행 가능 + 보스 구역 진입
  - 구역 진입 토스트 + 심층 조명 어두워짐
  - 미니맵: 새 바운드 기준 도트/마커 4개
  - 사망 → 가방 소실 + Management 복귀 (기존 흐름 유지)
- [ ] **Step 3: 문서 갱신** — `WORK_IN_PROGRESS.md` 완료 체크, `docs/MODULES.md` 모듈 5에 5-9(맵 확장) 행 추가
- [ ] **Step 4: 사용자에게 커밋 여부 확인**

---

## Self-Review 결과

- 스펙 커버리지: §3 레이아웃→Task 5/6, §4-1→Task 2/3, §4-2→Task 4, §5→Task 1, §6→Task 5/6, §7→Task 2/7/8 ✓
- 보류 결정 1건: 기존 Floor/Wall 타일맵의 실제 이름/상태는 실행 시 확인 (빌더가 이름 탐색 — "wall" 포함 여부로 분기). 실패 시 로그로 안내.
- 타입 일관성: `ComputeGateDamage` / `TakeGateDamage` / `IsDestroyed` Task 2↔3 일치 ✓
