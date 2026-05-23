# 모듈 개발 계획 — 몬스터 키친

> 최종 갱신: 2026-05-23
> **방침: 위에서 아래 순서대로. 한 모듈이 완전히 끝나야 다음 모듈을 시작한다.**
> 모듈 완료 테스트를 통과해야 ✅ 완료로 표시한다.

**상태 범례**: ✅ 완료 / 🔄 진행 중 / ⏳ 대기 / ❌ 미구현 / ⚠️ 부분 완료

---

## 확정 보류 사항 (최종 모듈 합산 시 결정)

| 항목 | 현재 방향 | 확정 시점 |
|---|---|---|
| 최종 스탯 계산식 | 플레이어 기본 스탯 + 장착 장비 Abil 합산 → SkillData damageMultiplier 적용 | 모듈 합산 단계 |

---

## 모듈 1 — 기반 인프라 ✅ 완료

> 모든 모듈의 공통 기반. 완료 후 재작업 없음.

| # | 항목 | 상태 |
|---|---|---|
| 1-1 | GameEnums (AttributeType, IngredientState, RarityType, PhaseType…) | ✅ |
| 1-2 | ScriptableObject 클래스 (MonsterData / IngredientData / RecipeData / FoodData / DropTableData) | ✅ |
| 1-3 | CsvParser (`;` 주석, `_` 무시, `#TYPE`, `\|` 배열) | ✅ |
| 1-4 | ScriptableObjectSync — CSV → SO 동기화 | ✅ |
| 1-5 | DataManagerWindow — Editor 창 (MonsterKitchen → Data Manager) | ✅ |
| 1-6 | 초기 CSV 데이터 (MON×5 / ING×8 / RCP×5 / FOOD×5 / DRP×5) | ✅ |
| 1-7 | InputManager — 통합 입력 싱글톤, C# 이벤트 배포, DontDestroyOnLoad | ✅ |
| 1-8 | UI 아키텍처 — UILayer / UIPanel / UIManager 싱글톤, 팝업 스택, ESC | ✅ |
| 1-9 | 오브젝트 풀 — ObjectPool\<T\> 제네릭 (Get / Return / 사전 워밍) | ✅ |

---

## 모듈 2 — 조작 ✅ 완료

> **범위 기준**: 플레이어 입력 → 위치·애니메이션 변화만 포함.
> 데미지 계산, 판정, 스킬 로직, 공격 데이터는 **모듈 3(전투)** 에서 다룬다.

> **모듈 완료 테스트** (DungeonScene 단독 플레이 기준)
> 1. WASD 8방향 이동 + 방향별 애니메이션 전환
> 2. Space 대시 — 이동 방향으로 순간이동, 쿨타임 후 재사용 가능
> 3. E키 — 근처 IInteractable 오브젝트에 프롬프트 표시 → 상호작용 실행

| # | 항목 | 상태 |
|---|---|---|
| 2-1 | Player.prefab (Rigidbody2D + Collider + SpriteRenderer + Animator) | ✅ |
| 2-2 | 8방향 이동 + 대각선 정규화 | ✅ |
| 2-3 | 이동 애니메이션 (MoveX / MoveY / Speed, Flip) | ✅ |
| 2-4 | 대시 (순간이동, 무적 프레임, 쿨타임) | ✅ |
| 2-5 | E키 상호작용 — IInteractable 호출 | ✅ |
| 2-6 | 상호작용 근접 프롬프트 — 범용 InteractionPrompt 컴포넌트, 월드 스페이스 말풍선 | ✅ |

> **모바일 전용 (Phase 2, 이 모듈에 포함하지 않음)**
> - 가상 조이스틱 → Move InputAction 연결

---

## 모듈 3 — 전투 🔄 진행 중 (애니메이션 구조 작업)

> **범위 기준**: 피격 수신 + 공격 실행 + 무기·스킬 시스템 전체.
> "데미지가 오가는 모든 것"이 이 모듈에 속한다.

