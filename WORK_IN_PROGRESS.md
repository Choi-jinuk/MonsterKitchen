# Work In Progress

> **운영 규칙 (2026-06-12)**: 사용자 지시사항은 작업 시작 전 이 파일의 `### 지시` 항목에 먼저 기입.
> 처리 내용은 진행 순서대로 즉시 기록 (몰아서 쓰지 않기). 세션 단절 시 이어서 작업 가능하게.

## 완료 (Editor 에셋 + 시각 검증 대기): 몬스터 군집 이동 & 둘러싸기 (2026-06-13)

### 지시 (2026-06-13)
- 사용자: WC3 Local Avoidance 영상 참고. 콜라이더 충돌 아닌 군집(flocking)으로 겹침 방지 + 플레이어 도착 시 멈추지 않고 빈공간 찾아 둘러싸기. 검증은 슬라임 아닌 연속이동 오크형 신규 몬스터로.
- 스펙: `docs/superpowers/specs/2026-06-13-monster-flocking-encircle-design.md`
- 플랜: `docs/superpowers/plans/2026-06-13-monster-flocking-encircle.md`

### 처리 내역 (브랜치 `feature/monster-flocking-encircle`, 10커밋)
- ✅ FlockSteering (순수함수: Separation 역제곱 / ComputeChase / ComputeEncircle arrival+tangential) + EditMode 8테스트
- ✅ IMonsterFlockAgent + SpatialHashGrid(공간해시) + EditMode 4테스트
- ✅ MonsterFlockManager(싱글톤, 매 FixedUpdate 그리드 재빌드, lazy 부트스트랩, 파괴 에이전트 자동 정리)
- ✅ BTMonsterController: OverlapCircle 제거 → 매니저 질의, IMonsterFlockAgent 구현, OnEnable/OnDisable 등록·해제
- ✅ ContinuousMovement(오크형 연속 조향 이동)
- ✅ BTAction_Attack: 정지/푸시백 → ComputeEncircle (멈추지 않고 둘러싸며 공격)
- ✅ SlimeMovement: dash 방향 군집 보정
- ✅ MON_006 오크 CSV 행
- ✅ 검증: EditMode 114/114 통과, PlayMode 6통과(+1 기존 ignore), 실패 0
- ✅ 버그픽스: 파괴된 MonoBehaviour는 C# null 아님 → FlockTransform 가드가 throw → 후속 테스트 오염. Unity == 가드 + 테스트 teardown 으로 해결.

### Editor 에셋 자동화 (batchmode -executeMethod FlockAutomation.Run 로 처리, 완료)
- ✅ SyncAllSO → TableData.asset MON_006 반영 (Monster 6개, 유효성 통과)
- ✅ 오크 프리팹 `Assets/Prefabs/Enemies/Orc.prefab` 생성 (SlimeMovement→ContinuousMovement), AssetManifest `prefab/monster/1006`·`sprite/monster/1006`(슬라임 재사용)·`bt/monster/1006`(MonsterBT 공유) 등록
- ✅ Physics2D Layer Collision Matrix: Enemy↔Enemy 해제 (Physics2DSettings.asset)
- ✅ **빌드깨짐 방지**: Slime/Orc 프리팹 BTMonsterController.m_FlockWeights=Default 직렬화 기입 (신규 필드라 기존 프리팹은 default 0 으로 로드됨)
- ✅ 새 .cs .meta + 자동화 스크립트 커밋

### 일반화 (2026-06-13 추가 지시): 동료 AI 공용화
- 지시: 플레이어 동료(컴패니언)도 AI로 작동 → 이동 로직을 몬스터 전용 아닌 공용으로.
- ✅ 공유 레이어 `Assets/Scripts/AI/Flock/`, 네임스페이스 `MonsterKitchen.AI` 로 이동: FlockSteering·SpatialHashGrid·FlockWeights
- ✅ `IMonsterFlockAgent`→`IFlockAgent`, `MonsterFlockManager`→`FlockManager` 리네임
- ✅ separation 범위 = 전체 공용 단일 그리드 (진영 구분 없이 모두 겹침 회피, WC3 방식)
- ✅ 컴패니언은 향후 `IFlockAgent` 구현 + `FlockSteering` 직접 호출로 재사용 (ContinuousMovement/IMonsterSeparation 은 몬스터측 잔류, YAGNI)
- ✅ 소비자(BTMonsterController/BTAction_Attack/테스트/FlockAutomation) 전부 갱신, 잔여 참조 0
- 검증: 일반화는 순수 이동/리네임 (로직 무변) — EditMode 재실행 대기 (사용자 Editor 락으로 batchmode 보류)

