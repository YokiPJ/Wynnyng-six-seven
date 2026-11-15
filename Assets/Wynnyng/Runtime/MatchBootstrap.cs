using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Wynnyng.Configuration;
using Wynnyng.Core;
using Wynnyng.Input;
using Wynnyng.Presentation;

namespace Wynnyng.Runtime
{
    [DefaultExecutionOrder(-200)]
    public sealed class MatchBootstrap : MonoBehaviour, ILaboratoryControls
    {
        private const float NoPendingReset = -1f;
        [SerializeField] private SimulationConfig simulation;
        [SerializeField] private PitchConfig pitch;
        [SerializeField] private BallConfig ball;
        [SerializeField] private PresentationConfig presentation;
        private GameObject world;
        private BallBody ballBody;
        private MatchSession session;
        private EventChannel<MatchEvent> events;
        private LocalInputHub inputs;
        private LaboratoryHud hud;
        private IGameLog log;
        private bool withPresentation = true, withInput = true, globalsChanged;
        private bool launchRequested, resetRequested;
        private float resetCountdown = NoPendingReset;
        private int selectedScenario;
        private string status = "Bola em campo";
        private float oldFixedDelta, oldMaximumDelta, oldBounce;
        private Vector3 oldGravity;
        private int oldTargetRate, oldVSync;
        private SimulationMode oldSimulationMode;
        public bool IsInitialized => session != null;
        public BallBody Ball => ballBody;
        public MatchSession Session => session;
        public IPlayerCommandSource PlayerInput => inputs;
        public long Tick => session?.Tick ?? 0;
        public float BallSpeed => ballBody != null ? ballBody.Velocity.Length : 0f;
        public int PositiveGoals => session?.GoalsAtPositiveEnd ?? 0;
        public int NegativeGoals => session?.GoalsAtNegativeEnd ?? 0;
        public int BallsOut => session?.BallsOut ?? 0;
        public string Status => status;
        public string ScenarioName => presentation.Scenarios[selectedScenario].Name;

