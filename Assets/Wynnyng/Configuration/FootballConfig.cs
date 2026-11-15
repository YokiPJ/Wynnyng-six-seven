using System;
using UnityEngine;
using Wynnyng.Core;

namespace Wynnyng.Configuration
{
    /// <summary>Original test teams, match tuning and procedural presentation. Copied before runtime edits.</summary>
    [CreateAssetMenu(menuName = "Wynnyng/Football Config")]
    public sealed class FootballConfig : ScriptableObject
    {
        public FootballParameters Gameplay = new FootballParameters();
        public string HomeName = "Aurora FC";
        public string AwayName = "Horizonte AC";
        public bool IsPlaceholder = true;
        public Color HomeColor = new Color(0.08f, 0.36f, 0.95f);
        public Color AwayColor = new Color(0.96f, 0.22f, 0.16f);
        public Color KeeperHomeColor = new Color(1f, 0.73f, 0.08f);
        public Color KeeperAwayColor = new Color(0.35f, 0.85f, 0.32f);
        public Color HomeShortsColor = new Color(0.95f, 0.96f, 1f);
        public Color AwayShortsColor = new Color(0.08f, 0.12f, 0.22f);
        public Color SocksColor = new Color(0.92f, 0.95f, 1f);
        public Color BootsColor = new Color(0.045f, 0.06f, 0.08f);
        public Color HairColor = new Color(0.09f, 0.055f, 0.035f);
        public Color SelectedColor = new Color(0.78f, 1f, 0.18f);
        public Color AwaySelectedColor = new Color(0.2f, 0.96f, 1f);
        public Color LabelColor = Color.white;
        public Color[] SkinColors =
        {
            new Color(0.73f, 0.46f, 0.30f), new Color(0.94f, 0.70f, 0.48f),
            new Color(0.39f, 0.23f, 0.15f), new Color(0.84f, 0.57f, 0.39f)
        };
        public float PlayerHeight = 1.82f;
        public float PlayerRadius = 0.32f;
        public float HeadRadius = 0.18f;
        public float TorsoHeightFraction = 0.34f;
        public float TorsoWidthFraction = 0.36f;
        public float TorsoDepthFraction = 0.20f;
        public float LegLengthFraction = 0.44f;
        public float LegWidthFraction = 0.12f;
        public float LegSpacingFraction = 0.20f;
        public float ArmLengthFraction = 0.38f;
        public float ArmWidthFraction = 0.10f;
        public float ArmSpreadDegrees = 12f;
        public float ShortsHeightFraction = 0.13f;
        public float SockLengthFraction = 0.20f;
        public float BootHeightFraction = 0.075f;
        public float BootLengthFraction = 0.19f;
        public float ShirtBandHeightFraction = 0.055f;
        public float NeckHeightFraction = 0.055f;
        public float HairHeightFraction = 0.06f;
        public float BodyTurnDegreesPerSecond = 720f;
        public float StrideRadiansPerMetre = 1.3f;
        public float LegSwingDegrees = 35f;
        public float ArmSwingDegrees = 28f;
        public float BodyBobMetres = 0.025f;
        public float SelectionRadius = 0.68f;
        public float SelectionLineWidth = 0.075f;
        public float SelectionElevation = 0.035f;
        public int SelectionSegments = 24;
        public float LabelHeight = 2.15f;
        public float LabelFontSize = 3.5f;
        public float LabelWidth = 1.3f;
        public float LabelBoxHeight = 0.55f;
        public float LabelYaw = -90f;
        public float CameraSpeedZoom = 0.15f;
        public float CameraMaximumZoom = 1.3f;
        public float CameraFieldOfView = 55f;
        public float CameraLeadSeconds = 0.12f;
        public Vector3 CameraOffset = new Vector3(42f, 36f, 0f);
        public float CameraDamping = 4f;
        public float CameraBallWeight = 0.7f;
        public Vector2 JogRange = new Vector2(3f, 8f);
        public Vector2 SprintRange = new Vector2(6f, 12f);
        public Vector2 PassRange = new Vector2(12f, 26f);
        public Vector2 ShotRange = new Vector2(18f, 40f);
        public Vector2 PressureRange = new Vector2(2f, 12f);

