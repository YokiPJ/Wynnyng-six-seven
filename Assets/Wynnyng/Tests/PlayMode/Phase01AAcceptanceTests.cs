using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Wynnyng.Configuration;
using Wynnyng.Core;
using Wynnyng.Presentation;
using Wynnyng.Runtime;

namespace Wynnyng.Tests.PlayMode
{
    public sealed class Phase01AAcceptanceTests
    {
        private GameObject root;
        private MatchBootstrap bootstrap;
        private SimulationConfig simulation;
        private PitchConfig pitch;
        private BallConfig ball;
        private PresentationConfig presentation;
        private readonly WaitForFixedUpdate fixedFrame = new WaitForFixedUpdate();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Tests own their laboratory and work even when the user starts from Phase01A.
            MatchBootstrap[] previous = Object.FindObjectsByType<MatchBootstrap>(FindObjectsSortMode.None);
            for (int i = 0; i < previous.Length; i++) Object.Destroy(previous[i].gameObject);
            if (previous.Length > 0) yield return null;
            simulation = ScriptableObject.CreateInstance<SimulationConfig>(); simulation.EnableLogging = false;
            pitch = ScriptableObject.CreateInstance<PitchConfig>();
            ball = ScriptableObject.CreateInstance<BallConfig>();
            presentation = ScriptableObject.CreateInstance<PresentationConfig>(); presentation.AutoResetDelay = 100f;
            root = new GameObject("Acceptance Fixture");
            bootstrap = root.AddComponent<MatchBootstrap>();
            bootstrap.Configure(simulation, pitch, ball, presentation, false, false);
            bootstrap.Initialize();
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            yield return null;
            Object.Destroy(simulation); Object.Destroy(pitch); Object.Destroy(ball); Object.Destroy(presentation);
            yield return null;
        }
        [UnityTest]
        public IEnumerator BootstrapUsesUnity6BodyAndSixtyHz()
        {
            Assert.That(bootstrap.IsInitialized, Is.True);
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 60f).Within(0.000001f));
            Assert.That(bootstrap.Ball.Rigidbody.mass, Is.EqualTo(0.43f));
            Assert.That(bootstrap.Ball.GetComponent<SphereCollider>().radius, Is.EqualTo(0.11f));
            Assert.That(bootstrap.Ball.Rigidbody.linearDamping, Is.Zero);
            Assert.That(bootstrap.Ball.Rigidbody.interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate));
            Assert.That(bootstrap.Ball.Rigidbody.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
            long before = bootstrap.Tick;
            yield return fixedFrame;
            Assert.That(bootstrap.Tick, Is.GreaterThan(before));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator WorldHasTwoSolidGoalsAndOneBall()
        {
            yield return null;
            Assert.That(root.GetComponentsInChildren<BallBody>().Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<CapsuleCollider>().Length, Is.EqualTo(6));
            Assert.That(root.GetComponentsInChildren<BoxCollider>().Length, Is.EqualTo(9));
            Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThanOrEqualTo(10));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator GroundShotScoresOnceAtEachEnd()
        {
            Launch(new Vec3(0f, 0.12f, pitch.Length * 0.5f - 2f), new Vec3(0f, 0f, 22f), default);
            for (int i = 0; i < 90; i++) yield return fixedFrame;
            Assert.That(bootstrap.PositiveGoals, Is.EqualTo(1));
            Assert.That(bootstrap.BallsOut, Is.Zero);
            Launch(new Vec3(0f, 0.12f, -pitch.Length * 0.5f + 2f), new Vec3(0f, 0f, -22f), default);
            for (int i = 0; i < 90; i++) yield return fixedFrame;
            Assert.That(bootstrap.NegativeGoals, Is.EqualTo(1));
            Assert.That(bootstrap.PositiveGoals, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator BallAboveBarIsOutInsteadOfGoal()
        {
            Launch(new Vec3(0f, 3.3f, pitch.Length * 0.5f - 2f), new Vec3(0f, 3f, 32f), default);
            for (int i = 0; i < 30; i++) yield return fixedFrame;
            Assert.That(bootstrap.PositiveGoals, Is.Zero);
            Assert.That(bootstrap.Session.LastCrossing.Kind, Is.EqualTo(BoundaryKind.GoalLine));
        }
        [UnityTest]
        public IEnumerator BallCrossesSideWithoutPhysicalInvisibleWall()
        {
            Launch(new Vec3(pitch.Width * 0.5f - 1f, 0.12f, 0f), new Vec3(18f, 0f, 0f), default);
            for (int i = 0; i < 30; i++) yield return fixedFrame;
            Assert.That(bootstrap.Session.LastCrossing.Kind, Is.EqualTo(BoundaryKind.Touchline));
            Assert.That(bootstrap.Ball.Position.X, Is.GreaterThan(pitch.Width * 0.5f + ball.Radius));
            Assert.That(bootstrap.BallsOut, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator PostReboundsWithoutScoring()
        {
            Launch(new Vec3(pitch.GoalWidth * 0.5f + pitch.PostRadius, 0.5f, pitch.Length * 0.5f - 2f),
                new Vec3(0f, 0f, 22f), default);
            bool reversed = false;
            for (int i = 0; i < 30; i++)
            {
                yield return fixedFrame;
                if (bootstrap.Ball.Velocity.Z < 0f) reversed = true;
            }
            Assert.That(reversed, Is.True);
            Assert.That(bootstrap.PositiveGoals, Is.Zero);
        }
        [UnityTest]
        public IEnumerator DropHasConfiguredApproximateRestitution()
        {
            Launch(new Vec3(0f, 3f, 0f), default, default);
            float incoming = 0f, ratio = 0f;
            for (int i = 0; i < 150; i++)
            {
                yield return fixedFrame;
                float vertical = bootstrap.Ball.Velocity.Y;
                if (vertical > 0f && incoming < -1f) { ratio = vertical / -incoming; break; }
                incoming = vertical;
            }
            Assert.That(ratio, Is.InRange(ball.Restitution - 0.15f, ball.Restitution + 0.15f));
        }
        [UnityTest]
        public IEnumerator SpinProducesLateralCurve()
        {
            Launch(new Vec3(0f, 2f, -12f), new Vec3(0f, 5f, 20f), new Vec3(0f, 40f, 0f));
            for (int i = 0; i < 30; i++) yield return fixedFrame;
            Assert.That(bootstrap.Ball.Position.X, Is.GreaterThan(0.03f));
        }
        [UnityTest]
        public IEnumerator ResetKeepsCountersAndRearmsDetector()
        {
            Launch(new Vec3(0f, 0.12f, pitch.Length * 0.5f - 2f), new Vec3(0f, 0f, 22f), default);
            for (int i = 0; i < 30; i++) yield return fixedFrame;
            Assert.That(bootstrap.PositiveGoals, Is.EqualTo(1));
            bootstrap.RequestReset();
            yield return fixedFrame;
            yield return fixedFrame;
            Assert.That(bootstrap.PositiveGoals, Is.EqualTo(1));
            Assert.That(bootstrap.Session.BallInPlay, Is.True);
            Assert.That(bootstrap.Ball.Position.Z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(bootstrap.Ball.Velocity.Z, Is.EqualTo(0f).Within(0.001f));
        }
        [UnityTest]
        public IEnumerator RejectedHudInitializationDoesNotProduceFrameErrors()
        {
            var incomplete = new GameObject("Incomplete HUD");
            try
            {
                LaboratoryHud hud = incomplete.AddComponent<LaboratoryHud>();
                var error = Assert.Throws<System.ArgumentNullException>(
                    () => hud.Initialize(null, bootstrap, presentation, null));
                Assert.That(error.ParamName, Is.EqualTo("defaultFont"));
                Assert.That(hud.IsInitialized, Is.False);
                Assert.That(hud.enabled, Is.False);
                // Even an external script enabling the incomplete component must not restart the error loop.
                hud.enabled = true;
                hud.ToggleBindings();
                yield return null;
                yield return null;
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.Destroy(incomplete); }
        }

        [UnityTest]
        public IEnumerator CompletePresentationStartsWithReadyHudAndCameraLookingAtThePitch()
        {
            Object.Destroy(root);
            yield return null;
            root = new GameObject("Presentation Acceptance Fixture");
            bootstrap = root.AddComponent<MatchBootstrap>();
            bootstrap.Configure(simulation, pitch, ball, presentation, true, true);
            bootstrap.Initialize();
            yield return null;
            yield return fixedFrame;
            LaboratoryHud hud = root.GetComponentInChildren<LaboratoryHud>();
            Assert.That(bootstrap.IsInitialized, Is.True);
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.IsInitialized, Is.True);
            Assert.That(hud.enabled, Is.True);
            Camera camera = root.GetComponentInChildren<Camera>();
            Assert.That(camera, Is.Not.Null);
            Vector3 target = camera.WorldToViewportPoint(presentation.OverviewTarget);
            Assert.That(target.z, Is.GreaterThan(camera.nearClipPlane));
            Assert.That(target.x, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(target.y, Is.EqualTo(0.5f).Within(0.01f));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GraphicalPlayerRendersGrassInsteadOfOnlyTheClearBackground()
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Pixel acceptance requires the graphical Editor with the Game view visible.");
            Object.Destroy(root);
            yield return null;
            root = new GameObject("Pixel Acceptance Fixture");
            bootstrap = root.AddComponent<MatchBootstrap>();
            bootstrap.Configure(simulation, pitch, ball, presentation, true, true);
            bootstrap.Initialize();
            yield return null;
            yield return new WaitForEndOfFrame();
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.That(frame, Is.Not.Null);
            try
            {
                Camera camera = root.GetComponentInChildren<Camera>();
                Color background = frame.GetPixelBilinear(0.95f, 0.95f);
                // Sample four grass locations away from the ball, marker, pitch lines and HUD panels.
                for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 sample = camera.WorldToViewportPoint(new Vector3(
                        x * pitch.Width * 0.25f, pitch.LineElevation, z * pitch.Length * 0.25f));
                    Assert.That(sample.x, Is.InRange(0f, 1f));
                    Assert.That(sample.y, Is.InRange(0f, 1f));
                    Assert.That(sample.z, Is.GreaterThan(camera.nearClipPlane));
                    Color grass = frame.GetPixelBilinear(sample.x, sample.y);
                    float difference = Mathf.Max(Mathf.Abs(grass.r - background.r),
                        Mathf.Abs(grass.g - background.g), Mathf.Abs(grass.b - background.b));
                    Assert.That(difference, Is.GreaterThan(0.05f), "Pitch sample still matches the camera clear background.");
                }
            }
            finally { Object.Destroy(frame); }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FailedBootstrapCanBeRetriedWithoutLeavingAReadySessionOrDuplicateWorld()
        {
            Object.Destroy(root);
            yield return null;
            root = new GameObject("Startup Failure Acceptance Fixture");
            bootstrap = root.AddComponent<MatchBootstrap>();
            bootstrap.Configure(simulation, pitch, ball, presentation, false, false);
            float previousStep = Time.fixedDeltaTime;
            Vector3 previousGravity = Physics.gravity;
            ball.Mass = 0f;
            Assert.Throws<System.ArgumentOutOfRangeException>(() => bootstrap.Initialize());
            Assert.That(bootstrap.IsInitialized, Is.False);
            Assert.That(bootstrap.Session, Is.Null);
            Assert.That(bootstrap.Ball, Is.Null);
            Assert.That(bootstrap.enabled, Is.False);
            Assert.That(root.transform.childCount, Is.Zero);
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(previousStep));
            Assert.That(Physics.gravity, Is.EqualTo(previousGravity));
            ball.Mass = 0.43f;
            bootstrap.Initialize();
            yield return null;
            yield return fixedFrame;
            Assert.That(bootstrap.IsInitialized, Is.True);
            Assert.That(bootstrap.enabled, Is.True);
            Assert.That(root.transform.childCount, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }
        private void Launch(Vec3 position, Vec3 velocity, Vec3 spin)
        {
            bootstrap.Ball.Teleport(position);
            bootstrap.Ball.Launch(velocity, spin);
            bootstrap.Session.ResetBall(position);
            Physics.SyncTransforms();
        }
    }
}
