# Mobile Input Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 가상 조이스틱(이동) + 컨텍스트 기반 액션 버튼으로 모바일 터치 입력 구현. 기존 `InputManager` 이벤트 시스템을 그대로 재사용.

**Architecture:**
- `VirtualJoystick` — dynamic floating 조이스틱, `InputManager.InjectMove` 호출
- `MobileButton` — 액션 버튼 1개, `SetAction(MobileAction)` 으로 런타임 교체 가능
- `MobileHUD` — DontDestroyOnLoad 싱글톤, `SetContext(MobileContext)` 으로 버튼 슬롯 재구성
- 씬 컨트롤러 + `InteractionPrompt` 가 `SetContext` 호출

**Tech Stack:** Unity 6 (6000.4.2f1), uGUI (EventSystem), C# New Input System (InputManager 이벤트), NUnit EditMode 테스트

---

## File Map

| 파일 | 신규/수정 | 역할 |
|---|---|---|
| `Assets/Scripts/UI/Mobile/MobileAction.cs` | 신규 | enum MobileAction |
| `Assets/Scripts/UI/Mobile/MobileContext.cs` | 신규 | enum MobileContext |
| `Assets/Scripts/UI/Mobile/VirtualJoystick.cs` | 신규 | 터치 드래그 → InjectMove |
| `Assets/Scripts/UI/Mobile/MobileButton.cs` | 신규 | 버튼 1개 — SetAction + Inject 발화 |
| `Assets/Scripts/UI/Mobile/MobileHUD.cs` | 신규 | 슬롯 관리 + SetContext 싱글톤 |
| `Assets/Scripts/Core/Managers/InputManager.cs` | 수정 | Inject* 메서드 추가 |
| `Assets/Scripts/Core/Asset/AssetKeys.cs` | 수정 | PREFAB_MOBILE_HUD 추가 |
| `Assets/Scripts/Core/Managers/GlobalController.cs` | 수정 | m_MobileHUDPrefab 필드 + Awake 스폰 |
| `Assets/Scripts/Dungeon/DungeonMapController.cs` | 수정 | Start 에서 SetContext(Dungeon) |
| `Assets/Scripts/UI/HUD/InteractionPrompt.cs` | 수정 | Trigger 진입/이탈 시 SetContext |
| `Assets/Scripts/Management/ManagementSceneController.cs` | 수정 | OnInit 에서 SetContext(Exploration) |
| `Assets/Scripts/Cooking/KitchenSceneController.cs` | 수정 | Start 에서 SetContext(Exploration) |
| `Assets/Scripts/Restaurant/RestaurantSceneController.cs` | 수정 | Start 에서 SetContext(Exploration) |
| `Assets/Prefabs/UI/MobileHUD.prefab` | 신규 | Canvas + Joystick + ButtonPanel (MCP) |
| `Assets/Tests/EditMode/MobileInputTests.cs` | 신규 | EditMode 테스트 |

---

## Task 1: 열거형 + InputManager Inject 메서드

**Files:**
- Create: `Assets/Scripts/UI/Mobile/MobileAction.cs`
- Create: `Assets/Scripts/UI/Mobile/MobileContext.cs`
- Modify: `Assets/Scripts/Core/Managers/InputManager.cs`
- Create: `Assets/Tests/EditMode/MobileInputTests.cs`

- [ ] **Step 1: 테스트 파일 작성 (실패 확인용)**

`Assets/Tests/EditMode/MobileInputTests.cs` 생성:

```csharp
using NUnit.Framework;
using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Tests
{
    public class MobileInputTests
    {
        [Test]
        public void InjectMove_FiresOnMoveEvent()
        {
            var mgr = new InputManager();
            mgr.Init();

            Vector2 received = Vector2.zero;
            mgr.OnMove += v => received = v;

            mgr.InjectMove(new Vector2(0.5f, 0.3f));

            Assert.AreEqual(new Vector2(0.5f, 0.3f), received);
        }

        [Test]
        public void InjectDash_FiresOnDashEvent()
        {
            var mgr = new InputManager();
            mgr.Init();

            bool fired = false;
            mgr.OnDash += () => fired = true;

            mgr.InjectDash();

            Assert.IsTrue(fired);
        }

        [Test]
        public void InjectSkill1_FiresOnSkill1Event()
        {
            var mgr = new InputManager();
            mgr.Init();

            bool fired = false;
            mgr.OnSkill1 += () => fired = true;

            mgr.InjectSkill1();

            Assert.IsTrue(fired);
        }

        [Test]
        public void InjectInteract_FiresOnInteractEvent()
        {
            var mgr = new InputManager();
            mgr.Init();

            bool fired = false;
            mgr.OnInteract += () => fired = true;

            mgr.InjectInteract();

            Assert.IsTrue(fired);
        }
    }
}
```

