namespace SwarmLab3.Simulation;

public sealed class SimulationLoop(SwarmEngine engine) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            engine.Step(0.05);
    }
}
