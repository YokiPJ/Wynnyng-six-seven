namespace Wynnyng.Core
{
    /// <summary>Input snapshot for phase 1B. Device details stay in the Input assembly.</summary>
    public struct PlayerCommand
    {
        public float MoveX, MoveY, Sprint;
        public bool ShootHeld, ShootPressed, ShootReleased;
        public bool PassPressed, ThroughPressed, LobPressed, SwitchPressed, PausePressed;
    }
    public interface IPlayerCommandSource
    {
        PlayerCommand ReadPlayer(int slot);
    }
}
