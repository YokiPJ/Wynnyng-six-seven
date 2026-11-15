using System;
using NUnit.Framework;
using Wynnyng.Core;

namespace Wynnyng.HeadlessChecks
{
    public sealed class AllocationTests
    {
        [Test]
        public void SixtyThousandFootballTicksAllocateNoManagedMemoryAfterWarmup()
        {
            var match = new FootballMatch(new FootballParameters { MatchSeconds = 1200f },
                new PitchGeometry(105f, 68f, 7.32f, 2.44f, 0.11f), new SeededRandom(2026)) { CpuHome = true };
            var physics = new BallPhysicsParameters(0.43f, 0.11f, 1.225f, 0.25f, 0.0015f, 0.015f);
            Vec3 ball = new Vec3(0f, 0.14f, 0f), velocity = default, spin = default;
            for (int i = 0; i < 1000; i++) FootballTick(match, physics, ref ball, ref velocity, ref spin);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 60000; i++) FootballTick(match, physics, ref ball, ref velocity, ref spin);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(match.ElapsedSeconds, Is.GreaterThan(0f));
            Assert.That(float.IsFinite(ball.LengthSquared), Is.True);
        }
        private static void FootballTick(FootballMatch match, BallPhysicsParameters physics,
            ref Vec3 ball, ref Vec3 velocity, ref Vec3 spin)
        {
            const float dt = 1f / 60f;
            FootballBallAction action = match.Step(dt, ball, velocity, default, default, false);
            if (action.Teleport) { ball = action.Position; velocity = default; spin = default; }
            if (action.HasKick) { velocity = action.Velocity; spin = action.Spin; }
            bool ground = ball.Y <= 0.1101f && Math.Abs(velocity.Y) < 0.2f;
            Vec3 acceleration = BallPhysics.Acceleration(velocity, spin, ground, 9.81f, dt, physics);
            if (!ground) acceleration += new Vec3(0f, -9.81f, 0f);
            velocity += acceleration * dt; ball += velocity * dt;
            if (ball.Y < 0.11f)
            {
                ball = new Vec3(ball.X, 0.11f, ball.Z);
                velocity = new Vec3(velocity.X, Math.Abs(velocity.Y) > 0.2f ? Math.Abs(velocity.Y) * 0.65f : 0f, velocity.Z);
            }
        }
        [Test]
        public void SixtyThousandSimulationTicksAllocateNoManagedMemoryAfterWarmup()
        {
            var channel = new EventChannel<MatchEvent>(1);
            int called = 0;
            channel.Subscribe(value => called++);
            var session = new MatchSession(60, new PitchGeometry(105f, 68f, 7.32f, 2.44f, 0.11f),
                new Vec3(0f, 1f, 0f), new SeededRandom(2026), channel);
            var parameters = new BallPhysicsParameters(0.43f, 0.11f, 1.225f, 0.25f, 0.0015f, 0.015f);
            float witness = 0f;
            for (int i = 0; i < 1000; i++) witness += Tick(session, channel, parameters);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 60000; i++) witness += Tick(session, channel, parameters);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(called, Is.EqualTo(61000));
            Assert.That(witness, Is.GreaterThan(0f));
        }
        private static float Tick(MatchSession session, EventChannel<MatchEvent> channel, BallPhysicsParameters parameters)
        {
            session.Step(new Vec3(0f, 1f, 0f));
            Vec3 acceleration = BallPhysics.Acceleration(new Vec3(0f, 0f, 22f), new Vec3(0f, 40f, 0f), true, 9.81f, 1f / 60f, parameters);
            _ = session.Random.NextUnit();
            channel.Publish(default);
            return acceleration.X;
        }
    }
}
