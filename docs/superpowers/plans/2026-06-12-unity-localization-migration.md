# Unity Localization Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 커스텀 `LocaleManager` + `StringTable` SO 를 `com.unity.localization` 패키지로 전면 교체 (패턴 B — 전 사용처 `LocalizedString` 객체).

**Architecture:** CSV Extension 으로 `StringData.csv`(패키지 포맷 변환) 를 단일 String Table Collection `Strings` 로 임포트. 모든 호출부는 `LocalizedString` 인스턴스를 캐시해 사용 — 테이블 클래스는 `Loc.Get(ref cache, key)` 동기 조회, UI 는 `StringChanged` 콜백 자가 갱신. `GameStartup.DataLoad` 에서 초기화+프리로드.

**Tech Stack:** Unity 6 (6000.4.2f1), com.unity.localization, Addressables(기설치), UIToolkit, Unity Test Framework.

**스펙:** `docs/superpowers/specs/2026-06-12-unity-localization-migration-design.md`

**프로젝트 규칙 (skill 기본값 오버라이드):**
- **커밋 금지** — 커밋은 모듈 전체 완성 후 사용자 명시 요청 시만. 각 Task 의 Commit 단계 없음.
- 스크립트 수정 후 `read_console` 로 컴파일 에러 0 확인 필수.
- 각 Task 완료 시 `WORK_IN_PROGRESS.md` 처리 내역에 한 줄 기록.

---

### Task 1: 패키지 설치 + asmdef 참조

**Files:**
- Modify: `Packages/manifest.json`
- Modify: `Assets/Scripts/MonsterKitchen.asmdef`
- Modify: `Assets/Tests/EditMode/Tests.EditMode.asmdef`
- Modify: `Assets/Tests/PlayMode/Tests.PlayMode.asmdef`

- [ ] **Step 1: 패키지 추가**

Unity MCP `manage_packages` 로 `com.unity.localization` 설치 (최신 verified). MCP 불가 시 `Packages/manifest.json` dependencies 에 추가:

```json
"com.unity.localization": "1.5.4",
```

- [ ] **Step 2: asmdef 참조 추가**

`MonsterKitchen.asmdef` references 에 `"Unity.Localization"` 추가:

```json
"references": [
    "Unity.InputSystem",
    "Unity.TextMeshPro",
    "Unity.Cinemachine",
    "Unity.RenderPipelines.Universal.Runtime",
    "Unity.RenderPipelines.Universal.2D.Runtime",
    "ZString",
    "Unity.Localization"
],
```

두 테스트 asmdef 에도 동일하게 `"Unity.Localization"` 추가.

- [ ] **Step 3: 컴파일 확인**

`refresh_unity` → `read_console` (Error 필터). Expected: 에러 0.

---

### Task 2: StringData.csv 포맷 변환 (1회성)

**Files:**
- Modify: `Assets/Data/CSV/StringData.csv` (in-place)

- [ ] **Step 1: 변환 전 데이터 행 수 기록**

```powershell
(Get-Content "Assets\Data\CSV\StringData.csv" -Encoding UTF8 |
  Where-Object { $_ -notmatch '^\s*;' -and $_ -notmatch '^#TYPE' -and $_ -notmatch '^_key' -and $_.Trim() -ne '' }).Count
```

Expected: 데이터 행 수 출력 (예: 74). 이 수를 N 으로 기록 — 변환 후 검증에 사용.

- [ ] **Step 2: 변환 스크립트 실행**

기존 포맷 `_key,Id,StringId,Ko,En` (+ `;` 주석, `#TYPE` 행) → 패키지 CSV Extension 기본 포맷:

