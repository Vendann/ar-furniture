using NUnit.Framework;
using UnityEngine;

namespace ARFurniture.Tests
{
    public sealed class ARPlacementTests
    {
        [Test]
        public void TwoFingerRotationUsesAngleOnly()
        {
            var delta = ARPlacementController.RotationDelta(
                new Vector2(-1, 0),
                new Vector2(-1, 1),
                new Vector2(1, 0),
                new Vector2(1, -1));

            Assert.That(delta, Is.EqualTo(90).Within(0.001f));
        }
    }
}