- [ ] **Step 2: EditMode 테스트 실행 — 컴파일 오류로 실패 확인**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit
```

Expected: `InjectMove`, `InjectDash` 등 메서드 없음 → 컴파일 오류

- [ ] **Step 3: MobileAction enum 작성**

`Assets/Scripts/UI/Mobile/MobileAction.cs`:

```csharp
namespace MonsterKitchen.UI.Mobile
{
    public enum MobileAction
    {
        Dash,
        Skill1,
        Skill2,
        Interact,
        Ultimate,
    }
}
```

- [ ] **Step 4: MobileContext enum 작성**

`Assets/Scripts/UI/Mobile/MobileContext.cs`:

```csharp
namespace MonsterKitchen.UI.Mobile
{
    // ====================================================================
    //  MobileContext — 씬/상황별 모바일 버튼 배치 컨텍스트
    //
    //  ▶ Dungeon         : [Dash][Skill1][Skill2]
    //  ▶ DungeonInteract : [Dash][Skill1][Interact]  ← Skill2 슬롯 교체
    //  ▶ Exploration     : [Dash][Interact]
    // ====================================================================

    public enum MobileContext
    {
        Dungeon,
        DungeonInteract,
        Exploration,
    }
}
```

- [ ] **Step 5: InputManager 에 Inject 메서드 추가**

`Assets/Scripts/Core/Managers/InputManager.cs` 의 `// ── UI 토글 등록 / 해제` 섹션 위에 아래 섹션 추가:

```csharp
        // ── 모바일 입력 주입 ──────────────────────────────────────────────
        // VirtualJoystick / MobileButton 이 InputManager 이벤트를 직접 발화할 때 사용.

        public void InjectMove(Vector2 dir)  => OnMove?.Invoke(dir);
        public void InjectDash()             => OnDash?.Invoke();
        public void InjectSkill1()           => OnSkill1?.Invoke();
        public void InjectSkill2()           => OnSkill2?.Invoke();
        public void InjectInteract()         => OnInteract?.Invoke();
        public void InjectUltimate()         => OnUltimate?.Invoke();
```

