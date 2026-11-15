using System;
using NUnit.Framework;
using Wynnyng.Core;

namespace Wynnyng.HeadlessChecks
{
    public sealed class AllocationTests
    {
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
