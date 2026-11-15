using System;

namespace Wynnyng.Core
{
    /// <summary>Original small-sided tactical model expanded to eleven roles. Rigidbody integration is external.</summary>
    public sealed class FootballMatch
    {
        public const int PlayersPerTeam = 11;
        public const int PlayerCount = PlayersPerTeam * 2;
        private const float DirectionEpsilon = 0.00001f;
        private readonly PitchGeometry pitch;
        private readonly IRandomSource random;
        private readonly BoundaryTracker boundaries;
        private readonly Vec3[] targets = new Vec3[PlayerCount];
        private readonly float[] kickCooldown = new float[PlayerCount];
        private readonly float[] possessionTime = new float[PlayerCount];
        private readonly float[] dribbleCooldown = new float[PlayerCount];
        private readonly float[] charge = new float[2];
        private readonly bool[] charging = new bool[2];
        private readonly int[] chargeActor = new int[2];
        private readonly int[] chasers = new int[2];
        private float phaseCountdown, aiCountdown, switchCountdown;
        private Vec3 restartPosition;
        private int restartTeam, lastTouchTeam, lastTouchActor = -1, pendingPassTeam = -1, pendingPassActor = -1;
        private bool teleportPending;
        public FootballParameters Parameters { get; }
        public FootballPlayer[] Players { get; } = new FootballPlayer[PlayerCount];
        public FootballStatistics HomeStats { get; } = new FootballStatistics();
        public FootballStatistics AwayStats { get; } = new FootballStatistics();
        public int ControlledPlayer { get; private set; }
        public int AwayControlledPlayer { get; private set; }
        public bool CpuHome { get; set; }
        public int HomeGoals { get; private set; }
        public int AwayGoals { get; private set; }
        public int Half { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public float RemainingSeconds => Math.Max(0f, Parameters.MatchSeconds - ElapsedSeconds);
        public float Charge01 => Clamp01(charge[0] / Parameters.ChargeSeconds);
        public FootballPhase Phase { get; private set; }
        public string Status { get; private set; }
        public int BallOwner { get; private set; } = -1;

        public FootballMatch(FootballParameters parameters, PitchGeometry geometry, IRandomSource rng)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Parameters.Validate();
            pitch = geometry;
            random = rng ?? throw new ArgumentNullException(nameof(rng));
            boundaries = new BoundaryTracker(pitch, CentreBall);
            for (int i = 0; i < PlayerCount; i++)
                Players[i] = new FootballPlayer { Team = i / PlayersPerTeam, Number = i % PlayersPerTeam + 1, IsGoalkeeper = i % PlayersPerTeam == 0 };
            RestartMatch();
        }
        private Vec3 CentreBall => new Vec3(0f, Math.Max(pitch.BallRadius, Parameters.BallResetHeight), 0f);
        public int AttackSign(int team) => (team == 0 ? 1 : -1) * (Half == 1 ? 1 : -1);
        private FootballStatistics Stats(int team) => team == 0 ? HomeStats : AwayStats;

        public void RestartMatch()
        {
            HomeGoals = 0; AwayGoals = 0; Half = 1; ElapsedSeconds = 0f;
            HomeStats.Clear(); AwayStats.Clear();
            for (int i = 0; i < PlayerCount; i++) Players[i].Stamina = 1f;
            PrepareKickoff(0);
        }
        public void ResetKickoff() { PrepareKickoff(0); }
        public void CancelCharges()
        {
            charge[0] = 0f; charge[1] = 0f;
            charging[0] = false; charging[1] = false;
            chargeActor[0] = -1; chargeActor[1] = -1;
        }
        private void PrepareKickoff(int team)
        {
            restartTeam = team; lastTouchTeam = team; lastTouchActor = -1;
            BallOwner = -1; pendingPassTeam = -1; pendingPassActor = -1;
            CancelCharges(); switchCountdown = 0f; aiCountdown = 0f;
            for (int i = 0; i < PlayerCount; i++)
            {
                FootballPlayer player = Players[i];
                FootballFormationSlot slot = Parameters.Formation[i % PlayersPerTeam];
                int attack = AttackSign(player.Team);
                float ownDepth = player.IsGoalkeeper ? -pitch.HalfLength + Parameters.KeeperGoalOffset :
                    Math.Min(-Parameters.KickoffPlayerOffset * 2f, -Math.Abs(slot.Depth) * pitch.HalfLength);
                player.Position = new Vec3(slot.Width * pitch.HalfWidth * Parameters.TeamWidth, 0f, ownDepth * attack);
                player.Facing = new Vec3(0f, 0f, attack); player.Velocity = default;
                targets[i] = player.Position; kickCooldown[i] = 0f; possessionTime[i] = 0f; dribbleCooldown[i] = 0f;
            }
            int taker = team * PlayersPerTeam + 9;
            Players[taker].Position = new Vec3(0f, 0f, -AttackSign(team) * Parameters.KickoffPlayerOffset);
            ControlledPlayer = team == 0 ? taker : 9;
            AwayControlledPlayer = team == 1 ? taker : PlayersPerTeam + 9;
            Phase = FootballPhase.Kickoff; phaseCountdown = Parameters.KickoffSeconds;
            Status = "Saida de bola";
            restartPosition = CentreBall; teleportPending = true;
            boundaries.Reset(restartPosition);
        }