- [ ] **Step 6: 테스트 실행 — 통과 확인**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit
```

Expected: `MobileInputTests` 4개 PASS

- [ ] **Step 7: read_console 로 컴파일 오류 없음 확인**

MCP `read_console` 호출 → Error 없음 확인

---

## Task 2: VirtualJoystick

**Files:**
- Create: `Assets/Scripts/UI/Mobile/VirtualJoystick.cs`

- [ ] **Step 1: VirtualJoystick 작성**

`Assets/Scripts/UI/Mobile/VirtualJoystick.cs`:

```csharp
using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MonsterKitchen.UI.Mobile
{
    // ====================================================================
    //  VirtualJoystick — Dynamic floating 가상 조이스틱
    //
    //  ▶ 터치 시작 위치로 배경 재배치 (화면 왼쪽 절반만 허용)
    //  ▶ 드래그 → 핸들 이동 + InjectMove(dir) 발화
    //  ▶ 릴리즈 → 핸들 중앙 복귀 + InjectMove(zero)
    //  ▶ 멀티터치 안전: 첫 손가락 ID 캐시, 다른 터치 무시
    // ====================================================================

    public class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] float         m_Radius   = 80f;
        [SerializeField] float         m_DeadZone = 0.1f;
        [SerializeField] RectTransform m_Background;
        [SerializeField] RectTransform m_Handle;

        int     m_ActivePointerId = -1;
        Vector2 m_Center;

        Canvas m_Canvas;

        void Awake()
        {
            m_Canvas = GetComponentInParent<Canvas>();
        }

        // ── IPointerDownHandler ──────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_ActivePointerId != -1) return;           // 이미 다른 터치 진행 중
            if (!IsInLeftHalf(eventData.position)) return; // 화면 오른쪽 터치 무시

            m_ActivePointerId = eventData.pointerId;

            // 배경을 터치 위치로 재배치
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_Background.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out m_Center);

            m_Background.anchoredPosition = m_Center;
            m_Handle.anchoredPosition     = Vector2.zero;
        }

        // ── IDragHandler ─────────────────────────────────────────────

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != m_ActivePointerId) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_Background.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);

            Vector2 delta = localPoint - m_Center;

            // 핸들 반경 클램프
            float   mag      = Mathf.Min(delta.magnitude, m_Radius);
            Vector2 clamped  = delta.normalized * mag;
            m_Handle.anchoredPosition = clamped;

            // 방향 벡터 계산 (0~1 아날로그)
            Vector2 dir = clamped / m_Radius;

            if (dir.magnitude < m_DeadZone)
                InputManager.Instance?.InjectMove(Vector2.zero);
            else
                InputManager.Instance?.InjectMove(dir);
        }

        // ── IPointerUpHandler ────────────────────────────────────────

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != m_ActivePointerId) return;

            m_ActivePointerId = -1;
            m_Handle.anchoredPosition = Vector2.zero;
            InputManager.Instance?.InjectMove(Vector2.zero);
        }

        // ── 내부 ─────────────────────────────────────────────────────

        bool IsInLeftHalf(Vector2 screenPos) => screenPos.x < Screen.width * 0.5f;
    }
}
```

- [ ] **Step 2: read_console 로 컴파일 오류 없음 확인**

MCP `read_console` 호출 → `VirtualJoystick` 관련 Error 없음

---

## Task 3: MobileButton

**Files:**
- Create: `Assets/Scripts/UI/Mobile/MobileButton.cs`
- Modify: `Assets/Tests/EditMode/MobileInputTests.cs`

- [ ] **Step 1: MobileButton SetAction 테스트 추가**

`Assets/Tests/EditMode/MobileInputTests.cs` 의 클래스 내에 아래 테스트 추가:

```csharp
        [Test]
        public void MobileButton_SetAction_UpdatesAction()
        {
            var go  = new UnityEngine.GameObject();
            var btn = go.AddComponent<MonsterKitchen.UI.Mobile.MobileButton>();

            btn.SetAction(MonsterKitchen.UI.Mobile.MobileAction.Interact);

            Assert.AreEqual(MonsterKitchen.UI.Mobile.MobileAction.Interact, btn.Action);

            UnityEngine.Object.DestroyImmediate(go);
        }
```

- [ ] **Step 2: MobileButton 작성**

`Assets/Scripts/UI/Mobile/MobileButton.cs`:

```csharp
using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MonsterKitchen.UI.Mobile
{
    // ====================================================================
    //  MobileButton — 모바일 액션 버튼 1개
    //
    //  ▶ m_Action 에 따라 InputManager.Inject*() 발화
    //  ▶ SetAction(MobileAction) 으로 런타임 교체 가능 (MobileHUD 가 사용)
    //  ▶ PointerDown 시각 피드백: scale 0.9 → PointerUp 시 1.0 복귀
    // ====================================================================

    public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] MobileAction m_Action = MobileAction.Dash;

        public MobileAction Action => m_Action;

        // ── 공개 API ─────────────────────────────────────────────────

        public void SetAction(MobileAction action)
        {
            m_Action = action;
        }

        // ── IPointerDownHandler ──────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.localScale = Vector3.one * 0.9f;
            FireAction();
        }

        // ── IPointerUpHandler ────────────────────────────────────────

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = Vector3.one;
        }

        // ── 내부 ─────────────────────────────────────────────────────

        void FireAction()
        {
            var input = InputManager.Instance;
            if (input == null) return;

            switch (m_Action)
            {
                case MobileAction.Dash:     input.InjectDash();     break;
                case MobileAction.Skill1:   input.InjectSkill1();   break;
                case MobileAction.Skill2:   input.InjectSkill2();   break;
                case MobileAction.Interact: input.InjectInteract(); break;
                case MobileAction.Ultimate: input.InjectUltimate(); break;
            }
        }
    }
}
```

- [ ] **Step 3: 테스트 실행 — 통과 확인**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit
```

Expected: `MobileInputTests` 5개 PASS

---

## Task 4: MobileHUD

