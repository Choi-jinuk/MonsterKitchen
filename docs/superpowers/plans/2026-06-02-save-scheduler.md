# Save Scheduler Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 주기 자동 저장(30s), 중요 패킷 즉시 저장, 앱 이벤트 저장, HUD 저장 표시 인디케이터를 구현한다.

**Architecture:** 순수 C# `SaveScheduler` 싱글톤이 dirty 플래그와 저장 타이머를 관리한다. `NetworkManager`가 패킷 중요도에 따라 `MarkDirty()` 또는 `ForceSave()`를 호출한다. `GameHUD`는 `OnSaveStateChanged` 이벤트를 구독해 "저장 중..." 인디케이터를 표시한다.

**Tech Stack:** Unity 6 (6000.4.2f1), C#, UIToolkit (UXML/USS), NUnit EditMode Tests

---

## 파일 목록

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Core/Managers/SaveScheduler.cs` | **신규** |
| `Assets/Tests/EditMode/SaveSchedulerTests.cs` | **신규** |
| `Assets/Scripts/Data/Table/GameConfig.cs` | 수정 — `AutoSaveIntervalSeconds` 필드 추가 |
| `Assets/Scripts/Core/Managers/GlobalController.cs` | 수정 — SaveScheduler 프로퍼티 + Init/Start/앱 이벤트 |
| `Assets/Scripts/Core/Managers/NetworkManager.cs` | 수정 — 패킷별 MarkDirty/ForceSave 호출 |
| `Assets/Data/CSV/StringData.csv` | 수정 — `UI_SAVING` 키 추가 |
| `Assets/UI/HUD.uxml` | 수정 — `save-indicator` Label 추가 |
| `Assets/UI/HUD.uss` | 수정 — `.save-indicator` 스타일 추가 |
| `Assets/Scripts/UI/HUD/GameHUD.cs` | 수정 — 인디케이터 바인딩 + SaveScheduler 구독 |

---

## Task 1: GameConfig — AutoSaveIntervalSeconds 필드 추가

**Files:**
- Modify: `Assets/Scripts/Data/Table/GameConfig.cs`

- [ ] **Step 1: 필드 추가**

`GradeThresholdLegendary` 필드 직후에 아래 블록을 삽입한다.

```csharp
        // ── Save ─────────────────────────────────────────────────────────
        [Header("Save")]
        [Tooltip("자동 저장 주기 (초). 0 이면 자동 저장 비활성.")]
        [SerializeField] int m_AutoSaveIntervalSeconds = 30;
        public int AutoSaveIntervalSeconds => m_AutoSaveIntervalSeconds;
```

`GameConfig.cs` 기존 `GradeThresholdLegendary` 라인 뒤, `// ── 향후 확장 예정` 주석 앞에 삽입.

- [ ] **Step 2: 컴파일 확인**

Unity Editor 콘솔에 에러 없음 확인. `Assets/Data/SO/` 폴더의 `GameConfig.asset`을 Inspector에서 열어 `Auto Save Interval Seconds = 30` 노출 확인.

---

## Task 2: SaveScheduler — 신규 클래스 + EditMode 테스트

**Files:**
- Create: `Assets/Scripts/Core/Managers/SaveScheduler.cs`
- Create: `Assets/Tests/EditMode/SaveSchedulerTests.cs`

- [ ] **Step 1: 테스트 파일 먼저 작성 (failing)**

`Assets/Tests/EditMode/SaveSchedulerTests.cs`:

```csharp
using NUnit.Framework;
using MonsterKitchen.Core;

namespace MonsterKitchen.Tests.EditMode
{
    // ====================================================================
    //  SaveSchedulerTests — SaveScheduler 순수 함수 동작 검증
    //
    //  SaveScheduler(Action saveAction) 생성자로 저장 액션을 주입한다.
    //  MonoBehaviour runner 없이 동기 경로만 테스트한다.
    // ====================================================================
    public class SaveSchedulerTests
    {
        SaveScheduler m_Sched;
        int           m_SaveCallCount;

        [SetUp]
        public void SetUp()
        {
            m_SaveCallCount = 0;
            m_Sched = new SaveScheduler(() => m_SaveCallCount++);
        }

        [TearDown]
        public void TearDown()
        {
            // Instance 초기화 (다음 테스트에 영향 방지)
            SaveScheduler.ResetInstanceForTest();
        }

        [Test]
        public void MarkDirty_SetsIsDirtyTrue()
        {
            m_Sched.MarkDirty();
            Assert.IsTrue(m_Sched.IsDirty);
        }

        [Test]
        public void ForceSave_CallsSaveAction()
        {
            m_Sched.ForceSave();
            Assert.AreEqual(1, m_SaveCallCount);
        }

        [Test]
        public void ForceSave_ClearsDirtyFlag()
        {
            m_Sched.MarkDirty();
            m_Sched.ForceSave();
            Assert.IsFalse(m_Sched.IsDirty);
        }

        [Test]
        public void ForceSave_InvokesOnSaveComplete()
        {
            bool completed = false;
            m_Sched.OnSaveComplete += () => completed = true;
            m_Sched.ForceSave();
            Assert.IsTrue(completed);
        }

        [Test]
        public void ForceSave_WhileInProgress_SkipsSaveAction()
        {
            // saveAction 내부에서 ForceSave 재진입 시 건너뜀을 검증
            SaveScheduler sched = null;
            int callCount = 0;
            sched = new SaveScheduler(() =>
            {
                callCount++;
                sched.ForceSave(); // 재진입 시도
            });
            sched.ForceSave();
            Assert.AreEqual(1, callCount); // 두 번째 호출은 무시됨
        }

        [Test]
        public void ForceSave_WithOnCompleteCallback_InvokesCallback()
        {
            bool cbCalled = false;
            m_Sched.ForceSave(onComplete: () => cbCalled = true);
            Assert.IsTrue(cbCalled);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

Unity Test Runner (EditMode) 실행. `SaveScheduler` 미존재 → 컴파일 에러로 테스트 0개 로드됨 확인.

- [ ] **Step 3: SaveScheduler 구현**

`Assets/Scripts/Core/Managers/SaveScheduler.cs`:

```csharp
using System;
using System.Collections;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  SaveScheduler — 자동 저장 주기 관리자
    //
    //  ▶ 역할
    //    dirty 플래그 + 주기 타이머로 ServerDBManager.Save() 호출 시점을 관리.
    //    중요 패킷은 NetworkManager에서 ForceSave()를 직접 호출한다.
    //
    //  ▶ 의존성 주입 (테스트 지원)
    //    생성자에 saveAction을 주입하면 ServerDBManager 없이 테스트 가능.
    //    런타임에는 기본값(null)을 사용해 ServerDBManager.Instance?.Save() 호출.
    //
    //  ▶ IsSaving 표시 시간
    //    실제 저장은 동기 (수 ms). UX를 위해 SaveIndicatorSeconds 동안 유지.
    //    m_IsSaveInProgress는 실제 파일 쓰기 중만 true → 재진입 방지.
    // ====================================================================

    public class SaveScheduler
    {
        public static SaveScheduler Instance { get; private set; }

        readonly Action    m_SaveAction;
        MonoBehaviour      m_Runner;
        bool               m_IsSaveInProgress;

        const float SaveIndicatorSeconds = 1f;

        // ── 공개 상태 ────────────────────────────────────────────────
        public bool IsDirty  { get; private set; }
        public bool IsSaving { get; private set; }

        public event Action<bool> OnSaveStateChanged;
        public event Action       OnSaveComplete;

        // ── 생성자 ───────────────────────────────────────────────────
        public SaveScheduler(Action saveAction = null)
        {
            m_SaveAction = saveAction ?? (() => ServerDBManager.Instance?.Save());
        }

        // ── 생명주기 ─────────────────────────────────────────────────

        public void Init() => Instance = this;

        /// <summary>GlobalController.Start() 에서 호출. 자동 저장 코루틴 시작.</summary>
        public void StartAutoSaveCycle(MonoBehaviour runner)
        {
            m_Runner = runner;
            runner.StartCoroutine(AutoSaveCycle());
        }

        // ── 공개 API ─────────────────────────────────────────────────

        /// <summary>일반 패킷 후 호출. 다음 주기 tick 에서 저장된다.</summary>
        public void MarkDirty() => IsDirty = true;

        /// <summary>
        /// 즉시 저장. 중요 패킷 / 앱 이벤트 / 수동 호출.
        /// runner가 없으면 (EditMode 테스트) 동기 완료 후 즉시 OnSaveComplete.
        /// </summary>
        public void ForceSave(Action onComplete = null)
        {
            if (m_IsSaveInProgress)
            {
                DebugUtil.LogWarning("[SaveScheduler] ForceSave 재진입 — 건너뜀.");
                onComplete?.Invoke();
                return;
            }

            m_IsSaveInProgress = true;
            SetSaving(true);

            try
            {
                m_SaveAction?.Invoke();
                IsDirty = false;
            }
            finally
            {
                m_IsSaveInProgress = false;
            }

            // runner 있으면 인디케이터 표시 유지 후 숨김
            if (m_Runner != null)
            {
                m_Runner.StartCoroutine(HideSavingIndicator(onComplete));
            }
            else
            {
                // 테스트 / runner 없는 환경 — 즉시 완료
                SetSaving(false);
                onComplete?.Invoke();
                OnSaveComplete?.Invoke();
            }
        }

        // ── 내부 ─────────────────────────────────────────────────────

        IEnumerator AutoSaveCycle()
        {
            while (true)
            {
                int interval = GameConfig.Current?.AutoSaveIntervalSeconds ?? 30;
                float wait   = interval > 0 ? interval : 30f;
                yield return new WaitForSeconds(wait);

                if (interval > 0 && IsDirty)
                    ForceSave();
            }
        }

        IEnumerator HideSavingIndicator(Action onComplete)
        {
            yield return new WaitForSeconds(SaveIndicatorSeconds);
            SetSaving(false);
            onComplete?.Invoke();
            OnSaveComplete?.Invoke();
        }

        void SetSaving(bool saving)
        {
            IsSaving = saving;
            OnSaveStateChanged?.Invoke(saving);
        }

#if UNITY_EDITOR
        /// <summary>EditMode 테스트에서 Instance 초기화 용도. 프로덕션 코드에서 호출 금지.</summary>
        public static void ResetInstanceForTest() => Instance = null;
#endif
    }
}
```

- [ ] **Step 4: 테스트 실행 — 통과 확인**

Unity Test Runner EditMode 실행.
기대 결과: `SaveSchedulerTests` 6개 모두 Passed.

- [ ] **Step 5: 컴파일 에러 없음 확인**

Unity 콘솔 에러 0개 확인.

---

## Task 3: GlobalController — SaveScheduler 연결

**Files:**
- Modify: `Assets/Scripts/Core/Managers/GlobalController.cs`

- [ ] **Step 1: 프로퍼티 추가**

`ServerDB` 프로퍼티 선언 직후에 삽입:

```csharp
        public SaveScheduler     SaveSched    { get; private set; }
