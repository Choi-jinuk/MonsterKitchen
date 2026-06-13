# 동료(컴패니언) 파티 시스템 — 첫 슬라이스 설계 스펙

> 작성: 2026-06-13
> 상태: 사용자 승인 대기
> 상위 컨텍스트: 캐릭터 시스템(서브프로젝트 6 "던전 파티 3인")의 첫 슬라이스 — 태그 전환 제외
> 의존: 군집 일반화 (`docs/superpowers/specs/2026-06-13-monster-flocking-encircle-design.md`, `MonsterKitchen.AI` flock 공용 레이어)

---

## 1. 목표

플레이어 동료를 AI로 던전에 동반 배치해, 공용화한 flock/encircle 이동을 실전 검증한다.

- 리더 1(조작) + AI 동료 최대 2명. 동료는 리더를 추종하고 적을 둘러싸며 공격.
- ManagementScene 에서 파티(동료) 선택 → 던전 진입 시 동반 스폰.
- 파티 구성은 ServerSaveData 에 영속.
- 테스트용 캐릭터 추가.

**비목표 (YAGNI)**: 태그 전환(조작 캐릭터 실시간 교체), 동료 레벨/성장, 가챠, 근무지 배치, 동료 사망/부활 규칙. → 다음 슬라이스.

---

## 2. 현재 상태

- `PlayerController` — BTRunner 구동(입력→블랙보드→player BT). `IBTBlackboardInitializer` 구현. `Init(PlayerCharData)` 에서 `BTRunner.SetAsset(playerBt)`. 전투·스탯·무기·HP·애님 보유.
- 플레이어 BT 액션: `BTAction_PlayerMove/AutoAttack/Dash/Skill`, `BTCondition_PlayerEnemyInAttackRange`.
- `PlayerManager`(싱글톤) — 단일 리더 스폰/리포지션. `SpawnOrReposition(spawnPoint)`, 씬 전환 시 `RepositionInScene()`.
- `PlayerCharTable` / CSV `Players.csv` — PLR_001(9001)~PLR_003(9003). 전부 9001 프리팹·bt/player/9001 재사용.
- `ServerSaveData` — `SelectedCharId=9001`(단일). 파티 필드 없음.
- 군집: `MonsterKitchen.AI` 의 `FlockManager`/`IFlockAgent`/`FlockSteering`/`FlockWeights` (단일 공용 그리드).
- 씬 진입점: 각 SceneController `OnInit()` 에서 매니저 초기화 + `PlayerManager.RepositionInScene()`.

---

## 3. 아키텍처

```
PlayerController (수정)
  + IFlockAgent 구현 → FlockManager 등록/해제 (리더·동료 공통, 단일 그리드)
  + bool IsCompanion, Transform Leader (스폰 시 주입)
  + 이동 진입점 헬퍼 MoveBy(Vector2 velocity) — BT 액션이 flock 결과 전달
  · 리더: 기존대로 입력→블랙보드→player BT (변경 없음)
  · 동료: 입력 배선 없음, companion BT 구동

CompanionManager (신규 싱글톤)
  · 던전 씬에서만 동료 스폰/디스폰 (DontDestroyOnLoad 아님)
  · PlayerDataManager 의 파티 ID 로 리더 주변에 PlayerController 스폰
  · SpawnManager 패턴: InstantiateDisabled → Init+BT/Leader 주입 → SetActive

Companion BT (신규 BTAsset: bt/companion/9001)
  Selector [ BTAction_CompanionEngage / BTAction_CompanionFollow ]

파티 데이터
  PlayerDataManager: IReadOnlyList<uint> PartyCompanionIds (읽기 직접 허용)
  NetworkManager:    RequestSetParty(ids, callback) (검증 후 반영, 변경은 이 경유만)
  ServerSaveData:    + List<uint> PartyCompanionIds (max 2, 영속)

Party Select UI (신규, ManagementScene, UIToolkit)
  PartySelectPanel — 로스터 카드 토글로 동료 2명 선택 → RequestSetParty → 저장

신규 테스트 캐릭터
  Players.csv: PLR_004/005 (9001 프리팹·BT 재사용, 스탯·클래스·★ 차등)
```

**경계:** PlayerController 가 leader/companion 두 역할을 플래그+BT로 분기(이동·전투 공통 코드 공유, 입력만 리더 전용). CompanionManager 는 스폰/디스폰만. 파티 구성은 PlayerDataManager(읽기)+NetworkManager(변경)+ServerSaveData(영속)로 분리. flock 은 공용 레이어 그대로 사용.

---

## 4. 동료 스폰 시점/순서 (핵심 — 반드시 준수)

