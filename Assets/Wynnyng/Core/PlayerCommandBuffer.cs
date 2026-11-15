namespace Wynnyng.Core
{
    /// <summary>Retains frame edges until a fixed step consumes them exactly once.</summary>
    public sealed class PlayerCommandBuffer
    {
        private PlayerCommand pending;
        public void Push(in PlayerCommand sample)
        {
            pending.MoveX = sample.MoveX;
            pending.MoveY = sample.MoveY;
            pending.Sprint = sample.Sprint;
            pending.ShootHeld = sample.ShootHeld;
            pending.ShootPressed |= sample.ShootPressed;
            pending.ShootReleased |= sample.ShootReleased;
            pending.PassPressed |= sample.PassPressed;
            pending.ThroughPressed |= sample.ThroughPressed;
            pending.LobPressed |= sample.LobPressed;
            pending.SwitchPressed |= sample.SwitchPressed;
            pending.PausePressed |= sample.PausePressed;
        }
        public PlayerCommand Consume()
        {
            PlayerCommand command = pending;
            pending.ShootPressed = pending.ShootReleased = false;
            pending.PassPressed = pending.ThroughPressed = pending.LobPressed = false;
            pending.SwitchPressed = pending.PausePressed = false;
            return command;
        }
        public void Clear() { pending = default; }
    }
}