        public FootballBallAction Step(float dt, Vec3 ballPosition, Vec3 ballVelocity,
            PlayerCommand homeCommand, PlayerCommand awayCommand, bool twoPlayers)
        {
            if (!float.IsFinite(dt) || dt <= 0f || !Finite(ballPosition) || !Finite(ballVelocity))
                throw new ArgumentOutOfRangeException(nameof(dt), "A football step needs finite positions and positive time.");
            if (teleportPending)
            {
                teleportPending = false;
                boundaries.Reset(restartPosition);
                return TeleportBall(restartPosition);
            }
            if (Phase != FootballPhase.Playing)
            {
                if (Phase == FootballPhase.FullTime) return default;
                phaseCountdown -= dt;
                if (phaseCountdown > 0f) return default;
                if (Phase == FootballPhase.Goal) { PrepareKickoff(restartTeam); teleportPending = false; return TeleportBall(restartPosition); }
                if (Phase == FootballPhase.HalfTime)
                {
                    Half = 2; PrepareKickoff(1); teleportPending = false; return TeleportBall(restartPosition);
                }
                Phase = FootballPhase.Playing; Status = "Bola em jogo";
                boundaries.Reset(restartPosition);
                return TeleportBall(restartPosition);
            }
            BoundaryCrossing crossing = boundaries.Step(ballPosition);
            if (crossing.HasCrossed) return HandleBoundary(crossing);
            ElapsedSeconds = Math.Min(Parameters.MatchSeconds, ElapsedSeconds + dt);
            if (Half == 1 && ElapsedSeconds >= Parameters.MatchSeconds * 0.5f)
            {
                ElapsedSeconds = Parameters.MatchSeconds * 0.5f;
                Phase = FootballPhase.HalfTime; phaseCountdown = Parameters.HalfTimeSeconds; CancelCharges();
                BallOwner = -1; Status = "Intervalo - troca de lados";
                StopPlayers(); return TeleportBall(ballPosition);
            }
            if (ElapsedSeconds >= Parameters.MatchSeconds)
            {
                Phase = FootballPhase.FullTime; CancelCharges(); BallOwner = -1; Status = "Fim de jogo";
                StopPlayers(); return TeleportBall(ballPosition);
            }
            for (int i = 0; i < PlayerCount; i++)
            {
                kickCooldown[i] = Math.Max(0f, kickCooldown[i] - dt);
                dribbleCooldown[i] = Math.Max(0f, dribbleCooldown[i] - dt);
            }
            UpdateOwnership(ballPosition, ballVelocity, dt);
            UpdateControlledPlayers(homeCommand, awayCommand, ballPosition, dt, twoPlayers);
            aiCountdown -= dt;
            if (aiCountdown <= 0f) { Think(ballPosition, ballVelocity); aiCountdown = Parameters.AiDecisionSeconds; }
            for (int i = 0; i < PlayerCount; i++)
            {
                bool human = (!CpuHome && i == ControlledPlayer) || (twoPlayers && i == AwayControlledPlayer);
                PlayerCommand command = i == ControlledPlayer && !CpuHome ? homeCommand : awayCommand;
                Vec3 move = human ? new Vec3(command.MoveX, 0f, command.MoveY) : targets[i] - Players[i].Position;
                bool sprint = human ? command.Sprint > 0f : move.LengthSquared > Parameters.AiSprintDistance * Parameters.AiSprintDistance;
                if (!human)
                {
                    if (i == chasers[Players[i].Team] && BallOwner != i &&
                        DistanceXZ(Players[i].Position, ballPosition) > Parameters.ControlRadius)
                        sprint = true;
                    float distance = move.Length;
                    move = distance > DirectionEpsilon ? move * (Math.Min(1f, distance / Parameters.AiArrivalRadius) / distance) : default;
                }
                MovePlayer(i, move, sprint, dt);
            }
            SeparatePlayers();
            FootballBallAction keeperAction = TryGoalkeeper(ballPosition, ballVelocity);
            if (keeperAction.HasKick) return keeperAction;
            FootballBallAction action;
            if (!CpuHome)
            {
                action = HumanAction(ControlledPlayer, homeCommand, ballPosition, dt);
                if (action.HasKick) return action;
            }
            if (twoPlayers)
            {
                action = HumanAction(AwayControlledPlayer, awayCommand, ballPosition, dt);
                if (action.HasKick) return action;
            }
            if (BallOwner >= 0)
            {
                int owner = BallOwner;
                bool human = (!CpuHome && owner == ControlledPlayer) || (twoPlayers && owner == AwayControlledPlayer);
                if (!human && possessionTime[owner] >= Parameters.AiReactionSeconds)
                {
                    action = AiAction(owner, ballPosition);
                    if (action.HasKick) return action;
                }
                if (dribbleCooldown[owner] <= 0f) return Dribble(owner, ballPosition);
            }
            return TryAiTackle(ballPosition, twoPlayers);
        }

