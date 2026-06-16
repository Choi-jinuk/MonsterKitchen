# 캐릭터 시스템 기반 — 설계 스펙

> 작성: 2026-06-13
> 상태: 사용자 승인 대기
> 상위 컨텍스트: 캐릭터 뽑기 중심 시스템 (6개 서브프로젝트 중 1번)

---

## 0. 상위 방향 — 캐릭터 뽑기 중심 시스템 (확정 결정 기록)

2026-06-13 브레인스토밍에서 확정된 게임 방향. 서브프로젝트 2~6의 설계 기준이 된다.

| 항목 | 결정 |
|---|---|
| 진행축 | 캐릭터 뽑기 + 배치 + 성장 + 무기 구매 + 무기 강화 |
| 클래스 | 캐릭터 클래스 = `WeaponType` 1:1. 해당 타입 무기만 장착 가능 |
| 희귀도 | 태생 ★1~3. 승급(중복 캐릭터 소모)으로 전 캐릭터 ★10 상한 통일 |
| 뽑기 재화 | 이중 — 골드 뽑기(일반 풀) + 전용 재화 뽑기(고급 풀) |
| 성장 | 재료 소모 레벨업 (골드/성장 재료). 대기 캐릭터도 성장 가능 |
| 중복 뽑기 | 승급 재료로 사용 (돌파/별 승급) |
| 근무지 배치 | 주방 or 식당 — 캐릭터당 배타적 1곳. 던전 배치는 근무지와 무관하게 중복 가능 |
| 근무 동작 | 데이브 더 다이버 방식 — 배치 캐릭터 AI 자동 요리/서빙 + 플레이어 개입 시 가속/품질 향상 |
| 작업 스탯 | 캐릭터에 요리 속도·서빙/보조 속도 스탯 추가 |
| 던전 | 파티 출전 3명 — 조작 1 + AI 동료 2, 키 입력 태그 전환(조작 캐릭터 실시간 교체) |
| 무기 획득 | 상점 구매 |
| 무기 강화 | 골드 강화 + 일정 단계마다 상위 재료 한계돌파 |

**서브프로젝트 구축 순서** (각각 스펙→플랜→구현 사이클):

1. **캐릭터 시스템 기반** ← 이 스펙
2. 뽑기 (재화·풀·확률·중복 변환·UI)
3. 성장 + 승급 (레벨업·★10 승급·성장 재료)
4. 무기 구매 + 강화/돌파
5. 근무지 배치 (직원 AI, 데이브 방식)
6. 던전 파티 (3인 편성·태그 전환·AI 동료)

---

## 1. 목표

다중 캐릭터 보유의 데이터 토대를 만든다. 보유 캐릭터를 저장/로드하고,
조작 캐릭터를 전환하고, 클래스 제한 무기 장착을 검증하는 데이터 계층 + API.
UI·뽑기·성장·배치는 범위 밖 (후속 서브프로젝트).

**아키텍처**: 기존 4-레이어 패턴 연장 (안 1 — 2026-06-13 승인).
TableData 확장 + PlayerData 서브 클래스 신설 + NetworkManager Request 추가 + ServerSaveData v3.

---

## 2. TableData 레이어 — PlayerCharData 확장

`Assets/Scripts/Data/Table/PlayerCharTable.cs` 의 `PlayerCharData` 에 CSV 컬럼 추가:

| 새 필드 | 타입 | 설명 |
|---|---|---|
| `CharClass` | `WeaponType` | 클래스. 무기 타입 1:1 — 이 타입 무기만 장착 가능 |
| `NatalStars` | `int` | 태생 성급 1~3. 뽑기 풀 배정·초기 Stars 값 |
| `CookSpeed` | `float` | 주방 배치 시 요리 속도 배율 기준 (1.0 = 표준) |
| `ServeSpeed` | `float` | 식당 배치 시 서빙/보조 속도 배율 기준 (1.0 = 표준) |

- 기존 필드 전부 유지 (스탯·스킬·`DefaultWeaponId`·프리팹/BT 주소·궁극기 게이지)
- 작업 스탯(`CookSpeed`/`ServeSpeed`)은 이 서브프로젝트에서 **데이터 정의만** — 적용은 서브 5
- ID 범위 기존 그대로 9001~

**시드 데이터** (`Assets/Data/CSV/PlayerChars.csv`):
- 기존 9001 에 새 컬럼 값 부여 (CharClass = 현재 DefaultWeaponId 의 타입과 일치시킴)
- 테스트 캐릭터 2~3체 추가 — Sword/Bow/Staff 클래스 각 1, 태생 성급 1·2·3 분산
- 신규 캐릭터 프리팹은 기존 9001 프리팹 재사용 가능 (아트 미존재 — 주소만 다르게 잡지 않고 동일 주소 허용)

---

## 3. PlayerData 레이어 — PlayerRosterData 신설