### 남은 작업
- [ ] EditMode/PlayMode 재실행 (사용자 Editor 닫으면 batchmode, 또는 Test Runner)
- [ ] DungeonScene 다수 스폰 **시각 검증** (겹침 0 + 둘러싸기 형성, 영상 대조) — 인터랙티브 플레이 필요, 사용자 확인

## 진행 중 (브레인스토밍 — 일시 중단): 무기 장비 시스템 (2026-06-13)

### 지시 (2026-06-13)
- 사용자: "캐릭터 장비를 사용할 수 있게 해줘. 검, 활, 지팡이 같은 무기들을 갈아끼울 수 있는 구조"
- brainstorming 스킬 진행 중 — 확정된 결정:
  - [x] 무기 획득: **B. 상점 구매** (ManagementScene 무기 상점)
  - [x] 보유 방식: **B. 컬렉션** (여러 무기 보유 + 별도 장착 슬롯)
  - [x] 장착 UI: **B. 인벤토리 탭 분리** (상점은 구매만, 장착은 Equipment/Inventory 화면)
  - [x] **상위 방향 확정 (2026-06-13)**: **캐릭터 뽑기 중심 시스템**
        구성: 캐릭터 뽑기 + 배치 + 성장 + 무기 구매 + 무기 강화
        무기는 클래스 제한 — 캐릭터 클래스별 장착 가능 무기 타입 제한
        (기존 확정 3건 중 "상점 구매"는 유지, "컬렉션+장착 UI"는 클래스 제한 구조로 재해석)
- 서브시스템 분해 (각각 스펙→플랜→구현 사이클):
  1. 캐릭터 시스템 기반 — 다중 캐릭터 보유 + 클래스 + 무기 제한 (데이터 구조)
  2. 캐릭터 뽑기 (가챠) — 재화, 확률, 연출, 풀
  3. 캐릭터 배치 — **확정**: 근무지(주방 or 식당)는 캐릭터당 배타적 1곳,
     던전 배치는 근무지와 무관하게 중복 가능 (주방 배치 캐릭터도 던전 출전 가능).
     캐릭터에 작업 스탯 추가 필요 — 요리 속도, 서빙/보조 속도 등.
     던전 = **파티 출전: 3명, 태그 전환 (키 입력 조작 캐릭터 실시간 교체)** 확정.
  - 뽑기 재화: **C. 이중** — 골드 뽑기(일반 풀) + 전용 재화 뽑기(고급 풀)
  - 성장: **B. 재료 소모** — 골드/성장 재료로 레벨업 (대기 캐릭터도 성장 가능)
  - 중복 뽑기: **A. 승급 재료** — 돌파/별 승급, 성장 상한 해제
  - 클래스: **B. 무기 타입 1:1** — 캐릭터 클래스 = WeaponType, 해당 타입 무기만 장착
  - 희귀도: **태생 ★1~3**, 승급으로 전 캐릭터 **★10 상한** 통일
  - 근무지 배치 동작: **C. 자동 + 플레이어 개입 (데이브 더 다이버 방식 적극 채용)**
    — 배치 캐릭터 AI 자동 요리/서빙, 플레이어 개입 시 가속/품질 향상
  - 무기 강화: **C. 강화 + 한계돌파** — 골드 강화, 일정 단계마다 상위 재료 돌파 필요
  4. 캐릭터 성장 — 레벨/경험치/스탯 성장
  5. 무기 구매 상점 + 무기 강화
- [x] 구축 순서 확정 (2026-06-13): 1 기반 → 2 뽑기 → 3 성장/승급 → 4 무기 구매/강화 → 5 근무지 배치 → 6 던전 파티
- [x] 서브 1 설계안 승인 (안 1 — 기존 4-레이어 패턴 연장) + 섹션 4개 (데이터/로스터/저장·API/테스트) 승인
- [x] 스펙 작성: `docs/superpowers/specs/2026-06-13-character-system-foundation-design.md`
      (상위 방향 전체 결정 기록 포함 — 서브 2~6 기준 문서)
