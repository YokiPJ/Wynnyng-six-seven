using System;
using NUnit.Framework;
using Wynnyng.Core;

namespace Wynnyng.Tests.EditMode
{
    /// <summary>Released shots travel to the goal under the shared ball forces, with an active goalkeeper.</summary>
    public sealed class FinishingTests
    {
        private const float Dt = 1f / 60f;
        private const float Radius = 0.11f;
        private static readonly PitchGeometry Pitch = new PitchGeometry(105f, 68f, 7.32f, 2.44f, Radius);
        private static readonly BallPhysicsParameters Physics = new BallPhysicsParameters(
            0.43f, Radius, 1.225f, 0.25f, 0.0015f, 0.015f);

        [TestCase(-0.3f, false)]
        [TestCase(0.3f, false)]
        [TestCase(-0.3f, true)]
        [TestCase(0.3f, true)]
        public void AimedPowerShotFromEighteenMetresCanBeatTheDefaultGoalkeeper(float lateral, bool secondHalf)
        {
            FootballMatch match = Create(secondHalf);
            Vec3 ball = AttackingPoint(match, 18f);
            FootballBallAction shot = ChargeAndRelease(match, ball, lateral, match.AttackSign(0));
            Assert.That(shot.Kind, Is.EqualTo(FootballKickKind.Shot));
            Assert.That(shot.Teleport, Is.False);
            Fly(match, ball, shot.Velocity, shot.Spin);
            Assert.That(match.HomeGoals, Is.EqualTo(1),
                "An aimed shot must cross the goal line under ball physics while the default goalkeeper is active.");
            Assert.That(match.AwayGoals, Is.Zero);
        }

        [Test]
        public void StationaryShooterCanFinishWithoutHavingToSteerDuringTheShot()
        {
            FootballMatch match = Create();
            Vec3 ball = AttackingPoint(match, 12f);
            FootballBallAction shot = ChargeAndRelease(match, ball, 0f, 0f);
            Assert.That(shot.Kind, Is.EqualTo(FootballKickKind.Shot));
            Fly(match, ball, shot.Velocity, shot.Spin);
            Assert.That(match.HomeGoals, Is.EqualTo(1), "No-stick assistance must provide a viable finish rather than force every shot at the keeper.");
        }

        [Test]
        public void PointBlankPowerShotCanBeatTheDefaultKeeperBeforeHisDive()
        {
            FootballMatch match = Create();
            Vec3 ball = AttackingPoint(match, 6f);
            FootballBallAction shot = ChargeAndRelease(match, ball, 0.3f, match.AttackSign(0));
            Assert.That(shot.Kind, Is.EqualTo(FootballKickKind.Shot));
            Fly(match, ball, shot.Velocity, shot.Spin);
            Assert.That(match.HomeGoals, Is.EqualTo(1), "A keeper must not instantly extend across a point-blank corner shot.");
        }

        [Test]
        public void GoalkeeperStillSavesAWeakShotStraightAtHisBody()
        {
            FootballMatch match = Create();
            Vec3 ball = AttackingPoint(match, 18f);
            ParkOutfielders(match, ball, false);
            Fly(match, ball, new Vec3(0f, 0f, 18f), default, stopAtSave: true);
            Assert.That(match.AwayStats.Saves, Is.GreaterThan(0));
            Assert.That(match.HomeGoals, Is.Zero);
        }

        [Test]
        public void GoalkeeperCannotInstantlyCollectABallMoreThanTwoMetresFromHisBody()
        {
            FootballMatch match = Create();
            FootballPlayer keeper = match.Players[FootballMatch.PlayersPerTeam];
            Vec3 ball = keeper.Position + new Vec3(2.25f, 0.7f, 0f);
            ParkOutfielders(match, ball, false);
            FootballBallAction action = match.Step(Dt, ball, new Vec3(0f, 0f, 30f), default, default, false);
            Assert.That(action.Kind, Is.Not.EqualTo(FootballKickKind.Save));
            Assert.That(match.AwayStats.Saves, Is.Zero);
        }

        [Test]
        public void GoalkeeperCannotUseDiveReachToVacuumAStationaryBall()
        {
            FootballMatch match = Create();
            FootballPlayer keeper = match.Players[FootballMatch.PlayersPerTeam];
            Vec3 ball = keeper.Position + new Vec3(1f, 0.14f, 0f);
            ParkOutfielders(match, ball, false);
            FootballBallAction action = match.Step(Dt, ball, default, default, default, false);
            Assert.That(action.HasKick, Is.False, "Stationary control must stay near the keeper's body, not use the dive envelope.");
        }

