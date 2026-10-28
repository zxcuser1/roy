namespace SsaLab4.Core;

public sealed class SalpSwarm
{
    private readonly MapData map;
    private readonly Pair agent;
    private readonly int population;
    private readonly int iterations;
    private readonly Random random;
    private readonly int dims;
    private readonly double[] lower, upper;
    private readonly double[] baseline;

    public SalpSwarm(MapData map, Pair agent, List<V3> seed, int popSize, int maxIterations, int seedValue)
    {
        this.map = map;
        this.agent = agent;
        population = popSize;
        iterations = maxIterations;
        random = new Random(seedValue);
        var controls = Geometry.AddControlPoints(seed);
        dims = (controls.Count - 2) * (map.Is3D ? 3 : 2);
        lower = new double[dims]; upper = new double[dims]; baseline = new double[dims];
        for (int k = 0; k < controls.Count - 2; k++)
        for (int axis = 0; axis < (map.Is3D ? 3 : 2); axis++)
        {
            int j = k * (map.Is3D ? 3 : 2) + axis;
            lower[j] = map.Min.At(axis);
            upper[j] = map.Max.At(axis);
            baseline[j] = controls[k + 1].At(axis);
        }
    }

    private List<V3> AsPath(double[] genome)
    {
        var route = new List<V3> { agent.Start };
        int axes = map.Is3D ? 3 : 2;
        for (int i = 0; i < genome.Length; i += axes)
            route.Add(new V3(genome[i], genome[i + 1], axes == 3 ? genome[i + 2] : 0));
        route.Add(agent.Goal);
        return route;
    }

    private double Fitness(double[] x)
    {
        var path = AsPath(x);
        // C_fuel = путь, C_divergence = резкие повороты, C_gap = 0 (конец закреплён в G).
        // Дополнительно: жёсткий штраф за пересечение препятствий и выход за пределы карты.
        double cost = Geometry.PathLength(path) + Geometry.CountSharpTurns(path) * 1.6;
        for (int i = 1; i < path.Count - 1; i++)
            if (!Geometry.InBounds(map, path[i])) cost += 10000;
        for (int i = 1; i < path.Count; i++)
            if (!Geometry.FreeSegment(map, path[i - 1], path[i])) cost += 10000;
        return cost;
    }

    private double Normal()
    {
        double u = Math.Max(1e-10, random.NextDouble()), v = random.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v);
    }

    private double[] Perturb(double[] source, double scale)
    {
        var result = (double[])source.Clone();
        // Изменяется небольшая группа координат, чтобы не разрушить допустимый маршрут.
        int waypoint = random.Next(dims / (map.Is3D ? 3 : 2));
        int axisCount = map.Is3D ? 3 : 2;
        for (int k = 0; k < axisCount; k++)
        {
            int j = waypoint * axisCount + k;
            double range = upper[j] - lower[j];
            result[j] = Math.Clamp(result[j] + Normal() * range * scale, lower[j], upper[j]);
        }
        return result;
    }

    public (List<V3> Best, List<double> History, double Cost) Optimize()
    {
        var populationSet = new double[population][];
        populationSet[0] = (double[])baseline.Clone();
        for (int i = 1; i < population; i++)
        {
            if (i < population * .75)
            {
                var mutated = (double[])baseline.Clone();
                int changes = 1 + random.Next(3);
                for (int k = 0; k < changes; k++)
                    mutated = Perturb(mutated, i % 4 == 0 ? .09 : .025);
                populationSet[i] = mutated;
            }
            else
            {
                var x = new double[dims];
                for (int j = 0; j < dims; j++)
                    x[j] = lower[j] + random.NextDouble() * (upper[j] - lower[j]);
                populationSet[i] = x;
            }
        }
        var best = (double[])baseline.Clone();
        double bestCost = Fitness(best);
        var history = new List<double>(iterations + 1) { bestCost };
        for (int iter = 0; iter < iterations; iter++)
        {
            // Сначала выбирается «источник пищи» F = лучшее найденное решение.
            Array.Sort(populationSet, (a, b) => Fitness(a).CompareTo(Fitness(b)));
            if (Fitness(populationSet[0]) < bestCost)
            {
                best = (double[])populationSet[0].Clone();
                bestCost = Fitness(best);
            }
            // Классический SSA: c1 = 2 * exp(- (4*l/L)^2).
            double t = 4.0 * (iter + 1) / iterations;
            double c1 = 2.0 * Math.Exp(-t * t);
            var old = populationSet.Select(x => (double[])x.Clone()).ToArray();
            for (int i = 0; i < population; i++)
            {
                for (int j = 0; j < dims; j++)
                {
                    if (i == 0)
                    {
                        double c2 = random.NextDouble(), c3 = random.NextDouble();
                        double step = c1 * ((upper[j] - lower[j]) * c2 + lower[j]);
                        populationSet[0][j] = best[j] + (c3 >= .5 ? step : -step);
                    }
                    else
                    {
                        populationSet[i][j] = .5 * (old[i][j] + populationSet[i - 1][j]);
                    }
                    populationSet[i][j] = Math.Clamp(populationSet[i][j], lower[j], upper[j]);
                }
            }
            // Небольшой локальный поиск и возврат части популяции к безопасному F.
            // Это дополнительная практическая эвристика, не базовое уравнение SSA.
            for (int n = 0; n < 4; n++)
            {
                double scale = (.055 * (1 - (double)iter / iterations) + .003);
                var proposal = Perturb(best, scale);
                double fitness = Fitness(proposal);
                if (fitness + 1e-9 < bestCost)
                {
                    best = proposal;
                    bestCost = fitness;
                }
            }
            if (population > 3 && iter % 5 == 0)
                populationSet[^1] = Perturb(best, .016);
            history.Add(bestCost);
        }
        foreach (var x in populationSet)
        {
            double value = Fitness(x);
            if (value < bestCost) { best = (double[])x.Clone(); bestCost = value; }
        }
        history[^1] = bestCost;
        return (AsPath(best), history, bestCost);
    }
}