- [x] 사용자 스펙 리뷰 (승인)
- [x] writing-plans → 구현 플랜: `docs/superpowers/plans/2026-06-13-character-system-foundation.md` (Task 1~6)
- [ ] 실행 방식 선택 (사용자) → 구현

### 기존 코드 현황 (탐색 완료)
- `PlayerStats.EquipWeapon(WeaponData)` 이미 존재 — 스탯 합산 + OnWeaponChanged 이벤트
- `WeaponSocketController.SetWeapon()` 이미 존재 — 스프라이트/애니메이터 런타임 교체
- `WeaponData` (WeaponTable.cs) — Abils, Tier, MaxDurability, NormalAttackGroup, 주소 키 보유
- 미구현: 무기 보유(인벤토리), 장착 영속 저장(ServerSaveData에 무기 필드 없음), 구매 상점, 장착 UI
- NetworkManager Request* 패턴 준수 필요 (RequestUpgrade 참고)

## 완료 (수동 점검 대기): Localization 전환 — Unity Localization 마이그레이션 (2026-06-12)

### 지시 (2026-06-12)
- 사용자: "Localization 전환 해달라" (이전 세션 요청, 기록 유실 — 상세 범위 재확인 필요)
- 현재 상태: 커스텀 `LocaleManager`(Core/LocaleManager.cs) + StringTable SO 사용 중, `com.unity.localization` 패키지 미설치
- [x] 범위 확인 (사용자) — **Unity Localization 패키지 마이그레이션** 확정:
      `com.unity.localization` 설치, 커스텀 LocaleManager + StringTable SO → 공식 패키지 이관
- [x] brainstorming → 설계안 확정 + 스펙 문서
- [x] 사용자 스펙 리뷰 (승인)
- [x] writing-plans → 구현 계획: `docs/superpowers/plans/2026-06-12-unity-localization-migration.md` (Task 1~13)
- [x] 구현 — 플랜 Task 1~13 전부 완료 (Inline 실행)
- [x] 검증 — EditMode 99/99, PlayMode 5/6+1 Skip(의도), 컴파일 에러 0, 잔여 참조 0
- [ ] 수동 점검 (사용자) — 에디터 Play: Touch To Start/HUD/InteractionPrompt 라벨 표기,
      언어 전환(`LocalizationSettings.SelectedLocale`) 시 UI 즉시 갱신 확인

### 처리 내역
- 2026-06-12: 범위 재확인 (Unity Localization 패키지 마이그레이션), brainstorming 시작
- 2026-06-12: 설계 결정 확정 — ① 원본 = 패키지 CSV Extension (StringData.csv in-place 포맷 변환),
  ② LocaleManager 전면 교체(삭제), ③ 패턴 B = 전 사용처 LocalizedString 객체
- 2026-06-12: 설계 승인 → 스펙 작성: `docs/superpowers/specs/2026-06-12-unity-localization-migration-design.md`
- 2026-06-12: 스펙 승인 → 플랜 작성: Task 1 패키지/asmdef → 2 CSV 변환 → 3 에디터 셋업 툴 →
  4 Loc 헬퍼 → 5 테이블 4종 → 6 LocalizedLabel → 7 UIPanel → 8 InteractionPrompt →
  9 CookingUI → 10 GameStartup → 11 구 시스템 삭제 → 12 테스트 → 13 최종 검증/문서
- 2026-06-12: [T1 완료] com.unity.localization@1.5.11 설치, asmdef 4개 참조 추가
  (MonsterKitchen / Tests.EditMode / Tests.PlayMode / MonsterKitchen.Editor — Editor 는 Unity.Localization.Editor 포함)
- 2026-06-12: [T2 완료] StringData.csv 변환 — 67행 보존, 헤더 `Key,Id,Shared Comments,Korean(ko),English(en)`, Key 중복 0
- 2026-06-12: [T3 완료] LocalizationSetupTool 작성 + Setup 실행 — Settings/ko·en Locale/en→ko 폴백/
  `{key}` 미등록 처리/Strings 컬렉션/프리로드/CSV 임포트 67키. SetPreloadTableFlag 는 테이블 단위 API 로 수정
