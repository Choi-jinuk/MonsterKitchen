# 통일 소팅 + Occluder Fade 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 월드 스프라이트 소팅 통일(0) + 플레이어 가림 시 prop 반투명 처리 (스펙: `docs/superpowers/specs/2026-06-11-unified-sorting-occluder-fade-design.md`)

**Architecture:** 기존 Perspective Distance 소트가 깊이 가림 담당 — sortingOrder 예외만 제거. 신규 `SpriteOccluderFade` 1컴포넌트가 bounds+Y 판정으로 알파 보간. 판정은 정적 순수 메서드 → EditMode 테스트.

**Tech Stack:** Unity 6 URP 2D, NUnit, UnityMCP (씬 적용)

**커밋 규칙:** 사용자 명시 요청 시에만 — 커밋 단계 없음, 검증 단계로 대체.

---

### Task 1: SpriteOccluderFade — TDD

**Files:**
- Create: `Assets/Tests/EditMode/SpriteOccluderFadeTests.cs`
- Create: `Assets/Scripts/Core/Rendering/SpriteOccluderFade.cs`

- [ ] **Step 1: 실패 테스트**

```csharp
using NUnit.Framework;
using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Tests
{
    public class SpriteOccluderFadeTests
    {
        static readonly Bounds PropBounds = new Bounds(
            new Vector3(0f, 0f, 0f), new Vector3(2f, 2f, 1f));

        [Test]
        public void ShouldFade_PlayerInsideBounds_AndBehind_True()
        {
            // 플레이어가 prop 영역 안 + 더 위(Y 큼) = 가려짐
            Assert.IsTrue(SpriteOccluderFade.ShouldFade(
                PropBounds, propY: 0f, playerPos: new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void ShouldFade_PlayerInsideBounds_ButInFront_False()
        {
            // 플레이어가 prop 영역 안이지만 더 아래(앞) = 가림 아님
            Assert.IsFalse(SpriteOccluderFade.ShouldFade(
                PropBounds, propY: 0f, playerPos: new Vector2(0.5f, -0.5f)));
        }

        [Test]
        public void ShouldFade_PlayerOutsideBounds_False()
        {
            Assert.IsFalse(SpriteOccluderFade.ShouldFade(
                PropBounds, propY: 0f, playerPos: new Vector2(5f, 5f)));
        }
    }
}
```

- [ ] **Step 2: RED 확인** — run_tests(EditMode, group `MonsterKitchen.Tests.SpriteOccluderFadeTests`), 기대: CS 컴파일 에러 (클래스 미존재)

- [ ] **Step 3: 구현**

```csharp
using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  SpriteOccluderFade — 플레이어를 가리는 prop 반투명 처리
    //
    //  ▶ 부착 대상: 나무·동상 등 지형 prop, 채집 노드.
    //  ▶ 판정: 플레이어가 스프라이트 bounds 안 + 플레이어 Y > prop Y (= 뒤).
    //  ▶ m_FadedAlpha 프리셋: 채집 노드 0.55 / 데코 지형 0.3.
    //  ▶ 틴트 RGB 유지, 알파만 보간 — ResourceNode m_NodeTint 와 공존.
    // ====================================================================

    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteOccluderFade : MonoBehaviour
    {
        [Header("페이드 설정")]
        [SerializeField, Range(0.05f, 0.95f)] float m_FadedAlpha = 0.3f;
        [SerializeField]                      float m_FadeTime   = 0.15f;

        SpriteRenderer m_Sprite;
        Transform      m_Player;
        float          m_CurrentAlpha = 1f;

        /// <summary>플레이어가 prop 에 가려졌는가 — bounds 안 + prop 기준 뒤(Y 큼).</summary>
        public static bool ShouldFade(Bounds propBounds, float propY, Vector2 playerPos)
        {
            if (playerPos.x < propBounds.min.x || playerPos.x > propBounds.max.x) return false;
            if (playerPos.y < propBounds.min.y || playerPos.y > propBounds.max.y) return false;
            return playerPos.y > propY;
        }

        void Awake() => m_Sprite = GetComponent<SpriteRenderer>();

        void LateUpdate()
        {
            if (m_Player == null)
            {
                m_Player = PlayerManager.Instance?.Player != null
                    ? PlayerManager.Instance.Player.transform : null;
                if (m_Player == null) return;
            }

            bool  fade   = ShouldFade(m_Sprite.bounds, transform.position.y, m_Player.position);
            float target = fade ? m_FadedAlpha : 1f;

            if (Mathf.Approximately(m_CurrentAlpha, target)) return;

            float step     = m_FadeTime > 0f ? Time.deltaTime / m_FadeTime : 1f;
            m_CurrentAlpha = Mathf.MoveTowards(m_CurrentAlpha, target, step);

            var c = m_Sprite.color;
            c.a = m_CurrentAlpha;
            m_Sprite.color = c;
        }
    }
}
```

- [ ] **Step 4: GREEN 확인** — 컴파일 성공(DLL 타임스탬프 + scriptCompilationFailed=false 확인) 후 run_tests(EditMode) 신규 3 PASS

---

### Task 2: 소팅 통일

**Files:**
- Modify: `Assets/Scripts/Combat/Projectile.cs` (Awake 의 sortingOrder)
- Modify: `Assets/Scenes/DungeonScene.unity` (DungeonGate sortingOrder, MCP)

- [ ] **Step 1: Projectile sortingOrder 제거**

```csharp
// 변경 전
sr.sortingOrder = 10;
// 변경 후 — 월드 스프라이트 소팅 통일(0): 깊이는 Perspective Distance 소트가 결정
// (라인 삭제)
```

- [ ] **Step 2: DungeonGate sortingOrder 0** — MCP `manage_components`(set_property `m_SortingOrder`... SpriteRenderer property `sortingOrder` = 0) 또는 execute_code
- [ ] **Step 3: 잔여 점검** — 씬/프리팹 월드 스프라이트 중 order ≠ 0 grep + 씬 질의, 발견 시 0
- [ ] **Step 4: 씬 저장 + read_console 에러 0

---

### Task 3: 씬 적용 (채집 노드 11개)

- [ ] **Step 1: DungeonScene 로드 → 노드 11개에 SpriteOccluderFade 추가 (m_FadedAlpha = 0.55)** — execute_code 일괄
- [ ] **Step 2: 씬 저장**
- [ ] **Step 3: 전체 테스트** — EditMode 96 PASS + PlayMode 3 PASS
- [ ] **Step 4: 수동 점검 안내** — 노드 뒤 이동 시 알파 0.55 + 플레이어 시인, 투사체/게이트 Y 가림 정상

---

## Self-Review
- 스펙 §3→Task 2, §4→Task 1, §5→Task 3, §6→Task 1/3 ✓
- 플레이스홀더 없음, ShouldFade 시그니처 Task1↔테스트 일치 ✓
