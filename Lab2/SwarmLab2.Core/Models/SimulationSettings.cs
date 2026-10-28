using System.Numerics;

namespace SwarmLab2.Core.Models;

public sealed class SimulationSettings
{
    public Vector3 WorldSize { get; init; } = new(120f, 80f, 60f);
    public Vector3 SpawnCenter { get; init; } = new(8f, 10f, 8f);
    public Vector3 Target { get; init; } = new(110f, 70f, 45f);

    public int AgentCount { get; set; } = 40;

    public float AgentRadius { get; init; } = 0.65f;
    public float TargetRadius { get; init; } = 2.8f;

    public float MaxSpeed { get; init; } = 13f;
    public float MaxAcceleration { get; init; } = 30f;

    public float Inertia { get; init; } = 0.55f;
    public float GoalWeight { get; init; } = 1.90f;
    public float CognitiveWeight { get; init; } = 0.35f;
    public float SocialWeight { get; init; } = 0.55f;

    public float SeparationWeight { get; init; } = 2.70f;
    public float SeparationRadius { get; init; } = 4.20f;

    public float ObstacleAvoidanceWeight { get; init; } = 5.50f;
    public float ObstacleDetectionDistance { get; init; } = 8f;
    public float LookAheadDistance { get; init; } = 7f;

    public float BoundaryWeight { get; init; } = 3f;
    public float BoundaryMargin { get; init; } = 4f;

    public float NoProgressThresholdSeconds { get; init; } = 1.1f;
    public float EscapeWeight { get; init; } = 2.5f;

    public float TrailSampleSeconds { get; init; } = 0.12f;
    public int MaxTrailPoints { get; init; } = 90;
}
