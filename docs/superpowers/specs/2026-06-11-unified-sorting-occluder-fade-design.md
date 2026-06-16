# 통일 소팅 + 가림 지형 투명 처리 (Occluder Fade) 설계

> 작성: 2026-06-11
> 상태: 승인됨 (사용자 확인 완료)

---

## 1. 목표

1. **소팅 통일** — 월드 오브젝트의 개별 `sortingOrder` 지정을 제거하고 전부 동일(0)하게.
   앞뒤 가림은 기존 Perspective 시스템(Perspective 카메라 + Distance TransparencySort)이
   Y(깊이) 기준으로 자동 처리 → 플레이어가 오브젝트 뒤(위쪽 Y)로 가면 가려진다.
2. **가림 투명 처리** — 나무·동상 등 지형 prop 뒤에 플레이어가 서면 prop 을
   반투명하게 만들어 플레이어가 보이게 한다.
   - **채집 노드는 덜 투명** (알파 0.55) — 채집 대상이 보여야 하므로.
   - **데코 지형은 더 투명** (알파 0.3) — 가림 해소가 우선.

## 2. 비범위

- 스텐실/실루엣(X-ray) 셰이더 — Post-MVP
- 타일맵(바닥/벽) per-tile 정렬 — 현 placeholder 단계에서 불필요
- 스프라이트 pivot 재조정 — 아트 에셋 확정 시 일괄 처리

## 3. 소팅 통일 규칙

| 계층 | sortingLayer / order | 비고 |
|---|---|---|
| 월드 엔티티·prop (플레이어, 몬스터, 채집 노드, 게이트, 투사체, 향후 데코) | Default / **0** | 앞뒤는 Y(카메라 거리)가 결정 |
| 바닥·벽 타일맵, Background 스프라이트 | 항상 맨 뒤 (현행 유지) | |
| EnemyHpBar(10) · SceneFader(9999) · UIDocument(UILayer) | 항상 앞 (현행 유지) | 의도적 차등 |

**수정 지점:**
- `Projectile.cs` — `sr.sortingOrder = 10` 제거 (0)
- DungeonScene `DungeonGate` — sortingOrder 5 → 0
- 그 외 월드 스프라이트 order ≠ 0 발견 시 0으로 (프리팹 점검 포함)

## 4. SpriteOccluderFade (신규 — `Core/Rendering/SpriteOccluderFade.cs`)

- `[RequireComponent(typeof(SpriteRenderer))]`, prop 에 부착.
- **판정** (LateUpdate, 순수 정적 메서드로 분리해 테스트):
  `ShouldFade(propBounds, propY, playerPos)` =
  플레이어 위치가 prop 스프라이트 bounds 안 (X·Y 평면) **AND** `playerPos.y > propY`
  (높은 Y = 화면 위 = 뒤 — 플레이어가 prop 에 가려진 상태).
- **페이드**: 조건 충족 시 알파를 `m_FadedAlpha` 로, 해제 시 1.0 으로 0.15s 보간.
  틴트 RGB 는 유지, 알파만 변경 (ResourceNode 의 m_NodeTint 와 공존).
- **알파 프리셋** (`m_FadedAlpha` 직렬화, 인스턴스별 설정):
  - 채집 노드: **0.55** (채집 대상 시인성 유지)
  - 데코 지형(나무·동상 등 향후): **0.3**
- 플레이어 참조: `PlayerManager.Instance?.Player` 1회 캐시 (Find 금지 규칙 준수).
  플레이어 없으면 페이드 해제 상태 유지.
- 거리 가드: bounds 검사 전 sqrMagnitude 근사 가드로 prop 다수여도 비용 무시 가능.

## 5. 씬 적용

- DungeonScene 채집 노드 11개 전부에 `SpriteOccluderFade` 부착 (m_FadedAlpha = 0.55).
- DungeonGate 는 미부착 — 파괴 대상이라 가림 시 페이드가 오히려 혼란.

## 6. 테스트

- **EditMode**: `ShouldFade` 순수 로직 — ① bounds 안 + 플레이어 뒤 → true,
  ② bounds 안 + 플레이어 앞(Y 작음) → false, ③ bounds 밖 → false.
- **수동**: 노드 뒤(위쪽)로 이동 → 노드 알파 0.55 + 플레이어 보임, 앞으로 나오면 복원.
  투사체·게이트가 다른 오브젝트와 Y 기준으로 자연스럽게 가려지는지 확인.
