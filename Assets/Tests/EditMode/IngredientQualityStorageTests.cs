using NUnit.Framework;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class IngredientQualityStorageTests
    {
        PlayerInventoryData m_Inv;

        [SetUp]
        public void SetUp()
        {
            m_Inv = new PlayerInventoryData();
            m_Inv.Init();
        }

        [Test]
        public void AddIngredientWithQuality_TracksQualityCount()
        {
            m_Inv.AddIngredientWithQuality(1001u, 2, IngredientQuality.III);
            Assert.AreEqual(2, m_Inv.GetIngredientCount(1001u, IngredientQuality.III));
        }

        [Test]
        public void AddIngredientWithQuality_TotalCountCorrect()
        {
            m_Inv.AddIngredientWithQuality(1001u, 2, IngredientQuality.II);
            m_Inv.AddIngredientWithQuality(1001u, 3, IngredientQuality.I);
            Assert.AreEqual(5, m_Inv.GetIngredientCount(1001u));
        }

        [Test]
        public void ConsumeIngredientQuality_RemovesFromHighestFirst()
        {
            m_Inv.AddIngredientWithQuality(1001u, 1, IngredientQuality.III);
            m_Inv.AddIngredientWithQuality(1001u, 2, IngredientQuality.I);
            m_Inv.ConsumeIngredientQuality(1001u, 1);
            Assert.AreEqual(0, m_Inv.GetIngredientCount(1001u, IngredientQuality.III));
            Assert.AreEqual(2, m_Inv.GetIngredientCount(1001u, IngredientQuality.I));
        }

        [Test]
        public void GetBestQuality_ReturnsHighestAvailable()
        {
            m_Inv.AddIngredientWithQuality(1001u, 1, IngredientQuality.II);
            m_Inv.AddIngredientWithQuality(1001u, 1, IngredientQuality.I);
            Assert.AreEqual(IngredientQuality.II, m_Inv.GetBestQuality(1001u));
        }

        [Test]
        public void GetBestQuality_NoQualityTracked_ReturnsGradeI()
        {
            Assert.AreEqual(IngredientQuality.I, m_Inv.GetBestQuality(9999u));
        }
    }
}
