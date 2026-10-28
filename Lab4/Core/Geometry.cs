namespace SsaLab4.Core;

public static class Geometry
{
    public const double Clearance = 0.055;

    public static bool InBounds(MapData map, V3 p)
    {
        const double e = 1e-8;
        return p.X >= map.Min.X - e && p.X <= map.Max.X + e &&
               p.Y >= map.Min.Y - e && p.Y <= map.Max.Y + e &&
               (!map.Is3D || (p.Z >= map.Min.Z - e && p.Z <= map.Max.Z + e));
    }

    public static bool FreePoint(MapData map, V3 p)
    {
        if (!InBounds(map, p)) return false;
        foreach (var b in map.Boxes)
            if (p.X >= b.Min.X - Clearance && p.X <= b.Max.X + Clearance &&
                p.Y >= b.Min.Y - Clearance && p.Y <= b.Max.Y + Clearance &&
                p.Z >= b.Min.Z - Clearance && p.Z <= b.Max.Z + Clearance) return false;
        foreach (var c in map.Circles)
            if (Math.Pow(p.X - c.Center.X, 2) + Math.Pow(p.Y - c.Center.Y, 2) <=
                Math.Pow(c.Radius + Clearance, 2)) return false;
        return true;
    }

    public static bool FreeSegment(MapData map, V3 a, V3 b)
    {
        if (!FreePoint(map, a) || !FreePoint(map, b)) return false;
        foreach (var obs in map.Boxes)
            if (SegmentBox(a, b, obs, Clearance)) return false;
        foreach (var obs in map.Circles)
            if (PointSegmentDistance2D(obs.Center, a, b) <= obs.Radius + Clearance) return false;
        return true;
    }

    public static bool FreePath(MapData map, IReadOnlyList<V3> path)
    {
        if (path.Count < 2) return false;
        for (int i = 1; i < path.Count; i++)
            if (!FreeSegment(map, path[i - 1], path[i])) return false;
        return true;
    }

    private static bool SegmentBox(V3 a, V3 b, Box box, double margin)
    {
        double lo = 0, hi = 1;
        var d = b - a;
        for (int i = 0; i < 3; i++)
        {
            double o = a.At(i), v = d.At(i);
            double min = box.Min.At(i) - margin, max = box.Max.At(i) + margin;
            if (Math.Abs(v) < 1e-12)
            {
                if (o < min || o > max) return false;
                continue;
            }
            double x = (min - o) / v, y = (max - o) / v;
            if (x > y) (x, y) = (y, x);
            lo = Math.Max(lo, x);
            hi = Math.Min(hi, y);
            if (lo > hi) return false;
        }
        return lo <= hi;
    }

    private static double PointSegmentDistance2D(V3 c, V3 a, V3 b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double sq = dx * dx + dy * dy;
        double t = sq > 1e-15 ? Math.Clamp(((c.X - a.X) * dx + (c.Y - a.Y) * dy) / sq, 0, 1) : 0;
        return Math.Sqrt(Math.Pow(c.X - (a.X + t * dx), 2) + Math.Pow(c.Y - (a.Y + t * dy), 2));
    }

    public static double PathLength(IReadOnlyList<V3> path)
    {
        double s = 0;
        for (int i = 1; i < path.Count; i++) s += V3.Distance(path[i - 1], path[i]);
        return s;
    }

    public static int CountSharpTurns(IReadOnlyList<V3> path, double thresholdDegrees = 35)
    {
        int n = 0;
        for (int i = 1; i < path.Count - 1; i++)
        {
            var a = path[i] - path[i - 1];
            var b = path[i + 1] - path[i];
            if (a.Norm() < 1e-7 || b.Norm() < 1e-7) continue;
            double cos = Math.Clamp(V3.Dot(a, b) / (a.Norm() * b.Norm()), -1, 1);
            if (Math.Acos(cos) * 180 / Math.PI > thresholdDegrees) n++;
        }
        return n;
    }

