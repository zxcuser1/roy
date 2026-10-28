using System.Numerics;

namespace SwarmLab2.Core.Models;

public sealed class BoxObstacle
{
    public BoxObstacle(Vector3 center, Vector3 size)
    {
        Center = center;
        Size = size;
    }

    public Vector3 Center { get; }
    public Vector3 Size { get; }

    public Vector3 Min => Center - Size / 2f;
    public Vector3 Max => Center + Size / 2f;

    public bool Contains(Vector3 point, float padding = 0f)
    {
        var min = Min - new Vector3(padding);
        var max = Max + new Vector3(padding);

        return point.X >= min.X && point.X <= max.X &&
               point.Y >= min.Y && point.Y <= max.Y &&
               point.Z >= min.Z && point.Z <= max.Z;
    }

    public Vector3 ClosestPoint(Vector3 point, float padding = 0f)
    {
        var min = Min - new Vector3(padding);
        var max = Max + new Vector3(padding);
        return Vector3.Clamp(point, min, max);
    }

    public Vector3 GetPushOutDirection(Vector3 point, float padding = 0f)
    {
        var min = Min - new Vector3(padding);
        var max = Max + new Vector3(padding);
        var closest = Vector3.Clamp(point, min, max);
        var outsideDelta = point - closest;

        if (outsideDelta.LengthSquared() > 1e-8f)
            return Vector3.Normalize(outsideDelta);

        var candidates = new (float distance, Vector3 direction)[]
        {
            (MathF.Abs(point.X - min.X), -Vector3.UnitX),
            (MathF.Abs(max.X - point.X),  Vector3.UnitX),
            (MathF.Abs(point.Y - min.Y), -Vector3.UnitY),
            (MathF.Abs(max.Y - point.Y),  Vector3.UnitY),
            (MathF.Abs(point.Z - min.Z), -Vector3.UnitZ),
            (MathF.Abs(max.Z - point.Z),  Vector3.UnitZ)
        };

        foreach (var candidate in candidates.OrderBy(x => x.distance))
        {
            // Obstacles in the default scene stand on the ground. Pushing a UAV
            // below the ground plane would only create another collision.
            if (candidate.direction == -Vector3.UnitZ && min.Z <= 0.01f)
                continue;

            return candidate.direction;
        }

        return Vector3.UnitZ;
    }
}