```powershell
$src  = "Assets\Data\CSV\StringData.csv"
$rows = Get-Content $src -Encoding UTF8 |
    Where-Object { $_ -notmatch '^\s*;' -and $_ -notmatch '^#TYPE' -and $_ -notmatch '^_key' -and $_.Trim() -ne '' }

$out = New-Object System.Collections.Generic.List[string]
$out.Add('Key,Id,Shared Comments,Korean(ko),English(en)')
foreach ($r in $rows) {
    # CSV 필드 파싱 (따옴표 내 콤마 보존)
    $f = [System.Text.RegularExpressions.Regex]::Split($r, ',(?=(?:[^"]*"[^"]*")*[^"]*$)')
    # _key[0], Id[1], StringId[2], Ko[3], En[4] → Key=StringId, Id 유지
    $out.Add(('{0},{1},,{2},{3}' -f $f[2], $f[1], $f[3], ($f[4..($f.Length-1)] -join ',')))
}
[System.IO.File]::WriteAllLines((Resolve-Path $src), $out, (New-Object System.Text.UTF8Encoding $true))
```

- [ ] **Step 3: 변환 검증**

```powershell
$lines = Get-Content "Assets\Data\CSV\StringData.csv" -Encoding UTF8
$lines[0]                                  # Expected: Key,Id,Shared Comments,Korean(ko),English(en)
$lines.Count - 1                           # Expected: N (Step 1 의 데이터 행 수와 동일)
$keys = $lines[1..($lines.Count-1)] | ForEach-Object { ($_ -split ',')[0] }
($keys | Group-Object | Where-Object Count -gt 1).Count   # Expected: 0 (Key 중복 없음)
```

한국어 텍스트 정상 표시 확인 (인코딩 UTF-8 BOM). 깨졌으면 git checkout 으로 복원 후 재시도.

---

### Task 3: 에디터 셋업 유틸리티 (Settings/Locale/Collection/CSV Import)

**Files:**
- Create: `Assets/Editor/Localization/LocalizationSetupTool.cs`

- [ ] **Step 1: 유틸리티 작성**

```csharp
// ====================================================================
//  LocalizationSetupTool — Unity Localization 1회성 셋업 + CSV 재임포트
//
//  ▶ MonsterKitchen/Localization/Setup       : 전체 셋업 (멱등 — 재실행 안전)
//  ▶ MonsterKitchen/Localization/Import CSV  : StringData.csv 재임포트만
// ====================================================================

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;

namespace MonsterKitchen.EditorTools
{
    public static class LocalizationSetupTool
    {
        const string RootDir        = "Assets/Localization";
        const string LocaleDir      = RootDir + "/Locales";
        const string TableDir       = RootDir + "/Strings";
        const string SettingsPath   = RootDir + "/LocalizationSettings.asset";
        const string CollectionName = "Strings";
        const string CsvPath        = "Assets/Data/CSV/StringData.csv";

        [MenuItem("MonsterKitchen/Localization/Setup")]
        public static void Setup()
        {
            Directory.CreateDirectory(LocaleDir);
            Directory.CreateDirectory(TableDir);

            // 1) Locale 에셋 (ko 기본, en)
            var ko = GetOrCreateLocale("ko");
            var en = GetOrCreateLocale("en");

            // en → ko 폴백
            var fallback = en.Metadata.GetMetadata<FallbackLocale>();
            if (fallback == null) en.Metadata.AddMetadata(new FallbackLocale(ko));
            else                  fallback.Locale = ko;
            EditorUtility.SetDirty(en);

            // 2) LocalizationSettings
            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = "LocalizationSettings";
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;

            // 선택 체인: PlayerPrefs → System → ko
            var selectors = settings.GetStartupLocaleSelectors();
            selectors.Clear();
            selectors.Add(new PlayerPrefLocaleSelector());
            selectors.Add(new SystemLocaleSelector());
            selectors.Add(new SpecificLocaleSelector { LocaleId = ko.Identifier });

            // 미등록 키 → 키 자체 표시 (기존 LocaleManager fallback 동작 보존)
            var db = settings.GetStringDatabase();
            db.NoTranslationFoundMessage = "{key}";
            EditorUtility.SetDirty(settings);

            // 3) String Table Collection
            var collection = LocalizationEditorSettings.GetStringTableCollection(CollectionName)
                          ?? LocalizationEditorSettings.CreateStringTableCollection(CollectionName, TableDir);

            // 프리로드 — GameStartup 동기 접근 보장
            LocalizationEditorSettings.SetPreloadTableFlag(collection, true);

            // 4) CSV 임포트
            ImportCsv();

            AssetDatabase.SaveAssets();
            Debug.Log("[LocalizationSetupTool] Setup 완료");
        }

        [MenuItem("MonsterKitchen/Localization/Import CSV")]
        public static void ImportCsv()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(CollectionName);
            if (collection == null)
            {
                Debug.LogError("[LocalizationSetupTool] Strings 컬렉션 없음 — Setup 먼저 실행");
                return;
            }

            using (var reader = new StreamReader(CsvPath, System.Text.Encoding.UTF8))
                Csv.ImportInto(reader, collection);

            // 빈 En 셀 → 엔트리 제거 (폴백이 ko 로 동작하도록)
            var enTable = collection.StringTables.FirstOrDefault(
                t => t.LocaleIdentifier.Code == "en");
            if (enTable != null)
            {
                var empty = enTable.Values.Where(e => string.IsNullOrEmpty(e.Value))
                                          .Select(e => e.KeyId).ToList();
                foreach (var keyId in empty) enTable.Remove(keyId);
                EditorUtility.SetDirty(enTable);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[LocalizationSetupTool] CSV 임포트 완료 — {collection.SharedData.Entries.Count}개 키");
        }

        static Locale GetOrCreateLocale(string code)
        {
            string path = $"{LocaleDir}/{code}.asset";
            var locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale == null)
            {
                locale = Locale.CreateLocale(new LocaleIdentifier(code));
                AssetDatabase.CreateAsset(locale, path);
                LocalizationEditorSettings.AddLocale(locale);
            }
            return locale;
        }
    }
}
```

