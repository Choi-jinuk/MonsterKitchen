using NUnit.Framework;
using MonsterKitchen;
using MonsterKitchen.Combat;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class KillQualityEvaluatorTests
    {
        // monsterRarity: RarityType enum int (0=Common..4=Legendary)
        // recommended = ceil(rarity / 2f): 0→0, 1→1, 2→1, 3→2, 4→2

        [Test]
        public void Evaluate_NoFactors_ReturnsGradeI()
        {
            // weaponTier=0, recommended=ceil(2/2)=1 → gap=-1 → 0점
            // no attribute match, no CC → total=0 → Grade I
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Rare,
                monsterAttribute: AttributeType.Fire,
                killAttribute:    AttributeType.None,
                hadSoftCC:        false,
                weaponTier:       0);

            Assert.AreEqual(IngredientQuality.I, result);
        }

        [Test]
        public void Evaluate_AttributeMatchOnly_ReturnsGradeI()
        {
            // attribute +1, no CC, weapon gap<0 → total=1 → Grade I
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Common,
                monsterAttribute: AttributeType.Water,
                killAttribute:    AttributeType.Water,
                hadSoftCC:        false,
                weaponTier:       -1);

            Assert.AreEqual(IngredientQuality.I, result);
        }

        [Test]
        public void Evaluate_AttributeMatchAndCC_ReturnsGradeII()
        {
            // attribute +1, CC +1, weapon gap<0 → total=2 → Grade II
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Epic,
                monsterAttribute: AttributeType.Fire,
                killAttribute:    AttributeType.Fire,
                hadSoftCC:        true,
                weaponTier:       0);

            Assert.AreEqual(IngredientQuality.II, result);
        }

        [Test]
        public void Evaluate_AllThreeFactors_ReturnsGradeIII()
        {
            // attribute +1, CC +1, weapon gap=0 → +1 → total=3 → Grade III
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Common,
                monsterAttribute: AttributeType.Wind,
                killAttribute:    AttributeType.Wind,
                hadSoftCC:        true,
                weaponTier:       0);

            Assert.AreEqual(IngredientQuality.III, result);
        }

        [Test]
        public void Evaluate_HighWeaponTierAlone_ReturnsGradeIII()
        {
            // no attribute, no CC, weapon gap≥2 → +3 → capped to 3 → Grade III
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Common,
                monsterAttribute: AttributeType.None,
                killAttribute:    AttributeType.None,
                hadSoftCC:        false,
                weaponTier:       3);

            Assert.AreEqual(IngredientQuality.III, result);
        }

        [Test]
        public void Evaluate_WeaponTierGap1_Returns2WeaponPoints()
        {
            // no attribute, no CC, weapon gap=1 → +2 → total=2 → Grade II
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Common,
                monsterAttribute: AttributeType.None,
                killAttribute:    AttributeType.None,
                hadSoftCC:        false,
                weaponTier:       1);

            Assert.AreEqual(IngredientQuality.II, result);
        }

        [Test]
        public void Evaluate_TotalCappedAt3()
        {
            // attribute +1, CC +1, weapon gap=2 → +3 → sum=5, capped to 3 → Grade III
            var result = KillQualityEvaluator.Evaluate(
                monsterRarity:    (int)RarityType.Common,
                monsterAttribute: AttributeType.Fire,
                killAttribute:    AttributeType.Fire,
                hadSoftCC:        true,
                weaponTier:       2);

            Assert.AreEqual(IngredientQuality.III, result);
        }
    }
}
