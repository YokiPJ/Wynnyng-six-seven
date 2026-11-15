using System;
using UnityEngine;
using Wynnyng.Core;

namespace Wynnyng.Configuration
{
    [CreateAssetMenu(menuName = "Wynnyng/Ball Config")]
    public sealed class BallConfig : ScriptableObject
    {
        [Min(0.01f)] public float Radius = 0.11f;
        [Min(0.01f)] public float Mass = 0.43f;
        [Min(0f)] public float AirDensity = 1.225f;
        [Min(0f)] public float DragCoefficient = 0.25f;
        [Min(0f)] public float MagnusCoefficient = 0.0015f;
        [Range(0f, 1f)] public float Restitution = 0.65f;
        [Range(0f, 1f)] public float SurfaceFriction = 0.18f;
        [Min(0f)] public float RollingFriction = 0.015f;
        [Min(0f)] public float AngularDamping = 0.05f;
        [Min(0.01f)] public float MaxAngularSpeed = 120f;
        [Min(0.001f)] public float ContactOffset = 0.005f;
        [Min(0.001f)] public float GroundProbeDistance = 0.018f;
        [Min(0f)] public float GroundedMaxVerticalSpeed = 0.5f;
        [Min(0f)] public float ResetClearance = 0.01f;
        public Color BaseColor = new Color(0.96f, 0.95f, 0.86f);
        public Color PanelColor = new Color(0.08f, 0.10f, 0.12f);
        [Min(1f)] public float PanelFrequency = 8f;
        public Vector3 RestPosition => new Vector3(0f, Radius + ResetClearance, 0f);
        public BallPhysicsParameters Parameters => new BallPhysicsParameters(
            Mass, Radius, AirDensity, DragCoefficient, MagnusCoefficient, RollingFriction);
        public void ValidateOrThrow()
        {
            _ = Parameters;
            if (!ConfigValidation.Positive(Mass) || !ConfigValidation.Positive(Radius) ||
                !ConfigValidation.NonNegative(AirDensity) || !ConfigValidation.NonNegative(DragCoefficient) ||
                !ConfigValidation.NonNegative(MagnusCoefficient) || !ConfigValidation.NonNegative(RollingFriction) ||
                !ConfigValidation.UnitRange(Restitution) || !ConfigValidation.UnitRange(SurfaceFriction) ||
                !ConfigValidation.NonNegative(AngularDamping) || !ConfigValidation.Positive(MaxAngularSpeed) ||
                !ConfigValidation.Positive(ContactOffset) || ContactOffset >= Radius ||
                !ConfigValidation.Positive(GroundProbeDistance) || !ConfigValidation.NonNegative(GroundedMaxVerticalSpeed) ||
                !ConfigValidation.NonNegative(ResetClearance) || !ConfigValidation.Finite(PanelFrequency) || PanelFrequency < 1f ||
                !ConfigValidation.Finite(BaseColor) || !ConfigValidation.Finite(PanelColor))
                throw new InvalidOperationException("BallConfig: invalid physical parameters.");
        }
    }
}