`Assets/Scripts/Data/Player/PlayerRosterData.cs` (pure C#, `PlayerInventoryData` 패턴).
`PlayerDataManager.Roster` 로 접근 — Coin·Inventory·Upgrades 와 동급 서브 데이터.

### CharacterState

```csharp
public class CharacterState
{
    public uint CharId;            // PlayerCharData 참조
    public int  Level     = 1;
    public int  Stars;             // 현재 성급. 태생값으로 시작, 승급 +1, 최대 10
    public uint EquippedWeaponId;  // 획득 시 DefaultWeaponId 로 초기화
}
```

### PlayerRosterData 공개 API

```csharp
// 읽기
IReadOnlyDictionary<uint, CharacterState> AllCharacters { get; }
bool Owns(uint charId);
CharacterState Get(uint charId);           // 미보유 시 null

// 이벤트
event Action<uint> OnCharacterAdded;       // (charId)
event Action<uint> OnCharacterChanged;     // 레벨/성급/무기 변경 (charId)

// Apply — PlayerDataManager 경유만 호출
void AddCharacter(uint charId, int natalStars, uint defaultWeaponId);  // 중복 시 무시(로그)
void SetEquippedWeapon(uint charId, uint weaponId);                    // 존재 검증만 — 클래스 검증은 NetworkManager
// Level/Stars Set 메서드는 서브 3 에서 추가

// 세이브/로드 — ServerDBManager 전용
void LoadCharacters(IEnumerable<(uint id, int level, int stars, uint weaponId)> entries);
IEnumerable<(uint id, int level, int stars, uint weaponId)> AllEntries();
```

### 규칙

- `PlayerDataManager.SelectedCharId` (기존 필드) 는 반드시 보유 캐릭터.
  로드 후 미보유 상태면 첫 보유 캐릭터로 폴백.
- **성장 미적용**: Level/Stars 는 저장만 한다. 스탯 공식·성급 효과는 서브 3.
  현재 전투 스탯 = 기존 그대로 `Base + 무기 Abils` (`PlayerStats.RecalculateStats` 무변경).

---

## 4. 저장 스키마 — ServerSaveData v3

`Assets/Scripts/Data/Server/ServerSaveData.cs`:

```csharp
public int Version = 3;                          // 2 → 3
public List<CharacterEntry> Characters = new();  // 신규

[Serializable]
public class CharacterEntry
{
    public uint Id;
    public int  Level;
    public int  Stars;
    public uint EquippedWeaponId;
}
```

**마이그레이션** (v1/v2 세이브 로드):
- `Characters` 가 null/빈 리스트 → `SelectedCharId`(기본 9001) 캐릭터 1체 자동 등록.
  `Stars` = TableData `NatalStars`, `EquippedWeaponId` = TableData `DefaultWeaponId`.
- 기존 필드 손실 없음. `SelectedCharId` 필드 유지.

---

## 5. NetworkManager API (Server Stub 패턴)

`RequestUpgrade` 와 동일 구조 — 검증 → Apply → MarkDirty → 콜백.

```csharp
/// 캐릭터 획득. 이미 보유 시 onResult(false).
/// (중복 뽑기 → 승급 재료 변환은 서브 2 에서 이 경로 위에 구현)
public void RequestAddCharacter(uint charId, Action<bool> onResult = null);

/// 조작 캐릭터 변경. 미보유 캐릭터면 거부.
public void RequestSelectCharacter(uint charId, Action<bool> onResult = null);

/// 캐릭터에 무기 장착. 클래스 검증:
/// weapon.WeaponType != charData.CharClass → 거부.
public void RequestEquipWeapon(uint charId, uint weaponId, Action<bool> onResult = null);
```

- 셋 다 성공 시 `MarkDirty` (골드 소모 없는 조작 — ForceSave 불필요)
- `PlayerDataManager` 에 대응 Apply: `ApplyAddCharacter` / `ApplySelectCharacter` / `ApplyEquipWeapon`
- 검증 데이터 조회: `DataRegistry.Instance.Players.Get(charId)` / `.Weapons.Get(weaponId)`

### PlayerStats 연결

`PlayerManager` 스폰 시 — 기존 `PlayerCharData.DefaultWeaponId` 직접 참조를
로스터 경유로 교체: `Roster.Get(SelectedCharId).EquippedWeaponId` → `PlayerStats.EquipWeapon`.
로스터에 없으면 (이론상 불가 — 마이그레이션 보장) DefaultWeaponId 폴백 + 경고 로그.

---

## 6. 테스트 계획

**EditMode** (`Assets/Tests/EditMode/PlayerRosterTests.cs`):
1. `AddCharacter` → `Owns` true, Level=1, Stars=태생값, 무기=DefaultWeaponId
2. 중복 `AddCharacter` → 무시, 상태 불변
3. `SetEquippedWeapon` — 보유 캐릭터 성공 / 미보유 무시
4. NetworkManager 클래스 검증 — 일치 무기 장착 성공 / 불일치 거부 (콜백 false)
5. 세이브 라운드트립 — `AllEntries()` → `LoadCharacters()` 복원 일치
6. v2 세이브 마이그레이션 — `Characters` 빈 리스트 → 기본 캐릭터 1체 자동 등록
7. `SelectedCharId` 미보유 폴백 — 첫 보유 캐릭터로

**PlayMode 스모크**:
- 부팅 → 로스터 초기화 → ManagementScene 스폰 시 로스터 장착 무기가 `PlayerStats.EquippedWeapon` 에 반영

**CSV 무결성** (기존 EditMode 패턴):
- PlayerChars.csv — `NatalStars` 1~3, `CharClass` 유효 `WeaponType`, `CookSpeed`/`ServeSpeed` > 0

---

## 7. 스코프 경계

| 제외 항목 | 담당 서브프로젝트 |
|---|---|
| 뽑기 로직·전용 재화·확률·UI | 2 |
| 레벨업/승급 실행·성장 스탯 공식·성장 재료 | 3 |
| 무기 구매·강화·한계돌파 | 4 |
| 근무지 배정·직원 AI·작업 스탯 적용 | 5 |
| 파티 편성·태그 전환·AI 동료 | 6 |
| 캐릭터 목록/장착 UI | 2~4 에서 점진 |

**완료 기준**:
- 테스트 전부 GREEN (EditMode + PlayMode)
- 기존 게임플레이 무변화 — 9001 단일 캐릭터 동작 동일
- 테스트 캐릭터 2~3체를 `RequestAddCharacter` 로 획득/전환 가능 (디버그 호출 수준)
