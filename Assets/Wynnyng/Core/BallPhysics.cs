using System;

namespace Wynnyng.Core
{
    public readonly struct BallPhysicsParameters
    {
        public readonly float Mass, Radius, AirDensity, DragCoefficient, MagnusCoefficient, RollingFriction;
        public BallPhysicsParameters(float mass, float radius, float density, float drag, float magnus, float rolling)
        {
            if (mass <= 0f || radius <= 0f || density < 0f || drag < 0f || magnus < 0f || rolling < 0f ||
                !float.IsFinite(mass + radius + density + drag + magnus + rolling))
                throw new ArgumentOutOfRangeException(nameof(mass));
            Mass = mass; Radius = radius; AirDensity = density;
            DragCoefficient = drag; MagnusCoefficient = magnus; RollingFriction = rolling;
        }
    }

    public static class BallPhysics
    {
        /// <summary>Acceleration in m/s². Angular velocity is in rad/s. Gravity is applied by the engine.</summary>
        public static Vec3 Acceleration(Vec3 velocity, Vec3 spin, bool grounded, float gravityMagnitude,
            float deltaSeconds, in BallPhysicsParameters parameters)
        {
            if (deltaSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            float area = (float)Math.PI * parameters.Radius * parameters.Radius;
            float dragRate = 0.5f * parameters.AirDensity * parameters.DragCoefficient * area *
                             velocity.Length / parameters.Mass;
            // Even extreme diagnostic velocities must not reverse under explicit drag integration.
            dragRate = Math.Min(dragRate, 1f / deltaSeconds);
            Vec3 acceleration = velocity * -dragRate + Vec3.Cross(spin, velocity) * parameters.MagnusCoefficient;
            if (!grounded) return acceleration;
            Vec3 horizontal = new Vec3(velocity.X, 0f, velocity.Z);
            float speed = horizontal.Length;
            if (speed <= 0f) return acceleration;
            float remainingRate = Math.Max(0f, 1f / deltaSeconds - dragRate);
            float rollingRate = Math.Min(parameters.RollingFriction * gravityMagnitude / speed, remainingRate);
            return acceleration - horizontal * rollingRate;
        }
    }

    public interface IBallBody
    {
        Vec3 Position { get; }
        Vec3 Velocity { get; }
        void Teleport(Vec3 position);
        void Launch(Vec3 velocity, Vec3 spin);
    }
}