> API 주의: `GetStartupLocaleSelectors()` / `SetPreloadTableFlag` 시그니처가 설치 버전과 다르면
> `unity_reflect` 로 실제 시그니처 확인 후 맞춰 수정 (의도는 주석 그대로).
>
> 스펙의 "CSV Extension 연결" 은 `Csv.ImportInto` (패키지 CSV 플러그인 API) 기반 재임포트 메뉴로 충족 —
> 운영 워크플로: CSV 편집 → `MonsterKitchen/Localization/Import CSV`.

- [ ] **Step 2: 컴파일 확인**

`read_console` Error 0 확인.

- [ ] **Step 3: Setup 실행**

`execute_menu_item`: `MonsterKitchen/Localization/Setup`
Expected 콘솔: `[LocalizationSetupTool] Setup 완료`, `CSV 임포트 완료 — N개 키` (N = Task 2 데이터 행 수).

- [ ] **Step 4: 에셋 생성 검증**

`Assets/Localization/` 아래 LocalizationSettings.asset, Locales/ko.asset, Locales/en.asset, Strings/ 테이블 에셋 존재 확인. Project Settings → Localization 에 Active Settings 지정 확인 (`LocalizationEditorSettings.ActiveLocalizationSettings` 가 설정했으므로 자동).

---

### Task 4: Loc 헬퍼 (공통 진입점)

**Files:**
- Create: `Assets/Scripts/Core/Util/Loc.cs`

- [ ] **Step 1: 작성**