    public static List<V3> Simplify(MapData map, List<V3> points)
    {
        var result = new List<V3> { points[0] };
        int i = 0;
        while (i < points.Count - 1)
        {
            int j = points.Count - 1;
            while (j > i + 1 && !FreeSegment(map, points[i], points[j])) j--;
            result.Add(points[j]);
            i = j;
        }
        return result;
    }

    public static List<V3> AddControlPoints(IReadOnlyList<V3> input, int innerMinimum = 8)
    {
        var path = input.ToList();
        while (path.Count < innerMinimum + 2)
        {
            int longest = 0;
            double length = -1;
            for (int i = 1; i < path.Count; i++)
            {
                double d = V3.Distance(path[i - 1], path[i]);
                if (d > length) { length = d; longest = i; }
            }
            path.Insert(longest, (path[longest - 1] + path[longest]) * .5);
        }
        return path;
    }

    // Геометрическое сглаживание квинтическим полиномом Эрмита.
    // Любой сглаженный путь проходит повторную проверку на пересечение препятствий.
    public static List<V3> SmoothQuintic(MapData map, IReadOnlyList<V3> path)
    {
        if (path.Count < 3) return path.ToList();
        foreach (double tension in new[] { .3, .15, .07, 0.0 })
        {
            var smooth = new List<V3> { path[0] };
            for (int i = 0; i < path.Count - 1; i++)
            {
                var p = path[i]; var q = path[i + 1];
                var before = path[Math.Max(i - 1, 0)];
                var after = path[Math.Min(i + 2, path.Count - 1)];
                var v0 = (q - before) * tension;
                var v1 = (after - p) * tension;
                var delta = q - p;
                var a3 = delta * 10 - v0 * 6 - v1 * 4;
                var a4 = delta * (-15) + v0 * 8 + v1 * 7;
                var a5 = delta * 6 - v0 * 3 - v1 * 3;
                for (int s = 1; s <= 16; s++)
                {
                    double t = s / 16.0;
                    var point = p + v0 * t + a3 * Math.Pow(t, 3) + a4 * Math.Pow(t, 4) + a5 * Math.Pow(t, 5);
                    smooth.Add(point);
                }
            }
            if (FreePath(map, smooth)) return smooth;
        }
        return path.ToList();
    }

    // Минимальная дистанция двух движущихся точек при постоянных скоростях.
    // До StartDelay агент отсутствует; после финиша исчезает.
    public static double MinimalSimultaneousDistance(IReadOnlyList<V3> a, double da,
        IReadOnlyList<V3> b, double db, double speed)
    {
        var ta = SegmentTimes(a, da, speed);
        var tb = SegmentTimes(b, db, speed);
        double best = double.PositiveInfinity;
        for (int i = 0; i < ta.Count; i++)
        for (int j = 0; j < tb.Count; j++)
        {
            var sa = ta[i]; var sb = tb[j];
            double from = Math.Max(sa.T0, sb.T0), to = Math.Min(sa.T1, sb.T1);
            if (from > to) continue;
            var va = (a[i + 1] - a[i]) / (sa.T1 - sa.T0);
            var vb = (b[j + 1] - b[j]) / (sb.T1 - sb.T0);
            var ra = a[i] + va * (from - sa.T0);
            var rb = b[j] + vb * (from - sb.T0);
            var delta = ra - rb;
            var dv = va - vb;
            double dur = to - from;
            double t = V3.Dot(dv, dv) < 1e-14 ? 0 : Math.Clamp(-V3.Dot(delta, dv) / V3.Dot(dv, dv), 0, dur);
            best = Math.Min(best, (delta + dv * t).Norm());
        }
        return best;
    }

    private static List<(double T0, double T1)> SegmentTimes(IReadOnlyList<V3> path, double delay, double speed)
    {
        var times = new List<(double, double)>();
        double current = delay;
        for (int i = 1; i < path.Count; i++)
        {
            var next = current + Math.Max(1e-8, V3.Distance(path[i - 1], path[i]) / speed);
            times.Add((current, next));
            current = next;
        }
        return times;
    }
}
