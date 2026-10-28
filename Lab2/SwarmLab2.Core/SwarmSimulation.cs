using System.Numerics;
using SwarmLab2.Core.Models;

namespace SwarmLab2.Core;

public sealed class SwarmSimulation
{
    private readonly Random _random;
    private float _trailAccumulator;

    public SwarmSimulation(SimulationSettings settings, int seed = 42)
    {
        Settings = settings;
        _random = new Random(seed);
        Reset(settings.AgentCount);
    }

    public SimulationSettings Settings { get; }
    public List<Agent> Agents { get; } = new();
    public List<BoxObstacle> Obstacles { get; } = new();

    public int ActiveCount => Agents.Count(a => !a.ReachedTarget);
    public int ReachedCount => Agents.Count(a => a.ReachedTarget);

    public void Reset(int? agentCount = null)
    {
        if (agentCount.HasValue)
            Settings.AgentCount = Math.Clamp(agentCount.Value, 1, 200);

        Agents.Clear();
        Obstacles.Clear();
        _trailAccumulator = 0f;

        CreateDefaultObstacles();
        CreateAgents();
    }

    public void Step(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        deltaTime = MathF.Min(deltaTime, 0.05f);
        _trailAccumulator += deltaTime;
        var shouldSampleTrail = _trailAccumulator >= Settings.TrailSampleSeconds;
        if (shouldSampleTrail)
            _trailAccumulator = 0f;

        var activeAgents = Agents.Where(a => !a.ReachedTarget).ToArray();
        if (activeAgents.Length == 0)
            return;

        var globalBestPosition = activeAgents
            .MinBy(a => Vector3.DistanceSquared(a.Position, Settings.Target))!
            .Position;

        var positions = activeAgents.Select(a => a.Position).ToArray();

        for (var index = 0; index < activeAgents.Length; index++)
        {
            var agent = activeAgents[index];
            UpdateAgent(agent, positions, index, globalBestPosition, deltaTime);

            if (shouldSampleTrail)
            {
                agent.Trail.Add(agent.Position);
                if (agent.Trail.Count > Settings.MaxTrailPoints)
                    agent.Trail.RemoveAt(0);
            }
        }
    }

    private void UpdateAgent(
        Agent agent,
        IReadOnlyList<Vector3> positions,
        int ownIndex,
        Vector3 globalBestPosition,
        float deltaTime)
    {
        var position = agent.Position;
        var distanceToTarget = Vector3.Distance(position, Settings.Target);

        if (distanceToTarget < agent.PersonalBestDistance)
        {
            agent.PersonalBestDistance = distanceToTarget;
            agent.PersonalBestPosition = position;
        }

        UpdateProgressState(agent, distanceToTarget, deltaTime);

        var goalDirection = SafeNormalize(Settings.Target - position);

        var desiredVelocity = Settings.Inertia * agent.Velocity;
        desiredVelocity += goalDirection * (Settings.GoalWeight * Settings.MaxSpeed);

        var cognitiveDirection = SafeNormalize(agent.PersonalBestPosition - position);
        desiredVelocity += cognitiveDirection *
                           (Settings.CognitiveWeight * Settings.MaxSpeed * NextFloat());

        var socialDirection = SafeNormalize(globalBestPosition - position);
        desiredVelocity += socialDirection *
                           (Settings.SocialWeight * Settings.MaxSpeed * NextFloat());

        desiredVelocity += ComputeSeparation(position, positions, ownIndex) *
                           (Settings.SeparationWeight * Settings.MaxSpeed);

        desiredVelocity += ComputeObstacleAvoidance(agent, goalDirection) *
                           (Settings.ObstacleAvoidanceWeight * Settings.MaxSpeed);

        desiredVelocity += ComputeBoundaryAvoidance(position) *
                           (Settings.BoundaryWeight * Settings.MaxSpeed);

        if (agent.NoProgressSeconds > Settings.NoProgressThresholdSeconds)
        {
            desiredVelocity += agent.EscapeDirection *
                               (Settings.EscapeWeight * Settings.MaxSpeed);
        }

        desiredVelocity = ClampLength(desiredVelocity, Settings.MaxSpeed);

        var velocityDelta = desiredVelocity - agent.Velocity;
        velocityDelta = ClampLength(velocityDelta, Settings.MaxAcceleration * deltaTime);

        var newVelocity = ClampLength(agent.Velocity + velocityDelta, Settings.MaxSpeed);
        var newPosition = position + newVelocity * deltaTime;

        ResolveObstacleCollisions(ref newPosition, ref newVelocity);
        ResolveWorldBounds(ref newPosition, ref newVelocity);

        agent.Position = newPosition;
        agent.Velocity = newVelocity;

        if (Vector3.Distance(agent.Position, Settings.Target) <= Settings.TargetRadius)
        {
            agent.ReachedTarget = true;
            agent.Velocity = Vector3.Zero;
        }
    }

