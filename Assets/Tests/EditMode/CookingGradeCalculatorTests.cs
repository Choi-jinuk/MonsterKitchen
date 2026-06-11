using NUnit.Framework;
using MonsterKitchen.Cooking;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests.EditMode
{
    // ====================================================================
    //  CookingGradeCalculatorTests — GradeFromAverage 순수 함수 테스트
    //
    //  thresholds: Good=1.0, Perfect=2.5, Legendary=4.0
    // ====================================================================
    public class CookingGradeCalculatorTests
    {
        const float Good      = 1.0f;
        const float Perfect   = 2.5f;
        const float Legendary = 4.0f;

        [Test]
        public void GradeFromAverage_Zero_ReturnsNormal()
        {
            var result = CookingGradeCalculator.GradeFromAverage(0f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Normal, result);
        }

        [Test]
        public void GradeFromAverage_BelowGood_ReturnsNormal()
        {
            var result = CookingGradeCalculator.GradeFromAverage(0.99f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Normal, result);
        }

        [Test]
        public void GradeFromAverage_AtGoodThreshold_ReturnsGood()
        {
            var result = CookingGradeCalculator.GradeFromAverage(1.0f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Good, result);
        }

        [Test]
        public void GradeFromAverage_BetweenGoodAndPerfect_ReturnsGood()
        {
            var result = CookingGradeCalculator.GradeFromAverage(2.0f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Good, result);
        }

        [Test]
        public void GradeFromAverage_AtPerfectThreshold_ReturnsPerfect()
        {
            var result = CookingGradeCalculator.GradeFromAverage(2.5f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Perfect, result);
        }

        [Test]
        public void GradeFromAverage_AtLegendaryThreshold_ReturnsLegendary()
        {
            var result = CookingGradeCalculator.GradeFromAverage(4.0f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Legendary, result);
        }

        [Test]
        public void GradeFromAverage_AboveLegendary_ReturnsLegendary()
        {
            var result = CookingGradeCalculator.GradeFromAverage(10f, Good, Perfect, Legendary);
            Assert.AreEqual(FoodGrade.Legendary, result);
        }
    }
}