        [Test]
        public void GoalkeeperDoesNotPullBackAFastBallMovingAwayFromHisGoal()
        {
            FootballMatch match = Create();
            FootballPlayer keeper = match.Players[FootballMatch.PlayersPerTeam];
            Vec3 ball = keeper.Position + new Vec3(0f, 0.7f, -0.3f);
            ParkOutfielders(match, ball, false);
            FootballBallAction action = match.Step(Dt, ball, new Vec3(0f, 0f, -30f), default, default, false);
            Assert.That(action.HasKick, Is.False, "A keeper must not remotely reverse a fast projectile leaving his contact area.");
        }

        [Test]
        public void AShotOutsideThePostsDoesNotBecomeAnAutomaticGoal()
        {
            FootballMatch match = Create();
            Vec3 ball = AttackingPoint(match, 18f) + new Vec3(6f, 0f, 0f);
            ParkOutfielders(match, ball, false);
            Fly(match, ball, new Vec3(0f, 2f, 32f), default);
            Assert.That(match.HomeGoals + match.AwayGoals, Is.Zero);
            Assert.That(match.Phase, Is.EqualTo(FootballPhase.Restart));
        }

        private static FootballMatch Create(bool secondHalf = false)
        {
            var match = new FootballMatch(new FootballParameters { MatchSeconds = secondHalf ? 10f : 300f },
                Pitch, new SeededRandom(2026));
            Vec3 centre = new Vec3(0f, 0.14f, 0f);
            for (int i = 0; i < 2000; i++)
            {
                if (match.Phase == FootballPhase.Playing && (!secondHalf || match.Half == 2)) return match;
                match.Step(Dt, centre, default, default, default, false);
            }
            Assert.Fail("Could not enter the requested half.");
            return match;
        }

        private static Vec3 AttackingPoint(FootballMatch match, float metresToGoal) =>
            new Vec3(0f, 0.14f, (Pitch.HalfLength - metresToGoal) * match.AttackSign(0));

        private static void ParkOutfielders(FootballMatch match, Vec3 ball, bool atShootersFeet = true)
        {
            for (int i = 0; i < match.Players.Length; i++)
            {
                FootballPlayer player = match.Players[i];
                if (player.IsGoalkeeper) continue;
                player.Position = new Vec3(i % 2 == 0 ? -30f : 30f, 0f, -20f + i);
                player.Velocity = default;
            }
            if (!atShootersFeet) return;
            FootballPlayer shooter = match.Players[9];
            shooter.Position = new Vec3(ball.X, 0f, ball.Z - match.AttackSign(0) * 0.8f);
            shooter.Facing = new Vec3(0f, 0f, match.AttackSign(0));
        }

        private static FootballBallAction ChargeAndRelease(FootballMatch match, Vec3 ball, float lateral, float forward)
        {
            ParkOutfielders(match, ball);
            // Keep the ball at the stationary shooter's feet while charging. Only the
            // released projectile is integrated; it is never moved beyond a line by the test.
            match.Step(Dt, ball, default, new PlayerCommand { ShootPressed = true, ShootHeld = true }, default, false);
            int chargeSteps = (int)Math.Ceiling(match.Parameters.ChargeSeconds / Dt);
            for (int i = 0; i < chargeSteps; i++)
                match.Step(Dt, ball, default, new PlayerCommand { ShootHeld = true }, default, false);
            Assert.That(match.Charge01, Is.GreaterThan(0.95f));
            return match.Step(Dt, ball, default,
                new PlayerCommand { ShootReleased = true, MoveX = lateral, MoveY = forward }, default, false);
        }

        private static void Fly(FootballMatch match, Vec3 ball, Vec3 velocity, Vec3 spin, bool stopAtSave = false)
        {
            int previousSaves = match.AwayStats.Saves;
            for (int i = 0; i < 240 && match.Phase == FootballPhase.Playing; i++)
            {
                bool grounded = ball.Y <= Radius + 0.0001f && Math.Abs(velocity.Y) < 0.2f;
                Vec3 acceleration = BallPhysics.Acceleration(velocity, spin, grounded, 9.81f, Dt, Physics);
                if (!grounded) acceleration += new Vec3(0f, -9.81f, 0f);
                velocity += acceleration * Dt;
                ball += velocity * Dt;
                if (ball.Y < Radius)
                {
                    ball = new Vec3(ball.X, Radius, ball.Z);
                    velocity = new Vec3(velocity.X, Math.Abs(velocity.Y) > 0.2f ? Math.Abs(velocity.Y) * 0.65f : 0f, velocity.Z);
                }
                FootballBallAction action = match.Step(Dt, ball, velocity, default, default, false);
                if (stopAtSave && match.AwayStats.Saves > previousSaves) return;
                if (action.Teleport) { ball = action.Position; velocity = default; spin = default; }
                if (action.HasKick) { velocity = action.Velocity; spin = action.Spin; }
            }
        }
    }
}
