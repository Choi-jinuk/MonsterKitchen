# 현재 작업 추적

> 마지막 업데이트: 2026-05-10
> **방침: docs/MODULES.md 모듈 순서대로. 한 모듈 완료 테스트 통과 후 다음 모듈.**

---

## 현재 위치: 모듈 3 — 전투 (구현 완료, 테스트 대기)

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

## 다음: 모듈 5 — 던전 (모듈 3 테스트 통과 후)

모듈 5 주요 작업:
- 타일맵 방 구조 (Rule Tile, 벽 충돌)
- 방 간 이동 일반화 (DungeonDoor 컴포넌트)
- 던전 미니맵 (선택)

---

## 대기 순서

```
모듈 3  전투           ✅ 구현완료 (테스트 대기)  ← 지금
모듈 5  던전           ⏳ (모듈 3 테스트 통과 후)
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
