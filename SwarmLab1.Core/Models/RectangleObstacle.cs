using System.Numerics;

namespace SwarmLab1.Core.Models;

public readonly record struct RectangleObstacle(
    float X,
    float Y,
    float Width,
    float Height)
{
    public float Left => X;
    public float Right => X + Width;
    public float Top => Y;
    public float Bottom => Y + Height;

    public Vector2 Center => new(X + Width / 2f, Y + Height / 2f);

    public bool Contains(Vector2 point, float margin = 0f) =>
        point.X >= Left - margin &&
        point.X <= Right + margin &&
        point.Y >= Top - margin &&
        point.Y <= Bottom + margin;

    public Vector2 NearestPoint(Vector2 point)
    {
        var x = Math.Clamp(point.X, Left, Right);
        var y = Math.Clamp(point.Y, Top, Bottom);
        return new Vector2(x, y);
    }
}
