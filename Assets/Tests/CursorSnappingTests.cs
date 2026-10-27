using NUnit.Framework;
using UnityEngine;
using Warmask.Planet;

namespace Warmask.Tests
{
    public class CursorSnappingTests
    {
        [Test]
        public void PressingRight_JumpsToTheNearestPlanetOnTheRight()
        {
            var candidates = new[]
            {
                new Vector2(200f, 100f),
                new Vector2(300f, 100f),
                new Vector2(100f, 300f)
            };

            Assert.IsTrue(CursorSnapping.TryPick(new Vector2(100f, 100f), Vector2.right, candidates,
                out Vector2 target));
            Assert.AreEqual(new Vector2(200f, 100f), target);
        }

        [Test]
        public void PlanetOutsideThePressedDirection_IsNotPicked()
        {
            var candidates = new[] { new Vector2(100f, 300f) };

            Assert.IsFalse(CursorSnapping.TryPick(new Vector2(100f, 100f), Vector2.right, candidates, out _));
        }

        [Test]
        public void PlanetUnderTheCursor_IsIgnored()
        {
            var candidates = new[] { new Vector2(100f, 100f) };

            Assert.IsFalse(CursorSnapping.TryPick(new Vector2(100f, 100f), Vector2.right, candidates, out _));
        }

        [Test]
        public void DiagonalKeys_SnapAlongTheDiagonal()
        {
            var candidates = new[] { new Vector2(200f, 200f), new Vector2(300f, 100f) };

            Assert.IsTrue(CursorSnapping.TryPick(new Vector2(100f, 100f), new Vector2(1f, 1f), candidates,
                out Vector2 target));
            Assert.AreEqual(new Vector2(200f, 200f), target);
        }
    }
}
