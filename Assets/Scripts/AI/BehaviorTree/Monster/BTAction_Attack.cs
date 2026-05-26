using MonsterKitchen.Combat;
using MonsterKitchen.Data;
using MonsterKitchen.Enemy;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Monster
{
    /// <summary>
    /// 플레이어 공격 액션.
    /// MonsterData.skills[] 를 우선순위 순으로 탐색해 조건을 만족하는 첫 스킬을 발동한다.
    /// </summary>
    [BTNode("Monster/Action/Attack")]
    public class BTAction_Attack : BTNode
    {
        static readonly int HashAttack = Animator.StringToHash("Attack");

        class State
        {
            public BTMonsterController ctrl;
            public Animator            anim;
            public float[]             skillTimers;
        }

        protected override BTStatus Execute(BTContext ctx)
        {
            var state = ctx.GetOrCreateState<State>(this);

            if (state.ctrl == null)
            {
                state.ctrl = ctx.Owner.GetComponent<BTMonsterController>();
                state.anim = ctx.Owner.GetComponent<Animator>();

                var initSkills = state.ctrl?.Data?.skills;
                int count      = initSkills != null ? Mathf.Min(initSkills.Length, 3) : 0;
                state.skillTimers = new float[count];
            }

            if (!ctx.Blackboard.TryGet<Transform>("Player", out var player) || player == null)
                return BTStatus.Failure;

            float   prefDist = ctx.Blackboard.Get<float>("PreferredDistance");
            float   dist     = Vector2.Distance(ctx.Owner.transform.position, player.position);
            Vector2 toPlayer = ((Vector2)player.position - (Vector2)ctx.Owner.transform.position).normalized;

            if (dist > prefDist) return BTStatus.Failure;

            if (state.ctrl != null)
            {
                float moveSpeed = ctx.Blackboard.Get<float>("MoveSpeed");
                state.ctrl.Rb.linearVelocity = dist < prefDist * 0.5f
                    ? state.ctrl.ApplySeparation(-toPlayer) * (moveSpeed * 0.5f)
                    : Vector2.zero;
            }

            TickSkillTimers(state);

            var data      = state.ctrl?.Data;
            var skillList = data?.skills;
            if (skillList == null) return BTStatus.Running;

            int limit = Mathf.Min(skillList.Length, state.skillTimers?.Length ?? 0, 3);
            for (int i = 0; i < limit; i++)
            {
                var skill = skillList[i];
                if (skill == null)            continue;
                if (state.skillTimers[i] > 0f) continue;
                if (dist > skill.searchRange)  continue;

                ExecuteSkill(state, i, skill, toPlayer, data, dist, player);
                break;
            }

            return BTStatus.Running;
        }

        void ExecuteSkill(State state, int index, SkillData skill, Vector2 toPlayer,
                          MonsterData data, float dist, Transform player)
        {
            state.skillTimers[index] = skill.cooltime;

            int triggerHash = string.IsNullOrEmpty(skill.animTriggerOverride)
                ? HashAttack
                : Animator.StringToHash(skill.animTriggerOverride);
            state.anim?.SetTrigger(triggerHash);

            int           baseDmg = data != null ? data.attack : 5;
            int           damage  = Mathf.Max(1, Mathf.RoundToInt(baseDmg * skill.damageMultiplier));
            AttributeType attr    = data != null ? data.attribute : AttributeType.None;

            if (skill.IsProjectile)
                state.ctrl?.FireProjectile(skill, damage, attr, toPlayer);
            else if (dist <= skill.attackRange)
                player.GetComponent<Health>()?.TakeDamage(damage, attr);
        }

        static void TickSkillTimers(State state)
        {
            if (state.skillTimers == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < state.skillTimers.Length; i++)
                if (state.skillTimers[i] > 0f) state.skillTimers[i] -= dt;
        }

#if UNITY_EDITOR
        public override string DebugLabel => "⚔ Attack";
#endif
    }
}