> **모듈 완료 테스트** (DungeonScene 기준)
> 1. 플레이어 피격 → HP 감소 + 데미지 팝업
> 2. 대시 중 피격 → 무적으로 HP 감소 없음
> 3. 플레이어 HP 0 → 사망 처리
> 4. 몬스터 HP 0 → 사망 + 막타 속성 전달
> 5. 근거리 무기 장착 → 평타 콤보 히트박스 동작 (OverlapCircle)
> 6. 원거리 무기 장착 → 투사체 발사 동작 (missileSpeed > 0)
> 7. 스킬 슬롯 1·2 발동 + 쿨타임 UI 표시
> 8. 궁극기 게이지 피격·처치 시 적립 → MAX 시 발동
> 9. 무기 타입 미호환 스킬 → 장착 거부

### 피격 시스템 (완료)

| # | 항목 | 상태 |
|---|---|---|
| 3-1 | Health 컴포넌트 (MaxHp / TakeDamage / Heal, OnHpChanged·OnDamaged·OnDeath 이벤트) | ✅ |
| 3-2 | 무적 프레임 (Health.IsInvincible, 대시 연동) | ✅ |
| 3-3 | 피격 레이어 설정 (Player·Enemy 레이어 분리, Physics2D 충돌 무시) | ✅ |
| 3-4 | 데미지 팝업 (TMP 팝업, 오브젝트 풀, 위로 페이드) | ✅ |
| 3-5 | 사망 처리 (애니메이션 → 콜라이더 비활성 → 1.5s 후 파괴) | ✅ |
| 3-6 | 막타 속성 감지 (OnDeath(AttributeType killAttr)) | ✅ |

### 공격 데이터 구조

| # | 항목 | 상태 |
|---|---|---|
| 3-7 | AbilType enum + AbilEntry struct | ✅ |
| 3-8 | SkillData SO — cooltime·comboWindow·damageMultiplier·searchRange·attackRange·maxTargets·missileSpeed·missileMaxRange·animTriggerOverride·ccForce·ccDuration·stunDuration | ✅ |
| 3-9 | SkillGroupData SO — skillIcon·skillName·description·skillChain·allowedWeaponTypes | ✅ |
| 3-10 | WeaponData SO — weaponType·abils(List\<AbilEntry\>)·normalAttackGroup(SkillGroupData) | ✅ |
| 3-11 | AttackPattern enum 제거 (GameEnums.cs), AttackSkillData Obsolete 처리 | ✅ |

### 공격 실행

| # | 항목 | 상태 |
|---|---|---|
| 3-12 | PlayerController 공격 실행 리팩터 — SkillData 수치 기반 (searchRange·attackRange·maxTargets·missileSpeed) | ✅ |
| 3-13 | 평타 콤보 — WeaponData.normalAttackGroup 체인 실행, comboWindow 타이머 | ✅ |
| 3-14 | 투사체 발사 — Projectile(공용) 생성, 유도·AoE·직선 지원, missileMaxRange 초과 시 소멸 | ✅ |
| 3-20 | CC 시스템 — CrowdControlComponent (넉백·풀인·스턴), CCType enum, SkillData CC 파라미터 연동 | ✅ |

### 무기·스킬 장착 시스템

| # | 항목 | 상태 |
|---|---|---|
| 3-15 | PlayerStats 컴포넌트 — 무기 슬롯·스킬 슬롯 1·2·궁극기 슬롯 보유, 최종 스탯 계산 | ✅ |
| 3-16 | 무기 장착 처리 — normalAttackGroup 평타 적용, AbilEntry 스탯 합산 | ✅ |
| 3-17 | 스킬 장착 처리 — allowedWeaponTypes 호환 체크, 슬롯에 SkillGroupData 등록 | ✅ |
| 3-18 | 스킬 슬롯 1·2 발동 — 쿨타임 체크 → SkillData 체인 실행 (Q/R키) | ✅ |
| 3-19 | 궁극기 게이지 적립 & 발동 — 피격·처치 시 게이지 적립, MAX → 궁극기 SkillGroup 실행 (F키) | ✅ |

