using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wynnyng.Configuration;
using Wynnyng.Core;
using Wynnyng.Runtime;
using Object = UnityEngine.Object;

namespace Wynnyng.Tests.PlayMode
{
    public sealed class FootballMatchAcceptanceTests
    {
        private GameObject root;
        private FootballBootstrap bootstrap;
        private SimulationConfig simulation;
        private PitchConfig pitch;
        private BallConfig ball;
        private PresentationConfig presentation;
        private FootballConfig football;
        private readonly WaitForFixedUpdate fixedFrame = new WaitForFixedUpdate();
        private float originalTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            FootballBootstrap[] matches = Object.FindObjectsByType<FootballBootstrap>(FindObjectsSortMode.None);
            MatchBootstrap[] laboratories = Object.FindObjectsByType<MatchBootstrap>(FindObjectsSortMode.None);
            for (int i = 0; i < matches.Length; i++) Object.Destroy(matches[i].gameObject);
            for (int i = 0; i < laboratories.Length; i++) Object.Destroy(laboratories[i].gameObject);
            if (matches.Length + laboratories.Length > 0) yield return null;
            originalTimeScale = Time.timeScale;
            simulation = ScriptableObject.CreateInstance<SimulationConfig>(); simulation.EnableLogging = false;
            pitch = ScriptableObject.CreateInstance<PitchConfig>();
            ball = ScriptableObject.CreateInstance<BallConfig>();
            presentation = ScriptableObject.CreateInstance<PresentationConfig>();
            football = ScriptableObject.CreateInstance<FootballConfig>();
            football.Gameplay.KickoffSeconds = 0.05f;
            root = new GameObject("Football Acceptance Fixture");
            bootstrap = root.AddComponent<FootballBootstrap>();
            bootstrap.Configure(simulation, pitch, ball, presentation, football, false, false);
            bootstrap.Initialize();
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
            Time.timeScale = originalTimeScale;
            Object.Destroy(simulation); Object.Destroy(pitch); Object.Destroy(ball);
            Object.Destroy(presentation); Object.Destroy(football);
            yield return null;
        }
        private IEnumerator WaitForLiveBall()
        {
            for (int i = 0; i < 60 && bootstrap.Match.Phase != FootballPhase.Playing; i++) yield return fixedFrame;
            Assert.That(bootstrap.Match.Phase, Is.EqualTo(FootballPhase.Playing));
        }
        [UnityTest]
        public IEnumerator MatchCreatesTwentyTwoInterpolatedPlayersAndIndependentContinuousBall()
        {
            Assert.That(bootstrap.IsInitialized, Is.True);
            Assert.That(bootstrap.PlayerBodies.Length, Is.EqualTo(22));
            int keepers = 0;
            for (int i = 0; i < bootstrap.PlayerBodies.Length; i++)
            {
                Rigidbody player = bootstrap.PlayerBodies[i];
                Assert.That(player.isKinematic, Is.True);
                Assert.That(player.interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate));
                Assert.That(player.GetComponent<CapsuleCollider>(), Is.Not.Null);
                if (bootstrap.Match.Players[i].IsGoalkeeper) keepers++;
            }
            Assert.That(keepers, Is.EqualTo(2));
            Assert.That(bootstrap.Ball.Rigidbody.isKinematic, Is.False);
            Assert.That(bootstrap.Ball.Rigidbody.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
            yield return fixedFrame;
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator CpuTeamsMoveAndKeepTheRealBallFinite()
        {
            bootstrap.SetCpuHome(true);
            yield return WaitForLiveBall();
            Vector3 before = bootstrap.PlayerBodies[9].position;
            for (int i = 0; i < 180; i++) yield return fixedFrame;
            Assert.That(bootstrap.Match.ElapsedSeconds, Is.GreaterThan(2f));
            Assert.That((bootstrap.PlayerBodies[9].position - before).sqrMagnitude, Is.GreaterThan(0.01f));
            Vector3 position = bootstrap.Ball.Rigidbody.position;
            Assert.That(float.IsFinite(position.x + position.y + position.z), Is.True);
            Assert.That(bootstrap.Match.HomeStats.PossessionSeconds + bootstrap.Match.AwayStats.PossessionSeconds, Is.GreaterThan(0f));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator CrossingScoresOnceAndRestartClearsScore()
        {
            yield return WaitForLiveBall();
            bootstrap.Ball.Teleport(new Vec3(0f, 0.5f, 54f));
            for (int i = 0; i < 3; i++) yield return fixedFrame;
            Assert.That(bootstrap.Match.HomeGoals, Is.EqualTo(1));
            Assert.That(bootstrap.Match.AwayGoals, Is.Zero);
            for (int i = 0; i < 20; i++) yield return fixedFrame;
            Assert.That(bootstrap.Match.HomeGoals, Is.EqualTo(1));
            bootstrap.Restart();
            Assert.That(bootstrap.Match.HomeGoals + bootstrap.Match.AwayGoals, Is.Zero);
            Assert.That(bootstrap.Match.ElapsedSeconds, Is.Zero);
            Assert.That(bootstrap.Ball.Rigidbody.position.z, Is.Zero.Within(0.001f));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator PauseFreezesMatchAndDestructionRestoresGlobalTime()
        {
            yield return WaitForLiveBall();
            bootstrap.TogglePause();
            Assert.That(Time.timeScale, Is.Zero);
            float elapsed = bootstrap.Match.ElapsedSeconds;
            Vector3 position = bootstrap.Ball.Rigidbody.position;
            yield return null; yield return null;
            Assert.That(bootstrap.Match.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(bootstrap.Ball.Rigidbody.position, Is.EqualTo(position));
            Object.Destroy(root); root = null;
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator RuntimeTuningAndRestartPreserveOriginalConfig()
        {
            float original = football.Gameplay.JogSpeed;
            bootstrap.Match.Parameters.JogSpeed = original * 0.8f;
            bootstrap.Match.Parameters.Formation[1].Width = 0f;
            bootstrap.Restart();
            Assert.That(football.Gameplay.JogSpeed, Is.EqualTo(original));
            Assert.That(football.Gameplay.Formation[1].Width, Is.EqualTo(-0.78f));
            Assert.That(bootstrap.Match.Parameters.JogSpeed, Is.EqualTo(original * 0.8f));
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator CompletePresentationHasPlayersCameraAndWorkingPauseMenu()
        {
            Object.Destroy(root); yield return null;
            root = new GameObject("Football Presentation Fixture");
            bootstrap = root.AddComponent<FootballBootstrap>();
            bootstrap.Configure(simulation, pitch, ball, presentation, football, true, true);
            bootstrap.Initialize();
            yield return null;
            Assert.That(bootstrap.Hud, Is.Not.Null);
            Assert.That(bootstrap.Hud.IsInitialized, Is.True);
            Assert.That(bootstrap.PlayerBodies[9].GetComponentInChildren<MeshRenderer>(), Is.Not.Null);
            Camera camera = root.GetComponentInChildren<Camera>();
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.WorldToViewportPoint(bootstrap.Ball.transform.position).z, Is.GreaterThan(camera.nearClipPlane));
            bootstrap.Hud.ToggleMenu();
            Assert.That(bootstrap.Hud.MenuVisible && bootstrap.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            bootstrap.Hud.CloseMenu();
            Assert.That(bootstrap.Hud.MenuVisible || bootstrap.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PoweredCornerShotScoresWithTheRealBallAndActiveGoalkeeper()
        {
            yield return WaitForLiveBall();
            bootstrap.enabled = false;
            SimulationMode previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                FootballMatch match = bootstrap.Match;
                Vec3 shotPosition = new Vec3(0f, ball.Radius + ball.ResetClearance, pitch.Length * 0.5f - 18f);
                for (int i = 0; i < match.Players.Length; i++)
                {
                    if (!match.Players[i].IsGoalkeeper)
                        match.Players[i].Position = new Vec3(i % 2 == 0 ? -30f : 30f, 0f, -20f + i);
                    match.Players[i].Velocity = default;
                }
                match.Players[9].Position = shotPosition - new Vec3(0f, shotPosition.Y, 0.8f);
                match.Players[9].Facing = new Vec3(0f, 0f, 1f);
                for (int i = 0; i < bootstrap.PlayerBodies.Length; i++)
                    bootstrap.PlayerBodies[i].position = BallBody.ToUnity(match.Players[i].Position);
                bootstrap.Ball.Teleport(shotPosition);
                Physics.SyncTransforms();
                ManualPhysicsStep(new PlayerCommand { ShootPressed = true, ShootHeld = true });
                int chargeSteps = Mathf.CeilToInt(match.Parameters.ChargeSeconds * simulation.FixedFrequency);
                for (int i = 0; i < chargeSteps; i++) ManualPhysicsStep(new PlayerCommand { ShootHeld = true });
                FootballBallAction shot = ManualPhysicsStep(new PlayerCommand { ShootReleased = true, MoveX = 0.3f, MoveY = 1f });
                Assert.That(shot.Kind, Is.EqualTo(FootballKickKind.Shot));
                for (int i = 0; i < 240 && match.Phase == FootballPhase.Playing; i++) ManualPhysicsStep(default);
                Assert.That(match.HomeGoals, Is.EqualTo(1), "The physical ball must enter the open goal with its goalkeeper still active.");
                Assert.That(match.AwayGoals, Is.Zero);
            }
            finally { Physics.simulationMode = previousMode; }
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
        private FootballBallAction ManualPhysicsStep(PlayerCommand command)
        {
            float dt = 1f / simulation.FixedFrequency;
            FootballBallAction action = bootstrap.Match.Step(dt, bootstrap.Ball.Position, bootstrap.Ball.Velocity,
                command, default, false);
            for (int i = 0; i < bootstrap.PlayerBodies.Length; i++)
                bootstrap.PlayerBodies[i].MovePosition(BallBody.ToUnity(bootstrap.Match.Players[i].Position));
            if (action.Teleport) bootstrap.Ball.Teleport(action.Position);
            if (action.HasKick) bootstrap.Ball.Launch(action.Velocity, action.Spin);
            bootstrap.Ball.FixedStep(dt);
            Physics.Simulate(dt);
            return action;
        }
    }
}
