# 현재 작업 추적

> 마지막 업데이트: 2026-05-23
> **방침: docs/MODULES.md 모듈 순서대로. 한 모듈 완료 테스트 통과 후 다음 모듈.**

---

## 현재 위치: 모듈 3 — 전투 (진행 중 — 모듈 완료 테스트 대기)

### 세션 1 완료 목록 (모듈 3 전투 기반)

| 항목 | 파일 |
|------|------|
| AbilType enum + AbilEntry struct | GameEnums.cs, AbilEntry.cs |
| SkillData SO 신규 설계 | SkillData.cs |
| SkillGroupData SO 신규 설계 | SkillGroupData.cs |
| WeaponData SO 재설계 (abils + normalAttackGroup) | WeaponData.cs |
| AttackPattern enum 제거, AttackSkillData Obsolete 처리 | GameEnums.cs, AttackSkillData.cs |
| PlayerController 전면 리팩터 (SkillData 기반) | PlayerController.cs |
| PlayerStats 컴포넌트 신규 구현 | PlayerStats.cs |
| InputManager 스킬 입력 추가 (Q/R/F) | InputManager.cs |
| SKL_001~004 SkillData SO 에셋 생성 | Assets/Data/SO/ |
| SGD_001~003 SkillGroupData SO 에셋 생성 | Assets/Data/SO/ |
| WPN_001 WeaponData SO 에셋 생성 (낡은 검) | Assets/Data/SO/ |
| PlayerSpawnData.asset — defaultWeapon=WPN_001 연결 | Assets/Data/ |
| Player.prefab — PlayerStats 추가, enemyLayer 설정 | Assets/Prefabs/Player/ |
| HUD.uxml — 하단 스킬 바(무기·스킬1·2·궁극기) 추가 | Assets/UI/ |
| HUD.uss — 스킬 바 스타일 추가 | Assets/UI/ |
| GameHUD.cs — PlayerStats 이벤트 연동 | Assets/Scripts/UI/ |
| **[버그수정]** Physics2D 충돌 매트릭스 — Player↔Enemy 충돌 비활성화 | ProjectSettings/Physics2D |
| **[버그수정]** PlayerAnimator.controller — ComboStep(Int) 파라미터 추가 | Assets/Animations/ |
| **[이동개선]** 이동 관성(MoveTowards + moveAcceleration) + 공격 중 이동 패널티 | PlayerController.cs |
| **[이동개선]** 몬스터 선호 거리 + 공격 보간 처리(preferredDistance < attackRange) | MonsterAI.cs |
| **[이동개선]** Rigidbody2D Interpolate 설정 (Player/Slime 프리팹) | Prefabs |
| **[이동개선]** CameraFollow smoothTime 0.25f (부드러운 추적) | CameraFollow.cs |

### 세션 4 완료 목록 (모듈 3 2.5D 원근 효과 — Perspective 카메라 방식)

| 항목 | 파일 |
|------|------|
| PerspectivePlaneSync 컴포넌트 신규 (Z = Y·sin(tilt), isStatic 플래그) | PerspectivePlaneSync.cs |
| PerspectiveManager 재설계 — PerspectivePlaneSync(isStatic=true) 자동 부착 | PerspectiveManager.cs |
| Player · Slime · Customer 프리팹 — PerspectiveDepth 제거, PerspectivePlaneSync 추가 | Prefabs |
| 전 씬(4개) Main Camera — Orthographic→Perspective, FOV=55, TransparencySort=Distance | Scenes |
| DungeonScene CM_Dungeon CinemachineCamera — ModeOverride=Perspective, FOV=55 | DungeonScene |
| **[제거]** PerspectiveDepth 방식 (스케일 보간) → PerspectivePlaneSync(Z 동기화)로 교체 | — |

### 세션 3 완료 목록 (모듈 3 애니메이션 구조 — WeaponSocket)

| 항목 | 파일 |
|------|------|
| WeaponSocketController 신규 컴포넌트 (360도 방향 회전, 무기 교체, 애니 트리거) | WeaponSocketController.cs |
| WeaponData — weaponSprite · weaponAnimController 필드 추가 | WeaponData.cs |
| PlayerController — _weaponSocket 필드 · 이벤트 구독 · Init 동기화 | PlayerController.cs |
| PlayerController — _facingDir.y < 0.5f 핵 2곳 제거 (전 방향 overlay 발동) | PlayerController.cs |
| PlayerController — 공격 트리거 시 TriggerWeaponAnim 호출 | PlayerController.cs |
| PlayerController — flipX 루프에서 WeaponSocket SR 제외 | PlayerController.cs |
| Player.prefab — WeaponSocket 자식 GO 추가 (position 0.3, 0, 0) | Player.prefab |
| Player.prefab — PlayerController._weaponSocket 참조 연결 | Player.prefab |
| Player.prefab — WeaponSocketController._sr · _anim 참조 연결 | Player.prefab |
| Player.prefab — WeaponSocket SpriteRenderer sortingOrder = 1 | Player.prefab |
| docs/MODULES.md — 애니메이션 구조 섹션 무기 분리 방식으로 재정의 | docs/MODULES.md |

