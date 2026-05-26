# 현재 작업 추적

> 마지막 업데이트: 2026-05-25
> **방침: docs/MODULES.md 모듈 순서대로. 한 모듈 완료 테스트 통과 후 다음 모듈.**

---

## 진행 중: 데이터 시스템 리팩터 — TableData 통합 SO

| # | 작업 | 상태 |
|---|------|------|
| R-1 | SerializedDictionary\<TKey,TValue\> 공용 유틸 | ✅ |
| R-2 | TableData ScriptableObject 신규 생성 | ✅ |
| R-3 | 데이터 클래스 ScriptableObject → [Serializable] 변환 (8종) | ✅ |
| R-4 | DataRegistry 단순화 (TableData 위임) | ✅ |
| R-5 | ScriptableObjectSync 대상을 TableData dict로 변경 | ✅ |
| R-6 | DataManagerWindow 업데이트 | ✅ |

---

## 현재 위치: 모듈 5 — 던전 (구현 완료, 플레이 테스트 대기)

### 모듈 5 구현 내역

| # | 작업 | 상태 |
|---|------|------|
| 5-A | DungeonRoom.cs 신규 — 방 몬스터 추적, OnRoomCleared 이벤트 | ✅ |
| 5-B | DungeonDoor.cs 신규 — 통로 차단(BoxCollider2D), Room 클리어 시 콜라이더 비활성화 + GO 숨김 | ✅ |
| 5-C | DungeonSceneController.cs 재설계 — RoomSetup[] + auto-discover + SpawnChain 순차 스폰 | ✅ |
| 5-D | DungeonScene 구성 — Room1/Room2(DungeonRoom), 벽 4개(BoxCollider2D), 스폰포인트 per-room, DungeonDoor x=8 | ✅ |
| 5-E | DungeonExit 초기 비활성화, _defaultSpawnTable + _exit Inspector 연결 | ✅ |
| 5-F | DungeonPortal — 충돌 즉시 전환 → E키 입력 방식으로 변경 + InteractionPrompt 추가 | ✅ |

### 모듈 5 플레이 테스트 체크리스트

ManagementScene Play → 아래 순서대로 확인:

- [ ] DungeonPortal 앞에서 E키 → "[E] 던전 입장" 프롬프트 표시 → DungeonScene 전환
- [ ] 던전 입장 시 Room1 슬라임만 스폰됨 (Room2는 아직 비어 있음)
- [ ] Room1 슬라임 전부 처치 → `[DungeonRoom] 'Room1' 클리어!` 로그 확인
- [ ] DungeonDoor(x=8) 사라짐 → Room2로 이동 가능
- [ ] Room2 슬라임 스폰됨 → 처치 → `[DungeonRoom] 'Room2' 클리어!` 로그 확인
- [ ] `[DungeonSceneController] 던전 출구 활성화!` 로그 + DungeonExit GO 활성화 확인
- [ ] DungeonExit 트리거 진입 → ManagementScene 복귀

---

## 모듈 3 완료 테스트 체크리스트

DungeonScene에서 아래 항목 순서대로 확인 (모듈 5 테스트와 함께 수행):

- [ ] 플레이어 피격 → 상단 HP 바 감소 + 데미지 팝업
- [ ] 대시(Space) 중 피격 → HP 감소 없음 (무적)
- [ ] 플레이어 HP 0 → 사망 처리
- [ ] 몬스터 HP 0 → 사망 + Console `[PlayerController] 처치 게이지 적립`
- [ ] 슬라임 근처 이동 → 자동 평타 3타 콤보
- [ ] 하단 HUD: WPN 슬롯에 낡은 검 표시
- [ ] Q키 → 스킬 발동 + 하단 스킬1 슬롯 쿨타임 오버레이
- [ ] 피격 또는 처치 → 궁극기 게이지 증가
- [ ] 게이지 MAX → "READY" 표시
- [ ] F키(게이지 MAX) → 궁극기 발동, 게이지 0 리셋

---

## 전체 플레이 사이클 검증 순서

Unity Editor → **ManagementScene (Build Index 0)** Play:

1. ManagementScene: WASD 이동 → DungeonPortal E키 진입
2. DungeonScene: Room1 클리어 → 문 열림 → Room2 클리어 → 출구 활성 → 귀환
3. ManagementScene: EveningStarter E키 → KitchenScene
4. KitchenScene: CookingStation E키 → 요리 → RestaurantEntry E키 → RestaurantScene
5. RestaurantScene: 손님 서빙(E키) → 골드 획득 → 다음 날 전환

---

## 모듈 진행 현황

