using System;

namespace Wynnyng.Core
{
    public enum BoundaryKind { None, Goal, Touchline, GoalLine }
    public enum PitchEnd { None, Negative, Positive }

    public readonly struct PitchGeometry
    {
        public readonly float HalfLength, HalfWidth, GoalHalfWidth, GoalHeight, BallRadius, ContactTolerance;
        public PitchGeometry(float length, float width, float goalWidth, float goalHeight, float ballRadius, float contactTolerance = 0f)
        {
            if (length <= goalWidth || width <= goalWidth || goalWidth <= ballRadius * 2f ||
                goalHeight <= ballRadius * 2f || ballRadius <= 0f || contactTolerance < 0f || contactTolerance >= ballRadius ||
                !float.IsFinite(length + width + goalWidth + goalHeight + ballRadius + contactTolerance))
                throw new ArgumentOutOfRangeException(nameof(length), "Invalid field or ball dimensions.");
            HalfLength = length * 0.5f;
            HalfWidth = width * 0.5f;
            GoalHalfWidth = goalWidth * 0.5f;
            GoalHeight = goalHeight;
            BallRadius = ballRadius;
            ContactTolerance = contactTolerance;
        }
    }

    public readonly struct BoundaryCrossing
    {
        public readonly BoundaryKind Kind;
        public readonly PitchEnd End;
        public readonly Vec3 Position;
        public readonly float SegmentFraction;
        public bool HasCrossed => Kind != BoundaryKind.None;
        public BoundaryCrossing(BoundaryKind kind, PitchEnd end, Vec3 position, float fraction)
        { Kind = kind; End = end; Position = position; SegmentFraction = fraction; }
    }

    public static class BoundaryEvaluator
    {
        /// <summary>Entire ball must cross. Tests the swept centre, rather than a trigger volume.</summary>
        public static BoundaryCrossing Evaluate(Vec3 from, Vec3 to, in PitchGeometry pitch)
        {
            float first = float.PositiveInfinity;
            BoundaryCrossing result = default;
            CheckEnd(from, to, pitch, 1f, PitchEnd.Positive, ref first, ref result);
            CheckEnd(from, to, pitch, -1f, PitchEnd.Negative, ref first, ref result);
            CheckSide(from, to, pitch, 1f, ref first, ref result);
            CheckSide(from, to, pitch, -1f, ref first, ref result);
            return result;
        }

        private static void CheckEnd(Vec3 from, Vec3 to, in PitchGeometry pitch, float sign,
            PitchEnd end, ref float first, ref BoundaryCrossing result)
        {
            float plane = pitch.HalfLength + pitch.BallRadius;
            float start = sign * from.Z;
            float finish = sign * to.Z;
            if (start > plane || finish <= plane || finish <= start) return;
            float t = (plane - start) / (finish - start);
            if (t >= first) return;
            Vec3 point = Vec3.Lerp(from, to, t);
            bool fits = Math.Abs(point.X) + pitch.BallRadius <= pitch.GoalHalfWidth + pitch.ContactTolerance &&
                        point.Y >= pitch.BallRadius - pitch.ContactTolerance &&
                        point.Y + pitch.BallRadius <= pitch.GoalHeight + pitch.ContactTolerance;
            first = t;
            result = new BoundaryCrossing(fits ? BoundaryKind.Goal : BoundaryKind.GoalLine, end, point, t);
        }

        private static void CheckSide(Vec3 from, Vec3 to, in PitchGeometry pitch, float sign,
            ref float first, ref BoundaryCrossing result)
        {
            float plane = pitch.HalfWidth + pitch.BallRadius;
            float start = sign * from.X;
            float finish = sign * to.X;
            if (start > plane || finish <= plane || finish <= start) return;
            float t = (plane - start) / (finish - start);
            if (t >= first) return;
            first = t;
            result = new BoundaryCrossing(BoundaryKind.Touchline, PitchEnd.None, Vec3.Lerp(from, to, t), t);
        }
    }

    public sealed class BoundaryTracker
    {
        private readonly PitchGeometry geometry;
        private Vec3 previous;
        public bool IsLive { get; private set; }
        public BoundaryTracker(PitchGeometry geometry, Vec3 initialPosition)
        { this.geometry = geometry; Reset(initialPosition); }
        public void Reset(Vec3 position) { previous = position; IsLive = true; }
        public BoundaryCrossing Step(Vec3 position)
        {
            if (!IsLive) return default;
            BoundaryCrossing crossing = BoundaryEvaluator.Evaluate(previous, position, geometry);
            previous = position;
            if (crossing.HasCrossed) IsLive = false;
            return crossing;
        }
    }
}
