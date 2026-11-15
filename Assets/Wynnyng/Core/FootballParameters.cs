using System;

namespace Wynnyng.Core
{
    [Serializable]
    public sealed class FootballFormationSlot
    {
        public float Width, Depth;
        public FootballFormationSlot(float width, float depth) { Width = width; Depth = depth; }
    }

    /// <summary>Serialized inside FootballConfig; copied at startup so runtime tuning never edits an asset.</summary>
    [Serializable]
    public sealed class FootballParameters
    {
        public float MatchSeconds = 300f;
        public float KickoffSeconds = 1.2f;
        public float GoalPauseSeconds = 2.4f;
        public float RestartSeconds = 1.1f;
        public float HalfTimeSeconds = 3f;
        public float JogSpeed = 5.2f;
        public float SprintSpeed = 9f;
        public float Acceleration = 16f;
        public float Braking = 20f;
        public float TurnRadiansPerSecond = 8f;
        public float StaminaDrain = 0.055f;
        public float StaminaRecovery = 0.035f;
        public float TiredSpeedFraction = 0.7f;
        public float MinimumSprintStamina = 0.08f;
        public float PlayerRadius = 0.4f;
        public float SeparationStrength = 3.5f;
        public float SeparationDistance = 1.2f;
        public float BoundaryMargin = 0.65f;
        public float ControlRadius = 1.65f;
        public float ControlHeight = 1.1f;
        public float ControlMaxSpeed = 25f;
        public float OwnerReleaseRadius = 2.8f;
        public float DribbleLead = 0.85f;
        public float DribbleInterval = 0.12f;
        public float DribbleCorrection = 8f;
        public float DribbleMaxSpeed = 13f;
        public float KickCooldown = 0.42f;
        public float ChargeSeconds = 0.95f;
        public float PassMinSpeed = 12f;
        public float PassMaxSpeed = 22f;
        public float PassSpeedPerMetre = 0.45f;
        public float PassAssistCosine = 0.8660254f;
        public float PassMaxDistance = 48f;
        public float PassMinimumDistance = 3f;
        public float PassAlignmentWeight = 3f;
        public float PassProgressWeight = 1.4f;
        public float PassSpaceWeight = 0.08f;
        public float PassDistancePenalty = 0.012f;
        public float PassInterceptionPenalty = 2f;
        public float InterceptionRadius = 2.5f;
        public float PassLeadSeconds = 0.35f;
        public float ThroughLeadSeconds = 1.1f;
        public float LobVerticalSpeed = 9f;
        public float ShotMinSpeed = 18f;
        public float ShotMaxSpeed = 32f;
        public float ShotVerticalSpeed = 3f;
        public float ShotLiftByCharge = 3f;
        public float ShotSpreadRadians = 0.045f;
        public float ShotAimAssist = 0.75f;
        public float ShotSpin = 22f;
        public float ShotCornerFraction = 0.8f;
        public float TackleDistance = 2.2f;
        public float TackleCooldown = 0.8f;
        public float AiTackleChance = 0.25f;
        public float TackleBallSpeed = 11f;
        public float AiDecisionSeconds = 0.1f;
        public float AiReactionSeconds = 0.22f;
        public float AiInterceptSeconds = 3f;
        public float AiArrivalRadius = 1.8f;
        public float AiShotDistance = 26f;
        public float AiShotCharge = 0.7f;
        public float AiShotHalfWidth = 17f;
        public float AiPassInterval = 3.6f;
        public float AiPressureRadius = 3.2f;
        public float AiSprintDistance = 7f;
        public float AiSupportingAdvance = 10f;
        public float TeamBallShift = 0.27f;
        public float TeamWidth = 0.88f;
        public float DefensiveLine = 0.68f;
        public float AutoSwitchDistance = 3.5f;
        public float AutoSwitchDelay = 0.65f;
        public float KeeperGoalOffset = 1.4f;
        public float KeeperAdvance = 6f;
        public float KeeperReach = 2.4f;
        public float KeeperHeight = 2.8f;
        public float KeeperSaveSpeed = 42f;
        public float KeeperClearSpeed = 24f;
        public float KeeperHoldSeconds = 0.45f;
        public float RestartInset = 2f;
        public float KickoffPlayerOffset = 0.8f;
        public float BallResetHeight = 0.14f;
        public FootballFormationSlot[] Formation =
        {
            new FootballFormationSlot(0f, -0.95f),
            new FootballFormationSlot(-0.78f, -0.57f), new FootballFormationSlot(-0.26f, -0.65f),
            new FootballFormationSlot(0.26f, -0.65f), new FootballFormationSlot(0.78f, -0.57f),
            new FootballFormationSlot(-0.75f, -0.15f), new FootballFormationSlot(-0.25f, -0.23f),
            new FootballFormationSlot(0.25f, -0.23f), new FootballFormationSlot(0.75f, -0.15f),
            new FootballFormationSlot(-0.24f, 0.13f), new FootballFormationSlot(0.24f, 0.13f)
        };
        public FootballParameters Clone()
        {
            var copy = (FootballParameters)MemberwiseClone();
            if (Formation == null) return copy;
            copy.Formation = new FootballFormationSlot[Formation.Length];
            for (int i = 0; i < Formation.Length; i++)
                if (Formation[i] != null) copy.Formation[i] = new FootballFormationSlot(Formation[i].Width, Formation[i].Depth);
            return copy;
        }
        public void Validate()
        {
            // Reflection is initialization-only and catches every scalar even when new tuning is added.
            var fields = typeof(FootballParameters).GetFields();
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType != typeof(float)) continue;
                float value = (float)fields[i].GetValue(this);
                if (!float.IsFinite(value) || value <= 0f)
                    throw new ArgumentOutOfRangeException(fields[i].Name, "Football tuning must be finite and positive.");
            }
            if (MatchSeconds < 10f || SprintSpeed < JogSpeed || PassMaxSpeed < PassMinSpeed ||
                ShotMaxSpeed < ShotMinSpeed || ShotAimAssist > 1f || ShotCornerFraction > 1f ||
                AiShotCharge > 1f || AiTackleChance > 1f || PassAssistCosine > 1f ||
                AiShotCharge > 1f || AiTackleChance > 1f || MinimumSprintStamina > 1f || TiredSpeedFraction > 1f || TeamWidth > 1f ||
                Formation == null || Formation.Length != FootballMatch.PlayersPerTeam)
                throw new ArgumentException("Invalid football speed, assistance, match length or formation.");
            for (int i = 0; i < Formation.Length; i++)
            {
                FootballFormationSlot slot = Formation[i];
                if (slot == null || !float.IsFinite(slot.Width) || !float.IsFinite(slot.Depth) ||
                    Math.Abs(slot.Width) > 1f || Math.Abs(slot.Depth) > 1f)
                    throw new ArgumentException("Formation positions must be finite normalized pitch coordinates.");
            }
        }
    }
}
