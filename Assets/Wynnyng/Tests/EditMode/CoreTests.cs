using System;
using NUnit.Framework;
using Wynnyng.Core;

namespace Wynnyng.Tests.EditMode
{
    public sealed class BoundaryTests
    {
        private readonly PitchGeometry pitch = new PitchGeometry(105f, 68f, 7.32f, 2.44f, 0.11f);

        [TestCase(1f)] [TestCase(-1f)]
        public void BallMustCrossEntireEndLine(float sign)
        {
            float plane = pitch.HalfLength + pitch.BallRadius;
            Vec3 start = new Vec3(0f, 1f, sign * 52f);
            Assert.That(BoundaryEvaluator.Evaluate(start, new Vec3(0f, 1f, sign * plane), pitch).HasCrossed, Is.False);
            Assert.That(BoundaryEvaluator.Evaluate(start, new Vec3(0f, 1f, sign * (plane + 0.001f)), pitch).Kind, Is.EqualTo(BoundaryKind.Goal));
        }
        [TestCase(1f)] [TestCase(-1f)]
        public void BothGoalsHaveCorrectEnd(float sign)
        {
            BoundaryCrossing result = BoundaryEvaluator.Evaluate(new Vec3(0f, 0.11f, sign * 52f),
                new Vec3(0f, 0.11f, sign * 54f), pitch);
            Assert.That(result.Kind, Is.EqualTo(BoundaryKind.Goal));
            Assert.That(result.End, Is.EqualTo(sign > 0f ? PitchEnd.Positive : PitchEnd.Negative));
        }
        [TestCase(3.549f, 1f, BoundaryKind.Goal)]
        [TestCase(3.551f, 1f, BoundaryKind.GoalLine)]
        [TestCase(-3.551f, 1f, BoundaryKind.GoalLine)]
        [TestCase(0f, 2.329f, BoundaryKind.Goal)]
        [TestCase(0f, 2.331f, BoundaryKind.GoalLine)]
        [TestCase(0f, -1f, BoundaryKind.GoalLine)]
        public void WholeSphereMustFitOpening(float x, float y, BoundaryKind expected)
        {
            Assert.That(BoundaryEvaluator.Evaluate(new Vec3(x, y, 52f), new Vec3(x, y, 54f), pitch).Kind, Is.EqualTo(expected));
        }
        [Test]
        public void EvaluatesHeightAtCrossingInsteadOfSegmentEnd()
        {
            BoundaryCrossing result = BoundaryEvaluator.Evaluate(new Vec3(0f, 1f, 52f), new Vec3(0f, 4f, 56f), pitch);
            Assert.That(result.Kind, Is.EqualTo(BoundaryKind.Goal));
            Assert.That(result.Position.Y, Is.LessThan(pitch.GoalHeight - pitch.BallRadius));
        }
        [TestCase(1f)] [TestCase(-1f)]
        public void TouchlineRequiresEntireBall(float sign)
        {
            float plane = pitch.HalfWidth + pitch.BallRadius;
            Assert.That(BoundaryEvaluator.Evaluate(new Vec3(0f, 1f, 0f), new Vec3(sign * plane, 1f, 0f), pitch).HasCrossed, Is.False);
            Assert.That(BoundaryEvaluator.Evaluate(new Vec3(0f, 1f, 0f), new Vec3(sign * (plane + 0.001f), 1f, 0f), pitch).Kind, Is.EqualTo(BoundaryKind.Touchline));
        }
        [Test]
        public void CornerUsesEarliestPlane()
        {
            Assert.That(BoundaryEvaluator.Evaluate(new Vec3(0f, 1f, 0f), new Vec3(100f, 1f, 100f), pitch).Kind,
                Is.EqualTo(BoundaryKind.Touchline));
            Assert.That(BoundaryEvaluator.Evaluate(new Vec3(33f, 1f, 50f), new Vec3(36f, 1f, 60f), pitch).Kind,
                Is.EqualTo(BoundaryKind.GoalLine));
        }
        [Test]
        public void SweptTestFindsFastGoalWithoutTriggerOverlap()
        {
            Assert.That(BoundaryEvaluator.Evaluate(new Vec3(0f, 1f, 45f), new Vec3(0f, 1f, 70f), pitch).Kind,
                Is.EqualTo(BoundaryKind.Goal));
        }
        [Test]
        public void MovingBackInDoesNotScore()
        {
            Assert.That(BoundaryEvaluator.Evaluate(new Vec3(0f, 1f, 55f), new Vec3(0f, 1f, 50f), pitch).HasCrossed, Is.False);
        }
        [Test]
        public void NoCrossingForStationaryOrInsideBall()
        {
            Assert.That(BoundaryEvaluator.Evaluate(default, default, pitch).HasCrossed, Is.False);
            Assert.That(BoundaryEvaluator.Evaluate(default, new Vec3(10f, 20f, 10f), pitch).HasCrossed, Is.False);
        }
        [Test]
        public void TrackerLatchesUntilExplicitReset()
        {
            var tracker = new BoundaryTracker(pitch, new Vec3(0f, 1f, 50f));
            Assert.That(tracker.Step(new Vec3(0f, 1f, 54f)).Kind, Is.EqualTo(BoundaryKind.Goal));
            Assert.That(tracker.Step(new Vec3(0f, 1f, 50f)).HasCrossed, Is.False);
            Assert.That(tracker.Step(new Vec3(0f, 1f, 54f)).HasCrossed, Is.False);
            tracker.Reset(new Vec3(0f, 1f, 50f));
            Assert.That(tracker.Step(new Vec3(0f, 1f, 54f)).HasCrossed, Is.True);
        }
        [Test]
        public void ContactToleranceOnlyAffectsOpeningNotLineCrossing()
        {
            var tolerant = new PitchGeometry(105f, 68f, 7.32f, 2.44f, 0.11f, 0.002f);
            Assert.That(BoundaryEvaluator.Evaluate(new Vec3(0f, 0.109f, 52f), new Vec3(0f, 0.109f, 54f), tolerant).Kind,
                Is.EqualTo(BoundaryKind.Goal));
            Assert.That(BoundaryEvaluator.Evaluate(new Vec3(0f, 0.11f, 52f), new Vec3(0f, 0.11f, 52.61f), tolerant).HasCrossed, Is.False);
        }
        [Test]
        public void InvalidGeometryIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PitchGeometry(105f, 68f, 7.32f, 2.44f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PitchGeometry(105f, 2f, 7.32f, 2.44f, 0.11f));
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void NonFiniteGeometryIsRejected(float length)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PitchGeometry(length, 68f, 7.32f, 2.44f, 0.11f));
        }
    }

    public sealed class PhysicsTests
    {
        private readonly BallPhysicsParameters parameters = new BallPhysicsParameters(0.43f, 0.11f, 1.225f, 0.25f, 0.0015f, 0.015f);
        [Test]
        public void DragMatchesQuadraticFormula()
        {
            Vec3 acceleration = BallPhysics.Acceleration(new Vec3(0f, 0f, 20f), default, false, 9.81f, 1f / 60f, parameters);
            float expected = -0.5f * 1.225f * 0.25f * (float)Math.PI * 0.11f * 0.11f * 400f / 0.43f;
            Assert.That(acceleration.Z, Is.EqualTo(expected).Within(0.0001f));
            Vec3 doubled = BallPhysics.Acceleration(new Vec3(0f, 0f, 40f), default, false, 9.81f, 1f / 60f, parameters);
            Assert.That(doubled.Z, Is.EqualTo(acceleration.Z * 4f).Within(0.0001f));
        }
        [Test]
        public void MagnusDirectionReversesWithSpin()
        {
            Vec3 velocity = new Vec3(0f, 0f, 20f);
            Vec3 left = BallPhysics.Acceleration(velocity, new Vec3(0f, 40f, 0f), false, 9.81f, 1f / 60f, parameters);
            Vec3 right = BallPhysics.Acceleration(velocity, new Vec3(0f, -40f, 0f), false, 9.81f, 1f / 60f, parameters);
            Assert.That(left.X, Is.GreaterThan(0f));
            Assert.That(right.X, Is.EqualTo(-left.X));
            Assert.That(left.Z, Is.EqualTo(right.Z));
        }
        [TestCase(0.00001f)] [TestCase(1f)] [TestCase(32f)] [TestCase(10000f)]
        public void ResistanceNeverReversesVelocity(float speed)
        {
            float dt = 1f / 60f;
            Vec3 velocity = new Vec3(speed, 0f, 0f);
            Vec3 next = velocity + BallPhysics.Acceleration(velocity, default, true, 9.81f, dt, parameters) * dt;
            Assert.That(next.X, Is.GreaterThanOrEqualTo(-0.001f));
            Assert.That(next.Length, Is.LessThanOrEqualTo(speed + 0.001f));
        }
        [Test]
        public void RollingActsOnlyOnGroundAndOnHorizontalMotion()
        {
            Vec3 velocity = new Vec3(10f, 0.2f, 0f);
            Vec3 air = BallPhysics.Acceleration(velocity, default, false, 9.81f, 1f / 60f, parameters);
            Vec3 ground = BallPhysics.Acceleration(velocity, default, true, 9.81f, 1f / 60f, parameters);
            Assert.That(ground.X, Is.LessThan(air.X));
            Assert.That(ground.Y, Is.EqualTo(air.Y));
            Assert.That(BallPhysics.Acceleration(default, default, true, 9.81f, 1f / 60f, parameters).Length, Is.Zero);
        }
        [Test]
        public void FreeRollingEventuallyStops()
        {
            Vec3 velocity = new Vec3(12f, 0f, 0f);
            for (int i = 0; i < 36000; i++)
                velocity += BallPhysics.Acceleration(velocity, default, true, 9.81f, 1f / 60f, parameters) * (1f / 60f);
            Assert.That(velocity.Length, Is.LessThan(0.001f));
        }
        [Test]
        public void InvalidParametersAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BallPhysicsParameters(0f, 0.11f, 1f, 0.25f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => BallPhysics.Acceleration(default, default, false, 9.81f, 0f, parameters));
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void NonFinitePhysicsParametersAreRejected(float mass)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BallPhysicsParameters(mass, 0.11f, 1f, 0.25f, 0f, 0f));
        }
    }

    public sealed class InfrastructureTests
    {
        [Test]
        public void SeedIsReproducibleAndUnitRangeIsHalfOpen()
        {
            var a = new SeededRandom(2026u); var b = new SeededRandom(2026u);
            for (int i = 0; i < 10000; i++)
            {
                Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
                float sample = a.NextUnit();
                Assert.That(sample, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                Assert.That(sample, Is.EqualTo(b.NextUnit()));
            }
        }
        [Test]
        public void ZeroSeedHasDefinedNonZeroSequence()
        {
            var a = new SeededRandom(0u); var b = new SeededRandom(SeededRandom.NonZeroSeed);
            Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()).And.Not.Zero);
        }
        [Test]
        public void ClockTicksExactlySixtyTimesPerSimulatedSecond()
        {
            var clock = new SimulationClock(60);
            for (int i = 0; i < 36000; i++) clock.Advance();
            Assert.That(clock.Tick, Is.EqualTo(36000));
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(600d).Within(0.0001d));
        }
        [Test]
        public void ChannelDeduplicatesAndUnsubscribes()
        {
            var channel = new EventChannel<MatchEvent>(2);
            int called = 0; Action<MatchEvent> listener = value => called++;
            channel.Subscribe(listener); channel.Subscribe(listener); channel.Publish(default);
            Assert.That(called, Is.EqualTo(1));
            channel.Unsubscribe(listener); channel.Publish(default);
            Assert.That(called, Is.EqualTo(1));
        }
        [Test]
        public void ChannelCapacityAndDispatchMutationAreExplicit()
        {
            var channel = new EventChannel<MatchEvent>(1);
            Action<MatchEvent> listener = value => channel.Unsubscribe(null);
            channel.Subscribe(listener);
            Assert.Throws<InvalidOperationException>(() => channel.Subscribe(value => { }));
            Assert.Throws<InvalidOperationException>(() => channel.Publish(default));
            channel.Unsubscribe(listener);
            Assert.DoesNotThrow(() => channel.Publish(default));
        }
        [Test]
        public void SessionPublishesAndCountsExactlyOncePerLiveBall()
        {
            var channel = new EventChannel<MatchEvent>(2);
            int events = 0; long tick = 0;
            channel.Subscribe(value => { events++; tick = value.Tick; });
            var session = new MatchSession(60, new PitchGeometry(105f, 68f, 7.32f, 2.44f, 0.11f),
                new Vec3(0f, 1f, 50f), new SeededRandom(2026), channel);
            session.Step(new Vec3(0f, 1f, 54f)); session.Step(new Vec3(0f, 1f, 55f));
            Assert.That(events, Is.EqualTo(1)); Assert.That(tick, Is.EqualTo(1));
            Assert.That(session.GoalsAtPositiveEnd, Is.EqualTo(1)); Assert.That(session.BallInPlay, Is.False);
            session.ResetBall(new Vec3(0f, 1f, -50f)); session.Step(new Vec3(0f, 1f, -54f));
            Assert.That(session.GoalsAtNegativeEnd, Is.EqualTo(1));
            session.ResetBall(default); session.Step(new Vec3(40f, 1f, 0f));
            Assert.That(session.BallsOut, Is.EqualTo(1)); Assert.That(events, Is.EqualTo(3));
        }
    }
}
