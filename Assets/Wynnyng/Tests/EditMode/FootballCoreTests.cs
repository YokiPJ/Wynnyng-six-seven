using System;
using NUnit.Framework;
using Wynnyng.Core;

namespace Wynnyng.Tests.EditMode
{
    public sealed class FootballCoreTests
    {
        private static readonly PitchGeometry Pitch = new PitchGeometry(105f, 68f, 7.32f, 2.44f, 0.11f);
        private const float Dt = 1f / 60f;
        private static readonly Vec3 Centre = new Vec3(0f, 0.14f, 0f);
        private static FootballMatch Create(FootballParameters parameters = null) =>
            new FootballMatch(parameters ?? new FootballParameters(), Pitch, new SeededRandom(2026));
        private static void EnterPlaying(FootballMatch match)
        {
            for (int i = 0; i < 300 && match.Phase != FootballPhase.Playing; i++)
                match.Step(Dt, Centre, default, default, default, false);
            Assert.That(match.Phase, Is.EqualTo(FootballPhase.Playing));
        }
        private static void SpreadForAction(FootballMatch match)
        {
            for (int i = 0; i < FootballMatch.PlayerCount; i++)
            {
                match.Players[i].Position = new Vec3(i < 11 ? -25f : 25f, 0f, -30f + i);
                match.Players[i].Velocity = default;
            }
            match.Players[9].Position = new Vec3(0f, 0f, -0.8f);
            match.Players[9].Facing = new Vec3(0f, 0f, 1f);
        }
        [Test]
        public void BuildsTwoDistinctElevenPlayerTeamsAndTwoKeepers()
        {
            FootballMatch match = Create();
            Assert.That(match.Players.Length, Is.EqualTo(22));
            int keepers = 0;
            for (int i = 0; i < match.Players.Length; i++)
            {
                Assert.That(match.Players[i].Team, Is.EqualTo(i / 11));
                Assert.That(match.Players[i].Number, Is.EqualTo(i % 11 + 1));
                if (match.Players[i].IsGoalkeeper) keepers++;
            }
            Assert.That(keepers, Is.EqualTo(2));
            Assert.That(match.Players[0].Position.Z, Is.LessThan(-49f));
            Assert.That(match.Players[11].Position.Z, Is.GreaterThan(49f));
        }
        [Test]
        public void RuntimeParameterCloneDoesNotMutateFormationAsset()
        {
            var original = new FootballParameters();
            FootballParameters clone = original.Clone();
            clone.Formation[1].Width = 0.1f; clone.JogSpeed = 4f;
            Assert.That(original.Formation[1].Width, Is.EqualTo(-0.78f));
            Assert.That(original.JogSpeed, Is.EqualTo(5.2f));
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(0f)] [TestCase(-1f)]
        public void RejectsInvalidScalarTuning(float value)
        {
            var parameters = new FootballParameters { AiPressureRadius = value };
            Assert.Throws<ArgumentOutOfRangeException>(() => parameters.Validate());
        }
        [Test]
        public void RejectsBrokenFormationAndSpeedBounds()
        {
            var parameters = new FootballParameters { Formation = new FootballFormationSlot[2] };
            Assert.Throws<ArgumentException>(() => parameters.Validate());
            parameters = new FootballParameters { JogSpeed = 15f, SprintSpeed = 5f };
            Assert.Throws<ArgumentException>(() => parameters.Validate());
        }
        [Test]
        public void KickoffTeleportsToCentreThenStartsAfterCountdown()
        {
            FootballMatch match = Create();
            FootballBallAction first = match.Step(Dt, Centre, default, default, default, false);
            Assert.That(first.Teleport, Is.True);
            Assert.That(match.Phase, Is.EqualTo(FootballPhase.Kickoff));
            Assert.That(match.ElapsedSeconds, Is.Zero);
            Assert.That(match.ControlledPlayer, Is.EqualTo(9));
            EnterPlaying(match);
            match.Step(Dt, Centre, default, default, default, false);
            Assert.That(match.ElapsedSeconds, Is.GreaterThan(0f));
        }
        [Test]
        public void HumanAccelerationIsLimitedAndSprintConsumesStamina()
        {
            FootballMatch match = Create(); EnterPlaying(match); SpreadForAction(match);
            var command = new PlayerCommand { MoveY = 1f, Sprint = 1f };
            match.Step(Dt, Centre, default, command, default, false);
            FootballPlayer player = match.Players[9];
            Assert.That(player.Velocity.Length, Is.EqualTo(match.Parameters.Acceleration * Dt).Within(0.0001f));
            Assert.That(player.Stamina, Is.LessThan(1f));
            Assert.That(player.Position.Z, Is.GreaterThan(-0.8f));
        }
        [Test]
        public void PassIsAssistedToTeammateAndCountsCompletionOnce()
        {
            FootballMatch match = Create(); EnterPlaying(match); SpreadForAction(match);
            match.Players[10].Position = new Vec3(0f, 0f, 12f);
            FootballBallAction action = match.Step(Dt, Centre, default,
                new PlayerCommand { PassPressed = true, MoveY = 1f }, default, false);
            Assert.That(action.Kind, Is.EqualTo(FootballKickKind.Pass));
            Assert.That(action.Velocity.Z, Is.GreaterThan(0f));
            Assert.That(Math.Abs(action.Velocity.X), Is.LessThan(1f));
            Assert.That(match.HomeStats.Passes, Is.EqualTo(1));
            Vec3 reception = match.Players[10].Position + new Vec3(0f, 0.14f, 0f);
            match.Step(Dt, reception, default, default, default, false);
            Assert.That(match.HomeStats.CompletedPasses, Is.EqualTo(1));
            match.Step(Dt, reception, default, default, default, false);
            Assert.That(match.HomeStats.CompletedPasses, Is.EqualTo(1));
            Assert.That(match.ControlledPlayer, Is.EqualTo(10));
        }
        [TestCase(false)] [TestCase(true)]
        public void ThroughAndLobProvideDifferentBallTrajectories(bool lob)
        {
            FootballMatch match = Create(); EnterPlaying(match); SpreadForAction(match);
            match.Players[10].Position = new Vec3(0f, 0f, 15f);
            FootballBallAction action = match.Step(Dt, Centre, default,
                new PlayerCommand { ThroughPressed = !lob, LobPressed = lob, MoveY = 1f }, default, false);
            Assert.That(action.Kind, Is.EqualTo(lob ? FootballKickKind.Lob : FootballKickKind.Through));
            Assert.That(new Vec3(action.Velocity.X, 0f, action.Velocity.Z).Length, Is.GreaterThanOrEqualTo(match.Parameters.PassMinSpeed - 0.001f));
            Assert.That(action.Velocity.Y, Is.EqualTo(lob ? match.Parameters.LobVerticalSpeed : 0f));
        }
        [Test]
        public void ShootingNeedsLivePressAndCancellationPreventsPhantomRelease()
        {
            FootballMatch match = Create(); EnterPlaying(match); SpreadForAction(match);
            FootballBallAction action = match.Step(Dt, Centre, default, new PlayerCommand { ShootReleased = true }, default, false);
            Assert.That(action.Kind, Is.Not.EqualTo(FootballKickKind.Shot));
            match.Step(Dt, Centre, default, new PlayerCommand { ShootPressed = true, ShootHeld = true }, default, false);
            Assert.That(match.Charge01, Is.GreaterThan(0f));
            match.CancelCharges();
            action = match.Step(Dt, Centre, default, new PlayerCommand { ShootReleased = true }, default, false);
            Assert.That(action.Kind, Is.Not.EqualTo(FootballKickKind.Shot));
            Assert.That(match.HomeStats.Shots, Is.Zero);
        }
        [Test]
        public void ChargedShotCreatesPowerfulFreeBallWithoutTeleporting()
        {
            FootballMatch match = Create(); EnterPlaying(match); SpreadForAction(match);
            match.Step(Dt, Centre, default, new PlayerCommand { ShootPressed = true, ShootHeld = true }, default, false);
            for (int i = 0; i < 40; i++) match.Step(Dt, Centre, default, new PlayerCommand { ShootHeld = true }, default, false);
            FootballBallAction action = match.Step(Dt, Centre, default,
                new PlayerCommand { ShootReleased = true, MoveY = 1f }, default, false);
            Assert.That(action.Kind, Is.EqualTo(FootballKickKind.Shot));
            Assert.That(action.Teleport, Is.False);
            Assert.That(action.Velocity.Length, Is.GreaterThan(25f));
            Assert.That(match.HomeStats.Shots, Is.EqualTo(1));
            Assert.That(match.BallOwner, Is.EqualTo(-1));
        }
        [Test]
        public void ManualSwitchChoosesAnotherNearbyOutfielder()
        {
            FootballMatch match = Create(); EnterPlaying(match); SpreadForAction(match);
            match.Players[10].Position = new Vec3(0.1f, 0f, 0f);
            int before = match.ControlledPlayer;
            match.Step(Dt, new Vec3(0f, 4f, 0f), default, new PlayerCommand { SwitchPressed = true }, default, false);
            Assert.That(match.ControlledPlayer, Is.EqualTo(10));
            Assert.That(match.ControlledPlayer, Is.Not.EqualTo(before));
        }
        [Test]
        public void TackleClearsBallAndIsCounted()
        {
            FootballMatch match = Create(); EnterPlaying(match); SpreadForAction(match);
            match.Players[9].Position = new Vec3(0f, 0f, -1.5f);
            match.Players[20].Position = new Vec3(0f, 0f, 0.2f);
            FootballBallAction action = match.Step(Dt, Centre, default,
                new PlayerCommand { ShootPressed = true }, default, false);
            Assert.That(action.Kind, Is.EqualTo(FootballKickKind.Tackle));
            Assert.That(match.HomeStats.Tackles, Is.EqualTo(1));
        }
        [Test]
        public void GoalkeeperParriesIncomingBallAwayFromOwnGoal()
        {
            FootballMatch match = Create(); EnterPlaying(match);
            FootballBallAction action = match.Step(Dt, new Vec3(0f, 0.7f, -50f), new Vec3(0f, 0f, -30f), default, default, false);
            Assert.That(action.Kind, Is.EqualTo(FootballKickKind.Save));
            Assert.That(action.Velocity.Z, Is.GreaterThan(0f));
            Assert.That(match.HomeStats.Saves, Is.EqualTo(1));
        }
        [TestCase(1f)] [TestCase(-1f)]
        public void GoalScoresOnceAndWaitsForRestart(float end)
        {
            FootballMatch match = Create(); EnterPlaying(match);
            Vec3 scored = new Vec3(0f, 0.5f, end * 54f);
            match.Step(Dt, scored, default, default, default, false);
            Assert.That(match.HomeGoals, Is.EqualTo(end > 0f ? 1 : 0));
            Assert.That(match.AwayGoals, Is.EqualTo(end < 0f ? 1 : 0));
            for (int i = 0; i < 60; i++) match.Step(Dt, scored, default, default, default, false);
            Assert.That(match.HomeGoals + match.AwayGoals, Is.EqualTo(1));
            Assert.That(match.Phase, Is.EqualTo(FootballPhase.Goal));
        }
        [Test]
        public void OutRestartsInsidePitchWithoutAnExtraCrossing()
        {
            FootballMatch match = Create(); EnterPlaying(match);
            FootballBallAction action = match.Step(Dt, new Vec3(36f, 0.2f, 0f), default, default, default, false);
            Assert.That(action.Teleport, Is.True);
            Assert.That(action.Position.X, Is.LessThan(34f));
            Assert.That(match.Phase, Is.EqualTo(FootballPhase.Restart));
            Vec3 restart = action.Position;
            for (int i = 0; i < 200 && match.Phase != FootballPhase.Playing; i++)
                match.Step(Dt, restart, default, default, default, false);
            match.Step(Dt, restart, default, default, default, false);
            Assert.That(match.Phase, Is.EqualTo(FootballPhase.Playing));
            Assert.That(match.HomeGoals + match.AwayGoals, Is.Zero);
        }
        [Test]
        public void MatchHasTwoHalvesSwapsEndsAndStopsAtFullTime()
        {
            FootballMatch match = Create(new FootballParameters { MatchSeconds = 10f });
            int originalDirection = match.AttackSign(0);
            bool sawHalfTime = false;
            for (int i = 0; i < 2000 && match.Phase != FootballPhase.FullTime; i++)
            {
                match.Step(Dt, Centre, default, default, default, false);
                sawHalfTime |= match.Phase == FootballPhase.HalfTime;
            }
            Assert.That(sawHalfTime, Is.True);
            Assert.That(match.Half, Is.EqualTo(2));
            Assert.That(match.AttackSign(0), Is.EqualTo(-originalDirection));
            Assert.That(match.Phase, Is.EqualTo(FootballPhase.FullTime));
            Assert.That(match.ElapsedSeconds, Is.EqualTo(10f));
            Assert.That(match.RemainingSeconds, Is.Zero);
        }
        [Test]
        public void RestartMatchClearsScoreStatisticsAndRestoresStamina()
        {
            FootballMatch match = Create(); EnterPlaying(match);
            match.Step(Dt, new Vec3(0f, 0.5f, 54f), default, default, default, false);
            match.Players[9].Stamina = 0.2f;
            match.RestartMatch();
            Assert.That(match.HomeGoals + match.AwayGoals, Is.Zero);
            Assert.That(match.ElapsedSeconds, Is.Zero);
            Assert.That(match.Players[9].Stamina, Is.EqualTo(1f));
            Assert.That(match.Phase, Is.EqualTo(FootballPhase.Kickoff));
        }
        [Test]
        public void AiVsAiRunsTenMinutesWithFinitePositionsAndCoherentStatistics()
        {
            FootballMatch match = Create(new FootballParameters { MatchSeconds = 1200f });
            match.CpuHome = true;
            Vec3 ball = Centre, velocity = default, spin = default;
            var physics = new BallPhysicsParameters(0.43f, 0.11f, 1.225f, 0.25f, 0.0015f, 0.015f);
            for (int i = 0; i < 600 * 60; i++)
            {
                FootballBallAction action = match.Step(Dt, ball, velocity, default, default, false);
                if (action.Teleport) { ball = action.Position; velocity = default; spin = default; }
                if (action.HasKick) { velocity = action.Velocity; spin = action.Spin; }
                bool ground = ball.Y <= 0.1101f && Math.Abs(velocity.Y) < 0.2f;
                Vec3 acceleration = BallPhysics.Acceleration(velocity, spin, ground, 9.81f, Dt, physics);
                if (!ground) acceleration += new Vec3(0f, -9.81f, 0f);
                velocity += acceleration * Dt;
                ball += velocity * Dt;
                if (ball.Y < 0.11f)
                {
                    ball = new Vec3(ball.X, 0.11f, ball.Z);
                    velocity = new Vec3(velocity.X, Math.Abs(velocity.Y) > 0.2f ? Math.Abs(velocity.Y) * 0.65f : 0f, velocity.Z);
                }
                Assert.That(float.IsFinite(ball.X + ball.Y + ball.Z), Is.True);
            }
            TestContext.WriteLine("CPU 600s: goals=" + match.HomeGoals + ":" + match.AwayGoals +
                "; shots=" + match.HomeStats.Shots + ":" + match.AwayStats.Shots +
                "; passes=" + match.HomeStats.Passes + ":" + match.AwayStats.Passes +
                "; saves=" + match.HomeStats.Saves + ":" + match.AwayStats.Saves +
                "; possession seconds=" + match.HomeStats.PossessionSeconds + ":" + match.AwayStats.PossessionSeconds);
            for (int i = 0; i < match.Players.Length; i++)
            {
                FootballPlayer player = match.Players[i];
                Assert.That(float.IsFinite(player.Position.X + player.Position.Z), Is.True);
                Assert.That(Math.Abs(player.Position.X), Is.LessThan(34f));
                Assert.That(Math.Abs(player.Position.Z), Is.LessThan(52.5f));
                Assert.That(player.Stamina, Is.InRange(0f, 1f));
            }
            Assert.That(match.HomeStats.Shots + match.AwayStats.Shots, Is.GreaterThan(0));
            Assert.That(match.HomeStats.Passes + match.AwayStats.Passes, Is.GreaterThan(0));
            Assert.That(match.HomeStats.PossessionSeconds, Is.GreaterThan(0f));
            Assert.That(match.AwayStats.PossessionSeconds, Is.GreaterThan(0f));
            Assert.That(match.HomeStats.CompletedPasses, Is.LessThanOrEqualTo(match.HomeStats.Passes));
            Assert.That(match.AwayStats.CompletedPasses, Is.LessThanOrEqualTo(match.AwayStats.Passes));

        }
    }
}