### 전투 테스트 UI

| # | 항목 | 상태 |
|---|---|---|
| 3-UI1 | 스킬 슬롯 1·2 UI — SkillGroupData.skillIcon + 쿨타임 오버레이 | ✅ |
| 3-UI2 | 궁극기 게이지 UI — 게이지 바 | ✅ |
| 3-UI3 | 무기 슬롯 UI — 현재 장착 무기 아이콘 표시 | ✅ |

### 2.5D 원근 효과

> **배경**: 배경/맵 GO를 X 축 -5도 기울이고, 엔티티 Z = Y * sin(5°) 로 동기화.
> Perspective 카메라가 Z 거리 차이를 자연스러운 원근감으로 변환한다.
> 스케일 조작 없음. Physics2D(XY 평면)와 완전 독립.

| # | 항목 | 상태 |
|---|---|---|
| 3-P1 | PerspectivePlaneSync 컴포넌트 (Z = Y·sin(tilt), isStatic 플래그) | ✅ |
| 3-P2 | Player · Slime · Customer 프리팹에 PerspectivePlaneSync(isStatic=false) 적용 | ✅ |
| 3-P3 | 전 씬 배경 GO x=-5 회전 + 카메라 Perspective FOV=55, TransparencySort=Distance | ✅ |
| 3-P4 | PerspectiveManager — static TiltAngleDeg 제공. PerspectivePlaneSync 가 직접 참조 (ScanAndApply 없음) | ✅ |
| 3-P5 | 씬별 tiltAngleDeg · 카메라 FOV 튜닝 (플레이 후 수치 조정) | ❌ |

### 애니메이션 구조 (무기 분리)

> **배경**: 무기만 WeaponSocket으로 분리. 상하체 Animator Layer 분리 없음.
> 목표: 무기 교체 용이성 + `_facingDir` 기반 360도 공격 방향 자유도 확보.
> 에셋(무기 스프라이트) 미준비 상태 → 3-A1~A5 코드/프리팹 작업 먼저 진행,
> 3-A6·A7은 에셋 완성 후 연동.

> **추가 완료 테스트**
> 10. WeaponSocket GO에서 무기 스프라이트 런타임 교체 확인
> 11. `_facingDir` 기반 WeaponSocket Transform 회전 → 360도 공격 방향 정상 동작
> 12. `_facingDir.y < 0.5f` 핵 제거 후 상하 공격 모두 overlayAnim 정상 재생
> 13. SkillData.animTriggerOverride — 스킬별 다른 공격 클립 재생 확인

| # | 항목 | 상태 |
|---|---|---|
| 3-A1 | WeaponSocket 자식 GameObject 추가 (Player.prefab, SpriteRenderer + Animator) | ✅ |
| 3-A2 | WeaponData — `weaponSprite`·`weaponAnimController` 필드 추가 | ✅ |
| 3-A3 | 무기 장착 시 WeaponSocket 스프라이트·AnimatorController 런타임 교체 | ✅ |
| 3-A4 | WeaponSocket Transform 회전 — `_facingDir` 기반 360도 방향 반영 | ✅ |
| 3-A5 | `_facingDir.y < 0.5f` 핵 제거 — 방향별 overlay 트리거 정리 | ✅ |
| 3-A6 | SkillData.animTriggerOverride 실 클립 연결 (에셋 준비 후) | ⚠️ 코드 연결 완료, 실 클립은 에셋 제작 후 |
| 3-A7 | 무기 스프라이트 Aseprite 임포트 (에셋 제작 후 연동) | ❌ |

### AI 활용 무기 에셋 제작 방식

> **전제**: 캐릭터 본체 파츠 분리는 하지 않음. **WeaponSocket에 붙는 무기 스프라이트만** 확보하면 된다.
> 무기 1종 = 스프라이트 1장(또는 소수의 프레임). AI는 제작량 감소가 목적이며,
> 최종 픽셀 정리는 Aseprite에서 수행한다.

#### 무기 에셋 제작 플로우

