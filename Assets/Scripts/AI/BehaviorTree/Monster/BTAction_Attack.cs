using MonsterKitchen.Combat;
using MonsterKitchen.Data;
using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Monster
{
    // ====================================================================
    //  BTAction_Attack — 플레이어 공격 액션
    //
    //  ▶ 개선 사항
    //    · OnEnter: 컴포넌트 및 스킬 타이머 배열을 1회 초기화
    //    · ctx.DeltaTime 사용 (이전: Time.deltaTime)
    //    · OnExit: 속도 리셋 보장
    //
    //  MonsterData.skillGroups[] 를 우선순위 순으로 탐색해 조건을 만족하는 첫 스킬 발동.
    //  각 그룹의 GetStep(0) (첫 번째 SkillData)을 대표 공격으로 사용.
    // ====================================================================

    [BTNode("Monster/Action/Attack")]
    public class BTAction_Attack : BTNode
    {
        static readonly int s_HashAttack = Animator.StringToHash("Attack");

        class State
        {
            public BTMonsterController Ctrl;
            public Animator            Anim;
            public float[]             SkillTimers;
        }

        // ── 라이프사이클 ─────────────────────────────────────────────────

        protected override void OnEnter(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Ctrl != null) return; // 이미 초기화됨

            s.Ctrl = ctx.Owner.GetComponent<BTMonsterController>();
            s.Anim = ctx.Owner.GetComponent<Animator>();

            var initGroups = s.Ctrl?.Data?.SkillGroups;
            int count      = initGroups != null ? Mathf.Min(initGroups.Length, 3) : 0;
            s.SkillTimers  = new float[count];
        }

        protected override BTStatus Execute(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);

            if (!ctx.Blackboard.TryGet<Transform>("Player", out var player) || player == null)
                return BTStatus.Failure;

            float   prefDist = ctx.Blackboard.Get<float>("PreferredDistance");
            float   dist     = Vector2.Distance(ctx.Owner.transform.position, player.position);
            Vector2 toPlayer = ((Vector2)player.position - (Vector2)ctx.Owner.transform.position).normalized;

            if (dist > prefDist) return BTStatus.Failure;

            // 둘러싸기: 멈추지 않고 링 주위 빈칸을 향해 미끄러지며 공격 유지
            if (s.Ctrl != null)
            {
                float moveSpeed = ctx.Blackboard.Get<float>("MoveSpeed");
                var   mgr       = MonsterKitchen.AI.FlockManager.Instance;
                var   weights   = s.Ctrl.FlockWeights;

                int count = mgr != null
                    ? mgr.QueryNeighbors(ctx.Owner.transform.position, weights.SepRadius,
                                         ctx.Owner.transform, s.Ctrl.NeighborBuffer)
                    : 0;

                Vector2 encircle = MonsterKitchen.AI.FlockSteering.ComputeEncircle(
                    ctx.Owner.transform.position, player.position, prefDist,
                    s.Ctrl.NeighborBuffer, count, moveSpeed, weights);

                s.Ctrl.Rb.linearVelocity = encircle;
            }

            TickSkillTimers(s, ctx.DeltaTime);
            TryFireSkill(s, toPlayer, dist, player);

            return BTStatus.Running;
        }

        protected override void OnExit(BTContext ctx)
        {
            var s = ctx.GetOrCreateState<State>(this);
            if (s.Ctrl?.Rb != null) s.Ctrl.Rb.linearVelocity = Vector2.zero;
        }

        // ── 스킬 처리 ────────────────────────────────────────────────────

        void TryFireSkill(State s, Vector2 toPlayer, float dist, Transform player)
        {
            var data   = s.Ctrl?.Data;
            var groups = data?.SkillGroups;
            if (groups == null || s.SkillTimers == null) return;

            int limit = Mathf.Min(groups.Length, s.SkillTimers.Length, 3);
            for (int i = 0; i < limit; i++)
            {
                var skill = groups[i]?.GetStep(0);
                if (skill == null)               continue;
                if (s.SkillTimers[i] > 0f)       continue;
                if (dist > skill.AttackRange)    continue;

                FireSkill(s, i, skill, toPlayer, data, dist, player);
                break;
            }
        }

        void FireSkill(State s, int index, SkillData skill, Vector2 toPlayer,
                       MonsterData data, float dist, Transform player)
        {
            int           baseDmg = data != null ? data.Attack : 5;
            int           damage  = Mathf.Max(1, Mathf.RoundToInt(baseDmg * skill.DamageMultiplier));
            AttributeType attr    = data != null ? data.Attribute : AttributeType.None;

            if (skill.IsProjectile)
                s.Ctrl?.FireProjectile(skill, damage, attr, toPlayer);
            else
                player.GetComponent<Health>()?.TakeDamage(damage, attr);

            // 공격 실행 후 쿨타임 설정
            s.SkillTimers[index] = skill.Cooltime;

            int triggerHash = string.IsNullOrEmpty(skill.AnimTriggerOverride)
                ? s_HashAttack
                : Animator.StringToHash(skill.AnimTriggerOverride);
            s.Anim?.SetTrigger(triggerHash);
        }

        static void TickSkillTimers(State s, float dt)
        {
            if (s.SkillTimers == null) return;
            for (int i = 0; i < s.SkillTimers.Length; i++)
                if (s.SkillTimers[i] > 0f) s.SkillTimers[i] -= dt;
        }

#if UNITY_EDITOR
        public override string DebugLabel => "⚔ Attack";
#endif
    }
}
