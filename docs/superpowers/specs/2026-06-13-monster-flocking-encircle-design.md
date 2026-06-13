# 몬스터 군집 이동 & 둘러싸기 — 설계 스펙

> 작성: 2026-06-13
> 상태: 사용자 승인 대기
> 참고 영상: Warcraft3 "Local Avoidance" (Lukas Chodosevičius) — 유닛이 hex 형태로 패킹되어 목표 주위를 깔끔한 원형으로 감싼다.

---

## 1. 목표

몬스터 추격 AI를 리워크한다. 두 가지 요구:

1. **겹침 방지를 콜라이더가 아닌 군집(flocking/local-avoidance)으로 해결.** 몬스터끼리는 콜라이더 충돌이 아닌 separation 스티어링으로 밀어낸다. 콜라이더는 벽 충돌 전용으로만 남긴다.
2. **둘러싸기(encircle).** 플레이어에 도착했을 때 한 점에 멈추지 않고, 빈 공간을 찾아 플레이어 주위를 돌며 원형으로 감싼다. 슬롯 예약 없는 순수 스티어링으로 창발적으로 형성한다 (영상과 동일 방식).

**규모:** 동시 추격 30~100마리.

**비목표 (YAGNI):** Unity DOTS/ECS, 슬롯 예약, 동시 공격 수 제한, boids alignment/cohesion 항, 슬라임 외 기존 몬스터 대량 추가.

---

## 2. 현재 상태

- BT 전용 (FSM `MonsterAI.cs` 는 제거됨. CLAUDE.md 의 FSM 언급은 stale).
- `BTMonsterController.ApplySeparation()` — 매 호출 `Physics2D.OverlapCircle` + `ContactFilter2D` 로 이웃 검색 후 **perpendicular slide** 1항만 적용. 30~100 규모에서 물리 쿼리·GC 비용 누적.
- `BTAction_MoveToPlayer` — `dist <= PreferredDistance` 에서 `Success` 로 **정지**. 여러 마리가 같은 링 반경에 도달하면 접근한 쪽에 뭉침. 둘러싸기 없음.
- `BTAction_Attack` — 사거리 내에서 속도 0, 너무 붙으면(`dist < pref*0.5`) `ApplySeparation(-toPlayer)` 로 밀어냄.
- 이동 모델은 `SlimeMovement`(윈드업→대시 투스테프) 하나뿐.
- `NavAgent` — A* 경로의 다음 경유지 방향만 반환(`GetDirection()`). 로컬 회피·군집은 없음.

---

## 3. 아키텍처

```
MonsterFlockManager (싱글톤, 신규)
  · 매 FixedUpdate: 등록된 모든 IMonsterFlockAgent 를 공간 해시 그리드에 재배치 (O(n))
  · QueryNeighbors(pos, radius, buffer) → 인접 9셀만 순회, 할당 없는 버퍼 채움 (O(k))
  · 셀 크기 = separationRadius (보통 self 셀 + 8 인접만 검사)
  · Instance == null 또는 미등록 → 질의는 0 이웃 반환 (크래시 없음)

FlockSteering (순수 static 클래스, 신규, Unity 의존 최소)
  · ComputeChase(selfPos, navDir, neighbors, in Weights) → seek + separation
  · ComputeEncircle(selfPos, playerPos, ringRadius, neighbors, in Weights)
        → arrival(ring) + tangential + separation
  · 입력은 위치/속도 배열 + 가중치 struct. NavGrid 미사용 → EditMode 테스트 가능.

IMonsterFlockAgent (인터페이스, 신규)
  · Vector2 Position { get; }
  · float   Radius   { get; }
  · GameObject (또는 식별자) — self 제외용

BTMonsterController → IMonsterFlockAgent 구현
  · OnEnable 시 MonsterFlockManager 등록, OnDisable 해제
  · 기존 ApplySeparation(OverlapCircle) 제거 → 매니저 질의 + FlockSteering 호출로 대체
  · IMonsterSeparation 인터페이스는 ApplySeparation 시그니처 유지하되 내부 구현 교체
    (SlimeMovement 가 m_Sep.ApplySeparation 을 그대로 호출 가능)

MonsterMovementBase
  · ContinuousMovement (신규) — TickChase 에서 Rb.linearVelocity = steeringResult,
    StepMove 로 벽 슬라이딩. 영상의 footman/orc 같은 연속 이동.
  · SlimeMovement (기존) — 윈드업 시점 m_DashDir 를 nav 방향 대신 스티어링 결과로 잠금.
    dash 사이클·벽 반사 로직 유지 ("조향된 dash").
```