```csharp
// ====================================================================
//  Loc — Unity Localization 헬퍼
//
//  ▶ 테이블 클래스 프로퍼티 (동기):
//      [NonSerialized] LocalizedString m_NameLs;
//      public string DisplayName => Loc.Get(ref m_NameLs, NameKey);
//
//  ▶ UI 자가 갱신 (StringChanged):
//      m_Ls = Loc.Create("UI_INTERACT");
//      m_Ls.StringChanged += OnTextChanged;
// ====================================================================

using UnityEngine.Localization;

namespace MonsterKitchen.Core
{
    public static class Loc
    {
        /// <summary>String Table Collection 이름.</summary>
        public const string Table = "Strings";

        /// <summary>Strings 테이블 참조 LocalizedString 생성.</summary>
        public static LocalizedString Create(string key) => new(Table, key);

        /// <summary>
        /// 캐시 생성 + 동기 조회. 테이블 클래스 프로퍼티용.
        /// key 비어 있음 → string.Empty. 미등록 키 → 키 자체 반환
        /// (LocalizationSettings.NoTranslationFoundMessage = "{key}").
        /// </summary>
        public static string Get(ref LocalizedString cache, string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            cache ??= Create(key);
            return cache.GetLocalizedString();
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인** — `read_console` Error 0.

---

### Task 5: 테이블 클래스 4종 교체

**Files:**
- Modify: `Assets/Scripts/Data/Table/MonsterTable.cs:17-20`
- Modify: `Assets/Scripts/Data/Table/IngredientTable.cs:17-20`
- Modify: `Assets/Scripts/Data/Table/FoodTable.cs:17-20`
- Modify: `Assets/Scripts/Data/Table/RecipeTable.cs:24-27`

- [ ] **Step 1: MonsterTable.cs**

기존:

```csharp
/// <summary>현재 언어에 맞는 이름. 로컬라이제이션 미로드 시 NameKey 반환.</summary>
public string DisplayName  => LocaleManager.Get(NameKey);
/// <summary>현재 언어에 맞는 설명. 로컬라이제이션 미로드 시 DescKey 반환.</summary>
public string Description  => LocaleManager.Get(DescKey);
```

교체 (파일 상단 `using UnityEngine.Localization;` 추가, `using MonsterKitchen.Core;` 유지):

```csharp
[NonSerialized] LocalizedString m_NameLs;
[NonSerialized] LocalizedString m_DescLs;

/// <summary>현재 언어에 맞는 이름. 미등록 키 → NameKey 반환.</summary>
public string DisplayName  => Loc.Get(ref m_NameLs, NameKey);
/// <summary>현재 언어에 맞는 설명. 미등록 키 → DescKey 반환.</summary>
public string Description  => Loc.Get(ref m_DescLs, DescKey);
```

- [ ] **Step 2: IngredientTable.cs / FoodTable.cs** — 동일 패턴 적용 (각 파일의 `LocaleManager.Get(NameKey)` / `Get(DescKey)` 프로퍼티 2개를 위와 같은 형태로 교체, `[NonSerialized]` 캐시 필드 2개 추가, using 추가).

- [ ] **Step 3: RecipeTable.cs** — 동일 패턴. 단 기존이 `MonsterKitchen.Core.LocaleManager.Get(NameKey)` 풀네임 — `Loc.Get(ref m_NameLs, NameKey)` 로 교체 (`using MonsterKitchen.Core;` 없으면 추가).

- [ ] **Step 4: 컴파일 확인** — `read_console`. 이 시점엔 LocaleManager 아직 존재 → Error 0 이어야 함.

---

### Task 6: LocalizedLabel 재작성 (자가 갱신)

**Files:**
- Modify: `Assets/Scripts/UI/Core/LocalizedLabel.cs` (전체 교체)

- [ ] **Step 1: 재작성**

```csharp
// ====================================================================
//  LocalizedLabel — UIToolkit 로컬라이제이션 레이블 커스텀 컨트롤
//
//  ▶ UXML 사용법
//    xmlns:mk="MonsterKitchen.UI" 선언 후:
//    <mk:LocalizedLabel string-id="UI_TITLE" class="my-class" />
//
//  ▶ 동작
//    string-id → LocalizedString("Strings", key) → StringChanged 콜백으로
//    text 자동 적용. 로케일 변경 시 자가 갱신 (UIPanel 개입 불필요).
//    Localization 미초기화 시 string-id 값 그대로 표시 (에디터 프리뷰용).
// ====================================================================