```

- [ ] **Step 2: CreateManagers()에 생성 추가**

`ServerDB = new ServerDBManager();` 줄 바로 다음에 삽입:

```csharp
            SaveSched   = new SaveScheduler();
```

- [ ] **Step 3: InitManagers()에 Init 추가**

`ServerDB.Init();` 줄 바로 다음에 삽입:

```csharp
            SaveSched.Init();
```

- [ ] **Step 4: Start()에 자동 저장 사이클 시작 추가**

기존:
```csharp
        void Start() => StartCoroutine(Startup.Run());
```

변경:
```csharp
        void Start()
        {
            StartCoroutine(Startup.Run());
            SaveSched.StartAutoSaveCycle(this);
        }
```

- [ ] **Step 5: 앱 이벤트 핸들러 추가**

`OnDestroy()` 메서드 직후에 추가:

```csharp
        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) SaveSched?.ForceSave();
        }

        void OnApplicationQuit() => SaveSched?.ForceSave();
```

- [ ] **Step 6: 컴파일 + 콘솔 확인**

Unity 재컴파일 후 에러 0개. Play Mode 진입 시 콘솔에 `[SaveScheduler]` 로그 미출력 (dirty 없으므로 정상).

---

## Task 4: NetworkManager — 패킷별 저장 트리거 추가

**Files:**
- Modify: `Assets/Scripts/Core/Managers/NetworkManager.cs`

- [ ] **Step 1: RequestEarnGold에 ForceSave 추가**

`onResult?.Invoke(newGold);` 줄 직전에 삽입:

```csharp
            GlobalController.Instance?.SaveSched.ForceSave();
