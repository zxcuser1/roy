namespace SsaLab4.Core;

// Детерминированное построение допустимого стартового решения; оптимизация выполняется SSA.
public sealed class GridAStar
{
    private readonly MapData map;
    private readonly double step;
    private readonly int nx, ny, nz;
    private readonly bool[] passable;
    private readonly (int Dx, int Dy, int Dz)[] dirs;

    public GridAStar(MapData map)
    {
        this.map = map;
        step = map.Id switch { 0 => .25, 4 => .7, _ => .5 };
        nx = Math.Max(1, (int)Math.Ceiling((map.Max.X - map.Min.X) / step));
        ny = Math.Max(1, (int)Math.Ceiling((map.Max.Y - map.Min.Y) / step));
        nz = map.Is3D ? Math.Max(1, (int)Math.Ceiling((map.Max.Z - map.Min.Z) / step)) : 1;
        passable = new bool[nx * ny * nz];
        for (int z = 0; z < nz; z++)
        for (int y = 0; y < ny; y++)
        for (int x = 0; x < nx; x++) passable[Key(x, y, z)] = Geometry.FreePoint(map, Pos(x, y, z));
        var list = new List<(int, int, int)>();
        for (int z = -1; z <= 1; z++)
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            if ((x != 0 || y != 0 || z != 0) && (map.Is3D || z == 0)) list.Add((x, y, z));
        dirs = list.ToArray();
    }

    private int Key(int x, int y, int z) => (z * ny + y) * nx + x;
    private V3 Pos(int x, int y, int z) => new(
        Math.Min(map.Max.X, map.Min.X + (x + .5) * step),
        Math.Min(map.Max.Y, map.Min.Y + (y + .5) * step),
        map.Is3D ? Math.Min(map.Max.Z, map.Min.Z + (z + .5) * step) : 0);
    private V3 AtKey(int key)
    {
        int x = key % nx, y = key / nx % ny, z = key / (nx * ny);
        return Pos(x, y, z);
    }

    public List<V3>? Find(V3 start, V3 goal)
    {
        if (!Geometry.FreePoint(map, start) || !Geometry.FreePoint(map, goal)) return null;
        if (Geometry.FreeSegment(map, start, goal)) return [start, goal];
        var open = new PriorityQueue<int, double>();
        var distance = new double[passable.Length];
        var previous = new int[passable.Length];
        Array.Fill(distance, double.PositiveInfinity);
        Array.Fill(previous, -2);
        int sources = 0;
        for (int i = 0; i < passable.Length; i++)
        {
            if (!passable[i]) continue;
            var p = AtKey(i);
            var d = V3.Distance(start, p);
            if (d > step * 3.5 || !Geometry.FreeSegment(map, start, p)) continue;
            open.Enqueue(i, d + V3.Distance(p, goal));
            distance[i] = d;
            previous[i] = -1;
            sources++;
        }
        if (sources == 0) return null;
        int found = -1;
        while (open.TryDequeue(out var curr, out var priority))
        {
            var point = AtKey(curr);
            if (priority > distance[curr] + V3.Distance(point, goal) + 1e-8) continue;
            if (V3.Distance(point, goal) < step * 3.5 && Geometry.FreeSegment(map, point, goal))
            {
                found = curr;
                break;
            }
            int cx = curr % nx, cy = curr / nx % ny, cz = curr / (nx * ny);
            foreach (var (dx, dy, dz) in dirs)
            {
                int x = cx + dx, y = cy + dy, z = cz + dz;
                if (x < 0 || y < 0 || z < 0 || x >= nx || y >= ny || z >= nz) continue;
                int key = Key(x, y, z);
                if (!passable[key]) continue;
                var next = AtKey(key);
                if (!Geometry.FreeSegment(map, point, next)) continue;
                double nd = distance[curr] + V3.Distance(point, next);
                if (nd + 1e-9 >= distance[key]) continue;
                distance[key] = nd;
                previous[key] = curr;
                open.Enqueue(key, nd + V3.Distance(next, goal));
            }
        }
        if (found < 0) return null;
        var intermediate = new List<V3>();
        int index = found;
        while (index >= 0)
        {
            intermediate.Add(AtKey(index));
            index = previous[index];
        }
        intermediate.Reverse();
        var result = new List<V3> { start };
        result.AddRange(intermediate);
        result.Add(goal);
        return Geometry.Simplify(map, result);
    }
}
