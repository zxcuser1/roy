using System.Numerics;

namespace SwarmLab3.Models;

public sealed record Obstacle(string Name, Vector3 Min, Vector3 Max)
{
    // Евклидово расстояние до осесимметричного параллелепипеда.
    public float Distance(Vector3 point)
    {
        var nearest = Vector3.Clamp(point, Min, Max);
        return Vector3.Distance(point, nearest);
    }

    public Vector3 DirectionAway(Vector3 point)
    {
        var nearest = Vector3.Clamp(point, Min, Max);
        var delta = point - nearest;
        if (delta.LengthSquared() > 1e-8f) return Vector3.Normalize(delta);

        // Точка внутри препятствия: направление к ближайшей грани.
        var distances = new[]
        {
            point.X - Min.X, Max.X - point.X,
            point.Y - Min.Y, Max.Y - point.Y,
            point.Z - Min.Z, Max.Z - point.Z
        };
        var idx = Array.IndexOf(distances, distances.Min());
        return idx switch
        {
            0 => -Vector3.UnitX, 1 => Vector3.UnitX,
            2 => -Vector3.UnitY, 3 => Vector3.UnitY,
            4 => -Vector3.UnitZ, _ => Vector3.UnitZ
        };
    }
}

public sealed class World
{
    public const float Width = 80f;
    public const float Length = 80f;
    public const float Height = 12f;
    public List<Obstacle> Obstacles { get; } = [];

    public bool IsFree(Vector3 p, float radius)
    {
        if (p.X < radius || p.X > Width - radius ||
            p.Y < radius || p.Y > Length - radius ||
            p.Z < radius || p.Z > Height - radius) return false;
        foreach (var o in Obstacles)
            if (o.Distance(p) < radius) return false;
        return true;
    }

    public float Clearance(Vector3 p)
    {
        var d = MathF.Min(MathF.Min(p.X, Width - p.X),
            MathF.Min(MathF.Min(p.Y, Length - p.Y), MathF.Min(p.Z, Height - p.Z)));
        foreach (var o in Obstacles) d = MathF.Min(d, o.Distance(p));
        return d;
    }

    // Консервативная непрерывная проверка отрезка, не только крайних узлов сетки.
    public bool LineOfSight(Vector3 a, Vector3 b, float radius)
    {
        var delta = b - a;
        int samples = Math.Max(1, (int)MathF.Ceiling(delta.Length() / 0.35f));
        for (int i = 0; i <= samples; ++i)
        {
            if (!IsFree(a + delta * (i / (float)samples), radius)) return false;
        }
        return true;
    }
}

public static class Vectors
{
    public static Vector3 Normalized(Vector3 v) =>
        v.LengthSquared() > 1e-10f ? Vector3.Normalize(v) : Vector3.Zero;

    public static Vector3 Limit(Vector3 v, float length) =>
        v.LengthSquared() > length * length ? Vector3.Normalize(v) * length : v;

    public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDelta)
    {
        var diff = target - current;
        return diff.Length() <= maxDelta ? target : current + Normalized(diff) * maxDelta;
    }
}