```

결과 (변경 후 메서드 말미):
```csharp
            PlayerDataManager.Instance.ApplyGold(newGold);
            DebugUtil.Log(StringUtil.Format("[Network] EarnGold +{0}G → 총 {1}G", amount, newGold));
            GlobalController.Instance?.SaveSched.ForceSave();
            onResult?.Invoke(newGold);
```

- [ ] **Step 2: RequestSpendGold에 ForceSave 추가**

`onResult?.Invoke(true, newGold);` 직전에 삽입:

```csharp
            GlobalController.Instance?.SaveSched.ForceSave();
```

- [ ] **Step 3: RequestAddIngredient에 MarkDirty 추가**

`onResult?.Invoke(ingredientId, newQty);` 직전에 삽입:

```csharp
            GlobalController.Instance?.SaveSched.MarkDirty();
```

- [ ] **Step 4: RequestCook에 ForceSave 추가**

`onResult?.Invoke(true, food);` 직전에 삽입:

```csharp
            GlobalController.Instance?.SaveSched.ForceSave();
```

- [ ] **Step 5: RequestServeFood에 ForceSave 추가**

`onResult?.Invoke(true, grade, remaining);` 직전에 삽입:

```csharp
            GlobalController.Instance?.SaveSched.ForceSave();
```

- [ ] **Step 6: RequestUpgrade에 ForceSave 추가**

`onResult?.Invoke(true);` 직전에 삽입:

```csharp
            GlobalController.Instance?.SaveSched.ForceSave();
```

- [ ] **Step 7: 컴파일 확인**

Unity 콘솔 에러 0개. Play Mode에서 골드 획득 시 콘솔에 `[ServerDB] 저장 완료` 로그 출력 확인.

---

## Task 5: StringData.csv — UI_SAVING 키 추가

**Files:**
- Modify: `Assets/Data/CSV/StringData.csv`

- [ ] **Step 1: CSV 마지막 줄 뒤에 행 추가**

파일 끝에 아래 행을 추가한다 (ID 8067, 마지막 ID 8066 다음):

```
UI_SAVING,8067,UI_SAVING,저장 중...,Saving...
```

- [ ] **Step 2: DataManager 동기화**

Unity Editor 메뉴 `MonsterKitchen > Data Manager` 창 열기 → `Sync All` 클릭 → StringTable SO에 `UI_SAVING` 행 추가됨 확인.

---

## Task 6: HUD.uxml + HUD.uss — save-indicator 요소 추가

**Files:**
- Modify: `Assets/UI/HUD.uxml`
- Modify: `Assets/UI/HUD.uss`

- [ ] **Step 1: HUD.uxml에 save-indicator 추가**

`hud-root` VisualElement 내부, `gold-label`을 감싸는 `<ui:VisualElement class="hud-chip">` 블록 직후에 삽입:

```xml
        <ui:VisualElement name="save-indicator" class="hud-chip save-indicator" style="display: none;">
            <ui:Label name="save-label" text="저장 중..." class="hud-label save-indicator-label" style="-unity-font-definition: url(&quot;project://database/Assets/TextMesh%20Pro/Fonts/GowunDodum-Regular.ttf?fileID=12800000&amp;guid=35231759caa53a64da6b9ef68c3011d2&amp;type=3#GowunDodum-Regular&quot;);"/>
        </ui:VisualElement>