**Files:**
- Create: `Assets/Scripts/UI/Mobile/MobileHUD.cs`
- Modify: `Assets/Tests/EditMode/MobileInputTests.cs`

- [ ] **Step 1: MobileHUD SetContext 테스트 추가**

`Assets/Tests/EditMode/MobileInputTests.cs` 의 클래스 내에 추가:

```csharp
        [Test]
        public void MobileHUD_SetContext_DungeonInteract_SetsSlot2ToInteract()
        {
            // MobileHUD + 슬롯 3개 직접 구성
            var hudGO = new UnityEngine.GameObject();
            var hud   = hudGO.AddComponent<MonsterKitchen.UI.Mobile.MobileHUD>();

            var slot0GO = new UnityEngine.GameObject(); var s0 = slot0GO.AddComponent<MonsterKitchen.UI.Mobile.MobileButton>();
            var slot1GO = new UnityEngine.GameObject(); var s1 = slot1GO.AddComponent<MonsterKitchen.UI.Mobile.MobileButton>();
            var slot2GO = new UnityEngine.GameObject(); var s2 = slot2GO.AddComponent<MonsterKitchen.UI.Mobile.MobileButton>();

            hud.SetSlotsForTest(new[] { s0, s1, s2 });
            hud.SetConfigsForTest(new[]
            {
                new MonsterKitchen.UI.Mobile.MobileHUD.ContextConfig
                {
                    Context     = MonsterKitchen.UI.Mobile.MobileContext.DungeonInteract,
                    SlotActions = new[]
                    {
                        MonsterKitchen.UI.Mobile.MobileAction.Dash,
                        MonsterKitchen.UI.Mobile.MobileAction.Skill1,
                        MonsterKitchen.UI.Mobile.MobileAction.Interact,
                    },
                },
            });

            hud.SetContext(MonsterKitchen.UI.Mobile.MobileContext.DungeonInteract);

            Assert.AreEqual(MonsterKitchen.UI.Mobile.MobileAction.Interact, s2.Action);

            UnityEngine.Object.DestroyImmediate(slot0GO);
            UnityEngine.Object.DestroyImmediate(slot1GO);
            UnityEngine.Object.DestroyImmediate(slot2GO);
            UnityEngine.Object.DestroyImmediate(hudGO);
        }

        [Test]
        public void MobileHUD_SetContext_Exploration_HidesSlot2()
        {
            var hudGO = new UnityEngine.GameObject();
            var hud   = hudGO.AddComponent<MonsterKitchen.UI.Mobile.MobileHUD>();

            var slot0GO = new UnityEngine.GameObject(); var s0 = slot0GO.AddComponent<MonsterKitchen.UI.Mobile.MobileButton>();
            var slot1GO = new UnityEngine.GameObject(); var s1 = slot1GO.AddComponent<MonsterKitchen.UI.Mobile.MobileButton>();
            var slot2GO = new UnityEngine.GameObject(); var s2 = slot2GO.AddComponent<MonsterKitchen.UI.Mobile.MobileButton>();

            hud.SetSlotsForTest(new[] { s0, s1, s2 });
            hud.SetConfigsForTest(new[]
            {
                new MonsterKitchen.UI.Mobile.MobileHUD.ContextConfig
                {
                    Context     = MonsterKitchen.UI.Mobile.MobileContext.Exploration,
                    SlotActions = new[]
                    {
                        MonsterKitchen.UI.Mobile.MobileAction.Dash,
                        MonsterKitchen.UI.Mobile.MobileAction.Interact,
                    },
                },
            });

            hud.SetContext(MonsterKitchen.UI.Mobile.MobileContext.Exploration);

            Assert.IsTrue(slot0GO.activeSelf);
            Assert.IsTrue(slot1GO.activeSelf);
            Assert.IsFalse(slot2GO.activeSelf);  // Exploration = 슬롯 2개만

            UnityEngine.Object.DestroyImmediate(slot0GO);
            UnityEngine.Object.DestroyImmediate(slot1GO);
            UnityEngine.Object.DestroyImmediate(slot2GO);
            UnityEngine.Object.DestroyImmediate(hudGO);
        }
```

- [ ] **Step 2: MobileHUD 작성**

`Assets/Scripts/UI/Mobile/MobileHUD.cs`:

