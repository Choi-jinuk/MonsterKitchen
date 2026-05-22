using MonsterKitchen.Combat;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree.Monster
{
    /// <summary>
    /// 플레이어 공격 액션.
    /// MonsterData.skills[] 를 우선순위 순으로 탐색해 조건을 만족하는 첫 스킬을 발동.
    /// MonsterAI.TickAttack() 로직과 동일.
    /// </summary>
    public class BTAction_Attack : BTNode
    {
        readonly MonsterBTAgent _agent;

        static readonly int HashAttack = Animator.StringToHash("Attack");

        public BTAction_Attack(MonsterBTAgent agent)
        {
            _agent = agent;
        }

        public override BTStatus Tick()
        {
            var player = _agent.PlayerTransform;
            if (player == null) return BTStatus.Failure;

            float   dist     = Vector2.Distance(_agent.transform.position, player.position);
            float   prefDist = _agent.PreferredDistance;
            Vector2 toPlayer = ((Vector2)player.position - (Vector2)_agent.transform.position).normalized;

            // 적정 거리를 벗어나면 실패 → Selector 가 Chase 로 전환
            if (dist > prefDist) return BTStatus.Failure;

            // 너무 가까우면 살짝 후퇴
            var rb = _agent.Controller.Rb;
            if (dist < prefDist * 0.5f)
                rb.linearVelocity = _agent.Controller.ApplySeparation(-toPlayer) * (_agent.MoveSpeed * 0.5f);
            else
                rb.linearVelocity = Vector2.zero;

            // 스킬 쿨타임 감소
            _agent.TickSkillTimers(Time.deltaTime);

            // 스킬 발동 (우선순위 순)
            var data   = _agent.Controller.Data;
            var skills = data?.skills;
            if (skills == null) return BTStatus.Running;

            var   timers = _agent.SkillTimers;
            int   limit  = Mathf.Min(skills.Length, timers?.Length ?? 0, 3);

            for (int i = 0; i < limit; i++)
            {
                var skill = skills[i];
                if (skill == null)              continue;
                if (timers[i] > 0f)             continue;
                if (dist > skill.searchRange)   continue;

                ExecuteSkill(i, skill, toPlayer, data, dist);
                break;
            }

            return BTStatus.Running;
        }

        void ExecuteSkill(int index, SkillData skill, Vector2 toPlayer, MonsterData data, float dist)
        {
            _agent.SkillTimers[index] = skill.cooltime;

            int triggerHash = string.IsNullOrEmpty(skill.animTriggerOverride)
                ? HashAttack
                : Animator.StringToHash(skill.animTriggerOverride);
            _agent.Anim.SetTrigger(triggerHash);

            int           baseDmg = data != null ? data.attack : 5;
            int           damage  = Mathf.Max(1, Mathf.RoundToInt(baseDmg * skill.damageMultiplier));
            AttributeType attr    = data != null ? data.attribute : AttributeType.None;

            if (skill.IsProjectile)
                _agent.FireProjectile(skill, damage, attr, toPlayer);
            else
                ApplyMeleeDamage(skill, damage, attr, dist);
        }

        void ApplyMeleeDamage(SkillData skill, int damage, AttributeType attr, float dist)
        {
            var player = _agent.PlayerTransform;
            if (player == null) return;

            if (dist <= skill.attackRange)
            {
                var hp = player.GetComponent<Health>();
                hp?.TakeDamage(damage, attr);
            }
        }
    }
}