```

삽입 위치 — `</ui:VisualElement>` (hud-root 닫기 태그) 바로 앞, gold chip 블록 뒤:

```xml
    <ui:VisualElement name="hud-root" class="hud-root">
        <!-- hp-chip -->
        <!-- day-label chip -->
        <!-- gold-label chip -->
        <ui:VisualElement class="hud-chip">
            <ui:Label name="gold-label" .../>
        </ui:VisualElement>
        <!-- ↓ 여기에 삽입 -->
        <ui:VisualElement name="save-indicator" class="hud-chip save-indicator" style="display: none;">
            <ui:Label name="save-label" text="저장 중..." class="hud-label save-indicator-label" style="-unity-font-definition: url(&quot;project://database/Assets/TextMesh%20Pro/Fonts/GowunDodum-Regular.ttf?fileID=12800000&amp;guid=35231759caa53a64da6b9ef68c3011d2&amp;type=3#GowunDodum-Regular&quot;);"/>
        </ui:VisualElement>
    </ui:VisualElement>
```

- [ ] **Step 2: HUD.uss에 스타일 추가**

파일 맨 끝에 추가:

```css
/* ── 저장 인디케이터 ─────────────────────────────── */
.save-indicator {
    opacity: 0.85;
}

.save-indicator-label {
    font-size: 14px;
    color: rgb(160, 220, 160);
}
```

---

## Task 7: GameHUD — save-indicator 바인딩

**Files:**
- Modify: `Assets/Scripts/UI/HUD/GameHUD.cs`

- [ ] **Step 1: 필드 추가**

`m_MinimapRoot` 필드 선언 직후에 삽입:

```csharp
        // ── 저장 인디케이터 ───────────────────────────────────────────
        VisualElement m_SaveIndicator;
```

- [ ] **Step 2: Start()에 요소 캐싱 추가**

`m_MinimapRoot = root.Q<VisualElement>("minimap-root");` 줄 직후에 삽입:

```csharp
            m_SaveIndicator = root.Q<VisualElement>("save-indicator");
```

- [ ] **Step 3: Start()에 SaveScheduler 구독 추가**

`SceneManager.sceneLoaded += OnSceneLoaded;` 줄 직후에 삽입:

```csharp
            if (GlobalController.Instance?.SaveSched != null)
                GlobalController.Instance.SaveSched.OnSaveStateChanged += OnSaveStateChanged;
```

- [ ] **Step 4: OnDestroy()에 구독 해제 추가**

`SceneManager.sceneLoaded -= OnSceneLoaded;` 줄 직후에 삽입:

```csharp
            if (GlobalController.Instance?.SaveSched != null)
                GlobalController.Instance.SaveSched.OnSaveStateChanged -= OnSaveStateChanged;
```

- [ ] **Step 5: OnSaveStateChanged 핸들러 메서드 추가**

`UpdateGold` 메서드 직전(또는 파일 맨 끝 `}` 전)에 추가:

```csharp
        // ================================================================
        //  저장 인디케이터
        // ================================================================

        void OnSaveStateChanged(bool isSaving)
        {
            if (m_SaveIndicator == null) return;
            m_SaveIndicator.style.display = isSaving ? DisplayStyle.Flex : DisplayStyle.None;
        }
```

- [ ] **Step 6: 최종 확인**

Unity Play Mode 진입. 골드 획득(던전 클리어 또는 식당 서빙) 후 HUD 우측 상단에 "저장 중..." 1초간 표시 → 자동 소멸 확인.

- [ ] **Step 7: EditMode 테스트 전체 재실행**

Unity Test Runner → 모든 EditMode 테스트 통과 확인.

---

## 자체 검토 결과

**Spec 커버리지:**
- ✅ 주기 자동 저장 (AutoSaveCycle coroutine)
- ✅ 중요 패킷 즉시 저장 (RequestEarnGold/Cook/ServeFood/Upgrade → ForceSave)
- ✅ OnApplicationPause / OnApplicationQuit
- ✅ ForceSave() 공개 API
- ✅ HUD 저장 표시
- ✅ GameConfig.AutoSaveIntervalSeconds

**타입 일관성:**
- `SaveScheduler.ForceSave(Action onComplete = null)` — Task 2, 3, 4 모두 동일 시그니처
- `SaveScheduler.OnSaveStateChanged` — `Action<bool>` — Task 2, 7 동일
- `GlobalController.SaveSched` — Task 3, 4, 7 모두 동일 프로퍼티명

**플레이스홀더:** 없음.