```csharp
using UnityEngine;

namespace MonsterKitchen.UI.Mobile
{
    // ====================================================================
    //  MobileHUD — 모바일 입력 UI 총괄 싱글톤
    //
    //  ▶ DontDestroyOnLoad — GlobalController 가 Awake 에서 생성
    //  ▶ SetContext(MobileContext) — 버튼 슬롯 액션 + 활성화 재구성
    //  ▶ m_ForceShow = true 로 에디터에서 강제 표시 가능 (테스트용)
    // ====================================================================

    public class MobileHUD : MonoBehaviour
    {
        // ── 직렬화 타입 ──────────────────────────────────────────────

        [System.Serializable]
        public struct ContextConfig
        {
            public MobileContext  Context;
            public MobileAction[] SlotActions;
        }

        // ── Inspector 필드 ───────────────────────────────────────────

        [SerializeField] bool            m_ForceShow;
        [SerializeField] MobileButton[]  m_Slots;
        [SerializeField] ContextConfig[] m_Configs;

        // ── 싱글톤 ───────────────────────────────────────────────────

        public static MobileHUD Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            bool show = Application.isMobilePlatform || m_ForceShow;
            gameObject.SetActive(show);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 공개 API ─────────────────────────────────────────────────

        /// <summary>
        /// 컨텍스트에 맞게 버튼 슬롯 액션 및 활성화 상태를 갱신한다.
        /// 매칭 Config 가 없으면 아무 동작도 하지 않는다.
        /// </summary>
        public void SetContext(MobileContext ctx)
        {
            if (m_Slots == null || m_Configs == null) return;

            ContextConfig? match = null;
            foreach (var cfg in m_Configs)
            {
                if (cfg.Context == ctx) { match = cfg; break; }
            }

            if (match == null) return;

            MobileAction[] actions = match.Value.SlotActions ?? System.Array.Empty<MobileAction>();

            for (int i = 0; i < m_Slots.Length; i++)
            {
                if (m_Slots[i] == null) continue;

                if (i < actions.Length)
                {
                    m_Slots[i].SetAction(actions[i]);
                    m_Slots[i].gameObject.SetActive(true);
                }
                else
                {
                    m_Slots[i].gameObject.SetActive(false);
                }
            }
        }

        // ── 테스트 전용 ──────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>EditMode 테스트에서 슬롯을 직접 주입할 때 사용.</summary>
        public void SetSlotsForTest(MobileButton[] slots) => m_Slots = slots;

        /// <summary>EditMode 테스트에서 컨텍스트 설정을 직접 주입할 때 사용.</summary>
        public void SetConfigsForTest(ContextConfig[] configs) => m_Configs = configs;
#endif
    }
}
```

- [ ] **Step 3: 테스트 실행 — 통과 확인**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit
```

Expected: `MobileInputTests` 7개 모두 PASS

---

## Task 5: GlobalController MobileHUD 스폰

**Files:**
- Modify: `Assets/Scripts/Core/Asset/AssetKeys.cs`
- Modify: `Assets/Scripts/Core/Managers/GlobalController.cs`

- [ ] **Step 1: AssetKeys 에 PREFAB_MOBILE_HUD 추가**

`Assets/Scripts/Core/Asset/AssetKeys.cs` 의 `// ── 고정 프리팹` 섹션에 추가:

```csharp
        public const string PREFAB_MOBILE_HUD   = "prefab/mobile_hud";
```

(기존 `PREFAB_HUD = "prefab/hud";` 다음 줄)

- [ ] **Step 2: GlobalController 에 MobileHUD 프리팹 필드 + 스폰 추가**

`Assets/Scripts/Core/Managers/GlobalController.cs` 에서:

1. `[Header("Assets")]` 아래에 필드 추가:
```csharp
        [SerializeField] GameObject m_MobileHUDPrefab;
```

2. `Awake()` 의 `CreateManagers()` 호출 **이전** 에 스폰 코드 추가:
```csharp
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SpawnMobileHUD();
            CreateManagers();
            InitManagers();
        }
```

3. `SpawnMobileHUD()` 메서드 추가 (`CreateManagers()` 아래):
```csharp
        void SpawnMobileHUD()
        {
            if (m_MobileHUDPrefab == null) return;
            Instantiate(m_MobileHUDPrefab);  // MobileHUD.Awake 에서 DontDestroyOnLoad 처리
        }
```

