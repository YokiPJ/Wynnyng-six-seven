using System;
using UnityEngine;
using Wynnyng.Core;

namespace Wynnyng.Configuration
{
    [CreateAssetMenu(menuName = "Wynnyng/Pitch Config")]
    public sealed class PitchConfig : ScriptableObject
    {
        [Header("Metres; outer edges of boundary lines; clear goal opening")]
        [Min(1f)] public float Length = 105f;
        [Min(1f)] public float Width = 68f;
        [Min(0.1f)] public float GoalWidth = 7.32f;
        [Min(0.1f)] public float GoalHeight = 2.44f;
        [Min(0.1f)] public float GoalDepth = 2.5f;
        [Min(0.01f)] public float PostRadius = 0.06f;
        [Min(0f)] public float BoundaryContactTolerance = 0.002f;
        [Min(0.01f)] public float SurfaceThickness = 0.25f;
        [Min(1f)] public float Runoff = 8f;
        [Header("Markings, metres")]
        [Min(0.01f)] public float LineWidth = 0.12f;
        [Min(0.001f)] public float LineElevation = 0.012f;
        [Min(0.1f)] public float CentreRadius = 9.15f;
        [Min(0.1f)] public float PenaltyAreaDepth = 16.5f;
        [Min(0.1f)] public float PenaltyAreaWidth = 40.32f;
        [Min(0.1f)] public float GoalAreaDepth = 5.5f;
        [Min(0.1f)] public float GoalAreaWidth = 18.32f;
        [Min(0.1f)] public float PenaltySpotDistance = 11f;
        [Min(0.01f)] public float SpotRadius = 0.11f;
        [Min(0.1f)] public float CornerRadius = 1f;
        [Min(8)] public int CircleSegments = 96;
        [Min(2)] public int GrassStripes = 20;
        [Header("Net blockout")]
        [Min(0.1f)] public float NetSpacing = 0.25f;
        [Min(0.01f)] public float NetColliderThickness = 0.04f;
        [Range(0f, 1f)] public float NetRestitution = 0.08f;
        [Range(0f, 1f)] public float NetFriction = 0.8f;
        [Range(0f, 1f)] public float SurfaceFriction = 0.18f;
        public Color GrassLight = new Color(0.13f, 0.36f, 0.23f);
        public Color GrassDark = new Color(0.10f, 0.29f, 0.18f);
        public Color RunoffColor = new Color(0.07f, 0.19f, 0.13f);
        public Color MarkingColor = new Color(0.90f, 0.95f, 0.88f);
        public Color NetColor = new Color(0.65f, 0.75f, 0.68f);

        public PitchGeometry Geometry(float ballRadius) => new PitchGeometry(Length, Width, GoalWidth, GoalHeight, ballRadius, BoundaryContactTolerance);
        public void ValidateOrThrow(float ballRadius)
        {
            _ = Geometry(ballRadius);
            if (!ConfigValidation.Positive(Length) || !ConfigValidation.Positive(Width) ||
                !ConfigValidation.Positive(GoalWidth) || !ConfigValidation.Positive(GoalHeight) ||
                !ConfigValidation.Positive(GoalDepth) || !ConfigValidation.Positive(PostRadius) ||
                !ConfigValidation.NonNegative(BoundaryContactTolerance) ||
                !ConfigValidation.Positive(SurfaceThickness) || !ConfigValidation.Positive(Runoff) || Runoff < GoalDepth ||
                !ConfigValidation.Positive(LineWidth) || !ConfigValidation.Positive(LineElevation) ||
                !ConfigValidation.Positive(CentreRadius) || !ConfigValidation.Positive(PenaltyAreaDepth) ||
                !ConfigValidation.Positive(PenaltyAreaWidth) || !ConfigValidation.Positive(GoalAreaDepth) ||
                !ConfigValidation.Positive(GoalAreaWidth) || !ConfigValidation.Positive(PenaltySpotDistance) ||
                !ConfigValidation.Positive(CornerRadius) || !ConfigValidation.Positive(SpotRadius) ||
                CircleSegments < 8 || GrassStripes < 2 || !ConfigValidation.Positive(NetSpacing) ||
                !ConfigValidation.Positive(NetColliderThickness) || !ConfigValidation.UnitRange(NetRestitution) ||
                !ConfigValidation.UnitRange(NetFriction) || !ConfigValidation.UnitRange(SurfaceFriction) ||
                PenaltyAreaWidth >= Width || GoalAreaWidth >= PenaltyAreaWidth || PenaltyAreaDepth >= Length * 0.5f ||
                PenaltyAreaDepth <= PenaltySpotDistance || CentreRadius <= PenaltyAreaDepth - PenaltySpotDistance ||
                GoalAreaDepth >= PenaltyAreaDepth || !ConfigValidation.Finite(GrassLight) ||
                !ConfigValidation.Finite(GrassDark) || !ConfigValidation.Finite(RunoffColor) ||
                !ConfigValidation.Finite(MarkingColor) || !ConfigValidation.Finite(NetColor))
                throw new InvalidOperationException("PitchConfig: invalid geometry, markings or net dimensions.");
        }
    }
}
