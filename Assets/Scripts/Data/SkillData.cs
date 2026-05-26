using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  SkillData — 단일 타격의 수치 데이터
    //
    //  콤보 체인의 각 타격(1타, 2타, 3타…)을 독립 데이터로 정의한다.
    //  SkillGroupData.skillChain 리스트에 순서대로 등록해 콤보화한다.
    //
    //  ▶ 공격 방식은 enum 분기 없이 수치만으로 결정된다.
    //    missileSpeed == 0  → 즉발(근거리). attackRange 를 히트박스 반지름으로 사용.
    //    missileSpeed  > 0  → 투사체 발사. attackRange 를 폭발 반지름(AoE)으로 사용.
    //    maxTargets == 1    → 단일 대상.
    //    maxTargets  > 1    → 범위 (OverlapCircle 또는 AoE 폭발).
    // ====================================================================

    [Serializable]
    public class SkillData
    {
        [Header("Identity")]
        [Tooltip("스킬 고유 ID. 예: SKL_001")]
        public string skillId;

        [Tooltip("내부 표시 이름. 예: 검 1타")]
        public string skillName;

        [Header("Timing")]
        [Tooltip("평타 자동 발동 간격 (초). 평타 체인에서 각 타격 사이의 대기 시간.")]
        [Min(0.05f)]
        public float cooltime = 0.4f;

        [Tooltip("이 타격 후 다음 체인 입력을 받아들이는 시간 창 (초).\n체인 마지막 타격에서는 무시된다.")]
        [Min(0f)]
        public float comboWindow = 1.0f;

        [Header("Damage")]
        [Tooltip("플레이어 최종 공격력(FinalAttack)에 곱하는 배율.\n1.0 = 기본 데미지, 1.5 = 1.5배")]
        [Min(0f)]
        public float damageMultiplier = 1.0f;

        [Header("Attack Range")]
        [Tooltip("타겟 자동 감지 거리. 이 범위 안에 적이 들어와야 공격을 시작한다.")]
        [Min(0.1f)]
        public float searchRange = 2.5f;

        [Tooltip("공격 적용 범위.\n" +
                 "즉발(missileSpeed=0): OverlapCircle 히트박스 반지름.\n" +
                 "투사체(missileSpeed>0): 적중 지점 AoE 폭발 반지름 (1 = 단일 타겟이면 작게).")]
        [Min(0f)]
        public float attackRange = 0.8f;

        [Tooltip("최대 피격 대상 수.\n1 = 단일 타겟, 큰 값(예: 99) = 범위 내 전체.")]
        [Min(1)]
        public int maxTargets = 3;

        [Header("Projectile (missileSpeed > 0 일 때만 유효)")]
        [Tooltip("0이면 즉발(근거리), 0 초과이면 이 속도의 투사체를 발사한다.")]
        [Min(0f)]
        public float missileSpeed = 0f;

        [Tooltip("투사체 최대 사거리. 이 거리를 초과하면 소멸.")]
        [Min(0.5f)]
        public float missileMaxRange = 8f;

        [Header("Crowd Control")]
        [Tooltip("0 = CC 없음\n양수 = 넉백 (피격 대상을 시전자 반대 방향으로 밀어냄, units/s)\n음수 = 풀인 (피격 대상을 시전자 방향으로 끌어당김, units/s)")]
        public float ccForce = 0f;

        [Tooltip("CC 지속 시간(초). ccForce != 0 일 때만 유효.")]
        [Min(0.05f)]
        public float ccDuration = 0.25f;

        [Tooltip("0이면 스턴 없음. 0 초과이면 이 시간(초)만큼 대상을 정지시킨다.")]
        [Min(0f)]
        public float stunDuration = 0f;

        [Header("Animation")]
        [Tooltip("사용할 Animator 트리거 이름.\n비워두면 'Attack' 기본 트리거를 사용한다.")]
        public string animTriggerOverride = "";

        // ── 편의 프로퍼티 ───────────────────────────────────────────────
        /// <summary>missileSpeed > 0 이면 투사체 공격.</summary>
        public bool IsProjectile => missileSpeed > 0f;

        /// <summary>투사체이고 attackRange > 0 이면 AoE 폭발.</summary>
        public bool IsAoe => IsProjectile && attackRange > 0f && maxTargets > 1;
    }
}
