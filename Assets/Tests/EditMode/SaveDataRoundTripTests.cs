using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    // ====================================================================
    //  SaveDataRoundTripTests — 재료 품질 / 음식 등급 저장-복원 검증 (v2 스키마)
    // ====================================================================
    public class SaveDataRoundTripTests
    {
        PlayerInventoryData m_Inv;

        [SetUp]
        public void SetUp()
        {
            m_Inv = new PlayerInventoryData();
            m_Inv.Init();
        }

        // ── 재료 품질 ─────────────────────────────────────────────────

        [Test]
        public void IngredientQualities_RoundTrip_PreservesCounts()
        {
            m_Inv.AddIngredientWithQuality(2001u, 3, IngredientQuality.I);
            m_Inv.AddIngredientWithQuality(2001u, 2, IngredientQuality.III);
            m_Inv.AddIngredientWithQuality(2002u, 1, IngredientQuality.II);

            var saved = m_Inv.AllIngredientQualities().ToList();

            var restored = new PlayerInventoryData();
            restored.Init();
            restored.LoadIngredients(new[] { (2001u, 5), (2002u, 1) });
            restored.LoadIngredientQualities(saved);

            Assert.AreEqual(3, restored.GetIngredientCount(2001u, IngredientQuality.I));
            Assert.AreEqual(2, restored.GetIngredientCount(2001u, IngredientQuality.III));
            Assert.AreEqual(1, restored.GetIngredientCount(2002u, IngredientQuality.II));
            Assert.AreEqual(IngredientQuality.III, restored.GetBestQuality(2001u));
        }

        [Test]
        public void LoadIngredients_ClearsStaleQualities()
        {
            m_Inv.AddIngredientWithQuality(2001u, 3, IngredientQuality.III);

            // 품질 데이터 없는 세이브 로드 → 이전 품질 잔존 금지
            m_Inv.LoadIngredients(new[] { (2002u, 1) });

            Assert.AreEqual(0, m_Inv.GetIngredientCount(2001u, IngredientQuality.III));
            Assert.AreEqual(IngredientQuality.I, m_Inv.GetBestQuality(2001u));
        }

        // ── 음식 등급 ─────────────────────────────────────────────────

        [Test]
        public void FoodGrades_RoundTrip_PreservesGrades()
        {
            m_Inv.AddFoodEntry(4001u, FoodGrade.Perfect);
            m_Inv.AddFoodEntry(4001u, FoodGrade.Normal);
            m_Inv.AddFoodEntry(4002u, FoodGrade.Good);

            var savedGrades = m_Inv.AllFoodGrades().ToList();
            var savedFoods  = m_Inv.AllFoods.Select(kv => (kv.Key, kv.Value)).ToList();

            var restored = new PlayerInventoryData();
            restored.Init();
            restored.LoadFoods(savedFoods);
            restored.LoadFoodGrades(savedGrades);

            // 등급 합계가 보존되어야 한다 (소모 순서는 큐 순서에 따름)
            var grades4001 = new List<FoodGrade>
            {
                restored.ConsumeFood(4001u).grade,
                restored.ConsumeFood(4001u).grade,
            };
            CollectionAssert.AreEquivalent(
                new[] { FoodGrade.Perfect, FoodGrade.Normal }, grades4001);

            Assert.AreEqual(FoodGrade.Good, restored.ConsumeFood(4002u).grade);
        }

        [Test]
        public void FoodGrades_V1SaveWithoutGrades_PadsWithNormal()
        {
            var restored = new PlayerInventoryData();
            restored.Init();
            restored.LoadFoods(new[] { (4001u, 2) });
            restored.LoadFoodGrades(System.Linq.Enumerable
                .Empty<(uint, FoodGrade, int)>());   // v1 세이브: 등급 리스트 없음

            Assert.AreEqual(FoodGrade.Normal, restored.ConsumeFood(4001u).grade);
            Assert.AreEqual(FoodGrade.Normal, restored.ConsumeFood(4001u).grade);
        }
    }
}