### 세션 2 완료 목록 (모듈 4 AI 고도화 + 정리)

| 항목 | 파일 |
|------|------|
| MonsterMovementBase 추상 클래스 신규 (컴포넌트 패턴) | MonsterMovementBase.cs |
| SlimeMovement 분리 (대시 사이클 전용, 첫 대시 스태거) | SlimeMovement.cs |
| MonsterAI 리팩터 — 이동 위임 (_movement 슬롯) | MonsterAI.cs |
| Slime.prefab — SlimeMovement 추가 + _movement 연결 | Slime.prefab |
| MonsterAI 스킬 시스템 — MonsterData.skills[] (최대 3개) | MonsterData.cs, MonsterAI.cs |
| MonsterAI DetectRange/PreferredDistance 자동 계산 | MonsterAI.cs |
| SKL_101 생성 (슬라임 박치기, searchRange 1.4, cooltime 1.2) | Assets/Data/SO/SKL_101.asset |
| MON_001 — SKL_101 연결 | Assets/Data/SO/Monsters/MON_001.asset |
| Projectile 통합 (PlayerProjectile + MonsterProjectile → Projectile) | Assets/Scripts/Combat/Projectile.cs |
| PlayerProjectile.cs 삭제, MonsterProjectile.cs 삭제 | — |
| **[정리]** AttackSkillData.cs 삭제 (Obsolete) | — |
| **[정리]** ATK_001/002/003.asset 삭제 | — |
| **[정리]** TestCombatSetup.cs 삭제 (테스트 전용 임시 코드) | — |

---

## 모듈 3 완료 테스트 체크리스트

DungeonScene에서 ManagementScene Play 후 진입해서 아래 항목 순서대로 확인:

- [ ] 플레이어 피격 → 상단 HP 바 감소 + 데미지 팝업
- [ ] 대시(Space) 중 피격 → HP 감소 없음 (무적)
- [ ] 플레이어 HP 0 → 사망 처리 (사망 로그 확인)
- [ ] 몬스터 HP 0 → 사망 + Console `[PlayerController] 처치 게이지 적립` 확인
- [ ] 슬라임 근처 이동 → 자동 평타 3타 콤보 (Console 1·2·3타 로그)
- [ ] 하단 HUD: WPN 슬롯에 낡은 검 표시 확인
- [ ] Q키 → 스킬 발동 + **하단 스킬1 슬롯 3초 쿨타임 오버레이 표시** (SGD_002 장착됨)
- [ ] 피격 또는 처치 → 하단 궁극기 게이지 바 증가
- [ ] 게이지 MAX → 게이지 슬롯 "READY" 표시
- [ ] F키(게이지 MAX 상태) → Console `[PlayerController] 궁극기 발동`, 게이지 0 리셋

---

## 다음: 모듈 3 테스트 → 모듈 5 전면 작업

### 모듈 3 테스트 통과 조건 (위 체크리스트 참고)

### 모듈 5 남은 작업:
- 타일맵 방 구조 (Rule Tile, 벽 충돌) — 5-1 ⚠️
- 방 간 이동 일반화 (DungeonDoor 컴포넌트) — 5-2 ⚠️
- 던전 미니맵 — 5-5 ❌ (선택)

---

## 대기 순서

```
모듈 3  전투           ✅ 구현완료 (테스트 대기)  ← 지금
모듈 5  던전           ⚠️ 부분완료 (모듈 3 테스트 통과 후 전면 작업)
모듈 6  인벤토리       ⏳
모듈 7  요리 시스템    ⏳
모듈 8  식당 시스템    ⏳
모듈 9  씬 흐름 & HUD  ⏳
모듈 10 성능 최적화    ⏳
```

---

## 플레이 사이클 전체 검증 순서

Unity Editor → **ManagementScene (Build Index 0)** Play:

1. ManagementScene: WASD → DungeonPortal E키 진입
2. DungeonScene: 슬라임 처치(모듈 3 전투 테스트 여기서) → 출구 진입 → ManagementScene 복귀
3. ManagementScene: EveningStarter E키 → KitchenScene
4. KitchenScene: CookingStation E키 → 요리 → RestaurantEntry E키 → RestaurantScene
5. RestaurantScene: 손님 서빙(E키) → 골드 획득 → 다음 날 전환


## 이세카이 게이트 게임 전투 분석
“상체/팔/무기 애니메이션 분리 구조”

야.

이 방식 때문에:

무기 교체
공격 타입 변경
스킬 다양화
상하체 독립 움직임

이 전부 가능해지는 거고, 최근 2D 액션/로그라이크/핵앤슬래시 게임에서 굉장히 많이 사용하는 구조임.

핵심 구조
Upper Body / Lower Body 분리 애니메이션

보통 구조는 이렇게 감.

Character Root
├ LowerBody
│   ├ Leg_L
│   └ Leg_R
│
├ UpperBody
│   ├ Body
│   ├ Arm_L
│   ├ Arm_R
│   └ WeaponSocket
│
└ Head

즉:

하체 = 이동 담당
상체 = 공격 담당

으로 분리됨.

왜 이렇게 만드는가?
1. 이동하면서 공격 가능

하체는:

Run Animation

계속 재생하고,

상체만:

Slash
Shoot
Cast
Stab

등을 재생 가능.

즉:

달리면서 공격

이 자연스럽게 가능함.

2. 무기 교체 대응이 쉬움

영상에서 보이는 것처럼:

검
창
총
도끼

등이 바뀌어도 하체는 그대로 사용 가능.

상체 애니메이션만 교체하면 됨.

3. 공격 모션 재활용 가능

예시:

1H_Slash
2H_Slash
Gun_Shoot
Bow_Shoot

같은 모션을:

다른 캐릭터
다른 무기
다른 스킨

에 재활용 가능.

특히 라이브 서비스 게임에서 엄청 중요함.

유니티에서 구현 방식

구조적으로는 크게 3가지.

방식 1.
Animator Layer 분리

가장 정석.

예시
Base Layer
→ 이동/점프

UpperBody Layer
→ 공격

상체 Bone만 마스킹함.

Unity 기능
Avatar Mask

사용.

구조 예시
Base Layer
└ Run

Upper Layer
└ Sword Slash

그러면:

다리는 Run 유지
상체만 Slash

됨.

방식 2.
본(Bone) 직접 회전

영상 느낌상 이쪽 가능성도 높음.

특히 탑다운 액션이면 많이 씀.

예시
armBone.rotation =
Quaternion.LookRotation(...)

또는:

arm.localEulerAngles

로 직접 조작.

장점

엄청 유연함.

예:

마우스 방향 조준
360도 공격
실시간 무기 회전
조준 보간

가능.

방식 3.
Spine 슬롯 교체 방식

Spine 에서 가장 많이 쓰는 방식.

구조
RightHand Slot
→ Sword
→ Axe
→ Gun

런타임 교체.

장점

무기만 바꿔 끼우면:

같은 공격 모션
다른 무기 외형

가능.

엄청 강력함.

영상에서 보이는 특징

영상의 공격은 보면:

몸통 회전
팔 스윙
무기만 독립 회전
하체 이동 유지

가 동시에 발생함.

즉 사실상:

“캐릭터 전체 애니메이션”

이 아니라,

“파츠 조합 애니메이션”

에 가까움.

그래서 가능한 것들
1. 무기별 공격속도 차이

검:

빠른 상체 회전

도끼:

느린 상체 회전
+
강한 후딜

만 조절 가능.

2. 무기별 궤적 변경

무기 본 위치만 바꾸면:

찌르기
베기
회전베기

쉽게 제작 가능.

3. 공격 방향 자유도

특히 탑다운 게임이면:

8방향
360도

공격 가능.

이게 프레임 애니메이션이면 제작량 폭증함.

실제 개발 관점에서 중요한 부분

이 구조는 사실:

“애니메이션 제작량 감소”

가 핵심 목적임.

프레임 애니메이션이면

예:

검 공격
도끼 공격
창 공격
총 공격

전부 새로 그림.

심지어:

상
하
좌
우
대각선

방향까지 추가되면 지옥됨.

본 기반이면
팔 회전
무기 회전
몸 회전

조합으로 해결 가능.

즉:

제작량 감소
+
런타임 확장성 증가

가 엄청 큼.

네가 만들 경우 추천 구조

너가 이전에 고민했던:

AI 생성 캐릭터
2D 액션
Spine/Unity
다양한 무기

생각하면 사실 이 구조가 거의 최적에 가까움.

추천 구조
캐릭터
Head
Body
UpperArm
LowerArm
Hand
Weapon

파츠 분리.

애니메이션
이동 = 하체
공격 = 상체

분리.

무기
WeaponSocket

에 장착.

공격
Animation Event
+
HitBox 생성
방향
Aim Direction 기반 Bone 회전
최종적으로 이 방식의 장점
항목	장점
무기 추가	매우 쉬움
공격 모션 추가	쉬움
캐릭터 추가	쉬움
AI 활용	좋음
런타임 조합	강력
메모리 효율	높음
방향 공격	유리
라이브 운영	매우 유리