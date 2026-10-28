using System.Diagnostics;

namespace SsaLab4.Core;

public static class Simulation
{
    public static RunResult Run(RunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MapId is < 0 or > 4) throw new ArgumentException("Карта должна быть 0–4.");
        if (request.Population is < 6 or > 150) throw new ArgumentException("Размер популяции: 6–150.");
        if (request.Iterations is < 1 or > 600) throw new ArgumentException("Итерации: 1–600.");
        if (request.Speed <= .05 || request.Speed > 20) throw new ArgumentException("Скорость должна быть в диапазоне (0.05;20].");
        if (request.Separation < .01 || request.Separation > 3) throw new ArgumentException("Дистанция безопасности: 0.01–3.");
        var timer = Stopwatch.StartNew();
        var map = MapFactory.Get(request.MapId);
        var warnings = new List<string>();
        var fixedAgents = new List<Pair>();
        for (int a = 0; a < map.Agents.Count; a++)
        {
            var pair = map.Agents[a];
            if (!Geometry.FreePoint(map, pair.Start) && request.RepairInvalidStarts)
            {
                var corrected = RepairStart(map, pair.Start);
                if (corrected.HasValue)
                {
                    warnings.Add($"Исходная таблица: старт БПЛА {a + 1} ({pair.Start.X:g}, {pair.Start.Y:g}, {pair.Start.Z:g}) внутри препятствия. Для расчёта перенесён в ({corrected.Value.X:g}, {corrected.Value.Y:g}, {corrected.Value.Z:g}).");
                    pair = pair with { Start = corrected.Value };
                }
            }
            else if (!Geometry.FreePoint(map, pair.Start))
                warnings.Add($"БПЛА {a + 1}: исходная точка находится внутри препятствия. Отключена коррекция старта.");
            fixedAgents.Add(pair);
        }
        map = map with { Agents = fixedAgents };
        var result = new List<RouteResult>();
        var search = new GridAStar(map);
        for (int id = 0; id < map.Agents.Count; id++)
        {
            var pair = map.Agents[id];
            var startPath = search.Find(pair.Start, pair.Goal);
            if (startPath is null)
            {
                warnings.Add($"БПЛА {id + 1}: на дискретной сетке не найден безопасный начальный маршрут.");
                var fallback = new List<V3> { pair.Start, pair.Goal };
                result.Add(new RouteResult(id + 1, pair.Start, pair.Goal, fallback, fallback, fallback,
                    0, Geometry.PathLength(fallback), Geometry.PathLength(fallback), 10000, 0, false, [], "Нет безопасного решения."));
                continue;
            }
            var ssa = new SalpSwarm(map, pair, startPath, request.Population, request.Iterations,
                unchecked(request.Seed + id * 7919));
            var (best, history, cost) = ssa.Optimize();
            // После оптимизации удаляем лишние точки, затем безопасно сглаживаем.
            var simplified = Geometry.Simplify(map, best);
            var actual = request.Smooth ? Geometry.SmoothQuintic(map, simplified) : simplified;
            bool safe = Geometry.FreePath(map, actual);
            string note = "SSA, начальное допустимое решение A*";
            if (!safe)
            {
                warnings.Add($"БПЛА {id + 1}: найденный SSA-маршрут недопустим; оставлен безопасный маршрут A*.");
                best = startPath;
                actual = startPath;
                safe = Geometry.FreePath(map, actual);
                note = "Запасной путь A* (SSA не нашёл допустимого решения)";
            }
            result.Add(new RouteResult(id + 1, pair.Start, pair.Goal, startPath, best, actual,
                0, Geometry.PathLength(actual), Geometry.PathLength(startPath), cost, Geometry.CountSharpTurns(simplified), safe, history, note));
        }
        // Временная координация: минимальные задержки запуска вместо пересечения движущихся БПЛА.
        for (int i = 1; i < result.Count; i++)
        {
            var route = result[i];
            if (!route.CollisionFree) continue;
            double delay = 0;
            bool success = false;
            for (int tries = 0; tries < 1000; tries++)
            {
                success = true;
                for (int j = 0; j < i; j++)
                {
                    if (!result[j].CollisionFree) continue;
                    var gap = Geometry.MinimalSimultaneousDistance(route.Path, delay,
                        result[j].Path, result[j].StartDelay, request.Speed);
                    if (gap < request.Separation - 1e-7) { success = false; break; }
                }
                if (success) break;
                delay += .25;
            }
            if (!success)
            {
                // Гарантированное последовательное выполнение, если встречных траекторий слишком много.
                delay = result.Take(i).Max(r => r.StartDelay + r.Length / request.Speed) + 1;
                warnings.Add($"БПЛА {i + 1}: пришлось дождаться завершения предыдущих полётов.");
            }
            result[i] = route with { StartDelay = delay };
        }
        bool separated = true;
        for (int i = 0; i < result.Count; i++)
        for (int j = i + 1; j < result.Count; j++)
            if (result[i].CollisionFree && result[j].CollisionFree &&
                Geometry.MinimalSimultaneousDistance(result[i].Path, result[i].StartDelay,
                    result[j].Path, result[j].StartDelay, request.Speed) < request.Separation - 1e-7)
                separated = false;
        if (!separated) warnings.Add("Есть нарушение временного разделения траекторий.");
        timer.Stop();
        return new RunResult(map, result, timer.Elapsed.TotalMilliseconds,
            result.All(x => x.CollisionFree), separated, request.Separation,
            request.Iterations, request.Population,
            "SSA (лидер/последователи) + A* для начального пути + локальный поиск + квинтическое сглаживание",
            warnings);
    }

    private static V3? RepairStart(MapData map, V3 start)
    {
        V3? nearest = null;
        double distance = double.PositiveInfinity;
        foreach (var obstacle in map.Boxes)
        for (int axis = 0; axis < 3; axis++)
        foreach (double value in new[] { obstacle.Min.At(axis) - Geometry.Clearance * 2,
                                         obstacle.Max.At(axis) + Geometry.Clearance * 2 })
        {
            var candidate = start.With(axis, value);
            if (!Geometry.FreePoint(map, candidate)) continue;
            double d = V3.Distance(start, candidate);
            if (d < distance) { distance = d; nearest = candidate; }
        }
        return nearest;
    }

    public static void SelfTest()
    {
        int[] expected = [3, 8, 7, 14, 15];
        for (int i = 0; i <= 4; i++)
        {
            var map = MapFactory.Get(i);
            int got = i == 0 ? map.Circles.Count : map.Boxes.Count;
            if (got != expected[i]) throw new Exception($"Карта {i}: препятствия {got} вместо {expected[i]}");
            if (map.Agents.Any(a => !Geometry.FreePoint(map, a.Goal)))
                throw new Exception($"Карта {i}: целевая точка внутри препятствия.");
            int badStarts = map.Agents.Count(a => !Geometry.FreePoint(map, a.Start));
            Console.WriteLine($"Карта {i}: препятствий {got}, агентов {map.Agents.Count}, старты внутри препятствий {badStarts}.");
        }
        foreach (int i in Enumerable.Range(0, 5))
        {
            var r = Run(new RunRequest { MapId = i, Population = 12, Iterations = 12, Seed = 42 });
            Console.WriteLine($"Тест карты {i}: safe={r.AllSafe}, separated={r.AgentsSeparated}, время={r.ElapsedMs:F0} мс");
            foreach (var x in r.Routes)
                Console.WriteLine($"  БПЛА {x.Id} путь={x.Length:F2}, задержка={x.StartDelay:F2}, safe={x.CollisionFree}");
            if (!r.AllSafe || !r.AgentsSeparated) throw new Exception($"Карта {i}: маршрут не прошёл проверку.");
            foreach (var x in r.Routes)
                if (!Geometry.FreePath(r.Map, x.Path)) throw new Exception("Путь пересекает препятствие");
        }
        Console.WriteLine("SELF-TEST PASSED");
    }
}