```
1. Midjourney / Leonardo.ai (또는 Scenario.gg)
   └─ 프롬프트 예시: "2D top-down fantasy sword sprite,
                      transparent background, pixel art, 16x32px"

2. Aseprite로 임포트
   └─ 픽셀 정리 + 팔레트 통일
   └─ 캔버스 규격 고정: 무기 16×32px 기준

3. Unity Aseprite Importer로 임포트
   └─ 개별 Sprite로 슬라이스
   └─ WeaponData.weaponSprite에 연결
```

#### 스타일 일관성 유지 포인트

- **팔레트 고정**: 첫 무기 확정 후 Aseprite 팔레트 파일(`.pal`) 저장 → 모든 무기에 동일 적용
- **캔버스 규격 고정**: 무기 16×32px 기준 문서화
- **레퍼런스 시트 유지**: 확정된 무기 1종을 `Assets/Art/Reference/` 에 보관, 신규 무기 생성 시 img2img 인풋으로 활용
- **반복 에셋(스킬 이펙트·아이콘)**: Scenario.gg 파인튜닝 후 대량 생성

---

## 모듈 4 — 몬스터 AI ✅ 완료

> **모듈 완료 테스트**
> 1. 스폰된 몬스터가 반경 내 순찰
> 2. 플레이어 진입 시 추적, 이탈 시 복귀
> 3. 공격 범위 진입 시 스킬 발동 → 플레이어 HP 감소
> 4. 사망 시 속성 보정 드롭 → Inventory에 재료 추가
> 5. 몬스터 머리 위 HP 바 색상 변화 확인

| # | 항목 | 상태 |
|---|---|---|
| 4-1 | FSM (Idle / Patrol / Chase / Attack / Die, 코루틴 AiLoop) | ✅ |
| 4-2 | 순찰 (스폰 반경 내 랜덤 위치) | ✅ |
| 4-3 | 추적 (DetectRange 자동계산, 이탈 반경, null 시 태그 재탐색) | ✅ |
| 4-4 | 몬스터 스킬 시스템 (MonsterData.skills[], 최대 3개, 쿨타임 개별 관리) | ✅ |
| 4-5 | 몬스터 데이터 연결 (MonsterBase.Init(MonsterData)) | ✅ |
| 4-6 | 드롭 처리 (DropResolver, 속성 보정, Inventory.Add) | ✅ |
| 4-7 | 적 HP 바 (World Space Canvas, 색상 초록→노랑→빨강) | ✅ |
| 4-8 | 이동 컴포넌트 패턴 (MonsterMovementBase + SlimeMovement 분리) | ✅ |
| 4-9 | Separation Steering (동료 몬스터 밀집 방지) | ✅ |
| 4-10 | 내비게이션 그리드 — NavGrid (IsWalkable 격자), NavPathfinder (A*), NavAgent (이동 위임), 플레이어 벽 슬라이딩 | ✅ |

---

## 모듈 5 — 던전 ⚠️ 부분 완료 (모듈 3 테스트 통과 후 전면 작업)

> 모듈 3(전투) 테스트 통과 후 전면 작업. 일부 항목은 선행 구현됨.

> **모듈 완료 테스트**
> 1. ManagementScene 포털 진입 → DungeonScene 전환
> 2. Room 1 몬스터 전부 처치 → 문 열림 → Room 2 진입
> 3. Room 2 클리어 → 출구 활성 → 귀환 → ManagementScene 복귀
> 4. (선택) 미니맵에 탐색한 방 표시

| # | 항목 | 상태 |
|---|---|---|
| 5-1 | 타일맵 방 (Rule Tile 기반, 벽 충돌, 카메라 경계) | ⚠️ BoxCollider 벽만, 타일맵 미적용 |
| 5-2 | 다방 구조 & 방 간 이동 (DungeonDoor 텔레포트, 방 클리어 → 문 개방) | ⚠️ Room1→복도→Room2 직렬 동작, 일반화 미완 |
| 5-3 | 몬스터 스폰 포인트 & SpawnTable SO | ✅ |
| 5-4 | 던전 클리어 & 귀환 (OnRoomCleared → DungeonExit → ReturnFromDungeon) | ✅ |
| 5-5 | 던전 미니맵 (탐색 방 표시, 현재 위치 마커) | ❌ |

