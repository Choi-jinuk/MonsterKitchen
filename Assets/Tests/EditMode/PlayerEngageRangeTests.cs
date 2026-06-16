using NUnit.Framework;
using MonsterKitchen.Data;
using MonsterKitchen.Player;

namespace MonsterKitchen.Tests
{
    // ====================================================================
    //  PlayerEngageRangeTests — BT 교전 거리 정책 검증
    //
    //  배경: 근접 평타가 SearchRange(2.5) 에서 이동을 멈추고
    //  AttackRange(0.8) 밖에서 허공 공격하던 교착 버그.
    //  교전 거리 = 투사체면 SearchRange, 근접이면 AttackRange.
    // ====================================================================
    public class PlayerEngageRangeTests
    {
        [Test]
        public void EngageRangeOf_Melee_UsesAttackRange()
        {
            var skill = new SkillData { SearchRange = 2.5f, AttackRange = 0.8f, MissileSpeed = 0f };
            Assert.AreEqual(0.8f, PlayerController.EngageRangeOf(skill, 1f));
        }

        [Test]
        public void EngageRangeOf_Projectile_UsesSearchRange()
        {
            var skill = new SkillData { SearchRange = 5f, AttackRange = 0.5f, MissileSpeed = 4f };
            Assert.AreEqual(5f, PlayerController.EngageRangeOf(skill, 1f));
        }

        [Test]
        public void EngageRangeOf_NullSkill_UsesFallback()
        {
            Assert.AreEqual(1.5f, PlayerController.EngageRangeOf(null, 1.5f));
        }
    }
}
