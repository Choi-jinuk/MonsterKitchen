using NUnit.Framework;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class PlayerCookingMasteryDataTests
    {
        PlayerCookingMasteryData m_Mastery;

        [SetUp]
        public void SetUp() => m_Mastery = new PlayerCookingMasteryData();

        // ── Level thresholds ─────────────────────────────────────────

        [Test]
        public void GetLevel_ZeroCooks_ReturnsOne()
            => Assert.AreEqual(1, m_Mastery.GetLevel(3001u));

        [Test]
        public void GetLevel_FiveCooks_ReturnsTwo()
        {
            for (int i = 0; i < 5; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(2, m_Mastery.GetLevel(3001u));
        }

        [Test]
        public void GetLevel_FifteenCooks_ReturnsThree()
        {
            for (int i = 0; i < 15; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(3, m_Mastery.GetLevel(3001u));
        }

        [Test]
        public void GetLevel_ThirtyCooks_ReturnsFour()
        {
            for (int i = 0; i < 30; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(4, m_Mastery.GetLevel(3001u));
        }

        [Test]
        public void GetLevel_FiftyCooks_ReturnsFive()
        {
            for (int i = 0; i < 50; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(5, m_Mastery.GetLevel(3001u));
        }

        [Test]
        public void GetLevel_OverFifty_CapsAtFive()
        {
            for (int i = 0; i < 100; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(5, m_Mastery.GetLevel(3001u));
        }

        // ── Price multiplier ─────────────────────────────────────────

        [Test]
        public void GetPriceMultiplier_Lv1_Returns1f()
            => Assert.AreEqual(1.0f, m_Mastery.GetPriceMultiplier(3001u), 0.001f);

        [Test]
        public void GetPriceMultiplier_Lv5_Returns1Point5f()
        {
            for (int i = 0; i < 50; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(1.5f, m_Mastery.GetPriceMultiplier(3001u), 0.001f);
        }

        // ── Independence ─────────────────────────────────────────────

        [Test]
        public void RecordCook_DifferentRecipes_IndependentCounts()
        {
            for (int i = 0; i < 5; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(2, m_Mastery.GetLevel(3001u));
            Assert.AreEqual(1, m_Mastery.GetLevel(3002u));
        }

        // ── OnLevelUp event ──────────────────────────────────────────

        [Test]
        public void RecordCook_WhenLevelUp_FiresOnLevelUp()
        {
            uint firedId  = 0;
            int  firedLvl = 0;
            m_Mastery.OnLevelUp += (id, lv) => { firedId = id; firedLvl = lv; };

            for (int i = 0; i < 5; i++) m_Mastery.RecordCook(3001u);

            Assert.AreEqual(3001u, firedId);
            Assert.AreEqual(2,     firedLvl);
        }

        [Test]
        public void RecordCook_NoLevelChange_DoesNotFireOnLevelUp()
        {
            bool fired = false;
            m_Mastery.OnLevelUp += (_, __) => fired = true;
            m_Mastery.RecordCook(3001u);   // 1 cook — still Lv1
            Assert.IsFalse(fired);
        }

        // ── Speed multiplier ─────────────────────────────────────────

        [Test]
        public void GetSpeedMultiplier_Lv1_Returns1f()
            => Assert.AreEqual(1.0f, m_Mastery.GetSpeedMultiplier(3001u), 0.001f);

        [Test]
        public void GetSpeedMultiplier_Lv2_Returns0Point85f()
        {
            for (int i = 0; i < 5; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(0.85f, m_Mastery.GetSpeedMultiplier(3001u), 0.001f);
        }

        [Test]
        public void GetSpeedMultiplier_Lv5_Returns0Point70f()
        {
            for (int i = 0; i < 50; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(0.70f, m_Mastery.GetSpeedMultiplier(3001u), 0.001f);
        }

        // ── Ingredient save chance ───────────────────────────────────

        [Test]
        public void GetIngredientSaveChance_Lv1_Returns0f()
            => Assert.AreEqual(0f, m_Mastery.GetIngredientSaveChance(3001u), 0.001f);

        [Test]
        public void GetIngredientSaveChance_Lv3_Returns0Point2f()
        {
            for (int i = 0; i < 15; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(0.20f, m_Mastery.GetIngredientSaveChance(3001u), 0.001f);
        }

        // ── Grade up chance ──────────────────────────────────────────

        [Test]
        public void GetGradeUpChance_Lv4_Returns0f()
        {
            for (int i = 0; i < 30; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(0f, m_Mastery.GetGradeUpChance(3001u), 0.001f);
        }

        [Test]
        public void GetGradeUpChance_Lv5_Returns0Point3f()
        {
            for (int i = 0; i < 50; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(0.30f, m_Mastery.GetGradeUpChance(3001u), 0.001f);
        }

        // ── GetCookCount ─────────────────────────────────────────────

        [Test]
        public void GetCookCount_AfterThreeCooks_ReturnsThree()
        {
            for (int i = 0; i < 3; i++) m_Mastery.RecordCook(3001u);
            Assert.AreEqual(3, m_Mastery.GetCookCount(3001u));
        }

        // ── LoadCounts ───────────────────────────────────────────────

        [Test]
        public void LoadCounts_RestoresLevelCorrectly()
        {
            var data = new System.Collections.Generic.Dictionary<uint, int>
                { [3001u] = 15 };
            m_Mastery.LoadCounts(data);
            Assert.AreEqual(3, m_Mastery.GetLevel(3001u));
        }
    }
}