---

## 모듈 6 — 인벤토리 ⏳ 대기 중 (모듈 5 완료 후 시작)

> **모듈 완료 테스트**
> 1. 던전 재료 드롭 → I키로 인벤토리 UI 열기 → 아이템 표시 확인
> 2. 인벤토리 슬롯 간 아이템 드래그 & 드롭 이동
> 3. 요리 재료 슬롯으로 아이템 드래그 가능

| # | 항목 | 상태 |
|---|---|---|
| 6-1 | 인벤토리 데이터 모델 (Inventory·FoodInventory 싱글톤, OnItemChanged 이벤트) | ✅ |
| 6-2 | 인벤토리 UI (InventoryPanel.uxml/uss, I키 토글, 재료·음식 Grid) | ✅ |
| 6-3 | 아이템 드래그 & 드롭 (슬롯 간 이동, 요리 슬롯으로 드래그) | ❌ |

---

## 모듈 7 — 요리 시스템 ⏳ 대기 중 (모듈 6 완료 후 시작)

> **모듈 완료 테스트**
> 1. 인벤토리에서 재료를 요리 슬롯에 드래그
> 2. 레시피 매칭 → 조리 시작 → 타이밍 미니게임 → 등급 판정
> 3. 완성된 음식이 FoodInventory에 등급과 함께 추가됨
> 4. 완성 연출(파티클 + 이름 팝업) 표시
> 5. 레시피 도감에서 해금된 레시피 확인

| # | 항목 | 상태 |
|---|---|---|
| 7-1 | 요리 재료 슬롯 UI (CookingPanel.uxml/uss, 슬롯 3칸 + 인벤토리 그리드) | ✅ |
| 7-2 | 레시피 매칭 (RecipeMatcher.FindMatchable / FindSlotMatch) | ✅ |
| 7-3 | 조리 실행 (CookingStation, cookDuration 카운트다운, FoodInventory 추가) | ✅ |
| 7-4 | 요리 등급 판정 (타이밍 미니게임 or 재료 상태 → Perfect/Good/Normal/Fail) | ❌ |
| 7-5 | 요리 완성 연출 (파티클 + 음식 이름 팝업) | ⚠️ ParticleSystem 슬롯 존재, VFX 에셋 미제작 |
| 7-6 | 레시피 도감 UI (해금 레시피 목록, 미해금 ??? 표시) | ❌ |
| 7-7 | 요리 마스터리 (동일 레시피 n회 → 숙련도 Lv, 재료 절약·등급 보정) | ❌ |

---

## 모듈 8 — 식당 시스템 ⏳ 대기 중 (모듈 7 완료 후 시작)

> **모듈 완료 테스트**
> 1. FoodInventory 음식 → 메뉴판 UI에 가격 설정 후 등록
> 2. 손님 등장 → 자리 착석 → 주문 → 서빙(E키) → 골드 획득
> 3. 손님 Patience 소진 전 서빙 시 만족도 Up, 소진 후 불만족 처리
> 4. 영업 종료 → 정산 화면(수입·손님 수·만족도) 표시 → 다음 날 전환
> 5. 명성 포인트 누적 → 다음 날 손님 수 증가 확인

