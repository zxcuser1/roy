using SwarmLab3.Models;
using SwarmLab3.Planning;

namespace SwarmLab3.Simulation;

public static class SelfTests
{
    public static int Run()
    {
        int errors = 0;
        foreach (var name in ScenarioFactory.Names)
        {
            var s = ScenarioFactory.Create(name);
            var planner = new GridPlanner(s.World);
            foreach (var theta in new[] { false, true })
            {
                var p = planner.FindPath(s.Start, s.Goal, theta);
                bool valid = p.Found && p.Path.Count >= 2 &&
                    p.Path.Zip(p.Path.Skip(1), (a, b) => s.World.LineOfSight(a, b, 1.10f)).All(x => x) &&
                    System.Numerics.Vector3.Distance(p.Path[0], s.Start) < 2f &&
                    System.Numerics.Vector3.Distance(p.Path[^1], s.Goal) < 2f;
                Console.WriteLine($"[{(valid ? "OK" : "ERROR")}] {name} " +
                    $"{(theta ? "Theta*-APF" : "A*")}: waypoints={p.Path.Count}; " +
                    $"length={p.Distance:F1}; expanded={p.Expanded}; ms={p.Milliseconds:F1}");
                if (!valid) errors++;
            }
        }
        foreach (var name in ScenarioFactory.Names)
        {
            var engine = new SwarmEngine();
            engine.Reset(name, "theta");
            bool safe = engine.IsSafe();
            for (int i = 0; i < 2000 && safe; i++)
            {
                engine.Step(0.05);
                safe = engine.IsSafe();
            }
            Console.WriteLine($"[{(safe ? "OK" : "ERROR")}] {name}: 100 сек. " +
                "без пересечений препятствий и дронов");
            if (!safe) errors++;
        }
        // Регрессия: в узком коридоре финишный строй сжимается рядом со стеной.
        // Раньше дроны доходили до цели, но arrived оставался false.
        foreach (var planner in new[] { "theta", "astar" })
        {
            var engine = new SwarmEngine();
            engine.Reset("corridor", planner);
            bool safe = engine.IsSafe();
            const int maxSteps = 3200; // 160 секунд модельного времени
            for (int i = 0; i < maxSteps && safe && !engine.HasArrived; i++)
            {
                engine.Step(0.05);
                safe = engine.IsSafe();
            }
            bool passed = safe && engine.HasArrived;
            Console.WriteLine($"[{(passed ? "OK" : "ERROR")}] corridor/{planner}: " +
                $"финиш достигнут, безопасность сохранена: {passed}");
            if (!passed) errors++;
        }

        Console.WriteLine(errors == 0 ? "ALL TESTS PASSED" : $"FAILED: {errors}");
        return errors == 0 ? 0 : 1;
    }
}
