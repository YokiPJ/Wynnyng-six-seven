namespace Wynnyng.Core
{
    public enum FootballPhase { Kickoff, Playing, Goal, Restart, HalfTime, FullTime }
    public enum FootballKickKind { None, Dribble, Pass, Through, Lob, Shot, Tackle, Save, Clear }

    public struct FootballBallAction
    {
        public bool Teleport, HasKick;
        public Vec3 Position, Velocity, Spin;
        public int Actor;
        public FootballKickKind Kind;
    }

    public sealed class FootballPlayer
    {
        public Vec3 Position, Velocity, Facing;
        public float Stamina = 1f;
        public bool IsGoalkeeper;
        public int Team, Number;
    }

    public sealed class FootballStatistics
    {
        public int Shots, Passes, CompletedPasses, Tackles, Saves;
        public float PossessionSeconds;
        public void Clear()
        {
            Shots = 0; Passes = 0; CompletedPasses = 0; Tackles = 0; Saves = 0; PossessionSeconds = 0f;
        }
    }
}