using MonsterKitchen.Core;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    [UxmlElement]
    public partial class LocalizedLabel : Label
    {
        [UxmlAttribute("string-id")]
        public string StringId
        {
            get => m_StringId;
            set { m_StringId = value; Bind(); }
        }

        string          m_StringId;
        LocalizedString m_Localized;

        public LocalizedLabel()
        {
            RegisterCallback<AttachToPanelEvent>(_ => Bind());
            RegisterCallback<DetachFromPanelEvent>(_ => Unbind());
        }

        void Bind()
        {
            Unbind();
            if (string.IsNullOrEmpty(m_StringId)) return;

            text = m_StringId; // 초기값 — 로드 완료 시 StringChanged 가 덮어씀

            if (panel == null) return; // 미부착 상태 — Attach 시 재바인딩
            m_Localized = Loc.Create(m_StringId);
            m_Localized.StringChanged += OnStringChanged;
        }

        void Unbind()
        {
            if (m_Localized == null) return;
            m_Localized.StringChanged -= OnStringChanged;
            m_Localized = null;
        }

        void OnStringChanged(string value) => text = value;
    }
}
```

(기존 `Refresh()` 공개 메서드 삭제 — 호출자는 UIPanel.RefreshLocale 뿐, Task 7 에서 함께 삭제.)

- [ ] **Step 2: 컴파일 확인** — `read_console`. UIPanel.RefreshLocale 이 `l.Refresh()` 호출 중 → 에러 발생 예상. Task 7 과 연속 작업 후 확인해도 됨.

---

### Task 7: UIPanel — 수동 갱신 제거

**Files:**
- Modify: `Assets/Scripts/UI/Core/UIPanel.cs:92, 112-115, 121, 152-155`

- [ ] **Step 1: OnDestroy (line 92)** — `LocaleManager.OnLanguageChanged -= RefreshLocale;` 삭제.

- [ ] **Step 2: OnOpen (lines 112-115)** — 삭제:

```csharp
RefreshLocale();

// 열려 있는 동안 언어 변경에 즉시 반응.
LocaleManager.OnLanguageChanged += RefreshLocale;
```

- [ ] **Step 3: OnClose (line 121)** — `LocaleManager.OnLanguageChanged -= RefreshLocale;` 삭제.

- [ ] **Step 4: RefreshLocale 메서드 (lines 152-155) 삭제**

```csharp
protected virtual void RefreshLocale()
{
    Root?.Query<LocalizedLabel>().ForEach(l => l.Refresh());
}
```

서브클래스 override 검색: `Grep "override.*RefreshLocale" Assets/Scripts` → 있으면 함께 삭제.

- [ ] **Step 5: 컴파일 확인** — `read_console` Error 0 (Task 6+7 묶어서).

---

### Task 8: InteractionPrompt 교체

**Files:**
- Modify: `Assets/Scripts/UI/HUD/InteractionPrompt.cs`

- [ ] **Step 1: 필드 + 생명주기 교체**

`using UnityEngine.Localization;` 추가. 기존 OnEnable/OnDisable/RefreshLabel/SetActionKey 교체:

```csharp
LocalizedString m_Localized;

void OnEnable()
{
    if (m_Localized == null) m_Localized = Loc.Create(m_ActionKey);
    m_Localized.StringChanged += OnActionTextChanged; // 구독 시 현재 값 즉시 콜백
}

void OnDisable()
{
    if (m_Localized != null) m_Localized.StringChanged -= OnActionTextChanged;
}

// 현재 Interact 바인딩 키 이름을 읽어 레이블을 갱신한다.
void OnActionTextChanged(string actionText)
{
    if (m_Label == null) return;

    string keyName = GetInteractKeyName();
    m_Label.text = keyName != null ? $"[{keyName}] {actionText}" : actionText;
}
```

`m_ActionKey` 필드 주석 갱신: `// Strings 테이블 키 — LocalizedString 으로 조회`.
`GetInteractKeyName` / `TryGetDisplayString` 은 그대로 유지.

- [ ] **Step 2: SetActionKey 교체**

```csharp
/// <summary>Strings 테이블 키를 변경하고 레이블을 즉시 갱신한다.</summary>
public void SetActionKey(string stringId)
{
    m_ActionKey = stringId;
    if (m_Localized != null) m_Localized.TableEntryReference = stringId; // setter 가 StringChanged 재발행
}
```

- [ ] **Step 3: 컴파일 확인** — `read_console` Error 0.

---

### Task 9: CookingUI 교체