동료 스폰은 다음 선행조건이 **모두** 충족된 뒤에만 실행한다:

1. `DataRegistry` 로드 완료 (PlayerCharData 조회 가능) — GameStartup Phase 2 이후.
2. **던전 씬 진입 + 해당 씬에 `NavGrid` 존재** (동료 NavAgent A* 필요). 동료는 던전에서만 스폰.
3. **리더(`PlayerManager.Instance.Player`) 가 던전 씬에 배치 완료** (`RepositionInScene()` 이후). 동료는 리더 주변에 스폰하므로 리더 위치 확정 필수.
4. `FlockManager` — lazy 부트스트랩이므로 별도 보장 불필요(등록 시 생성).

**호출 지점:** `DungeonMapController.OnInit()`(SceneControllerBase, 던전 씬 진입 훅 — 여기서 `PlayerManager.RepositionInScene()` 호출) 에서 리포지션 **직후** `CompanionManager.SpawnParty()` 호출. ManagementScene 등 다른 씬에서는 스폰하지 않는다.

**스폰 절차 (SpawnManager 패턴 — 순서 엄수):**
```
foreach companionId in PlayerDataManager.PartyCompanionIds:
  1. data = DataRegistry.GetPlayerChar(companionId)   // null 이면 skip + LogError
  2. go = InstantiateDisabled(playerPrefab, leaderPos + 분산 오프셋)
  3. pc = go.GetComponent<PlayerController>()
  4. pc.MarkAsCompanion(leader = PlayerManager.Instance.Player.transform)  // BT/입력 분기 플래그
  5. pc.Init(data)                                    // 내부에서 BTRunner.SetAsset
  6. pc.OverrideBtAsset(companionBt)                  // player BT → companion BT 로 교체
  7. go.SetActive(true)                               // OnEnable → BTRunner.Start (이제서야 트리 시작)
```
- **반드시 SetActive(true) 전에** IsCompanion·Leader·companion BT 가 세팅돼야 한다. `BTRunner.Start()` 는 활성화 후 자동 실행되므로(기존 BTMonsterController 패턴과 동일), 활성화 전에 BT 에셋·블랙보드 선행조건을 모두 주입한다.
- 분산 오프셋: 리더 뒤쪽 반원에 동료 수만큼 분산(겹침 스폰 방지). 스폰 직후 flock 이 재정렬.

**디스폰:** 던전 → 다른 씬 전환 시 `CompanionManager` 가 동료 GameObject 파괴(+ FlockManager Unregister 는 OnDisable 에서 자동).

---

## 5. Companion BT 동작

블랙보드 키: `Leader`(Transform), `FollowDistance`(≈1.5), `EngageRange`(=무기 사거리), `DetectRange`, `FlockWeights`.

### 5.1 BTAction_CompanionEngage (우선순위 1)
```
적 탐지: Physics2D.OverlapCircle(self, DetectRange, EnemyLayer) → 최근접 적
적 없음 → Failure (→ Follow)
적 있음:
  enemyDist > EngageRange  → FlockSteering.ComputeChase(navDir→적) 로 접근 (Running)
  enemyDist <= EngageRange → FlockSteering.ComputeEncircle(적, ringRadius=EngageRange)
                             둘러싸며 + 기존 공격 발동 (Running)
```
적을 flock 으로 둘러싸며 공격 — 몬스터 encircle 과 동일 함수, 타깃만 적.

### 5.2 BTAction_CompanionFollow (우선순위 2)
```
dist(self, Leader) <= FollowDistance → 정지 (Success)
dist > FollowDistance → FlockSteering.ComputeChase(navDir→Leader) (Running)
```
동료끼리·리더와 separation 으로 비겹침. 리더를 느슨히 둘러쌈.

### 5.3 이동/공격 적용
- 이동: 두 액션 모두 `FlockManager.QueryNeighbors` → `FlockSteering` 결과를 `PlayerController.MoveBy(velocity)` 로 전달. NavAgent A* 방향을 seek 에 사용(벽 회피).
- 공격: 기존 플레이어 공격 경로 재사용. 구현 방식(기존 `BTAction_PlayerAutoAttack` 재사용 vs `PlayerController.TryAutoAttack()` 공개 헬퍼)은 PlayerController 공격 코드 확인 후 플랜에서 확정. 신규 전투 로직 없음.

---

## 6. Party Select UI (ManagementScene, UIToolkit)

