# Save Scheduler — Design Spec

> 작성일: 2026-06-02  
> 모듈: 9-11 Save/Load  
> 상태: 승인됨

---

## 배경

`ServerDBManager.Save()` / `Load()` 는 이미 구현 완료.  
`GameStartup` Phase 4에서 `Load()` 호출도 연결됨.  
**미완성: 씬 전환·앱 이벤트·주기 타이머 기반 자동 저장 미연결.**

---

## 목표

| 요구사항 | 설명 |
|---|---|
| 주기 자동 저장 | 30초(GameConfig 설정 가능)마다 dirty 상태면 저장 |
| 중요 패킷 즉시 저장 | 골드·음식·업그레이드 변경 시 즉시 ForceSave |
| 앱 이벤트 저장 | OnApplicationPause(true) / OnApplicationQuit |
| 강제 저장 API | 외부에서 언제든 ForceSave() 호출 가능 |
| HUD 저장 표시 | 저장 중일 때 "저장 중..." 인디케이터 표시 |

---

## 아키텍처

### 신규 클래스: `SaveScheduler`

```
Assets/Scripts/Core/Managers/SaveScheduler.cs
```

- pure C# singleton (GlobalController 소유)
- `MonoBehaviour` 아님 — coroutine runner는 GlobalController 주입
- `ServerDBManager`에 저장 I/O 위임 (직접 파일 접근 안 함)

### 변경 클래스

| 클래스 | 변경 내용 |
|---|---|
| `GameConfig` | `autoSaveIntervalSeconds` (int, default 30) 필드 추가 |
| `GlobalController` | `SaveScheduler` 프로퍼티 추가, Init/Start/OnApplicationPause/OnApplicationQuit 연결 |
| `NetworkManager` | Request*() 메서드에 MarkDirty / ForceSave 호출 추가 |
| `GameHUD` | save-indicator 요소 추가, OnSaveStateChanged 구독 |
| `HUD.uxml` | `save-indicator` Label 추가 |

---

## SaveScheduler 상세

### 상태

```csharp
public bool IsDirty   { get; private set; }
public bool IsSaving  { get; private set; }
public event Action<bool> OnSaveStateChanged;   // arg: IsSaving
public event Action       OnSaveComplete;
```

### 공개 API

```csharp
public void Init()
public void StartAutoSaveCycle(MonoBehaviour runner)   // GlobalController에서 호출
public void MarkDirty()                                // 일반 패킷 후
public void ForceSave(Action onComplete = null)        // 중요 패킷 / 앱 이벤트 / 수동
```

### 내부 로직

```
StartAutoSaveCycle:
    loop:
        yield WaitForSeconds(GameConfig.Current.autoSaveIntervalSeconds)
        if IsDirty → SaveIfDirty()

MarkDirty:
    IsDirty = true

ForceSave(onComplete):
    if IsSaving → onComplete 큐잉 후 return  (중복 저장 방지)
    SetSaving(true)
    ServerDBManager.Instance.Save()
    IsDirty = false
    SetSaving(false)
    onComplete?.Invoke()
    OnSaveComplete?.Invoke()

SaveIfDirty:
    if !IsDirty → return
    ForceSave()
```

---

## 데이터 흐름

### 일반 패킷

```
NetworkManager.RequestAddIngredient(id, qty)
    → PlayerData.ApplyIngredient()
    → SaveScheduler.MarkDirty()
    ⋯ (최대 30초 후) → 타이머 tick → SaveIfDirty() → ServerDBManager.Save()
```

### 중요 패킷 (즉시 저장)

```
NetworkManager.RequestEarnGold(amount)
    → PlayerData.ApplyGold()
    → SaveScheduler.ForceSave()     ← 즉시
```

### 앱 이벤트

```
OnApplicationPause(true) / OnApplicationQuit()
    → SaveScheduler.ForceSave()
```

---

## 중요 패킷 분류

| 메서드 | 저장 방식 | 이유 |
|---|---|---|
| `RequestEarnGold` | ForceSave | 골드 손실 방지 |
| `RequestCook` | ForceSave | 재료 소모 + 음식 생성 |
| `RequestServeFood` | ForceSave | 음식 소모 + 골드 획득 |
| `RequestUpgrade` | ForceSave | 골드 소모 + 업그레이드 |
| `RequestAddIngredient` | MarkDirty | 던전 재획득 가능 |

---

## HUD 저장 표시

- `HUD.uxml`에 `<Label name="save-indicator" text="저장 중..." />` 추가
- `GameHUD.cs`에서 `SaveScheduler.OnSaveStateChanged` 구독
- `isSaving=true` → `display: flex` / `false` → `display: none`
- LocaleManager key: `UI_SAVING` (StringTable에 추가)

---

## GameConfig 변경

```csharp
[Header("Save")]
[Tooltip("자동 저장 주기 (초). 0 이면 자동 저장 비활성.")]
[SerializeField] int m_AutoSaveIntervalSeconds = 30;
public int AutoSaveIntervalSeconds => m_AutoSaveIntervalSeconds;
```

---

## 에러 처리

- `ServerDBManager.Save()`가 예외 발생 시 기존 catch 블록에서 LogError
- `IsSaving`은 예외 발생 후에도 `false`로 복원 (try/finally)
- 저장 실패해도 `IsDirty`는 그대로 유지 → 다음 tick에서 재시도

---

## 완료 기준 (모듈 9-11 테스트)

1. 게임 시작 → 골드 획득 → 30초 내 앱 강제 종료 후 재시작 → 골드 복원 확인
2. `RequestEarnGold` 직후 앱 강제 종료 → 재시작 시 골드 복원 (ForceSave 검증)
3. 저장 중 HUD에 "저장 중..." 표시 → 완료 후 숨김 확인
4. `GameConfig.autoSaveIntervalSeconds` 변경 → 해당 주기로 저장 동작 확인
5. `OnApplicationPause` → ForceSave 호출 확인 (Console 로그)

---

## 파일 목록

| 파일 | 변경 종류 |
|---|---|
| `Assets/Scripts/Core/Managers/SaveScheduler.cs` | **신규** |
| `Assets/Scripts/Data/Table/GameConfig.cs` | 수정 (필드 추가) |
| `Assets/Scripts/Core/Managers/GlobalController.cs` | 수정 |
| `Assets/Scripts/Core/Managers/NetworkManager.cs` | 수정 |
| `Assets/Scripts/UI/HUD/GameHUD.cs` | 수정 |
| `Assets/Prefabs/UI/HUD.uxml` (또는 .prefab) | 수정 |
| `Assets/Data/SO/StringTable` (또는 CSV) | `UI_SAVING` 키 추가 |