**Files:**
- Modify: `Assets/Scripts/Cooking/CookingUI.cs` (8곳: lines 70, 108, 130, 203, 225, 236, 237, 280)

- [ ] **Step 1: static readonly LocalizedString 캐시 추가**

`using UnityEngine.Localization;` 추가. 클래스 필드:

```csharp
static readonly LocalizedString s_LsCookStart     = Loc.Create("UI_COOKING_START");
static readonly LocalizedString s_LsNoIngredients = Loc.Create("UI_COOKING_NO_INGREDIENTS");
static readonly LocalizedString s_LsSlotEmpty     = Loc.Create("UI_COOKING_SLOT_EMPTY");
static readonly LocalizedString s_LsRecipePrefix  = Loc.Create("UI_COOKING_RECIPE_PREFIX");
static readonly LocalizedString s_LsNoMatch       = Loc.Create("UI_COOKING_NO_MATCH");
static readonly LocalizedString s_LsSelect        = Loc.Create("UI_COOKING_SELECT");
```

- [ ] **Step 2: 호출부 8곳 치환**

`LocaleManager.Get("UI_COOKING_START")` → `s_LsCookStart.GetLocalizedString()` 패턴으로 전부 치환 (각 키 ↔ 캐시 필드 매칭). 동작 주의: 패널 텍스트는 열 때 갱신 — 기존과 동일 (열린 채 언어 변경 즉시 반영은 비범위).

- [ ] **Step 3: 다른 잔여 호출 검색**

`Grep "LocaleManager" Assets/Scripts --files_with_matches` 실행.
Expected 잔여: `Core/LocaleManager.cs`(자신), `Core/Managers/GlobalController.cs` 뿐. 다른 파일 발견 시 같은 패턴으로 치환.

- [ ] **Step 4: 컴파일 확인** — `read_console` Error 0.

---

### Task 10: GameStartup — 초기화 + 프리로드

**Files:**
- Modify: `Assets/Scripts/Core/Startup/GameStartup.cs:115-128`

- [ ] **Step 1: StepDataLoad 수정**

`using UnityEngine.Localization.Settings;` 추가:

```csharp
/// <summary>
/// Unity Localization 초기화 + Strings 테이블 프리로드 후
/// TableData SO 를 AssetManifest 에서 로드해 DataRegistry 에 등록한다.
/// 실패 시 치명적 오류 — IsAborted = true.
/// </summary>
IEnumerator StepDataLoad()
{
    // Localization 초기화 (locale 선택 + 프리로드 플래그 테이블 로드)
    // 이후 LocalizedString.GetLocalizedString() 동기 접근 안전.
    yield return LocalizationSettings.InitializationOperation;

    GlobalController.Instance.Registry.Load();

    if (!GlobalController.Instance.Registry.IsReady)
    {
        string msg = "[GameStartup] TableData 로드 실패 — AssetManifest 'data/table_data' 키 확인 요망";
        DebugUtil.LogError(msg);
        IsAborted = true;
        OnError?.Invoke(msg);
    }

    yield break;
}
```

(프리로드는 Task 3 에서 테이블 Preload 플래그 설정 → InitializationOperation 이 함께 로드.)

- [ ] **Step 2: 컴파일 확인** — `read_console` Error 0.

---

### Task 11: 구 시스템 삭제

**Files:**
- Delete: `Assets/Scripts/Core/LocaleManager.cs` (+ .meta)
- Delete: `Assets/Scripts/Data/Table/StringTable.cs` (+ .meta)
- Modify: `Assets/Scripts/Core/Managers/GlobalController.cs:61, 126, 148`
- Modify: `Assets/Scripts/Data/Table/TableData.cs:44, 67`
- Modify: `Assets/Scripts/Data/Table/DataRegistry.cs:22, 52, 105, 107-108`
- Modify: `Assets/Editor/DataPipeline/DataManagerWindow.cs:30, 36, 67-68, 249, 450-454, 964`

