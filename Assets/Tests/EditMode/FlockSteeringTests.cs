using NUnit.Framework;
using UnityEngine;
using MonsterKitchen.Enemy;

namespace MonsterKitchen.Tests
{
    // ====================================================================
    //  FlockSteeringTests — 순수 군집 스티어링 함수 검증
    // ====================================================================
    public class FlockSteeringTests
    {
        // ── Separation ───────────────────────────────────────────────────

        [Test]
        public void Separation_TwoOverlapping_PushesAway()
        {
            var self      = new Vector2(0f, 0f);
            var neighbors = new[] { new Vector2(0.3f, 0f) };  // 오른쪽에 이웃
            Vector2 sep   = FlockSteering.Separation(self, neighbors, 1, sepRadius: 0.9f, sepWeight: 2f);

            Assert.Less(sep.x, 0f, "이웃 반대(왼쪽)로 밀려야 함");
            Assert.AreEqual(0f, sep.y, 0.001f);
        }

        [Test]
        public void Separation_NoNeighbors_ReturnsZero()
        {
            Vector2 sep = FlockSteering.Separation(Vector2.zero, new Vector2[4], 0, 0.9f, 2f);
            Assert.AreEqual(Vector2.zero, sep);
        }

        [Test]
        public void Separation_NeighborOutsideRadius_Ignored()
        {
            var neighbors = new[] { new Vector2(5f, 0f) };  // 반경 밖
            Vector2 sep   = FlockSteering.Separation(Vector2.zero, neighbors, 1, 0.9f, 2f);
            Assert.AreEqual(Vector2.zero, sep);
        }

        // ── ComputeChase ─────────────────────────────────────────────────

        [Test]
        public void ComputeChase_NoNeighbors_FollowsNavDir()
        {
            var w   = FlockWeights.Default;
            var nav = new Vector2(1f, 0f);
            Vector2 v = FlockSteering.ComputeChase(Vector2.zero, nav, new Vector2[4], 0, moveSpeed: 3f, w);

            Assert.Greater(v.x, 0f, "이웃 없으면 nav 방향으로 진행");
            Assert.LessOrEqual(v.magnitude, 3f + 0.001f, "moveSpeed 로 clamp");
        }

        [Test]
        public void ComputeChase_NeighborAhead_StillProgressesButSteers()
        {
            var w         = FlockWeights.Default;
            var nav       = new Vector2(1f, 0f);
            var neighbors = new[] { new Vector2(0.3f, 0.1f) };
            Vector2 v     = FlockSteering.ComputeChase(Vector2.zero, nav, neighbors, 1, 3f, w);

            Assert.LessOrEqual(v.magnitude, 3f + 0.001f);
            Assert.Less(v.y, 0f, "위쪽 이웃에서 멀어지는 성분");
        }

        // ── ComputeEncircle ──────────────────────────────────────────────

        [Test]
        public void ComputeEncircle_InsideRing_PushedOutward()
        {
            var w      = FlockWeights.Default;
            var player = Vector2.zero;
            var self   = new Vector2(0.3f, 0f);     // 링(1.0) 안쪽
            Vector2 v  = FlockSteering.ComputeEncircle(self, player, ringRadius: 1f, new Vector2[4], 0, 3f, w);

            Assert.Greater(v.x, 0f, "링 안쪽이면 바깥(플레이어 반대)으로");
        }

        [Test]
        public void ComputeEncircle_OutsideRing_PulledInward()
        {
            var w      = FlockWeights.Default;
            var player = Vector2.zero;
            var self   = new Vector2(3f, 0f);       // 링 밖
            Vector2 v  = FlockSteering.ComputeEncircle(self, player, 1f, new Vector2[4], 0, 3f, w);

            Assert.Less(v.x, 0f, "링 밖이면 안쪽(플레이어 방향)으로");
        }

        [Test]
        public void ComputeEncircle_OnRingWithCrowdedNeighbor_SlidesTangentially()
        {
            var w         = FlockWeights.Default;
            var player    = Vector2.zero;
            var self      = new Vector2(1f, 0f);                 // 링 위 (오른쪽)
            var neighbors = new[] { new Vector2(1f, 0.4f) };     // 같은 링, 위쪽에 밀집
            Vector2 v     = FlockSteering.ComputeEncircle(self, player, 1f, neighbors, 1, 3f, w);

            Assert.Less(v.y, 0f, "밀집한 위쪽 반대(아래)로 접선 미끄러짐");
            Assert.LessOrEqual(v.magnitude, 3f + 0.001f);
        }
    }
}
