using System.Text.Json;
using SsaLab4.Core;

if (args.Contains("--self-test"))
{
    Simulation.SelfTest();
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseWebRoot("wwwroot");
builder.WebHost.UseUrls("http://127.0.0.1:5084");
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = false;
});
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/maps", () => MapFactory.All().Select(m => new
{
    m.Id, m.Name, m.Is3D,
    Obstacles = m.Is3D ? m.Boxes.Count : m.Circles.Count,
    Agents = m.Agents.Count
}));
app.MapGet("/api/map/{id:int}", (int id) => id is >= 0 and <= 4 ? Results.Ok(MapFactory.Get(id)) : Results.NotFound());
app.MapPost("/api/run", (RunRequest request) =>
{
    try { return Results.Ok(Simulation.Run(request)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});
Console.WriteLine("SSA Lab 4 · http://127.0.0.1:5084");
Console.WriteLine("Для проверки без интерфейса: dotnet run -- --self-test");
app.Run();
