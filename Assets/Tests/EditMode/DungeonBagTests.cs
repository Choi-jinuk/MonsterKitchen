using NUnit.Framework;
using MonsterKitchen.Dungeon;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class DungeonBagTests
    {
        DungeonBag m_Bag;

        [SetUp]
        public void SetUp()
        {
            m_Bag = new DungeonBag(10);   // maxWeight=10
        }

        [TearDown]
        public void TearDown()
        {
            DungeonBag.ClearCurrent();
        }

        // ── TryAdd ───────────────────────────────────────────────────

        [Test]
        public void TryAdd_UnderCapacity_ReturnsTrue()
        {
            bool result = m_Bag.TryAdd(2001u, 2, IngredientQuality.I, 1);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryAdd_ExactCapacity_ReturnsTrue()
        {
            bool result = m_Bag.TryAdd(2001u, 10, IngredientQuality.I, 1);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryAdd_ExceedsCapacity_ReturnsFalse()
        {
            bool result = m_Bag.TryAdd(2001u, 11, IngredientQuality.I, 1);
            Assert.IsFalse(result);
        }

        [Test]
        public void TryAdd_AccumulatesWeight()
        {
            m_Bag.TryAdd(2001u, 3, IngredientQuality.I, 2);   // +6
            Assert.AreEqual(6, m_Bag.CurrentWeight);
        }

        [Test]
        public void TryAdd_SecondAddWouldExceedCapacity_ReturnsFalse()
        {
            m_Bag.TryAdd(2001u, 4, IngredientQuality.I, 2);   // +8, total=8
            bool result = m_Bag.TryAdd(2002u, 2, IngredientQuality.I, 2); // +4 → 12 > 10
            Assert.IsFalse(result);
        }

        [Test]
        public void TryAdd_SameIngredient_MergesQty()
        {
            m_Bag.TryAdd(2001u, 2, IngredientQuality.I, 1);
            m_Bag.TryAdd(2001u, 3, IngredientQuality.I, 1);
            m_Bag.Contents.TryGetValue(2001u, out var entry);
            Assert.AreEqual(5, entry.qty);
        }

        [Test]
        public void TryAdd_SameIngredient_KeepsBestQuality()
        {
            m_Bag.TryAdd(2001u, 1, IngredientQuality.I,   1);
            m_Bag.TryAdd(2001u, 1, IngredientQuality.III, 1);
            m_Bag.Contents.TryGetValue(2001u, out var entry);
            Assert.AreEqual(IngredientQuality.III, entry.quality);
        }

        // ── IsFull ───────────────────────────────────────────────────

        [Test]
        public void IsFull_WhenAtMaxWeight_True()
        {
            m_Bag.TryAdd(2001u, 10, IngredientQuality.I, 1);
            Assert.IsTrue(m_Bag.IsFull);
        }

        [Test]
        public void IsFull_WhenUnderMaxWeight_False()
        {
            m_Bag.TryAdd(2001u, 5, IngredientQuality.I, 1);
            Assert.IsFalse(m_Bag.IsFull);
        }

        // ── Remove ───────────────────────────────────────────────────

        [Test]
        public void Remove_DecreasesWeightAndQty()
        {
            m_Bag.TryAdd(2001u, 4, IngredientQuality.I, 2);   // weight=8
            m_Bag.Remove(2001u, 2);                             // remove 2 → weight=4
            Assert.AreEqual(4, m_Bag.CurrentWeight);
            m_Bag.Contents.TryGetValue(2001u, out var entry);
            Assert.AreEqual(2, entry.qty);
        }

        [Test]
        public void Remove_AllQty_RemovesKey()
        {
            m_Bag.TryAdd(2001u, 3, IngredientQuality.I, 1);
            m_Bag.Remove(2001u, 3);
            Assert.IsFalse(m_Bag.Contents.ContainsKey(2001u));
            Assert.AreEqual(0, m_Bag.CurrentWeight);
        }

        [Test]
        public void Remove_MoreThanQty_ClampsToActualQty()
        {
            m_Bag.TryAdd(2001u, 2, IngredientQuality.I, 1);
            m_Bag.Remove(2001u, 99);
            Assert.IsFalse(m_Bag.Contents.ContainsKey(2001u));
            Assert.AreEqual(0, m_Bag.CurrentWeight);
        }

        // ── Static Current ───────────────────────────────────────────

        [Test]
        public void Constructor_SetsCurrent()
        {
            Assert.AreSame(m_Bag, DungeonBag.Current);
        }

        [Test]
        public void ClearCurrent_SetsNull()
        {
            DungeonBag.ClearCurrent();
            Assert.IsNull(DungeonBag.Current);
        }

        // ── OnBagChanged event ────────────────────────────────────────

        [Test]
        public void TryAdd_Success_FiresOnBagChanged()
        {
            bool fired = false;
            m_Bag.OnBagChanged += () => fired = true;
            m_Bag.TryAdd(2001u, 1, IngredientQuality.I, 1);
            Assert.IsTrue(fired);
        }

        [Test]
        public void TryAdd_Fail_DoesNotFireOnBagChanged()
        {
            bool fired = false;
            m_Bag.OnBagChanged += () => fired = true;
            m_Bag.TryAdd(2001u, 100, IngredientQuality.I, 1);   // exceeds
            Assert.IsFalse(fired);
        }
    }
}
