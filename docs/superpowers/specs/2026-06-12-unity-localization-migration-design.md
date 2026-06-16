# Unity Localization 마이그레이션 — 설계 (2026-06-12)

## 목적

커스텀 로컬라이제이션 시스템(`LocaleManager` + `StringTable` SO)을 Unity 공식
`com.unity.localization` 패키지로 전면 교체한다.

**동기:**
- 언어 확장 대비 — Ja/ZhCn 추가 시 코드 수정(StringData 필드 + switch 케이스) 없이 데이터만 추가
- 에디터 툴링 + 표준 워크플로 — Localization Tables 창, 누락 검사, 공식 생태계

## 현재 상태 (As-Is)

| 구성요소 | 위치 | 역할 |
|---|---|---|
| `LocaleManager` | `Core/LocaleManager.cs` | 순수 C# 싱글톤. `Get(stringId)` 정적 조회, `OnLanguageChanged` 이벤트, Ko/En + fallback |
| `GameLanguage` | `Core/LocaleManager.cs` | Ko/En enum |
| `StringData`/`StringTable` | `Data/Table/StringTable.cs` | CSV→SO 데이터 클래스, StringId→텍스트 캐시 |
| `StringData.csv` | `Assets/Data/CSV/` | 81행. `;` 주석, `#TYPE` 행, 컬럼: stringId/Ko/En/id |
| `LocalizedLabel` | `UI/Core/LocalizedLabel.cs` | UIToolkit Label 커스텀 컨트롤, `string-id` UXML 속성 |
| `UIPanel.RefreshLocale` | `UI/Core/UIPanel.cs` | OnLanguageChanged 구독 → 패널 내 라벨 일괄 갱신 |
| 호출부 | 8파일 20곳 | 테이블 클래스 `DisplayName`/`Description` 프로퍼티(동기 필수), CookingUI, InteractionPrompt 등 |

## 결정 사항 (사용자 확정)

1. **원본(source of truth)**: 패키지 내장 **CSV Extension** 사용. 기존 `StringData.csv` 를 패키지 포맷으로 변환
2. **코드 API**: **전면 교체** — `LocaleManager` 삭제, 호출부 전부 공식 API
3. **교체 패턴**: **B. 전부 `LocalizedString` 객체** — 모든 사용처가 `LocalizedString` 인스턴스 생성/캐시

## 설계 (To-Be)

### 1. 패키지 & 에셋

- `com.unity.localization` 설치 (Unity 6 검증 최신)
- Locale 에셋: `ko`(기본), `en`. 이후 언어 추가 = Locale 에셋 + CSV 컬럼만 (코드 0)
- String Table Collection **1개**: `Strings` (현행 단일 테이블 구조 유지)
- Locale 선택 체인: `PlayerPrefLocaleSelector` → `SystemLocaleSelector` → `SpecificLocaleSelector(ko)`
- 패키지는 Addressables 기반 (프로젝트 기설치) — Locale/테이블 에셋 자동 등록

### 2. CSV — 패키지 CSV Extension

- `StringData.csv` → 패키지 포맷 1회 변환:
  - 컬럼: `Key,Id,Korean(ko),English(en)`
  - `;` 주석 행, `#TYPE` 행 제거. `stringId` → `Key`
  - 검증: 변환 후 81행(주석 제외 데이터 행) 보존, Key 중복 0
- 변환 파일 위치: 기존 경로 유지 — `Assets/Data/CSV/StringData.csv` (in-place 변환)
- `Strings` 컬렉션에 CSV Extension 연결 (위 파일 경로 지정)
- 운영 워크플로: Excel 에서 CSV 편집 → Localization Tables 창 → Import
- `DataManagerWindow` 동기화 목록에서 StringData 제거

### 3. 코드 — 전면 교체 (패턴 B)

**삭제:**
- `Core/LocaleManager.cs` (`LocaleManager`, `GameLanguage` enum)
- `Data/Table/StringTable.cs` (`StringData`, `StringTable`)
- `TableData` / `DataRegistry` 의 `Strings` 참조

**변경:**

| 대상 | 내용 |
|---|---|
| 테이블 클래스 4종 (Monster/Ingredient/Recipe/Food) | `[NonSerialized]` `LocalizedString` 캐시 필드, `RuntimeSetData()` 에서 생성. `DisplayName => m_NameLs.GetLocalizedString()` (동기 — 내부 WaitForCompletion) |
| `LocalizedLabel` | `string-id` 로 `LocalizedString("Strings", key)` 생성. `AttachToPanelEvent` 에서 `StringChanged` 구독, `DetachFromPanelEvent` 에서 해제 — 자가 갱신 |
| `UIPanel` | `RefreshLocale` / `OnLanguageChanged` 구독 코드 삭제 (라벨 자가 갱신으로 불필요) |
| `InteractionPrompt` / `CookingUI` / 알림 | 키별 `LocalizedString` 캐시, 라이브 갱신 필요 시 `StringChanged` 구독 |
| 언어 전환 | `LocalizationSettings.SelectedLocale = locale` (설정 UI 는 추후) |
| `GameStartup` DataLoad | `LocalizationSettings.InitializationOperation` yield + `Strings` 테이블 프리로드 → 이후 동기 접근 안전 |

### 4. Fallback (현행 동작 보존)

- En 셀 비어 있음 → ko 폴백: `en` Locale 의 Fallback 메타데이터 = `ko`, 테이블 UseFallback 활성
- 키 미등록 → 키 자체 표시: Missing Translation 커스텀 포맷 `{key}` (빈 문자열 방지)

### 5. 테스트

- **EditMode**: CSV 변환 무결성 (데이터 행 수 보존, Key 유니크), fallback 동작
- **PlayMode**: 로케일 전환 시 라벨 텍스트 변경 확인, 기존 스모크 3/3 유지

### 6. 리스크

- `WaitForCompletion` — Desktop/Mobile OK, WebGL 미지원 (비대상 플랫폼)
- 첫 접근 히치 — GameStartup 프리로드로 회피
- `LocalizedString` 은 UnityEngine.Object 아님 — 테이블 클래스 캐시 필드는 `[NonSerialized]` 런타임 전용, 데이터 클래스 순수성 규칙(CSV 값만 직렬화) 유지

## 비범위 (Out of Scope)

- 에셋(스프라이트/오디오) 로컬라이즈
- 언어 설정 UI (SelectedLocale 변경 진입점만 확보)
- Ja/ZhCn 실 데이터 입력
