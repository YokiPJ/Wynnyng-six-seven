namespace Wynnyng.Core
{
    /// <summary>Match UI contract; device and rendering details stay outside the simulation.</summary>
    public interface IFootballControls
    {
        FootballMatch Match { get; }
        bool IsPaused { get; }
        bool TwoPlayers { get; }
        void TogglePause();
        void Restart();
        void SetCpuHome(bool enabled);
        void SetTwoPlayers(bool enabled);
        void Exit();
    }
}
