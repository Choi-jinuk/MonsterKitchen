using NUnit.Framework;
using MonsterKitchen.Dungeon;

namespace MonsterKitchen.Tests
{
    public class DungeonGateLogicTests
    {
        [Test]
        public void ComputeGateDamage_ToolLevelBelowRequired_ReturnsZero()
        {
            Assert.AreEqual(0, DungeonGate.ComputeGateDamage(50, toolLevel: 1, requiredLevel: 2));
        }

        [Test]
        public void ComputeGateDamage_ToolLevelMeetsRequired_ReturnsDamage()
        {
            Assert.AreEqual(50, DungeonGate.ComputeGateDamage(50, toolLevel: 2, requiredLevel: 2));
        }

        [Test]
        public void ComputeGateDamage_ToolLevelAboveRequired_ReturnsDamage()
        {
            Assert.AreEqual(50, DungeonGate.ComputeGateDamage(50, toolLevel: 5, requiredLevel: 2));
        }

        [Test]
        public void ComputeGateDamage_ZeroDamageWithValidLevel_ClampsToOne()
        {
            Assert.AreEqual(1, DungeonGate.ComputeGateDamage(0, toolLevel: 2, requiredLevel: 2));
        }
    }
}