| # | 항목 | 상태 |
|---|---|---|
| 8-1 | 식당 씬 기본 구성 (테이블 배치, 손님 스폰 포인트) | ✅ |
| 8-2 | 메뉴 등록 UI (FoodInventory → 메뉴판 슬롯, 가격 설정) | ⚠️ 데이터 존재, 메뉴판 UI 미구현 |
| 8-3 | 손님 스폰 (DayManager.SpawnGuestsRoutine) | ✅ |
| 8-4 | 손님 AI FSM (Entering → Seated → Waiting → Served → Leaving) | ✅ |
| 8-5 | 주문 시스템 (possibleOrders 랜덤 선택) | ✅ |
| 8-6 | 서빙 (ServingSystem.TryServe, E키) | ✅ |
| 8-7 | 결제 & 골드 획득 (EatAndPay → GoldManager.Earn) | ✅ |
| 8-8 | 손님 만족도 (Patience 타이머 → 만족도 점수 계산) | ⚠️ 타이머 존재, 점수 시스템 미구현 |
| 8-9 | 테이블 청소 (퇴장 후 오염 마크, 플레이어 청소 상호작용) | ❌ |
| 8-10 | 영업 정산 화면 (수입 / 손님 수 / 만족도 요약 UI) | ⚠️ 다음 날 전환 동작, UI 미구현 |
| 8-11 | 명성 시스템 (서빙 품질 → Fame 포인트 → 손님 수 증가) | ❌ |

---

## 모듈 9 — 씬 흐름 & HUD ⏳ 대기 중 (모듈 8 완료 후 시작)

> **모듈 완료 테스트**
> 1. ManagementScene 시작 → 전체 사이클 1회전 (던전→주방→식당→다음날) 에러 없이 완료
> 2. 씬 전환마다 로딩 바 + 팁 텍스트 표시
> 3. HUD: 골드 / HP 바(색상) / Day / 스킬 쿨타임 모두 정상 표시
> 4. 게임 종료 후 재시작 시 저장 데이터(골드·인벤토리·Day) 복원

| # | 항목 | 상태 |
|---|---|---|
| 9-1 | 씬 매니저 래퍼 (SceneLoader, DontDestroyOnLoad) | ✅ |
| 9-2 | 던전 → ManagementScene 귀환 전환 | ✅ |
| 9-3 | ManagementScene → KitchenScene 전환 | ✅ |
| 9-4 | KitchenScene → RestaurantScene 전환 | ✅ |
| 9-5 | 영업 마감 → 다음 날 전환 (DayManager.EndDayRoutine) | ✅ |
| 9-6 | 로딩 화면 (로딩 바 + 팁 텍스트, 비동기 진행률 연동) | ❌ |
| 9-7 | HUD — 골드 표시 | ✅ |
| 9-8 | HUD — 플레이어 HP 바 (색상 변화) | ✅ |
| 9-9 | HUD — Day 카운터 | ✅ |
| 9-10 | HUD — 스킬 쿨타임 UI (스킬 1·2·궁극기 아이콘 + 쿨다운 오버레이) | ✅ |
| 9-11 | Save / Load (골드·명성·인벤토리·Day → JSON, 씬 전환 시 자동 저장) | ❌ |

---

## 모듈 10 — 성능 최적화 ⏳ 대기 중 (모듈 9 완료 후 시작)

> **모듈 완료 테스트**
> 1. Profiler에서 히트·사망·요리완성 이펙트 재사용 확인 (GC Alloc 0)
> 2. 식당 손님 10명 동시 활성 시 프레임 드롭 없음

| # | 항목 | 상태 |
|---|---|---|
| 10-1 | 오브젝트 풀 매니저 (ObjectPool\<T\> 제네릭) | ✅ |
| 10-2 | 이펙트 풀 (히트·사망·요리완성 파티클 풀 등록) | ❌ |

---

## 현재 작업 위치

```
모듈 1  기반 인프라     ✅ 완료
모듈 2  조작           ✅ 완료
모듈 3  전투           🔄 진행 중 (애니메이션 구조 작업)  ← 여기
모듈 4  몬스터 AI       ✅ 완료
모듈 5  던전            ⚠️ 부분완료 (모듈 3 테스트 통과 후 전면 작업)
모듈 6  인벤토리        ⏳ 모듈 5 완료 후
모듈 7  요리 시스템     ⏳ 모듈 6 완료 후
모듈 8  식당 시스템     ⏳ 모듈 7 완료 후
모듈 9  씬 흐름 & HUD   ⏳ 모듈 8 완료 후
모듈 10 성능 최적화     ⏳ 모듈 9 완료 후
```