- 2026-06-12: [T4~T10 완료] Loc 헬퍼 신설(Core/Util/Loc.cs), 테이블 4종(Monster/Ingredient/Food/Recipe)
  `[NonSerialized]` LocalizedString 캐시 + Loc.Get, LocalizedLabel StringChanged 자가 갱신 재작성,
  UIPanel RefreshLocale/OnLanguageChanged 제거, InteractionPrompt StringChanged 콜백,
  CookingUI 8곳 s_Ls* static 캐시 치환(RefreshLocale override 삭제), GameStartup StepDataLoad 에
  LocalizationSettings.InitializationOperation yield 추가. Unity.ResourceManager asmdef 참조 추가
  (MonsterKitchen/Tests.PlayMode — AsyncOperationHandle). 컴파일 에러 0
- 2026-06-12: [T11 완료] 구 시스템 삭제 — LocaleManager.cs/StringTable.cs 파일 삭제,
  GlobalController Locale 프로퍼티/생성/Init 제거, TableData.Strings + AllTables 항목 제거,
  DataRegistry Strings 프로퍼티/로그/OnDataLoaded 제거, DataManagerWindow Strings 탭/헤더/Sync 케이스/Mapper 제거,
  TableData.asset 재직렬화. 잔여 참조 0, 컴파일 에러 0
- 2026-06-12: [T12 완료] LocalizationCsvTests (EditMode 3종) + LocalizationPlayTests (PlayMode 3종) —
  EditMode 99/99 PASS, PlayMode 5 PASS + 1 Skip (En 빈 키 없음, Assert.Ignore 의도 동작)
- 2026-06-12: [T13 완료] CLAUDE.md (싱글톤 표/구조/다국어/패키지/GameStartup), MODULES.md 9-12 추가.
  수동 비주얼 스모크는 사용자 점검 항목으로 이관

## 완료 (수동 점검 대기): 던전 맵 확장 — 허브 + 3갈래 (2026-06-11)

스펙: `docs/superpowers/specs/2026-06-11-dungeon-map-expansion-design.md`
플랜: `docs/superpowers/plans/2026-06-11-dungeon-map-expansion.md`

- [x] Task 1 CSV — DST_003~006, RNO_004 추가 + Sync All SO (143 항목)
- [x] Task 2 DungeonGate — TDD (4 테스트 GREEN), ComputeGateDamage 순수 로직
- [x] Task 3 PlayerController.TryHarvestNode — 노드/게이트 최근접 분기
- [x] Task 4 DungeonZoneTrigger — 토스트 + Light2D 보간 (asmdef에 URP Runtime/2D.Runtime 참조 추가)
- [x] Task 5 DungeonMapBuilder — 바닥 1462셀 + 벽 318셀 페인트 (Ground/Walls), DevFloor/DevWall 타일 생성
- [x] Task 6 씬 재배치 — WorldBounds 60×48, 스폰존 4, 노드 11, 게이트 1, 존 트리거 5, 입구/출구 허브 이동
- [x] Task 7 PlayMode 스모크 — DungeonScene 4존 + NavGrid 폭(>50) 검증 통과
- [x] Task 8 마무리 — EditMode 90/90 + PlayMode 3/3, MODULES.md 5-9 추가, 전경 스크린샷 확인
- [ ] 수동 점검 (사용자) — 게이트 도구 Lv 게이팅, 보스 전투, 구역 토스트/조명, 미니맵 마커

### 완료: 통일 소팅 + Occluder Fade (2026-06-11)
- 스펙/플랜: `2026-06-11-unified-sorting-occluder-fade-*.md`
- 월드 스프라이트 sortingOrder 통일(0): Projectile 10 제거, DungeonGate 5→0 — 깊이는 Perspective Distance 소트
- 예외 유지: Background(-10/-20 맨 뒤), DirtyOverlay(5, 테이블 표면 합성), EnemyHpBar/SceneFader/UI(앞)
- SpriteOccluderFade 신규: bounds 안 + 플레이어 Y > prop Y → 알파 보간. 채집 노드 0.55 / 데코 지형 0.3 프리셋
- DungeonScene 노드 11개 부착(0.55). EditMode 96/96, PlayMode 3/3

### 버그픽스: 플레이어 교전 거리 교착 (2026-06-11)
- 증상: 플레이어가 공격 범위 밖에서 멈춰 허공 공격 (다가가지 않음)
- 원인: BT 전환 조건이 SearchRange(2.5) 기준, 근접 타격 판정은 AttackRange(0.8) — 그 사이 거리에서 이동 억제 + 미스 반복
- 수정: `PlayerController.GetCurrentEngageRange()` 신설 (투사체=SearchRange, 근접=AttackRange),
  `BTCondition_PlayerEnemyInAttackRange` / `BTAction_PlayerAutoAttack` 이 교전 거리 사용