        private void UpdateOwnership(Vec3 ball, Vec3 velocity, float dt)
        {
            int oldOwner = BallOwner;
            if (oldOwner >= 0 && (DistanceXZ(Players[oldOwner].Position, ball) > Parameters.OwnerReleaseRadius ||
                ball.Y > Parameters.ControlHeight || kickCooldown[oldOwner] > 0f)) BallOwner = -1;
            int nearest = -1;
            float distance = Parameters.ControlRadius * Parameters.ControlRadius;
            if (ball.Y <= Parameters.ControlHeight && velocity.Length <= Parameters.ControlMaxSpeed)
            {
                for (int i = 0; i < PlayerCount; i++)
                {
                    if (kickCooldown[i] > 0f || Players[i].IsGoalkeeper) continue;
                    float d = DistanceSquaredXZ(Players[i].Position, ball);
                    if (d < distance) { nearest = i; distance = d; }
                }
                if (nearest >= 0) BallOwner = nearest;
            }
            if (BallOwner >= 0)
            {
                FootballPlayer owner = Players[BallOwner];
                Stats(owner.Team).PossessionSeconds += dt;
                if (BallOwner != oldOwner)
                {
                    possessionTime[BallOwner] = 0f;
                    if (pendingPassTeam == owner.Team && pendingPassActor != BallOwner)
                        Stats(owner.Team).CompletedPasses++;
                    pendingPassTeam = -1; pendingPassActor = -1;
                }
                possessionTime[BallOwner] += dt;
                lastTouchTeam = owner.Team; lastTouchActor = BallOwner;
            }
        }
        private void UpdateControlledPlayers(PlayerCommand home, PlayerCommand away, Vec3 ball, float dt, bool twoPlayers)
        {
            switchCountdown -= dt;
            if (!CpuHome && home.SwitchPressed) ControlledPlayer = FindNearestPlayer(0, ball, ControlledPlayer);
            if (twoPlayers && away.SwitchPressed) AwayControlledPlayer = FindNearestPlayer(1, ball, AwayControlledPlayer);
            if (BallOwner >= 0)
            {
                if (Players[BallOwner].Team == 0 && !CpuHome && !home.ShootHeld) ControlledPlayer = BallOwner;
                if (Players[BallOwner].Team == 1 && twoPlayers && !away.ShootHeld) AwayControlledPlayer = BallOwner;
            }
            if (switchCountdown > 0f) return;
            switchCountdown = Parameters.AutoSwitchDelay;
            int nearest = FindNearestPlayer(0, ball);
            if (!CpuHome && !home.ShootHeld && DistanceXZ(Players[ControlledPlayer].Position, ball) >
                DistanceXZ(Players[nearest].Position, ball) + Parameters.AutoSwitchDistance) ControlledPlayer = nearest;
            nearest = FindNearestPlayer(1, ball);
            if (twoPlayers && !away.ShootHeld && DistanceXZ(Players[AwayControlledPlayer].Position, ball) >
                DistanceXZ(Players[nearest].Position, ball) + Parameters.AutoSwitchDistance) AwayControlledPlayer = nearest;
        }
        private void Think(Vec3 ball, Vec3 velocity)
        {
            chasers[0] = FindInterceptor(0, ball, velocity); chasers[1] = FindInterceptor(1, ball, velocity);
            for (int i = 0; i < PlayerCount; i++)
            {
                FootballPlayer player = Players[i];
                int attack = AttackSign(player.Team);
                if (player.IsGoalkeeper)
                {
                    float advance = Math.Min(Parameters.KeeperAdvance,
                        Math.Max(Parameters.KeeperGoalOffset, (pitch.HalfLength - Math.Abs(ball.Z)) * Parameters.TeamBallShift));
                    float goalDistance = Math.Max(Parameters.KeeperAdvance, pitch.HalfLength + ball.Z * attack);
                    float x = Clamp(ball.X * pitch.GoalHalfWidth / goalDistance, -pitch.GoalHalfWidth + Parameters.PlayerRadius,
                        pitch.GoalHalfWidth - Parameters.PlayerRadius);
                    targets[i] = new Vec3(x, 0f, (-pitch.HalfLength + advance) * attack);
                    continue;
                }
                if (i == BallOwner) { targets[i] = new Vec3(0f, 0f, attack * pitch.HalfLength); continue; }
                if (i == chasers[player.Team])
                {
                    float intercept = InterceptionTime(player, ball, velocity);
                    targets[i] = ClampPlayer(ball + velocity * intercept); continue;
                }
                FootballFormationSlot slot = Parameters.Formation[i % PlayersPerTeam];
                float z = slot.Depth * pitch.HalfLength * Parameters.DefensiveLine * attack + ball.Z * Parameters.TeamBallShift;
                if (BallOwner >= 0 && Players[BallOwner].Team == player.Team)
                    z += Parameters.AiSupportingAdvance * attack;
                float width = slot.Width * pitch.HalfWidth * Parameters.TeamWidth + ball.X * Parameters.TeamBallShift;
                targets[i] = ClampPlayer(new Vec3(width, 0f, z));
            }
        }
        private void MovePlayer(int index, Vec3 move, bool sprint, float dt)
        {
            FootballPlayer player = Players[index];
            move = new Vec3(FiniteAxis(move.X), 0f, FiniteAxis(move.Z));
            float magnitude = Math.Min(1f, move.Length);
            Vec3 direction = Normalize(move);
            if (magnitude > DirectionEpsilon)
            {
                float current = (float)Math.Atan2(player.Facing.X, player.Facing.Z);
                float desired = (float)Math.Atan2(direction.X, direction.Z);
                float difference = desired - current;
                while (difference > Math.PI) difference -= (float)(Math.PI * 2);
                while (difference < -Math.PI) difference += (float)(Math.PI * 2);
                current += Clamp(difference, -Parameters.TurnRadiansPerSecond * dt, Parameters.TurnRadiansPerSecond * dt);
                player.Facing = new Vec3((float)Math.Sin(current), 0f, (float)Math.Cos(current));
            }
            bool run = sprint && player.Stamina > Parameters.MinimumSprintStamina && magnitude > 0f;
            player.Stamina = Clamp01(player.Stamina + (run ? -Parameters.StaminaDrain : Parameters.StaminaRecovery) * dt);
            float tired = Parameters.TiredSpeedFraction + (1f - Parameters.TiredSpeedFraction) * player.Stamina;
            float speed = (run ? Parameters.SprintSpeed : Parameters.JogSpeed) * magnitude * tired;
            Vec3 desiredVelocity = player.Facing * speed;
            Vec3 change = desiredVelocity - player.Velocity;
            float maxChange = (speed > player.Velocity.Length ? Parameters.Acceleration : Parameters.Braking) * dt;
            if (change.LengthSquared > maxChange * maxChange) change = Normalize(change) * maxChange;
            player.Velocity = player.Velocity + change;
            player.Position = ClampPlayer(player.Position + player.Velocity * dt);
        }
        private void SeparatePlayers()
        {
            for (int a = 0; a < PlayerCount; a++)
            for (int b = a + 1; b < PlayerCount; b++)
            {
                Vec3 delta = Players[b].Position - Players[a].Position;
                float d2 = delta.LengthSquared;
                float diameter = Parameters.PlayerRadius * 2f;
                if (d2 >= diameter * diameter) continue;
                float d = (float)Math.Sqrt(d2);
                Vec3 separation = d > DirectionEpsilon ? delta * (1f / d) : new Vec3(a % 2 == 0 ? 1f : -1f, 0f, 0f);
                Vec3 correction = separation * ((diameter - d) * 0.5f);
                Players[a].Position = ClampPlayer(Players[a].Position - correction);
                Players[b].Position = ClampPlayer(Players[b].Position + correction);
            }
        }
        private FootballBallAction HumanAction(int actor, PlayerCommand command, Vec3 ball, float dt)
        {
            int team = Players[actor].Team;
            if (charging[team] && chargeActor[team] != actor) { charging[team] = false; charge[team] = 0f; }
            if (command.ShootPressed)
            {
                charging[team] = BallOwner == actor;
                chargeActor[team] = actor; charge[team] = 0f;
            }
            if (command.ShootHeld && charging[team]) charge[team] = Math.Min(Parameters.ChargeSeconds, charge[team] + dt);
            if (BallOwner == actor)
            {
                Vec3 aim = new Vec3(command.MoveX, 0f, command.MoveY);
                if (aim.LengthSquared < DirectionEpsilon) aim = Players[actor].Facing;
                if (command.ShootReleased && charging[team])
                {
                    float strength = Clamp01(charge[team] / Parameters.ChargeSeconds);
                    charge[team] = 0f; charging[team] = false; return Shoot(actor, ball, aim, strength);
                }
                if (command.PassPressed) return Pass(actor, ball, aim, FootballKickKind.Pass);
                if (command.ThroughPressed) return Pass(actor, ball, aim, FootballKickKind.Through);
                if (command.LobPressed) return Pass(actor, ball, aim, FootballKickKind.Lob);
            }
            else if ((command.ShootPressed || command.PassPressed || command.LobPressed) &&
                kickCooldown[actor] <= 0f && ball.Y <= Parameters.ControlHeight &&
                DistanceXZ(Players[actor].Position, ball) <= Parameters.TackleDistance)
                return Tackle(actor, ball);
            if (command.ShootReleased) { charge[team] = 0f; charging[team] = false; }
            return default;
        }
        private FootballBallAction AiAction(int actor, Vec3 ball)
        {
            FootballPlayer player = Players[actor];
            int attack = AttackSign(player.Team);
            float distanceToGoal = pitch.HalfLength - ball.Z * attack;
            if (distanceToGoal <= Parameters.AiShotDistance && Math.Abs(ball.X) <= Parameters.AiShotHalfWidth)
                return Shoot(actor, ball, new Vec3(-ball.X, 0f, attack * distanceToGoal), Parameters.AiShotCharge, true);
            float pressure = NearestOpponentDistance(actor);
            if (pressure < Parameters.AiPressureRadius || possessionTime[actor] >= Parameters.AiPassInterval)
            {
                // Human assistance stays inside its directional cone. CPU may also
                // recycle possession sideways/backwards when no forward lane exists.
                int target = FindPassTarget(actor, ball, player.Facing, false);
                if (target >= 0) return Pass(actor, ball, Players[target].Position - ball, FootballKickKind.Pass);
            }
            return default;
        }
        private FootballBallAction Dribble(int actor, Vec3 ball)
        {
            FootballPlayer player = Players[actor];
            Vec3 target = player.Position + player.Facing * Parameters.DribbleLead;
            Vec3 correction = new Vec3(target.X - ball.X, 0f, target.Z - ball.Z) * Parameters.DribbleCorrection;
            Vec3 velocity = player.Velocity + correction;
            if (velocity.Length > Parameters.DribbleMaxSpeed) velocity = Normalize(velocity) * Parameters.DribbleMaxSpeed;
            dribbleCooldown[actor] = Parameters.DribbleInterval;
            return new FootballBallAction { HasKick = true, Velocity = velocity, Actor = actor, Kind = FootballKickKind.Dribble };
        }
        private FootballBallAction Shoot(int actor, Vec3 ball, Vec3 aim, float charge01, bool cpu = false)
        {
            int attack = AttackSign(Players[actor].Team);
            float goalX = 0f;
            if (cpu)
            {
                FootballPlayer keeper = Players[(1 - Players[actor].Team) * PlayersPerTeam];
                float side = keeper.Position.X > 0f ? -1f : 1f;
                if (Math.Abs(keeper.Position.X) < Parameters.PlayerRadius) side = random.NextUnit() < 0.5f ? -1f : 1f;
                goalX = side * (pitch.GoalHalfWidth - pitch.BallRadius) * Parameters.ShotCornerFraction;
            }
            Vec3 goal = new Vec3(goalX, 0f, attack * pitch.HalfLength);
            Vec3 direct = Normalize(new Vec3(goal.X - ball.X, 0f, goal.Z - ball.Z));
            Vec3 direction = cpu ? direct : Normalize(aim);
            if (direction.LengthSquared < DirectionEpsilon) direction = Players[actor].Facing;
            if (Dot(direction, direct) > 0f) direction = Normalize(Vec3.Lerp(direction, direct, Parameters.ShotAimAssist));
            float spread = (random.NextUnit() * 2f - 1f) * Parameters.ShotSpreadRadians;
            direction = Rotate(direction, spread);
            float speed = Parameters.ShotMinSpeed + (Parameters.ShotMaxSpeed - Parameters.ShotMinSpeed) * charge01;
            Vec3 velocity = direction * speed + new Vec3(0f, Parameters.ShotVerticalSpeed + Parameters.ShotLiftByCharge * charge01, 0f);
            Stats(Players[actor].Team).Shots++;
            return Kick(actor, velocity, new Vec3(0f, Normalize(aim).X * Parameters.ShotSpin, 0f), FootballKickKind.Shot);
        }
        private FootballBallAction Pass(int actor, Vec3 ball, Vec3 aim, FootballKickKind kind)
        {
            int target = FindPassTarget(actor, ball, aim);
            Vec3 targetPosition;
            if (target >= 0)
            {
                float speed = Parameters.PassMinSpeed;
                targetPosition = Players[target].Position;
                float extra = kind == FootballKickKind.Through ? Parameters.ThroughLeadSeconds : Parameters.PassLeadSeconds;
                // Two fixed refinements approximate an interception point without iterative allocations.
                for (int i = 0; i < 2; i++)
                {
                    float travel = DistanceXZ(ball, targetPosition) / speed;
                    targetPosition = Players[target].Position + Players[target].Velocity * (travel + extra);
                    speed = Clamp(DistanceXZ(ball, targetPosition) * Parameters.PassSpeedPerMetre, Parameters.PassMinSpeed, Parameters.PassMaxSpeed);
                }
            }
            else targetPosition = ball + Normalize(aim) * Parameters.PassMaxDistance;
            Vec3 direction = Normalize(new Vec3(targetPosition.X - ball.X, 0f, targetPosition.Z - ball.Z));
            if (direction.LengthSquared < DirectionEpsilon) direction = Players[actor].Facing;
            float passSpeed = Clamp(DistanceXZ(ball, targetPosition) * Parameters.PassSpeedPerMetre, Parameters.PassMinSpeed, Parameters.PassMaxSpeed);
            if (kind == FootballKickKind.Through) passSpeed = Parameters.PassMaxSpeed;
            Vec3 velocity = direction * passSpeed;
            if (kind == FootballKickKind.Lob) velocity = velocity + new Vec3(0f, Parameters.LobVerticalSpeed, 0f);
            FootballBallAction action = Kick(actor, velocity, default, kind);
            Stats(Players[actor].Team).Passes++;
            pendingPassTeam = Players[actor].Team; pendingPassActor = actor;
            return action;
        }
        private int FindPassTarget(int actor, Vec3 ball, Vec3 aim, bool directionalCone = true)
        {
            aim = Normalize(aim);
            int result = -1;
            float best = float.NegativeInfinity;
            int start = Players[actor].Team * PlayersPerTeam;
            for (int i = start + 1; i < start + PlayersPerTeam; i++)
            {
                if (i == actor) continue;
                Vec3 offset = Players[i].Position - ball;
                float distance = offset.Length;
                if (distance < Parameters.PassMinimumDistance || distance > Parameters.PassMaxDistance) continue;
                Vec3 direction = Normalize(offset);
                float alignment = Dot(aim, direction);
                if (directionalCone && alignment < Parameters.PassAssistCosine) continue;
                float risk = 0f;
                int opponents = (1 - Players[actor].Team) * PlayersPerTeam;
                for (int opponent = opponents; opponent < opponents + PlayersPerTeam; opponent++)
                {
                    Vec3 toOpponent = Players[opponent].Position - ball;
                    float projection = Dot(toOpponent, direction);
                    if (projection <= 0f || projection >= distance) continue;
                    float lateral = (toOpponent - direction * projection).Length;
                    if (lateral < Parameters.InterceptionRadius) risk += 1f - lateral / Parameters.InterceptionRadius;
                }
                float progress = offset.Z * AttackSign(Players[actor].Team) / pitch.HalfLength;
                float space = NearestOpponentDistance(i);
                float score = alignment * Parameters.PassAlignmentWeight + progress * Parameters.PassProgressWeight +
                    space * Parameters.PassSpaceWeight - distance * Parameters.PassDistancePenalty - risk * Parameters.PassInterceptionPenalty;
                if (score > best) { best = score; result = i; }
            }
            return result;
        }
        private FootballBallAction TryGoalkeeper(Vec3 ball, Vec3 velocity)
        {
            for (int team = 0; team < 2; team++)
            {
                int actor = team * PlayersPerTeam;
                FootballPlayer keeper = Players[actor];
                if (kickCooldown[actor] > 0f || ball.Y > Parameters.KeeperHeight || velocity.Length > Parameters.KeeperSaveSpeed ||
                    DistanceXZ(keeper.Position, ball) > Parameters.KeeperReach) continue;
                int attack = AttackSign(team);
                if (pitch.HalfLength + ball.Z * attack > Parameters.KeeperAdvance + Parameters.KeeperReach) continue;
                bool save = velocity.Z * attack < 0f && velocity.Length >= Parameters.PassMinSpeed;
                if (save) Stats(team).Saves++;
                Vec3 direction = Normalize(new Vec3(-ball.X, 0f, attack * pitch.HalfLength));
                FootballBallAction action = Kick(actor, direction * Parameters.KeeperClearSpeed + new Vec3(0f, Parameters.LobVerticalSpeed, 0f),
                    default, save ? FootballKickKind.Save : FootballKickKind.Clear);
                kickCooldown[actor] += Parameters.KeeperHoldSeconds;
                return action;
            }
            return default;
        }
        private FootballBallAction TryAiTackle(Vec3 ball, bool twoPlayers)
        {
            if (BallOwner < 0 || ball.Y > Parameters.ControlHeight) return default;
            int ownerTeam = Players[BallOwner].Team;
            int defender = chasers[1 - ownerTeam];
            if ((!CpuHome && defender == ControlledPlayer) || (twoPlayers && defender == AwayControlledPlayer) ||
                kickCooldown[defender] > 0f || DistanceXZ(Players[defender].Position, ball) > Parameters.TackleDistance) return default;
            kickCooldown[defender] = Parameters.TackleCooldown;
            if (random.NextUnit() > Parameters.AiTackleChance) return default;
            return Tackle(defender, ball);
        }
        private FootballBallAction Tackle(int actor, Vec3 ball)
        {
            Vec3 direction = Normalize(new Vec3(ball.X - Players[actor].Position.X, 0f, ball.Z - Players[actor].Position.Z));
            if (direction.LengthSquared < DirectionEpsilon) direction = Players[actor].Facing;
            FootballBallAction action = Kick(actor, direction * Parameters.TackleBallSpeed, default, FootballKickKind.Tackle);
            kickCooldown[actor] = Parameters.TackleCooldown; Stats(Players[actor].Team).Tackles++;
            return action;
        }
        private FootballBallAction Kick(int actor, Vec3 velocity, Vec3 spin, FootballKickKind kind)
        {
            lastTouchTeam = Players[actor].Team; lastTouchActor = actor;
            kickCooldown[actor] = Parameters.KickCooldown; BallOwner = -1; possessionTime[actor] = 0f;
            pendingPassActor = -1; pendingPassTeam = -1;
            return new FootballBallAction { HasKick = true, Velocity = velocity, Spin = spin, Actor = actor, Kind = kind };
        }
        private FootballBallAction HandleBoundary(BoundaryCrossing crossing)
        {
            BallOwner = -1; pendingPassTeam = -1; pendingPassActor = -1;
            CancelCharges();
            StopPlayers();
            if (crossing.Kind == BoundaryKind.Goal)
            {
                int positiveTeam = AttackSign(0) > 0 ? 0 : 1;
                int scoringTeam = crossing.End == PitchEnd.Positive ? positiveTeam : 1 - positiveTeam;
                if (scoringTeam == 0) HomeGoals++; else AwayGoals++;
                restartTeam = 1 - scoringTeam; Phase = FootballPhase.Goal; phaseCountdown = Parameters.GoalPauseSeconds;
                Status = scoringTeam == 0 ? "GOL DO MANDANTE!" : "GOL DO VISITANTE!";
                return default;
            }
            restartTeam = 1 - lastTouchTeam;
            restartPosition = new Vec3(Clamp(crossing.Position.X, -pitch.HalfWidth + Parameters.RestartInset, pitch.HalfWidth - Parameters.RestartInset),
                CentreBall.Y, Clamp(crossing.Position.Z, -pitch.HalfLength + Parameters.RestartInset, pitch.HalfLength - Parameters.RestartInset));
            int taker = FindNearestPlayer(restartTeam, restartPosition);
            Players[taker].Position = ClampPlayer(restartPosition + new Vec3(0f, 0f, -AttackSign(restartTeam) * Parameters.KickoffPlayerOffset));
            Players[taker].Facing = new Vec3(0f, 0f, AttackSign(restartTeam));
            kickCooldown[taker] = 0f;
            if (restartTeam == 0) ControlledPlayer = taker; else AwayControlledPlayer = taker;
            Phase = FootballPhase.Restart; phaseCountdown = Parameters.RestartSeconds;
            Status = "Bola fora - reposicao de treino";
            boundaries.Reset(restartPosition);
            return TeleportBall(restartPosition);
        }
        private void StopPlayers() { for (int i = 0; i < PlayerCount; i++) Players[i].Velocity = default; }
        private int FindInterceptor(int team, Vec3 ball, Vec3 velocity)
        {
            int best = team * PlayersPerTeam + 1;
            float earliest = float.PositiveInfinity;
            for (int i = team * PlayersPerTeam + 1; i < (team + 1) * PlayersPerTeam; i++)
            {
                float time = InterceptionTime(Players[i], ball, velocity);
                Vec3 target = ClampPlayer(ball + velocity * time);
                float travel = DistanceXZ(Players[i].Position, target) / Parameters.SprintSpeed;
                if (travel < earliest) { earliest = travel; best = i; }
            }
            return best;
        }
        private float InterceptionTime(FootballPlayer player, Vec3 ball, Vec3 velocity)
        {
            Vec3 relative = new Vec3(ball.X - player.Position.X, 0f, ball.Z - player.Position.Z);
            Vec3 horizontal = new Vec3(velocity.X, 0f, velocity.Z);
            float speed = Parameters.SprintSpeed * (Parameters.TiredSpeedFraction +
                (1f - Parameters.TiredSpeedFraction) * player.Stamina);
            float a = horizontal.LengthSquared - speed * speed;
            float b = 2f * Dot(relative, horizontal);
            float c = relative.LengthSquared;
            float time = relative.Length / speed;
            if (Math.Abs(a) < DirectionEpsilon)
            {
                if (b < -DirectionEpsilon) time = -c / b;
            }
            else
            {
                float discriminant = b * b - 4f * a * c;
                if (discriminant >= 0f)
                {
                    float root = (float)Math.Sqrt(discriminant);
                    float first = (-b - root) / (2f * a), second = (-b + root) / (2f * a);
                    if (first > 0f) time = first;
                    if (second > 0f && (first <= 0f || second < first)) time = second;
                }
            }
            return Clamp(time, Parameters.AiReactionSeconds, Parameters.AiInterceptSeconds);
        }
        private int FindNearestPlayer(int team, Vec3 point, int exclude = -1)
        {
            int best = team * PlayersPerTeam + 1;
            float distance = float.PositiveInfinity;
            for (int i = team * PlayersPerTeam + 1; i < (team + 1) * PlayersPerTeam; i++)
            {
                if (i == exclude) continue;
                float d = DistanceSquaredXZ(Players[i].Position, point);
                if (d < distance) { distance = d; best = i; }
            }
            return best;
        }
        private float NearestOpponentDistance(int actor)
        {
            float distance = float.PositiveInfinity;
            int start = (1 - Players[actor].Team) * PlayersPerTeam;
            for (int i = start; i < start + PlayersPerTeam; i++)
                distance = Math.Min(distance, DistanceXZ(Players[actor].Position, Players[i].Position));
            return distance;
        }
        private Vec3 ClampPlayer(Vec3 position) => new Vec3(
            Clamp(position.X, -pitch.HalfWidth + Parameters.BoundaryMargin, pitch.HalfWidth - Parameters.BoundaryMargin), 0f,
            Clamp(position.Z, -pitch.HalfLength + Parameters.BoundaryMargin, pitch.HalfLength - Parameters.BoundaryMargin));
        private static FootballBallAction TeleportBall(Vec3 point) => new FootballBallAction { Teleport = true, Position = point, Actor = -1 };
        private static bool Finite(Vec3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        private static float FiniteAxis(float x) => float.IsFinite(x) ? x : 0f;
        private static float Clamp(float x, float min, float max) => Math.Max(min, Math.Min(max, x));
        private static float Clamp01(float x) => Clamp(x, 0f, 1f);
        private static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        private static Vec3 Normalize(Vec3 v) => v.LengthSquared > DirectionEpsilon ? v * (1f / v.Length) : default;
        private static float DistanceXZ(Vec3 a, Vec3 b) => (float)Math.Sqrt(DistanceSquaredXZ(a, b));
        private static float DistanceSquaredXZ(Vec3 a, Vec3 b) { float x = a.X - b.X, z = a.Z - b.Z; return x * x + z * z; }
        private static Vec3 Rotate(Vec3 direction, float angle)
        {
            float s = (float)Math.Sin(angle), c = (float)Math.Cos(angle);
            return new Vec3(direction.X * c + direction.Z * s, 0f, direction.Z * c - direction.X * s);
        }
    }
}
