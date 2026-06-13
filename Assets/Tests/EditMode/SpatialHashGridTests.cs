using NUnit.Framework;
using UnityEngine;
using MonsterKitchen.Enemy;

namespace MonsterKitchen.Tests
{
    // ====================================================================
    //  SpatialHashGridTests — 공간 해시 이웃 질의 검증
    // ====================================================================
    public class SpatialHashGridTests
    {
        [Test]
        public void Query_FindsNeighborInSameCell()
        {
            var grid = new SpatialHashGrid(cellSize: 1f);
            grid.Clear();
            grid.Insert(new Vector2(0.1f, 0.1f), id: 1);
            grid.Insert(new Vector2(0.2f, 0.2f), id: 2);

            var buffer = new Vector2[8];
            int count  = grid.Query(new Vector2(0.1f, 0.1f), radius: 0.9f, excludeId: 1, buffer);

            Assert.AreEqual(1, count);
            Assert.AreEqual(new Vector2(0.2f, 0.2f), buffer[0]);
        }

        [Test]
        public void Query_ExcludesSelf()
        {
            var grid = new SpatialHashGrid(1f);
            grid.Clear();
            grid.Insert(Vector2.zero, id: 99);

            var buffer = new Vector2[8];
            int count  = grid.Query(Vector2.zero, 0.9f, excludeId: 99, buffer);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Query_FindsNeighborInAdjacentCell()
        {
            var grid = new SpatialHashGrid(1f);
            grid.Clear();
            grid.Insert(new Vector2(0.9f, 0f), id: 1);   // cell (0,0)
            grid.Insert(new Vector2(1.1f, 0f), id: 2);   // cell (1,0) 인접

            var buffer = new Vector2[8];
            int count  = grid.Query(new Vector2(0.9f, 0f), 0.5f, excludeId: 1, buffer);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Query_RespectsBufferCapacity()
        {
            var grid = new SpatialHashGrid(1f);
            grid.Clear();
            for (int i = 0; i < 10; i++) grid.Insert(new Vector2(0.05f * i, 0f), id: i + 100);

            var buffer = new Vector2[3];
            int count  = grid.Query(Vector2.zero, 0.9f, excludeId: -1, buffer);

            Assert.LessOrEqual(count, 3, "버퍼 용량 초과 금지");
        }
    }
}