**경계 원칙:**
- `FlockSteering` = 순수 함수. 테스트 가능, 부수효과 없음. 입력→desired velocity 출력.
- `MonsterFlockManager` = 공간 인덱싱만. 스티어링 계산 안 함.
- 이동 컴포넌트 = 스티어링 결과 소비만.
- 콜라이더 = 벽 충돌 전용. 몬스터 간 콜라이더 충돌은 OFF (Layer 매트릭스 또는 trigger).

---

## 4. 스티어링 동작 (FlockSteering)

세 힘을 가중 합산해 desired velocity 산출. 각 항 정규화 후 가중치 곱, 최종 `clampMagnitude(moveSpeed)`.

### 4.1 Separation (겹침 방지 — 핵심)

```
force = 0
for each neighbor within sepRadius:
    away  = self.pos - neighbor.pos
    dist  = away.magnitude
    force += away.normalized / max(dist, ε)²     // 역제곱: 가까울수록 급격히 강함
separation = force.normalized * sepWeight
```

역제곱 가중이 영상의 hex 패킹을 만든다.

### 4.2 Seek / Arrival (목표 접근)

- 추격 중 (`dist > ringRadius`): `seek = navDir * seekWeight` — A* 경로 방향(`NavAgent.GetDirection()`).
- 링 도달 (`dist ≈ ringRadius`): arrival — 반경에서 속도 감쇠.

```
toRing  = dist - ringRadius
arrival = toPlayerDir * clamp(toRing / slowRadius, -1, 1) * arrivalWeight
```

`toRing` 음수(너무 붙음) → 바깥으로 밀려 링 반경 유지.

### 4.3 Tangential (둘러싸기 — 빈 공간 채우기)

```
tangent     = perpendicular(toPlayerDir)                 // 플레이어 기준 접선
side        = sign(dot(separationForce, tangent))        // 이웃 밀집 반대쪽
ringProx    = 1 - clamp(abs(toRing) / ringBand, 0, 1)    // 링 근처일수록 1
tangential  = tangent * side * tangentWeight * ringProx
```

링에서 멀면 `ringProx≈0` → 접선 0(직진 추격). 링 근처에서 접선 활성 → 플레이어 주위를 돌며 빈 각도로 미끄러져 들어감. **이것이 "한 점에 안 멈추고 둘러싸는" 동작.**

### 4.4 합산 & 창발

```
desired = clampMagnitude(separation + (seek 또는 arrival) + tangential, moveSpeed)
```

슬롯 예약 없음. separation 이 같은 각도 경쟁을 자동 해소 → 안쪽 링이 꽉 차면 바깥 동심원 링 형성. 30~100마리 자연 처리.

### 4.5 기본 가중치 (직렬화, 튜닝 대상)

| 항 | 기본값 |
|---|---|
| `sepWeight` | 2.0 |
| `seekWeight` | 1.0 |
| `arrivalWeight` | 1.5 |
| `tangentWeight` | 1.2 |
| `sepRadius` | 0.9 |
| `slowRadius` | 1.0 |
| `ringBand` | 1.5 |

---

## 5. BT 통합 (트리 구조 유지, 의미만 변경)

