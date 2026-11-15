namespace Wynnyng.Core
{
    public interface ILaboratoryControls
    {
        long Tick { get; }
        float BallSpeed { get; }
        int PositiveGoals { get; }
        int NegativeGoals { get; }
        int BallsOut { get; }
        string Status { get; }
        string ScenarioName { get; }
        void SelectScenario(int direction);
        void RequestLaunch();
        void RequestReset();
        void RequestExit();
    }
}