```
모듈 1  기반 인프라     ✅ 완료
모듈 2  조작           ✅ 완료
모듈 3  전투           ✅ 구현완료 (플레이 테스트 대기)
모듈 4  몬스터 AI       ✅ 완료
모듈 5  던전           🔄 구현완료 (플레이 테스트 대기)  ← 지금
모듈 6  인벤토리        ⏳ 모듈 5 완료 후
모듈 7  요리 시스템     ⏳
모듈 8  식당 시스템     ⏳
모듈 9  씬 흐름 & HUD   ⏳
모듈 10 성능 최적화     ⏳
```

---

## 완료 작업 내역

### 모듈 5 — 던전 (이번 세션)

| 항목 | 파일 |
|------|------|
| DungeonRoom.cs 신규 | Assets/Scripts/Dungeon/ |
| DungeonDoor.cs 신규 | Assets/Scripts/Dungeon/ |
| DungeonSceneController.cs 재설계 (SpawnChain + AutoDiscover) | Assets/Scripts/Dungeon/ |
| DungeonPortal.cs — E키 방식으로 변경, InteractionPrompt 추가 | Assets/Scripts/Management/ |
| DungeonScene — Room1/Room2/DungeonDoor/벽/스폰포인트 구성 | Assets/Scenes/DungeonScene.unity |

### 모듈 3 — 2.5D 원근 효과

| 항목 | 파일 |
|------|------|
| PerspectivePlaneSync (Z = Y·tan(tilt), isStatic) | PerspectivePlaneSync.cs |
| PerspectiveManager — pure static, GameConfig.Current 읽기 | PerspectiveManager.cs |
| PerspectiveMapTilt — 배경 GO에 배치, X 회전 + GameConfig.Current 등록 | PerspectiveMapTilt.cs |
| GameConfig SO (tiltAngleDeg, 맵 공통 static 고정값) | GameConfig.cs |
| 전 씬 Main Camera — Perspective FOV=55, TransparencySort=Distance | Scenes |

### 모듈 3 — 애니메이션 구조 (WeaponSocket)

| 항목 | 파일 |
|------|------|
| WeaponSocketController — 360도 방향 회전, 무기 교체, 애니 트리거 | WeaponSocketController.cs |
| WeaponData — weaponSprite · weaponAnimController 추가 | WeaponData.cs |
| Player.prefab — WeaponSocket 자식 GO 추가 | Player.prefab |

### 모듈 3 — 전투 시스템

| 항목 | 파일 |
|------|------|
| AbilType / AbilEntry / SkillData / SkillGroupData / WeaponData SO 설계 | Data/ |
| PlayerController 리팩터 (SkillData 기반) | PlayerController.cs |
| PlayerStats — 무기/스킬 슬롯, 궁극기 게이지 | PlayerStats.cs |
| 스킬 슬롯 HUD (무기·스킬1·2·궁극기) | HUD.uxml, GameHUD.cs |

### 모듈 4 — 몬스터 AI 고도화

| 항목 | 파일 |
|------|------|
| MonsterMovementBase + SlimeMovement 분리 | Enemy/ |
| MonsterAI 스킬 시스템 (skills[], 쿨타임 개별 관리) | MonsterAI.cs |
| Projectile 통합 (Player + Monster → 공용) | Combat/Projectile.cs |

---

## 참고: 애니메이션 파츠 분리 구조 (향후 고도화 시 참고)

> 현재 구현: **WeaponSocket 분리** (무기 스프라이트만 분리, 상하체 Layer 없음)
> 향후 고려: **Animator Layer + Avatar Mask** 방식으로 확장 가능

### 핵심 구조
```
Character Root
├ LowerBody  (이동 애니메이션 담당)
├ UpperBody  (공격 애니메이션 담당)
│   └ WeaponSocket  (무기 스프라이트/애니 런타임 교체)
└ Head
```

### Unity 구현 방식 비교

| 방식 | 특징 | 적합한 경우 |
|------|------|------------|
| Animator Layer + Avatar Mask | 정석, 상하체 독립 재생 | 스켈레탈 애니 완성 후 |
| Bone 직접 회전 | 360도 조준, 실시간 회전 | 탑다운 조준 필요 시 |
| WeaponSocket 분리 (현재) | 무기만 교체, 구현 간단 | MVP 단계 |

### 장점 요약

| 항목 | 효과 |
|------|------|
| 무기 추가 | WeaponData SO + 스프라이트만 추가하면 됨 |
| 공격 모션 재활용 | 다른 캐릭터/무기/스킨에 동일 모션 적용 |
| 제작량 감소 | 방향별 프레임 애니 대신 Bone 회전 조합으로 해결 |
| 런타임 확장 | 스킬 다양화, 무기 교체, 상하체 독립 가능 |
