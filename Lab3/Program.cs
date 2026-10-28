using SwarmLab3.Simulation;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    Environment.ExitCode = SelfTests.Run();
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<SwarmEngine>();
builder.Services.AddHostedService<SimulationLoop>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/state", (SwarmEngine simulation) => simulation.Snapshot());
app.MapPost("/api/control", (ControlRequest request, SwarmEngine simulation) =>
{
    if (request.Running is bool running) simulation.SetRunning(running);
    if (request.Speed is double speed) simulation.SetSpeed(speed);
    return Results.Ok(simulation.Snapshot());
});
app.MapPost("/api/reset", (ResetRequest request, SwarmEngine simulation) =>
{
    if (!ScenarioFactory.Names.Contains(request.Scenario ?? "urban"))
        return Results.BadRequest(new { error = "Неизвестный сценарий" });
    if (request.Planner is not (null or "theta" or "astar"))
        return Results.BadRequest(new { error = "Неизвестный алгоритм" });
    simulation.Reset(request.Scenario ?? "urban", request.Planner ?? "theta");
    return Results.Ok(simulation.Snapshot());
});

app.Run();

public sealed record ControlRequest(bool? Running, double? Speed);
public sealed record ResetRequest(string? Scenario, string? Planner);
