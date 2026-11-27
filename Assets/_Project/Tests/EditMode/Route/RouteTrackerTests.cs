using NUnit.Framework;
using PermisVR.Route;

namespace PermisVR.Exam.Tests.Route
{
    public class RouteTrackerTests
    {
        private static RouteDefinition StraightNorth(float lengthM = 1000f) =>
            new("straight", new[] { 0f, 0f }, new[] { 0f, lengthM }, null, null);

        // North 200 m, east 20 m, back south 200 m: the two long legs are 20 m apart.
        private static RouteDefinition UTurnLoop() =>
            new("loop", new[] { 0f, 0f, 20f, 20f }, new[] { 0f, 200f, 200f, 0f }, null, null);

        [Test]
        public void Position_FollowsCarAlongRoute()
        {
            var tracker = new RouteTracker(StraightNorth(), new RouteTrackerConfig());
            tracker.Update(1.5f, 0f);
            tracker.Update(1.5f, 40f);
            tracker.Update(1.5f, 100f);
            Assert.That(tracker.PositionM, Is.EqualTo(100f).Within(0.01f));
            Assert.That(tracker.LateralM, Is.EqualTo(1.5f).Within(0.01f));
        }

        [Test]
        public void DoesNotJump_ToLaterLegOfLoopThatPassesNearby()
        {
            var tracker = new RouteTracker(UTurnLoop(), new RouteTrackerConfig());
            tracker.Update(0f, 10f);
            tracker.Update(0f, 40f);
            tracker.Update(12f, 50f); // closer to the return leg (x = 20) than to the outbound one
            Assert.That(tracker.PositionM, Is.EqualTo(50f).Within(0.5f));
        }

        [Test]
        public void NeverSlidesBackwards()
        {
            var tracker = new RouteTracker(StraightNorth(), new RouteTrackerConfig());
            tracker.Update(0f, 50f);
            tracker.Update(0f, 100f);
            tracker.Update(0f, 95f);
            Assert.That(tracker.PositionM, Is.EqualTo(100f).Within(0.01f));
        }

        [Test]
        public void PicksUpRouteAgain_WhereCarRejoinsItAfterWrongTurn()
        {
            var tracker = new RouteTracker(StraightNorth(), new RouteTrackerConfig());
            tracker.Update(0f, 50f);
            tracker.Update(0f, 100f);
            tracker.Update(60f, 120f);
            Assert.That(tracker.LateralM, Is.GreaterThan(25f));
            tracker.Update(0.5f, 400f);
            Assert.That(tracker.PositionM, Is.EqualTo(400f).Within(0.01f));
        }

        [Test]
        public void Finished_NearTheEnd()
        {
            var tracker = new RouteTracker(StraightNorth(100f), new RouteTrackerConfig());
            tracker.Update(0f, 50f);
            Assert.That(tracker.Finished, Is.False);
            tracker.Update(0f, 95f);
            Assert.That(tracker.Finished, Is.True);
        }
    }
}