- [ ] **Step 3: read_console 로 컴파일 오류 없음 확인**

MCP `read_console` → Error 없음

---

## Task 6: 씬 컨트롤러 연동

**Files:**
- Modify: `Assets/Scripts/Dungeon/DungeonMapController.cs`
- Modify: `Assets/Scripts/UI/HUD/InteractionPrompt.cs`
- Modify: `Assets/Scripts/Management/ManagementSceneController.cs`
- Modify: `Assets/Scripts/Cooking/KitchenSceneController.cs`
- Modify: `Assets/Scripts/Restaurant/RestaurantSceneController.cs`

- [ ] **Step 1: DungeonMapController — Start 에서 SetContext(Dungeon)**

`Assets/Scripts/Dungeon/DungeonMapController.cs` 의 `Start()` 또는 `OnInit()` 끝에 추가:

```csharp
            UI.Mobile.MobileHUD.Instance?.SetContext(UI.Mobile.MobileContext.Dungeon);
```

using 없이 전체 네임스페이스로 참조. 파일 상단에 using 추가해도 됨:
```csharp
using MonsterKitchen.UI.Mobile;
```
그러면 `MobileHUD.Instance?.SetContext(MobileContext.Dungeon);`

- [ ] **Step 2: InteractionPrompt — Trigger 진입/이탈 시 SetContext**

`Assets/Scripts/UI/HUD/InteractionPrompt.cs` 에 using 추가:
```csharp
using MonsterKitchen.UI.Mobile;
```

`OnTriggerEnter2D` 끝에 추가:
```csharp
            MobileHUD.Instance?.SetContext(MobileContext.DungeonInteract);
```

`OnTriggerExit2D` 끝에 추가 (던전씬에서만 복귀):
```csharp
            // 던전씬에서만 Dungeon 컨텍스트로 복귀 (다른 씬은 각 컨트롤러가 관리)
            if (Dungeon.DungeonMapController.Instance != null)
                MobileHUD.Instance?.SetContext(MobileContext.Dungeon);
```

- [ ] **Step 3: ManagementSceneController — OnInit 에서 SetContext(Exploration)**

`Assets/Scripts/Management/ManagementSceneController.cs` 에 using 추가:
```csharp
using MonsterKitchen.UI.Mobile;
```

`OnInit()` 끝에 추가:
```csharp
            MobileHUD.Instance?.SetContext(MobileContext.Exploration);
```

- [ ] **Step 4: KitchenSceneController — Start 에서 SetContext(Exploration)**

`Assets/Scripts/Cooking/KitchenSceneController.cs` 를 읽어 `Start()` 또는 `Awake()` 끝에 추가:
```csharp
using MonsterKitchen.UI.Mobile;
// ...
MobileHUD.Instance?.SetContext(MobileContext.Exploration);
```

- [ ] **Step 5: RestaurantSceneController — Start 에서 SetContext(Exploration)**

`Assets/Scripts/Restaurant/RestaurantSceneController.cs` 를 읽어 `Start()` 또는 `Awake()` 끝에 추가:
```csharp
using MonsterKitchen.UI.Mobile;
// ...
MobileHUD.Instance?.SetContext(MobileContext.Exploration);
```

- [ ] **Step 6: read_console 로 컴파일 오류 없음 확인**

MCP `read_console` → Error 없음

---

## Task 7: MobileHUD.prefab 생성 (Unity MCP)

**Files:**
- Create: `Assets/Prefabs/UI/MobileHUD.prefab`

> MCP 도구 사용. `isCompiling: false` 확인 후 진행.

- [ ] **Step 1: MobileHUD 루트 GameObject + Canvas 생성**

MCP `manage_gameobject(action: "create", name: "MobileHUD")` →  
MCP `manage_components(action: "add", component: "Canvas")` → RenderMode = Screen Space - Overlay, sortingOrder = 100  
MCP `manage_components(action: "add", component: "CanvasScaler")` → UIScaleMode = Scale With Screen Size, referenceResolution (1080, 1920)  
MCP `manage_components(action: "add", component: "GraphicRaycaster")`  
MCP `manage_components(action: "add", component: "EventSystem")` (없으면)  
MCP `manage_components(action: "add", component: "MonsterKitchen.UI.Mobile.MobileHUD")`  

- [ ] **Step 2: Joystick 자식 생성**

