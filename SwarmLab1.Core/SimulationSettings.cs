namespace SwarmLab1.Core;

public sealed class SimulationSettings
{
    public int AgentCount { get; init; } = 12;

    public float WorldWidth { get; init; } = 100f;
    public float WorldHeight { get; init; } = 70f;

    // Коэффициенты составляющих движения.
    public float InertiaCoefficient { get; init; } = 0.70f;
    public float CognitiveCoefficient { get; init; } = 0.75f;
    public float SocialCoefficient { get; init; } = 0.55f;

    // Дополнительные члены для выполнения прикладной части задания:
    // движение к точке сборки, обход препятствий и предотвращение столкновений.
    public float GoalCoefficient { get; init; } = 0.48f;
    public float ObstacleCoefficient { get; init; } = 1.55f;
    public float SeparationCoefficient { get; init; } = 0.90f;
    public float CongestionCoefficient { get; init; } = 0.35f;

    public float MaxSpeed { get; init; } = 1.25f;
    public float MinAgentDistance { get; init; } = 2.4f;
    public float ObstacleAvoidanceRadius { get; init; } = 7.0f;
    public float NeighborhoodRadius { get; init; } = 28f;
    public float TargetRadius { get; init; } = 2.5f;

    public int TrailLength { get; init; } = 160;
}
