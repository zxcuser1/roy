using System.Numerics;

namespace SwarmLab2.Core.Models;

public sealed class Agent
{
    public Agent(int id, Vector3 position, Vector3 velocity)
    {
        Id = id;
        Position = position;
        Velocity = velocity;
        PersonalBestPosition = position;
        LastDistanceToTarget = float.PositiveInfinity;
        PersonalBestDistance = float.PositiveInfinity;
    }

    public int Id { get; }
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }

    public Vector3 PersonalBestPosition { get; set; }
    public float PersonalBestDistance { get; set; }

    public bool ReachedTarget { get; set; }

    public float NoProgressSeconds { get; set; }
    public float LastDistanceToTarget { get; set; }
    public Vector3 EscapeDirection { get; set; } = Vector3.UnitZ;

    public List<Vector3> Trail { get; } = new();
}
