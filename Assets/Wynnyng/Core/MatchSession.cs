namespace Wynnyng.Core
{
    public readonly struct MatchEvent
    {
        public readonly long Tick;
        public readonly BoundaryCrossing Crossing;
        public MatchEvent(long tick, BoundaryCrossing crossing) { Tick = tick; Crossing = crossing; }
    }

    /// <summary>Phase 1A laboratory session. Restarts and ownership rules belong to phase 3.</summary>
    public sealed class MatchSession
    {
        private SimulationClock clock;
        private readonly BoundaryTracker boundaries;
        private readonly IEventChannel<MatchEvent> events;
        public IRandomSource Random { get; }
        public long Tick => clock.Tick;
        public float StepSeconds => clock.StepSeconds;
        public bool BallInPlay => boundaries.IsLive;
        public int GoalsAtPositiveEnd { get; private set; }
        public int GoalsAtNegativeEnd { get; private set; }
        public int BallsOut { get; private set; }
        public BoundaryCrossing LastCrossing { get; private set; }

        public MatchSession(int frequency, PitchGeometry geometry, Vec3 ballPosition,
            IRandomSource random, IEventChannel<MatchEvent> events)
        {
            clock = new SimulationClock(frequency);
            boundaries = new BoundaryTracker(geometry, ballPosition);
            Random = random ?? throw new System.ArgumentNullException(nameof(random));
            this.events = events ?? throw new System.ArgumentNullException(nameof(events));
        }
        public void Step(Vec3 ballPosition)
        {
            clock.Advance();
            BoundaryCrossing crossing = boundaries.Step(ballPosition);
            if (!crossing.HasCrossed) return;
            LastCrossing = crossing;
            if (crossing.Kind == BoundaryKind.Goal)
            {
                if (crossing.End == PitchEnd.Positive) GoalsAtPositiveEnd++;
                else GoalsAtNegativeEnd++;
            }
            else BallsOut++;
            events.Publish(new MatchEvent(clock.Tick, crossing));
        }
        public void ResetBall(Vec3 position)
        {
            LastCrossing = default;
            boundaries.Reset(position);
        }
    }
}