- [ ] **Step 1: GlobalController** — 삭제 3곳:
  - line 61: `public LocaleManager     Locale       { get; private set; }`
  - line 126: `Locale      = new LocaleManager();`
  - line 148: `Locale.Init();       // Instance 설정 ...`

- [ ] **Step 2: TableData.cs** — line 44 `public StringTable Strings = new();`, line 67 `yield return Strings;` 삭제.

- [ ] **Step 3: DataRegistry.cs** — 삭제:
  - line 22 주석 `//    DataRegistry.Instance.Strings.FindByStringId(...)`
  - line 52 `public StringTable        Strings            => m_Table?.Strings;`
  - line 105 로그 문자열에서 ` Strings:{m_Table.Strings.Count}` 부분 제거
  - lines 107-108 `// LocaleManager 초기화 ...` + `LocaleManager.Instance?.OnDataLoaded();`

- [ ] **Step 4: DataManagerWindow.cs** — StringData 항목 제거:
  - line 30 탭 배열에서 `"Strings"` 제거
  - line 36 파일 배열에서 `"StringData.csv"` 제거
  - lines 67-68 헤더 스펙 `new[] { "_key","Id","StringId","Ko","En" }` (주석 `// 12: Strings` 포함) 제거
  - line 249 `13 => ScriptableObjectSync.Sync<StringData>(...)` 케이스 제거
  - lines 450-454 `StringDataMapper` 메서드 제거
  - line 964 `case 13: ... Sync<StringData> ... break;` 제거
  - ⚠ 배열 인덱스/케이스 번호가 위치 기반 — Strings 가 마지막 항목인지 확인 후 제거. 마지막이 아니면 이후 인덱스 시프트 여부 점검.

- [ ] **Step 5: 파일 삭제** — `LocaleManager.cs`, `StringTable.cs` (meta 포함).

- [ ] **Step 6: 전체 잔여 참조 검색**

`Grep "LocaleManager|GameLanguage|StringTable|StringData" Assets/Scripts Assets/Editor` (StringTable/StringData 는 Localization 패키지 타입과 혼동 주의 — `MonsterKitchen.Data` 것만 잔여로 판단). Expected: 0건.

- [ ] **Step 7: 컴파일 확인** — `refresh_unity` → `read_console` Error 0.

- [ ] **Step 8: TableData.asset 재동기화** — DataManagerWindow 열어 Sync All (Strings 필드 제거 반영). `manage_scene(action: save)` 불필요 (에셋만).

---

### Task 12: 테스트 작성

**Files:**
- Create: `Assets/Tests/EditMode/LocalizationCsvTests.cs`
- Create: `Assets/Tests/PlayMode/LocalizationPlayTests.cs`

- [ ] **Step 1: EditMode — CSV 무결성 테스트 작성**

```csharp
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MonsterKitchen.Tests
{
    public class LocalizationCsvTests
    {
        const string CsvPath = "Assets/Data/CSV/StringData.csv";

        [Test]
        public void Csv_HeaderMatchesPackageFormat()
        {
            string header = File.ReadLines(CsvPath).First();
            Assert.AreEqual("Key,Id,Shared Comments,Korean(ko),English(en)", header);
        }

        [Test]
        public void Csv_KeysAreUnique()
        {
            var keys = File.ReadLines(CsvPath).Skip(1)
                           .Where(l => !string.IsNullOrWhiteSpace(l))
                           .Select(l => l.Split(',')[0])
                           .ToList();
            Assert.Greater(keys.Count, 0);
            CollectionAssert.AllItemsAreUnique(keys);
        }

        [Test]
        public void Csv_EveryRowHasKoreanText()
        {
            var rows = File.ReadLines(CsvPath).Skip(1)
                           .Where(l => !string.IsNullOrWhiteSpace(l));
            foreach (var row in rows)
            {
                var f = System.Text.RegularExpressions.Regex.Split(
                    row, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                Assert.IsFalse(string.IsNullOrEmpty(f[3]),
                    $"Ko 비어 있음: {f[0]}");
            }
        }
    }
}
```

- [ ] **Step 2: EditMode 실행** — `run_tests` (EditMode) 또는 CLI. Expected: 신규 3개 포함 전체 PASS.

