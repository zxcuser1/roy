using System.Diagnostics;
using System.Numerics;
using SwarmLab3.Models;

namespace SwarmLab3.Planning;

public sealed record PlanResult(List<Vector3> Path, int Expanded, double Milliseconds,
    float Distance, int Turns, bool Found);

public sealed class GridPlanner
{
    private const float Cell = 1.5f;
    private const float Radius = 1.10f;
    private static readonly int Nx = (int)(World.Width / Cell) + 1;
    private static readonly int Ny = (int)(World.Length / Cell) + 1;
    private static readonly int Nz = (int)(World.Height / Cell) + 1;
    private static readonly (int x, int y, int z)[] Dirs = GenerateDirections();
    private readonly World _world;

    public GridPlanner(World world) => _world = world;

    public PlanResult FindPath(Vector3 start, Vector3 goal, bool theta)
    {
        var stopwatch = Stopwatch.StartNew();
        int size = Nx * Ny * Nz;
        var g = new float[size];
        var parent = new int[size];
        var closed = new bool[size];
        Array.Fill(g, float.PositiveInfinity);
        Array.Fill(parent, -1);

        int source = ToIndex(start);
        int destination = ToIndex(goal);
        var queue = new PriorityQueue<(int Id, float G), double>();
        g[source] = 0;
        parent[source] = source;
        queue.Enqueue((source, 0), Vector3.Distance(ToPoint(source), goal));
        int expanded = 0;

        while (queue.Count > 0)
        {
            var (current, oldG) = queue.Dequeue();
            if (closed[current] || oldG > g[current] + 1e-4f) continue;
            closed[current] = true;
            expanded++;
            if (current == destination) break;

            var cp = ToPoint(current);
            var (x, y, z) = Decode(current);
            foreach (var (dx, dy, dz) in Dirs)
            {
                int ix = x + dx, iy = y + dy, iz = z + dz;
                if (ix < 0 || ix >= Nx || iy < 0 || iy >= Ny || iz < 0 || iz >= Nz) continue;
                int neighbor = Index(ix, iy, iz);
                if (closed[neighbor]) continue;
                var np = ToPoint(neighbor);
                if (!_world.LineOfSight(cp, np, Radius)) continue;

                int via = current;
                float newG = g[current] + Vector3.Distance(cp, np);
                // Theta*: используем родителя текущего узла при прямой видимости.
                if (theta && parent[current] >= 0)
                {
                    int ancestor = parent[current];
                    var ap = ToPoint(ancestor);
                    if (ancestor != current && _world.LineOfSight(ap, np, Radius))
                    {
                        float shortcutG = g[ancestor] + Vector3.Distance(ap, np);
                        if (shortcutG < newG)
                        {
                            newG = shortcutG;
                            via = ancestor;
                        }
                    }
                }
                if (newG >= g[neighbor] - 1e-5f) continue;
                g[neighbor] = newG;
                parent[neighbor] = via;
                // Небольшой потенциал отталкивания направляет Theta* вдали от стен.
                // При таком ранжировании математическая гарантия оптимальности не заявляется.
                float potential = theta ? Potential(np) : 0f;
                double priority = newG + Vector3.Distance(np, ToPoint(destination))
                    + (theta ? 0.25 * potential : 0.0);
                queue.Enqueue((neighbor, newG), priority);
            }
        }

        var route = new List<Vector3>();
        if (!closed[destination])
        {
            stopwatch.Stop();
            return new PlanResult(route, expanded, stopwatch.Elapsed.TotalMilliseconds, 0, 0, false);
        }
        int id = destination;
        for (int i = 0; i <= size; ++i)
        {
            route.Add(ToPoint(id));
            if (id == source) break;
            id = parent[id];
            if (id < 0) throw new InvalidOperationException("Сломана цепочка родителей");
        }
        route.Reverse();
        // Начальную и конечную координаты делаем точными при условии прямой видимости.
        if (route.Count > 1 && _world.LineOfSight(start, route[1], Radius)) route[0] = start;
        if (route.Count > 1 && _world.LineOfSight(route[^2], goal, Radius)) route[^1] = goal;
        // Возможна корректная одноклеточная траектория.
        if (route.Count == 1)
        {
            route[0] = start;
            if (start != goal) route.Add(goal);
        }
        float length = 0;
        for (int i = 1; i < route.Count; ++i)
            length += Vector3.Distance(route[i - 1], route[i]);
        int turns = 0;
        for (int i = 2; i < route.Count; ++i)
        {
            var a = Vectors.Normalized(route[i - 1] - route[i - 2]);
            var b = Vectors.Normalized(route[i] - route[i - 1]);
            if (Vector3.Dot(a, b) < 0.996f) turns++;
        }
        stopwatch.Stop();
        return new PlanResult(route, expanded, stopwatch.Elapsed.TotalMilliseconds, length, turns, true);
    }

    private float Potential(Vector3 p)
    {
        // Ограниченный потенциал, чтобы близость стен не доминировала над длиной пути.
        const float influence = 4.5f;
        float value = 0;
        foreach (var o in _world.Obstacles)
        {
            float d = MathF.Max(0.25f, o.Distance(p) - Radius);
            if (d < influence)
                value += MathF.Pow(1f / d - 1f / influence, 2);
        }
        return MathF.Min(5f, value);
    }

    private static (int, int, int)[] GenerateDirections()
    {
        var result = new List<(int, int, int)>();
        for (int z = -1; z <= 1; ++z)
        for (int y = -1; y <= 1; ++y)
        for (int x = -1; x <= 1; ++x)
            if (x != 0 || y != 0 || z != 0) result.Add((x, y, z));
        return result.ToArray();
    }

    private static int Index(int x, int y, int z) => x + Nx * (y + Ny * z);
    private static (int x, int y, int z) Decode(int id)
    {
        int x = id % Nx;
        int y = (id / Nx) % Ny;
        int z = id / (Nx * Ny);
        return (x, y, z);
    }
    private static Vector3 ToPoint(int id)
    {
        var (x, y, z) = Decode(id);
        return new Vector3(x * Cell, y * Cell, z * Cell);
    }
    private static int ToIndex(Vector3 point) => Index(
        Math.Clamp((int)MathF.Round(point.X / Cell), 0, Nx - 1),
        Math.Clamp((int)MathF.Round(point.Y / Cell), 0, Ny - 1),
        Math.Clamp((int)MathF.Round(point.Z / Cell), 0, Nz - 1));
}