```
MobileHUD
└── Joystick             ← RectTransform, VirtualJoystick 컴포넌트
    ├── Background       ← Image (white circle sprite, alpha 0.3, size 160x160)
    └── Handle           ← Image (white circle sprite, alpha 0.6, size 80x80)
```

MCP `manage_gameobject(action: "create", name: "Joystick", parent: "MobileHUD")`  
MCP `manage_components(action: "add", component: "MonsterKitchen.UI.Mobile.VirtualJoystick")`  

RectTransform 설정 (좌측 하단):
- anchors: min(0,0) max(0,0), pivot(0,0)
- anchoredPosition: (60, 60)

Background:  
MCP `manage_gameobject(action: "create", name: "Background", parent: "Joystick")`  
MCP `manage_components(action: "add", component: "Image")` → color (1,1,1,0.3), size (160,160)

Handle:  
MCP `manage_gameobject(action: "create", name: "Handle", parent: "Joystick")`  
MCP `manage_components(action: "add", component: "Image")` → color (1,1,1,0.6), size (80,80)

VirtualJoystick Inspector 연결:
- m_Background → Background RectTransform
- m_Handle → Handle RectTransform

- [ ] **Step 3: ButtonPanel + Slot 3개 생성**

```
MobileHUD
└── ButtonPanel          ← RectTransform (우측 하단)
    ├── Slot0            ← Image + MobileButton (Dash)
    ├── Slot1            ← Image + MobileButton (Skill1)
    └── Slot2            ← Image + MobileButton (Skill2)
```

ButtonPanel RectTransform:
- anchors: min(1,0) max(1,0), pivot(1,0)
- anchoredPosition: (-20, 20)
- width: 280, height: 120

각 Slot (size 80x80, 간격 10px):
- Slot0: anchoredPosition (-260, 20) — Dash
- Slot1: anchoredPosition (-170, 20) — Skill1
- Slot2: anchoredPosition (-80,  20) — Skill2

각 Slot 에 `MobileButton` 컴포넌트 추가 + `m_Action` 설정

- [ ] **Step 4: MobileHUD Inspector 연결**

MobileHUD 컴포넌트:
- `m_ForceShow` = true (에디터 테스트용, 릴리즈 전 false 로 변경)
- `m_Slots` = [Slot0, Slot1, Slot2]
- `m_Configs` (3개):

```
[0] Context: Dungeon
    SlotActions: [Dash, Skill1, Skill2]

[1] Context: DungeonInteract
    SlotActions: [Dash, Skill1, Interact]

[2] Context: Exploration
    SlotActions: [Dash, Interact]
```

- [ ] **Step 5: Prefab 저장**

MCP `manage_asset(action: "create_prefab", gameObjectName: "MobileHUD", prefabPath: "Prefabs/UI/MobileHUD.prefab")`

- [ ] **Step 6: GlobalController Inspector 에서 m_MobileHUDPrefab 연결**

StartScene 의 GlobalController GameObject 선택 →  
Inspector `m_MobileHUDPrefab` 슬롯에 `MobileHUD.prefab` 연결

---

## Task 8: 검증

- [ ] **Step 1: EditMode 전체 테스트 통과**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -projectPath "C:/Users/CHOI/Desktop/Program/Unity/FantasyTycoon" -runTests -testPlatform EditMode -testResults results.xml -batchmode -quit
```

Expected: 기존 테스트 포함 모두 PASS

- [ ] **Step 2: 에디터 Play 모드 — 조이스틱 이동 확인**

1. DungeonScene 열기
2. `MobileHUD.m_ForceShow = true` 확인
3. Play → Game View 에서 조이스틱 Background 드래그
4. Player 이동 확인

- [ ] **Step 3: 에디터 Play 모드 — 컨텍스트 전환 확인**

1. DungeonScene Play
2. 초기 상태: Slot2 = Skill2 버튼
3. 플레이어를 DungeonExit/ResourceNode 트리거 안으로 이동
4. Slot2 → Interact 버튼으로 교체됨 확인
5. 트리거 이탈 → Slot2 → Skill2 복귀 확인

- [ ] **Step 4: ManagementScene 컨텍스트 확인**

1. ManagementScene Play
2. MobileHUD 버튼 2개만 표시 (Dash + Interact)
3. Slot2 숨김 확인