- 테스트: PlayerEngageRangeTests 3종 (EditMode 93/93, PlayMode 3/3)

### 트러블슈팅 기록
- Light2D = `Unity.RenderPipelines.Universal.2D.Runtime` 별도 어셈블리 (Unity 6 분리)
- refresh_unity 가 외부 파일 변경을 못 잡는 경우 있음 → `AssetDatabase.ImportAsset(ForceUpdate)` + DLL 타임스탬프 확인으로 검증
- scriptCompilationFailed 상태에서 테스트 러너가 스테일 DLL로 통과할 수 있음 — 컴파일 성공 확인 후 테스트 실행 필수

## 완료: MVP 점검 후속 — B(버그) → C(구조) → A(MVP 갭) 순차 수정 (2026-06-11)

### B — 버그 수정
- [x] B-2 PhaseManager.EndDay — KitchenScene → ManagementScene (다음 날 = 기지 아침)
- [x] B-4 NetworkManager 저장 정책 — EarnGold/ServeFood → MarkDirty, SpendGold/Upgrade/Cook → ForceSave 유지
- [x] B-6 CustomerAI.PickOrder — 재고·메뉴 무관 폴백 제거(null 반환), 주문 전 이탈 손님 통계 제외
- [x] B-1 DayManager — 스폰 성공 시에만 카운트, 스폰 환경 소실 시 IsOpen=false 강제 종료 (소프트락 방지)
- [x] B-8 PlayerInventoryData.LoadIngredients — m_IngredientQualities 클리어 추가
- [x] B-12 CookingStation — 등급 계산을 재료 소모 시점으로 이동
- [x] B-3 CookingStation — RequestCook 후처리 전부 콜백 내부로 (비동기 서버 대비)
- [x] B-7 DungeonBag — (id, quality) 품질별 독립 스택 (승급 인플레 제거) + 테스트 갱신
- [x] B-10 PlayerManager — 비활성 부모 Instantiate (프리팹 에셋 보존), SceneControllerBase.m_PlayerSpawnPoint 주입 + 4씬 Inspector 연결 완료
- [x] B-11 Projectile — static 풀 (Spawn/Release), 매 발사 GameObject 생성 제거
- [x] B-5/B-9 — C-1 InteractionHub 로 해결
- [x] (추가) MobileHUD.SetContext — 데스크톱에서 슬롯 구성 무시되던 기존 버그 수정 (표시만 플랫폼 게이트)
- [x] (추가) DungeonExit — Keyboard.current 직접 사용 제거 → InteractionHub (모바일 InjectInteract 동작)

### C — 구조 개선
- [x] C-1 InteractionHub + IInteractable + InteractableBehaviour 신규 (Core/Interaction/)
      — 8개 OnInteract 구독자 전부 이관, E키 1회 = 최근접 후보 1개만 실행
- [x] C-5 씬 이름 상수화 — CommonString.Scene* 추가, 전 리터럴 치환
- [x] C-2/C-3/C-4/C-6 — B 단계에서 처리

### A — MVP 갭
- [x] A-1 저장 스키마 v2 — IngredientQualityEntry / FoodGradeEntry 추가,
      PlayerInventoryData 품질·등급 save/load, v1 세이브 호환 (Normal 패딩)
- [x] A-2 일시정지 — GameHUD ESC 오버레이 (계속하기 / 저장 후 종료), Time.timeScale 제어
- [x] A-3 비던전 씬 사망 — 1.5s 후 Revive + 스폰포인트 리스폰 (페널티 없음)
- [x] A-4 PlayMode 스모크 테스트 — SceneFlowSmokeTests (부팅 완료 + Management 전환·플레이어 스폰)

### 검증
- [x] EditMode 86/86 통과
- [x] PlayMode 2/2 통과
- [x] read_console 컴파일 에러 0
- [x] 4씬 m_PlayerSpawnPoint 연결 + 저장 (Management/Kitchen/Dungeon/Restaurant)

## 남은 항목 (아트 의존 / 후순위)
- 3-A6/A7 무기 클립·스프라이트, 5-1 타일맵 페인트, 7-5 VFX 에셋
- 다음 우선 작업: 모듈 3-A6/A7 — 무기 에셋 제작 후 SkillData.animTriggerOverride 실 클립 연결
