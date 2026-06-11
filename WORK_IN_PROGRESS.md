# Work In Progress

## 완료: 모듈 7-6, 7-7 — 레시피 도감 UI + 요리 마스터리 보너스

- RecipeBookUI: 마스터리 보너스 표시 (속도·재료절약·등급업)
- PlayerCookingMasteryData: GetSpeedMultiplier / GetIngredientSaveChance / GetGradeUpChance
- CookingStation: 마스터리 보너스 실제 적용 (등급업 확률, 조리속도, 재료절약 환급)
- EditMode 테스트 7종 추가

## 완료: 모듈 8-2, 8-8, 8-9, 8-10, 8-11 — 식당 경영 완성

- PlayerFameData: 명성 시스템 (GetDailyFame, MaxGuestsPerDay, MenuSlotCount)
- DailyMenuData: 메뉴 슬롯 (Reset/SetSlot/ClearSlot)
- PlayerDataManager: Fame + DailyMenu 프로퍼티 추가
- ServerSaveData: TotalFame 저장 필드
- CustomerAI: 만족도 점수 (patience 타이머 보너스/패널티, 등급 보너스, 테이블 오염)
- RestaurantTable: 오염 상태 + 청소 상호작용 (E키)
- DayManager: 일일 통계 (DayRevenue, DayTips, AvgSatisfaction), OnAllGuestsLeft 이벤트, CompleteDay()
- MenuSetupUI + MenuSetupPanel.uxml/uss: 영업 전 메뉴 구성
- SettlementUI + SettlementPanel.uxml/uss: 영업 결산 화면
- RestaurantSceneController: 전체 흐름 조율 (MenuSetup → StartDay → Settlement → CompleteDay)
- HUD.prefab: MenuSetupUI + SettlementUI 자식 추가, UIDocument sourceAsset 연결
- EditMode 테스트 13종 추가 (PlayerFameDataTests)

## 완료: 모듈 5-2, 5-4, 5-5, 5-6 — 던전 리워크

- DungeonMapController (DungeonSceneController 교체)
- DungeonSpawnZone (DungeonRoom + MonsterRespawnManager 교체, 자동 리스폰 8초)
- DungeonBag (무게 기반 임시 컨테이너, TryAdd/Remove/FlushToInventory)
- DungeonBagUI (B키 토글, 버리기)
- DungeonExit (항상 활성, 가방 여유 → 확인 팝업, FlushToInventory)
- IngredientData.Weight, PlayerUpgradeData.BagCapacity
- GameHUD: 미니맵 + 가방 무게 바 + ShowNotification 토스트
- DungeonScene 재배선 (SpawnZone1/2 id=6001/6002, m_WorldBounds=-10,-8,20,16)
- 기존 파일 삭제: DungeonSceneController, DungeonRoom, DungeonDoor, MonsterRespawnManager
- EditMode 74/74 통과

## 완료: 모듈 9-6 — 로딩 화면

- Loading.uxml: progress-bar-bg/fill + tip-label 추가
- Loading.uss: 프로그레스 바 + 팁 텍스트 CSS
- SceneLoader: LoadingProgress 프로퍼티 + OnProgressChanged 이벤트 추가
- LoadingSceneController.cs: OnProgressChanged 구독 → fill width 갱신, 랜덤 팁 10종
- LoadingScene: LoadingUI에 LoadingSceneController 컴포넌트 추가

## 완료: 모듈 10-2 — 이펙트 풀

- PooledParticle: ParticleSystem 래퍼, 재생 완료 시 콜백으로 풀 자동 반환
- VFXManager: 히트·사망·요리완성 3종 ObjectPool<PooledParticle> 관리 싱글톤
- VFXTrigger: Health.OnDamaged→PlayHit / Health.OnDeath→PlayDeath 브릿지 컴포넌트
- AssetKeys: PREFAB_VFX_HIT / DEATH / COOK_COMPLETE 추가
- CookingStation: VFXManager.PlayCookComplete 우선 호출, 직접 PS는 fallback 유지
- VFX 프리팹은 아트 작업 후 AssetManifest 에 등록 필요 (현재 미등록 시 LogWarning)

## 완료: 코드 품질 고도화 (2026-06-05-hardening-design.md)