        public void Configure(SimulationConfig simulationConfig, PitchConfig pitchConfig, BallConfig ballConfig,
            PresentationConfig presentationConfig, bool createPresentation = true, bool createInput = true)
        {
            if (IsInitialized) throw new InvalidOperationException("Configure before initialization.");
            simulation = simulationConfig; pitch = pitchConfig; ball = ballConfig; presentation = presentationConfig;
            withPresentation = createPresentation; withInput = createInput;
        }
        private void Start()
        {
            try { Initialize(); }
            catch (Exception exception) { enabled = false; Debug.LogException(exception, this); }
        }
        public void Initialize()
        {
            if (IsInitialized) return;
            if (simulation == null) simulation = Resources.Load<SimulationConfig>("Config/Simulation");
            if (pitch == null) pitch = Resources.Load<PitchConfig>("Config/Pitch");
            if (ball == null) ball = Resources.Load<BallConfig>("Config/Ball");
            if (presentation == null) presentation = Resources.Load<PresentationConfig>("Config/Presentation");
            if (simulation == null || pitch == null || ball == null || presentation == null)
                throw new InvalidOperationException("Missing configs. Run Wynnyng > Preparar Fases 0 e 1A.");
            if (FindObjectsByType<MatchBootstrap>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Open one laboratory scene at a time.");
            simulation.ValidateOrThrow(); ball.ValidateOrThrow(); pitch.ValidateOrThrow(ball.Radius); presentation.ValidateOrThrow();
            log = new UnityGameLog(simulation.EnableLogging);
            SaveAndApplyGlobals();
            world = new GameObject("Wynnyng Runtime World");
            world.transform.SetParent(transform, false);
            RuntimeAssets assets = world.AddComponent<RuntimeAssets>();
            PitchFactory.Build(world.transform, assets, pitch, ball);
            var ballObject = new GameObject("Ball");
            ballObject.transform.SetParent(world.transform, false);
            ballBody = ballObject.AddComponent<BallBody>();
            ballBody.Initialize(ball, simulation, assets);
            events = new EventChannel<MatchEvent>(simulation.EventCapacity);
            events.Subscribe(OnMatchEvent);
            session = new MatchSession(simulation.FixedFrequency, pitch.Geometry(ball.Radius), ballBody.Position,
                new SeededRandom(simulation.Seed), events);
            if (withInput)
            {
                InputActionAsset source = Resources.Load<InputActionAsset>("Input/Match");
                inputs = new LocalInputHub(source, log);
            }
            if (withPresentation)
            {
                OverviewCameraFactory.Build(world.transform, presentation);
                var marker = new GameObject("Ball Ground Marker");
                marker.transform.SetParent(world.transform, false);
                marker.AddComponent<BallMarker>().Initialize(ballBody.transform, assets, pitch, presentation);
                if (inputs != null)
                {
                    var canvas = new GameObject("Laboratory HUD");
                    canvas.transform.SetParent(world.transform, false);
                    hud = canvas.AddComponent<LaboratoryHud>();
                    hud.Initialize(inputs, this, presentation);
                }
            }
            Physics.SyncTransforms();
            log.Info("Fases 0 + 1A ready. Fixed simulation: 60 Hz.");
        }

        private void Update()
        {
            if (inputs == null) return;
            inputs.ReadLaboratory(out int direction, out bool launch, out bool reset, out bool bindings);
            if (bindings && hud != null) hud.ToggleBindings();
            if (hud != null && hud.RebindVisible) return;
            if (direction != 0) SelectScenario(direction);
            if (launch) RequestLaunch();
            if (reset) RequestReset();
        }
        private void FixedUpdate()
        {
            if (!IsInitialized) return;
            if (resetRequested) ResetBall();
            else if (launchRequested) LaunchScenario();
            session.Step(ballBody.Position);
            if (resetCountdown >= 0f)
            {
                resetCountdown -= session.StepSeconds;
                if (resetCountdown <= 0f) ResetBall();
            }
            if (ballBody.Position.Y < -presentation.SafetyResetHeight) ResetBall();
            ballBody.FixedStep(session.StepSeconds);
        }
        public void SelectScenario(int direction)
        {
            int count = presentation.Scenarios.Length;
            selectedScenario = (selectedScenario + direction % count + count) % count;
        }
        public void RequestLaunch() { launchRequested = true; resetRequested = false; }
        public void RequestReset() { resetRequested = true; launchRequested = false; }
        private void LaunchScenario()
        {
            LaboratoryScenario scenario = presentation.Scenarios[selectedScenario];
            Vector3 position = new Vector3(scenario.Start.x * pitch.Width * 0.5f,
                Mathf.Max(ball.Radius + ball.ResetClearance, scenario.Start.y), scenario.Start.z * pitch.Length * 0.5f);
            if (scenario.AlignWithPost) position.x = pitch.GoalWidth * 0.5f + pitch.PostRadius;
            ballBody.Teleport(BallBody.ToCore(position));
            ballBody.Launch(BallBody.ToCore(scenario.Velocity), BallBody.ToCore(scenario.Spin));
            session.ResetBall(ballBody.Position);
            resetCountdown = NoPendingReset; launchRequested = false; resetRequested = false;
            status = "Bola em campo";
            Physics.SyncTransforms();
        }
        private void ResetBall()
        {
            ballBody.Teleport(BallBody.ToCore(ball.RestPosition));
            session.ResetBall(ballBody.Position);
            resetCountdown = NoPendingReset; resetRequested = false; launchRequested = false;
            status = "Bola em campo";
            Physics.SyncTransforms();
        }
        private void OnMatchEvent(MatchEvent value)
        {
            switch (value.Crossing.Kind)
            {
                case BoundaryKind.Goal:
                    status = value.Crossing.End == PitchEnd.Positive ? "GOL no fundo +Z" : "GOL no fundo -Z";
                    break;
                case BoundaryKind.Touchline: status = "FORA: linha lateral"; break;
                default: status = "FORA: linha de fundo"; break;
            }
            resetCountdown = presentation.AutoResetDelay;
            log.Info(status);
        }
        private void SaveAndApplyGlobals()
        {
            oldFixedDelta = Time.fixedDeltaTime; oldMaximumDelta = Time.maximumDeltaTime;
            oldGravity = Physics.gravity; oldBounce = Physics.bounceThreshold;
            oldTargetRate = Application.targetFrameRate; oldVSync = QualitySettings.vSyncCount;
            oldSimulationMode = Physics.simulationMode; globalsChanged = true;
            Time.fixedDeltaTime = 1f / simulation.FixedFrequency;
            Time.maximumDeltaTime = simulation.MaximumFrameDelta;
            Physics.gravity = simulation.Gravity; Physics.bounceThreshold = simulation.BounceThreshold;
            Physics.simulationMode = SimulationMode.FixedUpdate;
            Application.targetFrameRate = simulation.TargetFrameRate; QualitySettings.vSyncCount = 0;
        }
        private void OnDestroy()
        {
            inputs?.Dispose();
            if (events != null) events.Unsubscribe(OnMatchEvent);
            if (world != null) Destroy(world);
            if (!globalsChanged) return;
            Time.fixedDeltaTime = oldFixedDelta; Time.maximumDeltaTime = oldMaximumDelta;
            Physics.gravity = oldGravity; Physics.bounceThreshold = oldBounce;
            Physics.simulationMode = oldSimulationMode;
            Application.targetFrameRate = oldTargetRate; QualitySettings.vSyncCount = oldVSync;
        }
    }
}