    private void UpdateProgressState(Agent agent, float distanceToTarget, float deltaTime)
    {
        if (agent.LastDistanceToTarget == float.PositiveInfinity ||
            distanceToTarget < agent.LastDistanceToTarget - 0.15f)
        {
            agent.LastDistanceToTarget = distanceToTarget;
            agent.NoProgressSeconds = 0f;
            return;
        }

        agent.NoProgressSeconds += deltaTime;

        if (agent.NoProgressSeconds > 2.5f)
        {
            agent.EscapeDirection = RandomEscapeDirection();
            // Keep the escape mode active, but avoid changing direction every frame.
            agent.NoProgressSeconds = 1.3f;
        }
    }

    private Vector3 ComputeSeparation(
        Vector3 position,
        IReadOnlyList<Vector3> positions,
        int ownIndex)
    {
        var force = Vector3.Zero;

        for (var i = 0; i < positions.Count; i++)
        {
            if (i == ownIndex)
                continue;

            var delta = position - positions[i];
            var distance = delta.Length();

            if (distance <= 1e-5f || distance >= Settings.SeparationRadius)
                continue;

            var factor = (Settings.SeparationRadius - distance) / Settings.SeparationRadius;
            force += delta / distance * factor;
        }

        return force;
    }

    private Vector3 ComputeObstacleAvoidance(Agent agent, Vector3 goalDirection)
    {
        var force = Vector3.Zero;
        var position = agent.Position;
        var padding = Settings.AgentRadius;

        foreach (var obstacle in Obstacles)
        {
            var closest = obstacle.ClosestPoint(position, padding);
            var delta = position - closest;
            var distance = delta.Length();

            if (distance <= 1e-5f)
            {
                force += obstacle.GetPushOutDirection(position, padding) * 1.5f;
            }
            else if (distance < Settings.ObstacleDetectionDistance)
            {
                var factor = (Settings.ObstacleDetectionDistance - distance) /
                             Settings.ObstacleDetectionDistance;
                force += delta / distance * factor * factor;
            }

            var heading = SafeNormalize(agent.Velocity + goalDirection * Settings.MaxSpeed);
            var predictedPosition = position + heading * Settings.LookAheadDistance;

            if (!obstacle.Contains(predictedPosition, padding))
                continue;

            var push = obstacle.GetPushOutDirection(predictedPosition, padding);
            var tangent = Vector3.Cross(push, Vector3.UnitZ);

            if (tangent.LengthSquared() < 1e-5f)
                tangent = Vector3.Cross(push, Vector3.UnitY);

            tangent = SafeNormalize(tangent);
            if ((agent.Id & 1) != 0)
                tangent = -tangent;

            // The push term prevents collision, while the tangent term breaks the
            // symmetric "everyone pushes against everyone" deadlock from Lab 1.
            force += push * 1.25f + tangent * 0.35f;
        }

        return force;
    }

    private Vector3 ComputeBoundaryAvoidance(Vector3 position)
    {
        var force = Vector3.Zero;
        var margin = Settings.BoundaryMargin;
        var size = Settings.WorldSize;

        if (position.X < margin)
            force.X += (margin - position.X) / margin;
        else if (position.X > size.X - margin)
            force.X -= (position.X - (size.X - margin)) / margin;

        if (position.Y < margin)
            force.Y += (margin - position.Y) / margin;
        else if (position.Y > size.Y - margin)
            force.Y -= (position.Y - (size.Y - margin)) / margin;

        if (position.Z < margin)
            force.Z += (margin - position.Z) / margin;
        else if (position.Z > size.Z - margin)
            force.Z -= (position.Z - (size.Z - margin)) / margin;

        return force;
    }