        public void ValidateOrThrow()
        {
            if (Gameplay == null) throw new InvalidOperationException("FootballConfig: missing gameplay tuning.");
            Gameplay.Validate();
            if (string.IsNullOrWhiteSpace(HomeName) || string.IsNullOrWhiteSpace(AwayName))
                throw new InvalidOperationException("FootballConfig: both test teams need a name.");
            // These checks run only during initialization; every scalar/color remains validated
            // as presentation tuning grows, including NaN and Infinity entered through scripts.
            var fields = typeof(FootballConfig).GetFields();
            for (int i = 0; i < fields.Length; i++)
            {
                Type type = fields[i].FieldType;
                if (type == typeof(float) && !ConfigValidation.Finite((float)fields[i].GetValue(this)))
                    throw new InvalidOperationException("FootballConfig: non-finite " + fields[i].Name + ".");
                if (type == typeof(Color) && !ConfigValidation.Finite((Color)fields[i].GetValue(this)))
                    throw new InvalidOperationException("FootballConfig: non-finite " + fields[i].Name + ".");
            }
            if (!ConfigValidation.Positive(PlayerHeight) || !ConfigValidation.Positive(PlayerRadius) ||
                PlayerRadius * 2f >= PlayerHeight || Gameplay.PlayerRadius * 2f >= PlayerHeight ||
                !ConfigValidation.Positive(HeadRadius) || HeadRadius * 2f >= PlayerHeight ||
                !Fraction(TorsoHeightFraction) || !Fraction(TorsoWidthFraction) || !Fraction(TorsoDepthFraction) ||
                !Fraction(LegLengthFraction) || !Fraction(LegWidthFraction) || !Fraction(LegSpacingFraction) ||
                !Fraction(ArmLengthFraction) || !Fraction(ArmWidthFraction) || !Fraction(ShortsHeightFraction) ||
                !Fraction(SockLengthFraction) || !Fraction(BootHeightFraction) || !Fraction(BootLengthFraction) ||
                !Fraction(ShirtBandHeightFraction) || !Fraction(NeckHeightFraction) || !Fraction(HairHeightFraction) ||
                TorsoHeightFraction + LegLengthFraction >= 1f ||
                !ConfigValidation.NonNegative(ArmSpreadDegrees) || ArmSpreadDegrees >= 90f ||
                !ConfigValidation.Positive(BodyTurnDegreesPerSecond) || !ConfigValidation.Positive(StrideRadiansPerMetre) ||
                !ConfigValidation.NonNegative(LegSwingDegrees) || LegSwingDegrees >= 90f ||
                !ConfigValidation.NonNegative(ArmSwingDegrees) || ArmSwingDegrees >= 90f ||
                !ConfigValidation.NonNegative(BodyBobMetres) || !ConfigValidation.Positive(SelectionRadius) ||
                !ConfigValidation.Positive(SelectionLineWidth) || !ConfigValidation.Positive(SelectionElevation) ||
                SelectionSegments < 8 || SelectionSegments > 128 ||
                !ConfigValidation.Positive(LabelHeight) || !ConfigValidation.Positive(LabelFontSize) ||
                !ConfigValidation.Positive(LabelWidth) || !ConfigValidation.Positive(LabelBoxHeight) ||
                SkinColors == null || SkinColors.Length == 0)
                throw new InvalidOperationException("FootballConfig: invalid player proportions or presentation limits.");
            for (int i = 0; i < SkinColors.Length; i++)
                if (!ConfigValidation.Finite(SkinColors[i])) throw new InvalidOperationException("FootballConfig: invalid skin color.");
            if (!ConfigValidation.Finite(CameraOffset) || CameraOffset.y <= 0f ||
                !ConfigValidation.Positive(CameraDamping) || !ConfigValidation.UnitRange(CameraBallWeight) ||
                !ConfigValidation.NonNegative(CameraSpeedZoom) || CameraMaximumZoom < 1f || CameraMaximumZoom > 3f ||
                !ConfigValidation.Positive(CameraFieldOfView) || CameraFieldOfView >= 120f ||
                !ConfigValidation.NonNegative(CameraLeadSeconds) ||
                !Range(JogRange) || !Range(SprintRange) || !Range(PassRange) || !Range(ShotRange) || !Range(PressureRange) ||
                JogRange.y > SprintRange.y || SprintRange.x < JogRange.x ||
                PassRange.x < Gameplay.PassMinSpeed || ShotRange.x < Gameplay.ShotMinSpeed)
                throw new InvalidOperationException("FootballConfig: invalid camera or runtime tuning ranges.");
        }
        private static bool Fraction(float value) => ConfigValidation.Positive(value) && value < 1f;
        private static bool Range(Vector2 value) => ConfigValidation.Finite(value) && value.x > 0f && value.y > value.x;
    }
}
