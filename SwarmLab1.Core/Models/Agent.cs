using System.Numerics;

namespace SwarmLab1.Core.Models;

public sealed class Agent
{
    public Agent(int id, Vector2 position, Vector2 velocity)
    {
        Id = id;
        Position = position;
        Velocity = velocity;
        PersonalBestPosition = position;
    }

    public int Id { get; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public Vector2 PersonalBestPosition { get; set; }
    public float PersonalBestDistance { get; set; } = float.PositiveInfinity;
    public float PathLength { get; set; }
    public bool ReachedTarget { get; set; }

    public List<Vector2> Trail { get; } = [];
}
