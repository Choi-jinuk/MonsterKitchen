# 설계 결정 기록 — 몬스터 키친

> 이 문서는 개발 과정에서 확정된 설계 결정들을 기록한다.
> 한 번 확정된 사항은 재토론 없이 이 문서를 참고해 그대로 적용한다.
> 보류 중인 사항은 별도 표기하고, 확정되면 업데이트한다.

---

## 작업 진행 방식

### 모듈 완료 원칙 (2026-05-09 확정)
- 모듈 하나를 **완전히** 끝낸 뒤 전체 테스트를 통과해야 다음 모듈로 넘어간다.
- 우선순위 기준으로 모듈 간 이동 금지. 반드시 `docs/MODULES.md` 순서대로.
- 각 모듈에는 "모듈 완료 테스트" 항목이 명시되어 있으며, 전부 통과해야 ✅.
- 모듈 완료 테스트용 UI는 해당 모듈 안에서 작업한다. 디테일은 최종 모듈 합산 시 재작업.

### 기획 → 구현 순서 (2026-05-09 확정)
- 새 기능은 기획(구조 확정) 먼저, 코드 작업은 그 다음.
- 대화에서 구조가 확정되면 이 문서에 기록 후 구현에 들어간다.

---

## 전투 / 공격 시스템

### AttackPattern enum 폐기 (2026-05-09 확정)
- **결정**: `AttackPattern` enum을 사용하지 않는다.
- **이유**: 공격 방식은 enum 분기가 아닌 `SkillData` 수치 자체로 표현된다.
  - `missileSpeed == 0` → 즉발(근거리), `missileSpeed > 0` → 투사체 발사
  - `maxTargets == 1` → 단일, `maxTargets > 1` → 범위
  - `attackRange` → 히트박스 또는 폭발 반지름
- **현재 코드 상태**: `GameEnums.cs`에 `AttackPattern` enum이 남아 있음 → 구현 단계에서 제거.

---

## 데이터 구조

### SkillData SO (2026-05-09 확정)

단일 타격(1타, 2타, 3타 등)의 모든 수치를 담는 ScriptableObject.

| 필드 | 타입 | 설명 |
|---|---|---|
| skillId | string | 고유 ID (예: SKL_001) |
| skillName | string | 내부 표시 이름 |
| cooltime | float | 이 타격의 쿨타임 (초) |
| comboWindow | float | 다음 타격 입력 허용 시간 (마지막 타격에서는 무시) |
| damageMultiplier | float | 플레이어 최종 공격력에 곱하는 배율 |
| searchRange | float | 타겟 자동 감지 범위 |
| attackRange | float | 공격 적용 범위 (근거리: OverlapCircle 반지름 / 투사체 폭발: 폭발 반지름) |
| maxTargets | int | 최대 피격 대상 수 (1 = 단일 타겟) |
| missileSpeed | float | 0이면 즉발, 0 초과이면 투사체를 해당 속도로 발사 |
| missileMaxRange | float | 투사체 최대 사거리 (missileSpeed > 0일 때 유효) |
| animTriggerOverride | string | Animator 트리거명 (비워두면 기본 'Attack' 사용) |

**예시 — 무기별 SkillData 값**

| 무기 | searchRange | attackRange | maxTargets | missileSpeed |
|---|---|---|---|---|
| 검 1타 (스윙) | 2.5 | 1.0 | 3 | 0 |
| 단검 1타 (찌르기) | 2.5 | 0.8 | 1 | 0 |
| 활 1타 | 7.0 | 0.2 | 1 | 15 |
| 지팡이 1타 (AoE) | 6.0 | 2.0 | 10 | 8 |

---

### SkillGroupData SO (2026-05-09 확정)

UI에 표시되는 스킬 단위. 하나의 스킬 그룹이 콤보 체인(1타→2타→3타)을 소유한다.

| 필드 | 타입 | 설명 |
|---|---|---|
| skillGroupId | string | 고유 ID (예: SGD_001) |
| skillName | string | UI에 표시되는 스킬 이름 |
| description | string | 스킬 설명 텍스트 |
| skillIcon | Sprite | UI 슬롯 아이콘 |
| skillChain | List\<SkillData\> | 콤보 체인 (순서대로 1타→2타→3타) |
| allowedWeaponTypes | List\<WeaponType\> | 장착 가능한 무기 타입. **빈 리스트 = 모든 무기 공용** |

---

### AbilType enum + AbilEntry struct (2026-05-09 확정)

장비가 플레이어 스탯에 더하는 수치를 표현하는 구조.

**AbilType enum** (확장 가능)

| 값 | 설명 |
|---|---|
| Attack | 공격력 |
| Defense | 방어력 |
| MaxHp | 최대 체력 |
| Speed | 이동 속도 |
| AttackSpeed | 공격 속도 (쿨타임 감소) |
| CritRate | 치명타 확률 |
| CritDamage | 치명타 데미지 배율 |

