using NUnit.Framework;
using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Tests
{
    public class SpriteOccluderFadeTests
    {
        static readonly Bounds PropBounds = new Bounds(
            new Vector3(0f, 0f, 0f), new Vector3(2f, 2f, 1f));

        [Test]
        public void ShouldFade_PlayerInsideBounds_AndBehind_True()
        {
            // 플레이어가 prop 영역 안 + 더 위(Y 큼) = 가려짐
            Assert.IsTrue(SpriteOccluderFade.ShouldFade(
                PropBounds, propY: 0f, playerPos: new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void ShouldFade_PlayerInsideBounds_ButInFront_False()
        {
            // 플레이어가 prop 영역 안이지만 더 아래(앞) = 가림 아님
            Assert.IsFalse(SpriteOccluderFade.ShouldFade(
                PropBounds, propY: 0f, playerPos: new Vector2(0.5f, -0.5f)));
        }

        [Test]
        public void ShouldFade_PlayerOutsideBounds_False()
        {
            Assert.IsFalse(SpriteOccluderFade.ShouldFade(
                PropBounds, propY: 0f, playerPos: new Vector2(5f, 5f)));
        }
    }
}
