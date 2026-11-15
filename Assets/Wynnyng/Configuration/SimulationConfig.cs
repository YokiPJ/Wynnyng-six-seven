using System;
using UnityEngine;

namespace Wynnyng.Configuration
{
    [CreateAssetMenu(menuName = "Wynnyng/Simulation Config")]
    public sealed class SimulationConfig : ScriptableObject
    {
        [Min(1)] public int FixedFrequency = 60;
        [Min(1)] public int TargetFrameRate = 60;
        [Min(0.02f)] public float MaximumFrameDelta = 0.1f;
        public Vector3 Gravity = new Vector3(0f, -9.81f, 0f);
        public uint Seed = 2026;
        [Min(1)] public int EventCapacity = 16;
        [Min(1)] public int SolverIterations = 8;
        [Min(1)] public int SolverVelocityIterations = 4;
        [Min(0f)] public float BounceThreshold = 0.2f;
        public bool EnableLogging = true;

        public void ValidateOrThrow()
        {
            if (FixedFrequency != 60 || TargetFrameRate < 1 ||
                MaximumFrameDelta < 1f / FixedFrequency || EventCapacity < 1 ||
                SolverIterations < 1 || SolverVelocityIterations < 1 || BounceThreshold < 0f ||
                !float.IsFinite(MaximumFrameDelta) || !float.IsFinite(Gravity.sqrMagnitude) || Gravity.y >= 0f)
                throw new InvalidOperationException("SimulationConfig: use 60 Hz, gravity down and valid positive limits.");
        }
    }
}