**AbilEntry struct**
```
{ AbilType abilType; float value; }
```

- 장비 장착 시 플레이어 기본 스탯에 `AbilEntry.value`가 더해진다.
- 무기뿐 아니라 방어구, 악세서리 등 모든 장비가 이 구조를 사용할 수 있다.
- 예: 검 `abils = [(Attack, +15), (Defense, +3)]` → 장착하면 공격력 +15, 방어력 +3

---

### WeaponData SO (2026-05-09 확정)

무기 정의. 무기는 스탯(abils)과 평타 체인(normalAttackGroup)을 소유한다.
액티브 스킬은 무기가 소유하지 않으며, 플레이어가 별도로 장착한다.

| 필드 | 타입 | 설명 |
|---|---|---|
| weaponId | string | 고유 ID (예: WPN_001) |
| weaponName | string | 표시 이름 |
| weaponType | WeaponType | 무기 타입 (Sword·Axe·DualSword·Bow 등) |
| abils | List\<AbilEntry\> | 이 무기 장착 시 플레이어에 더해지는 스탯 |
| normalAttackGroup | SkillGroupData | 무기 고유 평타 체인. 무기 장착 시 자동 적용. |

> **주의**: 구 WeaponData(현재 코드)는 잘못된 구조. 구현 단계에서 이 명세대로 재설계.

---

## 플레이어 시스템

### 스킬 장착 시스템 (2026-05-09 확정)

플레이어가 보유한 장착 슬롯:

| 슬롯 | 내용 | 결정 방식 |
|---|---|---|
| 무기 슬롯 | WeaponData | 플레이어가 장착 → normalAttackGroup 자동 적용, abils 스탯 합산 |
| 스킬 슬롯 1 | SkillGroupData | 플레이어가 직접 장착. allowedWeaponTypes 호환 체크 필수 |
| 스킬 슬롯 2 | SkillGroupData | 플레이어가 직접 장착. allowedWeaponTypes 호환 체크 필수 |
| 궁극기 슬롯 | SkillGroupData | 플레이어가 직접 장착. allowedWeaponTypes 호환 체크 필수 |

**스킬 호환 체크 규칙**
- `SkillGroupData.allowedWeaponTypes`가 비어있으면 → 모든 무기에서 장착 가능 (공용 스킬)
- 값이 있으면 → 현재 장착 무기의 `WeaponType`이 리스트 안에 있어야만 장착 가능

**평타 슬롯**
- 별도 슬롯 없음. 무기 장착 시 `WeaponData.normalAttackGroup`이 자동으로 평타로 사용됨.
- 플레이어는 평타를 직접 선택할 수 없다.

---

### 스탯 계산 방식 — ⚠️ 확정 보류

| 항목 | 현재 방향 | 확정 시점 |
|---|---|---|
| 최종 스탯 계산식 | 플레이어 기본 스탯 + 장착 장비 전체 AbilEntry 합산 | 최종 모듈 합산 단계 |
| SkillData 데미지 계산 | 플레이어 최종 공격력 × SkillData.damageMultiplier | 최종 모듈 합산 단계 |
| 치명타 계산 포함 여부 | 미정 | 최종 모듈 합산 단계 |

---

## WeaponType enum (2026-05-09 확정, GameEnums.cs)

| 값 | 무기 | 기본 공격 방식 예시 |
|---|---|---|
| Sword | 검 | 근거리 범위 (searchRange=2.5, maxTargets=3) |
| Axe | 도끼 | 근거리 범위 (느리고 강함, maxTargets=5) |
| DualSword | 쌍검 | 근거리 단일 빠름 (maxTargets=1, cooltime 짧음) |
| Spear | 창 | 근거리 범위 (전방 직선, maxTargets=2) |
| Dagger | 단검 | 근거리 단일 매우 빠름 (maxTargets=1) |
| Bow | 활 | 원거리 단일 투사체 (missileSpeed=15) |
| Gun | 총 | 원거리 단일 빠른 투사체 (missileSpeed=25) |
| Staff | 지팡이 | 원거리 범위 투사체 (missileSpeed=8, attackRange=2.0) |

---

## 폐기된 설계

| 항목 | 이유 | 대체 방향 |
|---|---|---|
| `AttackPattern` enum | 데이터 수치로 충분히 표현 가능. enum 분기는 불필요한 복잡도 | SkillData.missileSpeed·maxTargets·attackRange 조합 |
| `WeaponData.normalAttackChain` (List) | 무기가 평타 체인을 직접 소유하는 방식 | `WeaponData.normalAttackGroup` (SkillGroupData 참조) |
| `PlayerSpawnData.normalAttackChain` | 무기 시스템 도입으로 불필요 | `WeaponData.normalAttackGroup`으로 이동 |
| `AttackSkillData` SO 클래스 | 구 구조. 새 SkillData로 대체 | `SkillData` SO로 전면 교체 |