```
┌─ 파티 편성 ─────────────────────────────┐
│  리더: [기본 플레이어 ★1 Sword]  (고정)   │
│  동료 선택 (n/2)                          │
│  ┌────────┐ ┌────────┐ ┌────────┐        │
│  │견습 궁수│ │견습 마법사│ │ PLR_004 │  …    │
│  │ ★2 Bow │ │ ★3 Staff│ │  ★1    │        │
│  │  [✓]   │ │  [✓]   │ │  [ ]   │        │
│  └────────┘ └────────┘ └────────┘        │
│         [ 저장 ]      [ 닫기 ]            │
└──────────────────────────────────────────┘
```
- 로스터 = PlayerCharTable 전체(리더 `SelectedCharId` 제외). 카드 = DisplayName/NatalStars/CharClass.
- 카드 토글, 최대 2명(초과 선택 시 막기). **저장** → `NetworkManager.RequestSetParty(ids)` → ServerSaveData 반영 + SaveScheduler dirty.
- UIToolkit(GameHUD 스택과 일관). 컴포넌트 `PartySelectPanel` + UXML/USS. 오픈 트리거: ManagementScene UI 버튼.

---

## 7. 데이터 · 영속

- `ServerSaveData`: `+ List<uint> PartyCompanionIds = new()`. 직렬화 라운드트립.
- `PlayerDataManager`: `IReadOnlyList<uint> PartyCompanionIds` 읽기 직접 허용.
- `NetworkManager.RequestSetParty(IReadOnlyList<uint> ids, Action callback)`: 검증(최대 2, 리더 ID 제외, 중복 제거, 유효 PlayerChar ID) 후 ServerSaveData 반영 + dirty.
- 로드 시 무효/리더 중복 ID 드롭 + `DebugUtil.LogWarning`.

---

## 8. 신규 캐릭터 + Companion BT 에셋

- `Players.csv`: PLR_004/005 추가 — 9001 프리팹·bt/player/9001 재사용(플레이스홀더 아트), 스탯/클래스/★ 차등.
- `bt/companion/9001` BTAsset 생성 + AssetManifest 등록.
- Editor 자산(SO sync, BTAsset, manifest)은 헤드리스 batchmode 자동화로 처리(`FlockAutomation` 패턴 재사용). `[[reference-headless-editor-automation]]`

---

## 9. 에러 처리 (CLAUDE.md)

- `PlayerManager.Instance.Player == null` → 동료 스폰 skip + LogError.
- companion BT / PlayerChar 데이터 로드 실패 → 해당 동료 skip + LogError.
- `FlockManager.Instance == null` → seek-only 폴백(기존 동일).
- 파티 0명 → 동료 없이 정상 진행.
- 무효 가중치(음수) → OnValidate clamp(기존 동일).

---

## 10. 테스트

### EditMode
- `ServerSaveData` 파티 라운드트립(저장→로드 동일, 빈 리스트 포함).
- `RequestSetParty` 검증: 3명 시도→2로 컷, 리더 ID 제외, 중복 제거, 무효 ID 드롭.
- 동료 상태 결정 순수 헬퍼(추출 시): 적 DetectRange 내→Engage, 밖→Follow.

### PlayMode
- 리더 + 동료 2 스폰 → N초 후 동료가 FollowDistance 내 + 서로/리더와 비겹침(평균 최근접거리 ≥ sepRadius*0.8).
- 적 스폰 시 동료가 적 방향으로 전환(Engage) 검증.

### 시각 검증
- 던전에서 동료 추종·적 둘러싸기 육안 (영상 대조).

---

## 11. 영향 범위 (파일)

| 파일 | 변경 |
|---|---|
| `Player/PlayerController.cs` | IFlockAgent 구현, MarkAsCompanion/Leader, MoveBy, OverrideBtAsset |
| `Core/Managers/CompanionManager.cs` | 신규 — 던전 동료 스폰/디스폰 |
| `AI/BehaviorTree/Player/BTAction_CompanionFollow.cs` | 신규 |
| `AI/BehaviorTree/Player/BTAction_CompanionEngage.cs` | 신규 |
| `Core/Managers/PlayerDataManager.cs` | PartyCompanionIds 읽기 접근 |
| `Core/Managers/NetworkManager.cs` | RequestSetParty + 검증 |
| `Data/Server/ServerSaveData.cs` | PartyCompanionIds 필드 |
| `Dungeon/DungeonMapController.cs` | OnInit → RepositionInScene 직후 SpawnParty 호출 |
| `UI/.../PartySelectPanel.cs` (+UXML/USS) | 신규 파티 편성 UI |
| `Data/CSV/Players.csv` + SO | PLR_004/005 |
| BTAsset `bt/companion/9001` + AssetManifest | 신규 (자동화) |
| `Assets/Tests/EditMode|PlayMode/...` | 파티/동료 테스트 |
