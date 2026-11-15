using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Wynnyng.Configuration;
using Wynnyng.Core;
using Wynnyng.Input;
using Wynnyng.Presentation;

namespace Wynnyng.Runtime
{
    /// <summary>Fixed-step match adapter. Pure decisions read the independent PhysX ball once per step.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class FootballBootstrap : MonoBehaviour, IFootballControls
    {
        [SerializeField] private SimulationConfig simulation;
        [SerializeField] private PitchConfig pitch;
        [SerializeField] private BallConfig ball;
        [SerializeField] private PresentationConfig presentation;
        [SerializeField] private FootballConfig football;
        private readonly PlayerCommandBuffer homeInput = new PlayerCommandBuffer();
        private readonly PlayerCommandBuffer awayInput = new PlayerCommandBuffer();
        private GameObject world;
        private BallBody ballBody;
        private Rigidbody[] playerBodies;
        private FootballPlayerVisuals visuals;
        private FootballCameraRig cameraRig;
        private FootballHud hud;
        private LocalInputHub inputs;
        private bool withPresentation = true, withInput = true, globalsChanged;
        private bool suppressHomeShot, suppressAwayShot, fullTimeMenuShown;
        private float oldFixedDelta, oldMaximumDelta, oldBounce, oldTimeScale;
        private Vector3 oldGravity;
        private int oldTargetRate, oldVSync;
        private SimulationMode oldSimulationMode;
        public FootballMatch Match { get; private set; }
        public BallBody Ball => ballBody;
        public Rigidbody[] PlayerBodies => playerBodies;
        public FootballHud Hud => hud;
        public bool IsInitialized { get; private set; }
        public bool IsPaused { get; private set; }
        public bool TwoPlayers { get; private set; }

        public void Configure(SimulationConfig simulationConfig, PitchConfig pitchConfig, BallConfig ballConfig,
            PresentationConfig presentationConfig, FootballConfig footballConfig,
            bool createPresentation = true, bool createInput = true)
        {
            if (IsInitialized) throw new InvalidOperationException("Configure before initialization.");
            simulation = simulationConfig; pitch = pitchConfig; ball = ballConfig;
            presentation = presentationConfig; football = footballConfig;
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
            try
            {
                BuildWorld();
                IsInitialized = true;
                enabled = true;
            }
            catch
            {
                ReleaseWorld();
                enabled = false;
                throw;
            }
        }

        private void BuildWorld()
        {
            if (simulation == null) simulation = Resources.Load<SimulationConfig>("Config/Simulation");
            if (pitch == null) pitch = Resources.Load<PitchConfig>("Config/Pitch");
            if (ball == null) ball = Resources.Load<BallConfig>("Config/Ball");
            if (presentation == null) presentation = Resources.Load<PresentationConfig>("Config/Presentation");
            if (football == null) football = Resources.Load<FootballConfig>("Config/Football");
            if (simulation == null || pitch == null || ball == null || presentation == null || football == null)
                throw new InvalidOperationException("Configuracoes ausentes. Execute Wynnyng > Preparar e jogar partida.");
            if (transform.position != Vector3.zero || transform.rotation != Quaternion.identity || transform.lossyScale != Vector3.one)
                throw new InvalidOperationException("FootballBootstrap deve ficar na origem, sem rotacao e com escala 1.");
            if (FindObjectsByType<FootballBootstrap>(FindObjectsSortMode.None).Length != 1 ||
                FindObjectsByType<MatchBootstrap>(FindObjectsSortMode.None).Length != 0)
                throw new InvalidOperationException("Abra somente uma cena de partida ou laboratorio por vez.");
            simulation.ValidateOrThrow(); pitch.ValidateOrThrow(ball.Radius); ball.ValidateOrThrow();
            presentation.ValidateOrThrow(); football.ValidateOrThrow();
            if (withPresentation) RenderingResources.RequireConfigured();
            TMP_FontAsset font = withPresentation ? TmpResources.RequireDefaultFont() : null;
            SaveAndApplyGlobals();
            world = new GameObject("Wynnyng Football World");
            world.transform.SetParent(transform, false);
            RuntimeAssets assets = world.AddComponent<RuntimeAssets>();
            PitchFactory.Build(world.transform, assets, pitch, ball);
            var ballObject = new GameObject("Match Ball");
            ballObject.transform.SetParent(world.transform, false);
            ballBody = ballObject.AddComponent<BallBody>();
            ballBody.Initialize(ball, simulation, assets);
            FootballParameters parameters = football.Gameplay.Clone();
            parameters.BallResetHeight = Mathf.Max(parameters.BallResetHeight, ball.Radius + ball.ResetClearance);
            Match = new FootballMatch(parameters, pitch.Geometry(ball.Radius), new SeededRandom(simulation.Seed));
            playerBodies = new Rigidbody[FootballMatch.PlayerCount];
            Transform[] playerTransforms;
            if (withPresentation)
            {
                visuals = FootballPlayerVisuals.Build(world.transform, assets, football, font);
                playerTransforms = visuals.PlayerTransforms;
            }
            else
            {
                playerTransforms = new Transform[FootballMatch.PlayerCount];
                for (int i = 0; i < playerTransforms.Length; i++)
                {
                    var player = new GameObject("Football Player " + i);
                    player.transform.SetParent(world.transform, false);
                    playerTransforms[i] = player.transform;
                }
            }
            for (int i = 0; i < playerBodies.Length; i++)
            {
                GameObject player = playerTransforms[i].gameObject;
                player.transform.position = BallBody.ToUnity(Match.Players[i].Position);
                var collider = player.AddComponent<CapsuleCollider>();
                collider.radius = parameters.PlayerRadius;
                collider.height = football.PlayerHeight;
                collider.center = Vector3.up * (football.PlayerHeight * 0.5f);
                collider.contactOffset = ball.ContactOffset;
                Rigidbody body = player.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                playerBodies[i] = body;
            }
            if (withInput)
            {
                var source = Resources.Load<InputActionAsset>("Input/Match");
                inputs = new LocalInputHub(source, new UnityGameLog(simulation.EnableLogging));
            }
            if (withPresentation)
            {
                cameraRig = FootballCameraRig.Build(world.transform, ballBody.transform, playerTransforms, football, presentation);
                var marker = new GameObject("Match Ball Marker");
                marker.transform.SetParent(world.transform, false);
                marker.AddComponent<BallMarker>().Initialize(ballBody.transform, assets, pitch, presentation);
                if (inputs != null)
                {
                    var canvas = new GameObject("Football HUD");
                    canvas.transform.SetParent(world.transform, false);
                    hud = canvas.AddComponent<FootballHud>();
                    hud.Initialize(this, inputs, football, presentation, font);
                }
            }
            TeleportKickoff();
            Physics.SyncTransforms();
        }

        private void Update()
        {
            if (!IsInitialized) return;
            if (Match.Phase == FootballPhase.FullTime && !fullTimeMenuShown && hud != null)
            {
                fullTimeMenuShown = true;
                if (!hud.MenuVisible) hud.ToggleMenu();
            }
            if (inputs == null) return;
            if (TwoPlayers && !inputs.HasDevice(1)) SetTwoPlayers(false);
            PlayerCommand home = inputs.ReadPlayer(0);
            PlayerCommand away = inputs.ReadPlayer(1);
            Keyboard keyboard = Keyboard.current;
            bool keyboardPause = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            if (!inputs.IsRebinding && !inputs.RebindFinishedThisFrame &&
                (home.PausePressed || away.PausePressed || keyboardPause))
            {
                if (hud != null) hud.ToggleMenu();
                else TogglePause();
            }
            if (IsPaused || inputs.IsRebinding)
            {
                ClearInput();
                return;
            }
            home.PausePressed = away.PausePressed = false;
            home = ToWorldCommand(home);
            away = ToWorldCommand(away);
            FilterShot(ref home, ref suppressHomeShot);
            FilterShot(ref away, ref suppressAwayShot);
            homeInput.Push(home);
            awayInput.Push(away);
        }

        private PlayerCommand ToWorldCommand(PlayerCommand command)
        {
            if (cameraRig == null) return command;
            Transform camera = cameraRig.OutputCamera.transform;
            Vector3 right = camera.right; right.y = 0f; right.Normalize();
            Vector3 forward = camera.forward; forward.y = 0f; forward.Normalize();
            Vector3 direction = right * command.MoveX + forward * command.MoveY;
            command.MoveX = direction.x; command.MoveY = direction.z;
            return command;
        }

        private static void FilterShot(ref PlayerCommand command, ref bool suppress)
        {
            if (!suppress) return;
            if (!command.ShootHeld) suppress = false;
            command.ShootHeld = command.ShootPressed = command.ShootReleased = false;
        }

        private void FixedUpdate()
        {
            if (!IsInitialized || IsPaused) return;
            float seconds = 1f / simulation.FixedFrequency;
            FootballBallAction action = Match.Step(seconds, ballBody.Position, ballBody.Velocity,
                homeInput.Consume(), awayInput.Consume(), TwoPlayers);
            for (int i = 0; i < playerBodies.Length; i++)
            {
                Vector3 position = BallBody.ToUnity(Match.Players[i].Position);
                if (action.Teleport) playerBodies[i].position = position;
                else playerBodies[i].MovePosition(position);
            }
            if (action.Teleport) ballBody.Teleport(action.Position);
            if (action.HasKick) ballBody.Launch(action.Velocity, action.Spin);
            if (ballBody.Position.Y < -presentation.SafetyResetHeight)
            {
                Match.ResetKickoff();
                TeleportKickoff();
            }
            ballBody.FixedStep(seconds);
        }

        private void LateUpdate()
        {
            if (!IsInitialized || visuals == null) return;
            visuals.Render(Match.Players, Match.ControlledPlayer, Match.AwayControlledPlayer, TwoPlayers, Time.time);
        }

        public void TogglePause()
        {
            if (!IsInitialized) return;
            IsPaused = !IsPaused;
            Time.timeScale = IsPaused ? 0f : 1f;
            ClearInput();
        }
        private void ClearInput()
        {
            homeInput.Clear(); awayInput.Clear();
            suppressHomeShot = suppressAwayShot = true;
            Match?.CancelCharges();
        }
        public void Restart()
        {
            if (!IsInitialized) return;
            Match.RestartMatch();
            fullTimeMenuShown = false;
            ClearInput();
            TeleportKickoff();
        }
        private void TeleportKickoff()
        {
            ballBody.Teleport(new Vec3(0f, Match.Parameters.BallResetHeight, 0f));
            for (int i = 0; i < playerBodies.Length; i++)
                playerBodies[i].position = BallBody.ToUnity(Match.Players[i].Position);
            Physics.SyncTransforms();
        }
        public void SetCpuHome(bool enabled)
        {
            if (Match == null) return;
            Match.CpuHome = enabled;
            if (enabled) TwoPlayers = false;
            ClearInput();
        }
        public void SetTwoPlayers(bool enabled)
        {
            TwoPlayers = enabled && (inputs == null || inputs.HasDevice(1));
            if (TwoPlayers && Match != null) Match.CpuHome = false;
            ClearInput();
        }
        public void Exit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SaveAndApplyGlobals()
        {
            oldFixedDelta = Time.fixedDeltaTime; oldMaximumDelta = Time.maximumDeltaTime;
            oldGravity = Physics.gravity; oldBounce = Physics.bounceThreshold;
            oldTargetRate = Application.targetFrameRate; oldVSync = QualitySettings.vSyncCount;
            oldTimeScale = Time.timeScale; oldSimulationMode = Physics.simulationMode;
            globalsChanged = true;
            Time.fixedDeltaTime = 1f / simulation.FixedFrequency;
            Time.maximumDeltaTime = simulation.MaximumFrameDelta;
            Physics.gravity = simulation.Gravity; Physics.bounceThreshold = simulation.BounceThreshold;
            Physics.simulationMode = SimulationMode.FixedUpdate;
            Application.targetFrameRate = simulation.TargetFrameRate; QualitySettings.vSyncCount = 0;
            Time.timeScale = 1f;
        }
        private void ReleaseWorld()
        {
            IsInitialized = false;
            if (world != null) { world.SetActive(false); Destroy(world); world = null; }
            inputs?.Dispose(); inputs = null;
            Match = null; ballBody = null; playerBodies = null;
            visuals = null; cameraRig = null; hud = null;
            homeInput.Clear(); awayInput.Clear();
            if (!globalsChanged) return;
            Time.fixedDeltaTime = oldFixedDelta; Time.maximumDeltaTime = oldMaximumDelta;
            Physics.gravity = oldGravity; Physics.bounceThreshold = oldBounce;
            Physics.simulationMode = oldSimulationMode;
            Application.targetFrameRate = oldTargetRate; QualitySettings.vSyncCount = oldVSync;
            Time.timeScale = oldTimeScale;
            IsPaused = false; globalsChanged = false;
        }
        private void OnDestroy() { ReleaseWorld(); }
    }
}