- [ ] **Step 3: PlayMode — 로케일 전환/폴백 테스트 작성**

```csharp
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;

namespace MonsterKitchen.Tests.PlayMode
{
    public class LocalizationPlayTests
    {
        Locale m_Original;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LocalizationSettings.InitializationOperation;
            m_Original = LocalizationSettings.SelectedLocale;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LocalizationSettings.SelectedLocale = m_Original;
            yield return null;
        }

        static Locale FindLocale(string code) =>
            LocalizationSettings.AvailableLocales.Locales
                .First(l => l.Identifier.Code == code);

        [UnityTest]
        public IEnumerator LocaleSwitch_ChangesString()
        {
            var ls = new LocalizedString("Strings", "MON_001_NAME");

            LocalizationSettings.SelectedLocale = FindLocale("ko");
            yield return null;
            Assert.AreEqual("초록 슬라임", ls.GetLocalizedString());

            LocalizationSettings.SelectedLocale = FindLocale("en");
            yield return null;
            Assert.AreEqual("Green Slime", ls.GetLocalizedString());
        }

        [UnityTest]
        public IEnumerator MissingKey_ReturnsKeyItself()
        {
            yield return null;
            var ls = new LocalizedString("Strings", "NOT_A_REAL_KEY_999");
            Assert.AreEqual("NOT_A_REAL_KEY_999", ls.GetLocalizedString());
        }

        [UnityTest]
        public IEnumerator EmptyEnglish_FallsBackToKorean()
        {
            // 전제: En 빈 키 최소 1개 존재. CSV 에서 En 빈 첫 키를 찾아 검증.
            string key = System.IO.File.ReadLines("Assets/Data/CSV/StringData.csv")
                .Skip(1).Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => System.Text.RegularExpressions.Regex.Split(
                    l, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)"))
                .Where(f => f.Length > 4 && string.IsNullOrEmpty(f[4]))
                .Select(f => f[0]).FirstOrDefault();

            if (key == null)
                Assert.Ignore("En 빈 키 없음 — 폴백 케이스 데이터 없음");

            LocalizationSettings.SelectedLocale = FindLocale("en");
            yield return null;

            var ls = new LocalizedString("Strings", key);
            string result = ls.GetLocalizedString();
            Assert.IsFalse(string.IsNullOrEmpty(result), "폴백 실패 — 빈 문자열");
            Assert.AreNotEqual(key, result, "폴백 실패 — 키 자체 반환");
        }
    }
}
```

- [ ] **Step 4: PlayMode 실행** — Expected: 신규 3개 + 기존 스모크 PASS.
  실패 시 폴백 설정(en FallbackLocale, 빈 엔트리 제거) 재점검 — `systematic-debugging` 스킬.

---

### Task 13: 최종 검증 + 문서 갱신

**Files:**
- Modify: `WORK_IN_PROGRESS.md`, `docs/MODULES.md`, `CLAUDE.md`

- [ ] **Step 1: 전체 테스트** — EditMode + PlayMode 전체 실행. Expected: 전부 PASS (기존 96/96 + 신규).

- [ ] **Step 2: 인게임 스모크** — Editor Play (StartScene): Touch To Start 라벨 표기, Management 진입, 던전 포털 InteractionPrompt `[E] ...` 정상 표기. `read_console` 에러 0.

- [ ] **Step 3: CLAUDE.md 갱신** — 싱글톤 표에서 `LocaleManager` 행 제거, 다국어 항목을 `Unity Localization (Strings 컬렉션 + StringData.csv CSV Extension)` 으로 교체, 데이터 4-레이어/스크립트 구조에서 StringTable 제거, CSV 운영 워크플로(편집 → MonsterKitchen/Localization/Import CSV) 추가.

- [ ] **Step 4: MODULES.md + WIP 갱신** — 모듈 항목 추가, WIP 처리 내역/체크 마감.

- [ ] **Step 5: verification-before-completion 스킬** 실행 후 완료 보고 (한글). 커밋은 사용자 확인 후.