- `BTAction_MoveToPlayer`: `dist > ringRadius` 동안 `FlockSteering.ComputeChase`(seek+separation) → `TickChase` 위임. `dist <= ringRadius` → `Success`.
- `BTAction_Attack`: 사거리 내에서 속도 0 / 푸시백 대신 `FlockSteering.ComputeEncircle`(arrival+tangential+separation) 적용 → 공격하면서 링 주위를 돌며 빈칸 채움. 기존 `ApplySeparation(-toPlayer)` 푸시백 제거.
- `ringRadius` = blackboard `PreferredDistance`(= 최소 AttackRange) 재사용. **신규 BB 키 불필요.**

---

## 6. 검증용 신규 몬스터 — 기본 이동 오크

슬라임 dash 가 아닌 **연속 이동(ContinuousMovement)** 으로 동작하는 영상의 footman/orc 형 몬스터를 신규로 하나 추가해 군집·둘러싸기를 시각 검증한다.

- `ContinuousMovement` 컴포넌트 사용.
- TableData 에 신규 MON 엔트리 (예: `MON_006` 오크) + CSV 행 + 프리팹 + BTAsset(Selector: Attack / MoveToPlayer / Patrol).
- 스프라이트/애님은 기존 자산 재사용 가능(플레이스홀더 허용) — 목적은 이동 검증.
- PlayMode 에서 다수 스폰해 둘러싸기 형성 관찰.

---

## 7. 성능 (30~100마리)

- `MonsterFlockManager` 그리드 1회/FixedUpdate 재빌드 — O(n).
- 질의는 인접 9셀 — O(k), k = 평균 근방 이웃 수.
- 기존 per-monster `Physics2D.OverlapCircle` + `ContactFilter2D` **완전 제거** → 물리 쿼리·GC 0.
- 이웃 버퍼는 사전 할당 풀(`IMonsterFlockAgent[]`) 재사용 — 런타임 할당 없음.

---

## 8. 에러 처리 (CLAUDE.md 규칙)

- `MonsterFlockManager.Instance == null` → separation 없이 seek 만 적용(겹침 가능, 크래시 없음) + `DebugUtil.LogError` 1회.
- `NavGrid` 없으면 기존대로 이동 불가 로그 (현 동작 유지).
- 잘못된 가중치(음수 등)는 `OnValidate` 에서 clamp + 경고.

---

## 9. 테스트

### EditMode (FlockSteering 순수 함수)
- **separation:** 두 에이전트 겹침 → 서로 반대 방향 성분.
- **arrival:** 링 안쪽 에이전트 → 바깥(플레이어 반대) 방향 성분; 링 밖 → 안쪽 성분.
- **tangential:** 링 위 + 한쪽 이웃 밀집 → 빈쪽 접선 부호.
- **clamp:** 합산 크기 ≤ moveSpeed.
- **빈 이웃:** neighbors 0 → seek/arrival 만, 크래시 없음.

### PlayMode (선택)
- 오크 50마리 스폰 → N초 후 평균 최근접 이웃거리 ≥ `sepRadius * 0.8` (겹침 해소 검증).
- 정지 플레이어 주위 링 형성 — 평균 각도 분포 균등성 체크.

---

## 10. 영향 범위 (파일)

| 파일 | 변경 |
|---|---|
| `Enemy/MonsterFlockManager.cs` | 신규 — 공간 해시 매니저 |
| `Enemy/Movement/FlockSteering.cs` | 신규 — 순수 스티어링 함수 |
| `Enemy/Movement/IMonsterFlockAgent.cs` | 신규 — 에이전트 인터페이스 |
| `Enemy/Movement/ContinuousMovement.cs` | 신규 — 연속 이동 모델 |
| `Enemy/BTMonsterController.cs` | ApplySeparation 내부 교체, 등록/해제 |
| `AI/BehaviorTree/Monster/BTAction_MoveToPlayer.cs` | ComputeChase 적용 |
| `AI/BehaviorTree/Monster/BTAction_Attack.cs` | ComputeEncircle 적용, 푸시백 제거 |
| `Enemy/Movement/SlimeMovement.cs` | dash 방향 스티어링 보정 |
| TableData CSV + SO | 검증용 오크 MON 엔트리 |
| `Assets/Tests/` EditMode | FlockSteering 테스트 |
| 오크 프리팹 + BTAsset | 검증용 |
