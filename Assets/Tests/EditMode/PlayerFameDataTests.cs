using NUnit.Framework;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class PlayerFameDataTests
    {
        PlayerFameData m_Fame;

        [SetUp] public void SetUp() => m_Fame = new PlayerFameData();

        [Test]
        public void TotalFame_Initial_IsZero()
            => Assert.AreEqual(0, m_Fame.TotalFame);

        [Test]
        public void AddFame_Positive_IncreasesFame()
        {
            m_Fame.AddFame(50);
            Assert.AreEqual(50, m_Fame.TotalFame);
        }

        [Test]
        public void AddFame_Negative_ClampedAtZero()
        {
            m_Fame.AddFame(-999);
            Assert.AreEqual(0, m_Fame.TotalFame);
        }

        [Test]
        public void GetDailyFame_Satisfaction50_Returns0()
            => Assert.AreEqual(0, m_Fame.GetDailyFame(50f));

        [Test]
        public void GetDailyFame_Satisfaction100_Returns40()
            => Assert.AreEqual(40, m_Fame.GetDailyFame(100f));

        [Test]
        public void GetDailyFame_Satisfaction0_ReturnsMinus40()
            => Assert.AreEqual(-40, m_Fame.GetDailyFame(0f));

        [Test]
        public void MaxGuestsPerDay_Fame0_Returns3()
            => Assert.AreEqual(3, m_Fame.MaxGuestsPerDay());

        [Test]
        public void MaxGuestsPerDay_Fame100_Returns4()
        {
            m_Fame.AddFame(100);
            Assert.AreEqual(4, m_Fame.MaxGuestsPerDay());
        }

        [Test]
        public void MenuSlotCount_Fame0_Returns3()
            => Assert.AreEqual(3, m_Fame.MenuSlotCount());

        [Test]
        public void MenuSlotCount_Fame250_Returns4()
        {
            m_Fame.AddFame(250);
            Assert.AreEqual(4, m_Fame.MenuSlotCount());
        }

        [Test]
        public void Load_RestoresFame()
        {
            m_Fame.Load(300);
            Assert.AreEqual(300, m_Fame.TotalFame);
        }

        [Test]
        public void OnFameChanged_FiredOnAddFame()
        {
            int fired = 0;
            m_Fame.OnFameChanged += (_, __) => fired++;
            m_Fame.AddFame(10);
            Assert.AreEqual(1, fired);
        }
    }
}