    private void ResolveObstacleCollisions(ref Vector3 position, ref Vector3 velocity)
    {
        foreach (var obstacle in Obstacles)
        {
            if (!obstacle.Contains(position, Settings.AgentRadius))
                continue;

            var direction = obstacle.GetPushOutDirection(position, Settings.AgentRadius);
            var min = obstacle.Min - new Vector3(Settings.AgentRadius);
            var max = obstacle.Max + new Vector3(Settings.AgentRadius);
            const float epsilon = 0.01f;

            if (direction == -Vector3.UnitX)
                position.X = min.X - epsilon;
            else if (direction == Vector3.UnitX)
                position.X = max.X + epsilon;
            else if (direction == -Vector3.UnitY)
                position.Y = min.Y - epsilon;
            else if (direction == Vector3.UnitY)
                position.Y = max.Y + epsilon;
            else if (direction == -Vector3.UnitZ)
                position.Z = MathF.Max(Settings.AgentRadius, min.Z - epsilon);
            else
                position.Z = max.Z + epsilon;

            var normalVelocity = Vector3.Dot(velocity, direction);
            if (normalVelocity < 0f)
                velocity -= direction * normalVelocity;
        }
    }

    private void ResolveWorldBounds(ref Vector3 position, ref Vector3 velocity)
    {
        var min = new Vector3(Settings.AgentRadius);
        var max = Settings.WorldSize - min;

        if (position.X < min.X || position.X > max.X)
            velocity.X *= -0.25f;
        if (position.Y < min.Y || position.Y > max.Y)
            velocity.Y *= -0.25f;
        if (position.Z < min.Z || position.Z > max.Z)
            velocity.Z *= -0.25f;

        position = Vector3.Clamp(position, min, max);
    }

    private void CreateDefaultObstacles()
    {
        // Axis-aligned 3D obstacles. Their different heights force the swarm to
        // choose between lateral detours and flying over them.
        Obstacles.Add(new BoxObstacle(
            new Vector3(35f, 25f, 12f),
            new Vector3(10f, 24f, 24f)));

        Obstacles.Add(new BoxObstacle(
            new Vector3(58f, 48f, 18f),
            new Vector3(18f, 16f, 36f)));

        Obstacles.Add(new BoxObstacle(
            new Vector3(78f, 28f, 10f),
            new Vector3(16f, 20f, 20f)));

        Obstacles.Add(new BoxObstacle(
            new Vector3(90f, 58f, 16f),
            new Vector3(12f, 18f, 32f)));
    }

    private void CreateAgents()
    {
        for (var id = 0; id < Settings.AgentCount; id++)
        {
            var position = Settings.SpawnCenter + new Vector3(
                RandomRange(-2f, 2f),
                RandomRange(-4f, 4f),
                RandomRange(-2f, 3f));

            var min = new Vector3(Settings.AgentRadius);
            var max = Settings.WorldSize - min;
            position = Vector3.Clamp(position, min, max);

            var velocity = new Vector3(
                RandomRange(-1f, 1f),
                RandomRange(-1f, 1f),
                RandomRange(-0.5f, 0.5f));

            var agent = new Agent(id, position, velocity)
            {
                PersonalBestDistance = Vector3.Distance(position, Settings.Target),
                LastDistanceToTarget = Vector3.Distance(position, Settings.Target),
                EscapeDirection = RandomEscapeDirection()
            };

            agent.Trail.Add(position);
            Agents.Add(agent);
        }
    }

    private Vector3 RandomEscapeDirection()
    {
        var vector = new Vector3(
            RandomRange(-1f, 1f),
            RandomRange(-1f, 1f),
            RandomRange(-0.1f, 1f));

        return SafeNormalize(vector);
    }

    private float NextFloat() => (float)_random.NextDouble();

    private float RandomRange(float min, float max) =>
        min + (max - min) * NextFloat();

    private static Vector3 SafeNormalize(Vector3 vector)
    {
        var lengthSquared = vector.LengthSquared();
        return lengthSquared < 1e-8f
            ? Vector3.Zero
            : vector / MathF.Sqrt(lengthSquared);
    }

    private static Vector3 ClampLength(Vector3 vector, float maxLength)
    {
        var lengthSquared = vector.LengthSquared();
        var maxLengthSquared = maxLength * maxLength;

        if (lengthSquared <= maxLengthSquared || lengthSquared < 1e-8f)
            return vector;

        return vector / MathF.Sqrt(lengthSquared) * maxLength;
    }
}