- **A — 이벤트 누수**: CookingUI OnOpen/OnClose 구독쌍, InventoryUI OnOpen/OnClose 구독쌍, GameHUD SaveScheduler OnEnable/OnDisable
- **B — 널 안전성**: CustomerAI.Serve null guard, RestaurantSceneController prefab null→return, PlayerManager.SpawnOrReposition null→return, DayManager.SetRestaurantConfig prefab null→return, CookingStation 요리실패 early exit
- **C — 에러 피드백**: CookingStation 실패 시 GameHUD.ShowNotification, CookingUI TryCook 실패 알림, DataRegistry 재료 Weight≤0 보정 로그, PlayerDataManager.LoadFrom null 로그
- **D — 성능**: PlayerController.OnMoved 이벤트, DungeonSpawnZone.OnMonsterCountChanged 이벤트, GameHUD.Update() 폴링 제거 → 이벤트 구동
- **E — 인터페이스**: INetworkManager, IServerDBManager 신규, NetworkManager/ServerDBManager 구현, FakeNetworkManager(Tests)

## 완료: MODULES.md MVP 완성도 검토 (2026-06-05)

- 모듈 5 완료 테스트 8·9 추가 (플레이어 사망 처리, ResourceNode 채집)
- 모듈 5 항목 5-7, 5-8 추가 및 ✅ 표시
- 모듈 9 항목 9-0 추가 (StartScene — GlobalController + GameStartup + StartSceneController)
- **신규 모듈 11** — ManagementScene 기지 허브 추가 (11-1~11-3 ✅, 11-4~11-5 ❌)
- **Post-MVP 모듈 12~15** 추가 (오디오, 음식 버프, 튜토리얼, 농장)
- "현재 작업 위치" MVP 경계선 표시

## 완료: 모듈 11-4 — 업그레이드 스테이션 HUD 피드백 (2026-06-05)

- ToolUpgradeStation: Trigger 진입 시 `[도구 {label}] Lv{n} → Lv{n+1}  비용: {cost}G  (E 업그레이드)` 힌트
- ToolUpgradeStation: 성공 → `Lv{n} 업그레이드 완료!` / 실패 → `골드 부족 — {cost}G 필요합니다.`
- ShopUpgradeStation: 동일 패턴 (좌석·팁 배율)
- DebugUtil.Log → GameHUD.ShowNotification 전환

## 완료: 모듈 11-5 — BagUpgradeStation 씬 배치 (2026-06-05)

- BagUpgradeStation.cs 신규 스크립트 (ToolUpgradeStation 패턴 동일)
- Trigger 진입 시 `[가방 용량] Lv{n} → Lv{n+1}  현재: {cap}kg  비용: {cost}G` 힌트
- 성공/실패 GameHUD.ShowNotification
- ManagementScene BagUpgrade GO 배치 (position 4, 2, 0) — ShopUpgrade_Seats/Tip 라인 연장
- ManagementScene 저장 완료

## 완료: 모바일 Input (2026-06-05)

- MobileAction / MobileContext enum
- VirtualJoystick (dynamic floating, 화면 왼쪽 절반, 멀티터치 안전)
- MobileButton (SetAction 런타임 교체, PointerDown scale 피드백)
- MobileHUD (DontDestroyOnLoad 싱글톤, SetContext 슬롯 재구성, m_ForceShow 에디터 토글)
- InputManager Inject* 메서드 (InjectMove/Dash/Skill1/Skill2/Interact/Ultimate)
- GlobalController m_MobileHUDPrefab 필드 + SpawnMobileHUD()
- 씬 컨트롤러 연동: DungeonMapController(Dungeon), ManagementSceneController(Exploration), KitchenSceneController(Exploration), RestaurantSceneController(Exploration)
- InteractionPrompt: Trigger 진입 → DungeonInteract, 이탈 → Dungeon
- MobileHUD.prefab: Assets/Prefabs/UI/MobileHUD.prefab (Canvas 100 + Joystick + ButtonPanel 3슬롯)
- StartScene GlobalController.m_MobileHUDPrefab 연결 완료
- EditMode 테스트 7종 (MobileInputTests.cs)

## 다음 우선 작업: 모듈 3-A6/A7 — 무기 에셋 제작 후 SkillData.animTriggerOverride 실 클립 연결
