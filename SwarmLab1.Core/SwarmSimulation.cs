using System.Numerics;
using SwarmLab1.Core.Models;

namespace SwarmLab1.Core;

public sealed class SwarmSimulation
{
    private readonly Random _random;
    private readonly SimulationSettings _settings;

    public SwarmSimulation(SimulationSettings? settings = null, int seed = 42)
    {
        _settings = settings ?? new SimulationSettings();
        _random = new Random(seed);

        RandomizeEnvironment();
    }

    public IReadOnlyList<Agent> Agents => _agents;
    public IReadOnlyList<RectangleObstacle> Obstacles { get; private set; } = [];
    public Vector2 Target { get; private set; }
    public SimulationSettings Settings => _settings;

    public int Iteration { get; private set; }
    public int ReachedCount => _agents.Count(a => a.ReachedTarget);
    public bool IsFinished => ReachedCount == _agents.Count;
    public float AveragePathLength =>
        _agents.Count == 0 ? 0f : _agents.Average(a => a.PathLength);

    private readonly List<Agent> _agents = [];

    public void RandomizeEnvironment()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            Target = new Vector2(
                NextFloat(_settings.WorldWidth * 0.86f, _settings.WorldWidth * 0.94f),
                NextFloat(8f, _settings.WorldHeight - 8f));

            var obstacleCount = _random.Next(4, 9);
            var obstacles = new List<RectangleObstacle>(obstacleCount);
            var availableWidth = 56f;
            var slotWidth = availableWidth / obstacleCount;

            for (var i = 0; i < obstacleCount; i++)
            {
                var slotStart = 22f + i * slotWidth;
                var slotEnd = slotStart + slotWidth - 1.5f;

                obstacles.Add(CreateRandomObstacle(slotStart, slotEnd));
            }

            Obstacles = obstacles;

            if (!HasReachableRoute())
                continue;

            Reset();
            if (CanCompleteSimulation())
            {
                Reset();
                return;
            }
        }

        Reset();
    }

    public void Reset()
    {
        _agents.Clear();
        Iteration = 0;

        for (var i = 0; i < _settings.AgentCount; i++)
        {
            var row = i % 6;
            var column = i / 6;

            var position = new Vector2(
                6f + column * 3.2f + NextFloat(-0.5f, 0.5f),
                14f + row * 7.2f + NextFloat(-0.7f, 0.7f));

            // Небольшая начальная скорость вправо нужна, чтобы система вышла
            // из симметричного начального состояния.
            var velocity = new Vector2(
                NextFloat(0.15f, 0.35f),
                NextFloat(-0.12f, 0.12f));

            var agent = new Agent(i + 1, position, velocity)
            {
                PersonalBestDistance = Vector2.Distance(position, Target)
            };

            agent.Trail.Add(position);
            _agents.Add(agent);
        }
    }

    public void Step()
    {
        if (IsFinished)
            return;

        Iteration++;

        // Снимок состояния нужен, чтобы результат одного агента в текущем шаге
        // не влиял на вычисление другого агента этого же шага.
        var positions = _agents.Select(a => a.Position).ToArray();
        var velocities = _agents.Select(a => a.Velocity).ToArray();
        var personalBests = _agents.Select(a => a.PersonalBestPosition).ToArray();
        var reached = _agents.Select(a => a.ReachedTarget).ToArray();

        var nextVelocities = new Vector2[_agents.Count];
        var nextPositions = new Vector2[_agents.Count];

        for (var i = 0; i < _agents.Count; i++)
        {
            var agent = _agents[i];

            if (agent.ReachedTarget)
            {
                nextVelocities[i] = Vector2.Zero;
                nextPositions[i] = agent.Position;
                continue;
            }

            var position = positions[i];
            var velocity = velocities[i];

            var bestNeighborPosition = FindBestNeighborPosition(i, positions, personalBests, reached);

            var inertia = _settings.InertiaCoefficient * velocity;

            var cognitiveRandom = NextFloat(0f, 1f);
            var cognitive =
                _settings.CognitiveCoefficient *
                cognitiveRandom *
                (personalBests[i] - position);

            var socialRandom = NextFloat(0f, 1f);
            var social =
                _settings.SocialCoefficient *
                socialRandom *
                (bestNeighborPosition - position);

            var goal = SafeNormalize(Target - position) * _settings.GoalCoefficient;
            var separation = CalculateSeparation(i, positions, reached);
            var obstacleAvoidance = CalculateObstacleAvoidance(i, position, velocity);
            var congestionAvoidance = CalculateCongestionAvoidance(
                i,
                positions,
                velocities,
                reached);

            var newVelocity =
                inertia +
                cognitive +
                social +
                goal +
                separation +
                obstacleAvoidance +
                congestionAvoidance;

            newVelocity = LimitLength(newVelocity, _settings.MaxSpeed);

            var newPosition = position + newVelocity;

            ResolveWorldBounds(ref newPosition, ref newVelocity);
            ResolveObstaclePenetration(position, ref newPosition, ref newVelocity);

            nextVelocities[i] = newVelocity;
            nextPositions[i] = newPosition;
        }

        ResolveAgentCollisions(nextPositions, nextVelocities, reached);

        for (var i = 0; i < _agents.Count; i++)
        {
            var agent = _agents[i];

            var oldPosition = agent.Position;
            agent.Position = nextPositions[i];
            agent.Velocity = nextVelocities[i];

            agent.PathLength += Vector2.Distance(oldPosition, agent.Position);

            var distanceToTarget = Vector2.Distance(agent.Position, Target);

            if (distanceToTarget < agent.PersonalBestDistance)
            {
                agent.PersonalBestDistance = distanceToTarget;
                agent.PersonalBestPosition = agent.Position;
            }

            if (distanceToTarget <= _settings.TargetRadius)
            {
                agent.ReachedTarget = true;
                agent.Velocity = Vector2.Zero;
            }

            agent.Trail.Add(agent.Position);
            if (agent.Trail.Count > _settings.TrailLength)
                agent.Trail.RemoveAt(0);
        }
    }

    private Vector2 FindBestNeighborPosition(
        int agentIndex,
        IReadOnlyList<Vector2> positions,
        IReadOnlyList<Vector2> personalBests,
        IReadOnlyList<bool> reached)
    {
        var current = positions[agentIndex];
        var best = Target;
        var bestObjective = float.PositiveInfinity;
        var found = false;

        for (var j = 0; j < positions.Count; j++)
        {
            if (j == agentIndex)
                continue;

            if (reached[j])
                continue;

            if (Vector2.Distance(current, positions[j]) > _settings.NeighborhoodRadius)
                continue;

            var objective = Vector2.Distance(personalBests[j], Target);

            if (objective < bestObjective)
            {
                bestObjective = objective;
                best = personalBests[j];
                found = true;
            }
        }

        // Если соседей в радиусе нет, социальная составляющая не должна
        // искусственно превращаться в прямое притяжение к цели.
        return found ? best : current;
    }

    private Vector2 CalculateSeparation(
        int agentIndex,
        IReadOnlyList<Vector2> positions,
        IReadOnlyList<bool> reached)
    {
        var current = positions[agentIndex];
        var result = Vector2.Zero;

        for (var j = 0; j < positions.Count; j++)
        {
            if (j == agentIndex)
                continue;

            if (reached[j])
                continue;

            var delta = current - positions[j];
            var distance = delta.Length();

            if (distance <= 0.0001f || distance >= _settings.MinAgentDistance)
                continue;

            var strength =
                (_settings.MinAgentDistance - distance) /
                _settings.MinAgentDistance;

            result += SafeNormalize(delta) * strength;
        }

        return result * _settings.SeparationCoefficient;
    }

    private Vector2 CalculateCongestionAvoidance(
        int agentIndex,
        IReadOnlyList<Vector2> positions,
        IReadOnlyList<Vector2> velocities,
        IReadOnlyList<bool> reached)
    {
        if (reached[agentIndex])
            return Vector2.Zero;

        var current = positions[agentIndex];
        var movementDirection = SafeNormalize(Target - current);
        if (movementDirection == Vector2.Zero)
            movementDirection = SafeNormalize(velocities[agentIndex]);

        if (movementDirection == Vector2.Zero)
            return Vector2.Zero;

        var sideDirection = new Vector2(-movementDirection.Y, movementDirection.X);
        if ((agentIndex + 1) % 2 == 0)
            sideDirection = -sideDirection;

        var result = Vector2.Zero;
        var congestionRadius = _settings.MinAgentDistance * 2.25f;

        for (var j = 0; j < positions.Count; j++)
        {
            if (j == agentIndex || reached[j])
                continue;

            var offset = positions[j] - current;
            var distance = offset.Length();
            if (distance <= 0.0001f || distance >= congestionRadius)
                continue;

            var ahead = Vector2.Dot(SafeNormalize(offset), movementDirection);
            if (ahead <= 0.25f)
                continue;

            var proximity = (congestionRadius - distance) / congestionRadius;
            var forwardWeight = (ahead - 0.25f) / 0.75f;
            result += sideDirection * proximity * forwardWeight;
        }

        return result * _settings.CongestionCoefficient;
    }

    private void ResolveAgentCollisions(
        Vector2[] positions,
        Vector2[] velocities,
        IReadOnlyList<bool> reached)
    {
        for (var pass = 20; pass > 0; pass--)
        {
            for (var i = 0; i < positions.Length; i++)
            {
                if (reached[i])
                    continue;

                for (var j = i + 1; j < positions.Length; j++)
                {
                    if (reached[j])
                        continue;

                    var delta = positions[i] - positions[j];
                    var distance = delta.Length();
                    if (distance >= _settings.MinAgentDistance)
                        continue;

                    var direction = distance > 0.0001f
                        ? delta / distance
                        : ((i + j) % 2 == 0 ? Vector2.UnitX : Vector2.UnitY);
                    var correction = direction * (_settings.MinAgentDistance - distance);
                    var moveI = correction * 0.5f;
                    var moveJ = -correction * 0.5f;

                    var canMoveI = CanOccupy(positions[i] + moveI);
                    var canMoveJ = CanOccupy(positions[j] + moveJ);

                    if (canMoveI && canMoveJ)
                    {
                        positions[i] += moveI;
                        positions[j] += moveJ;
                        velocities[i] = LimitLength(velocities[i] + moveI, _settings.MaxSpeed);
                        velocities[j] = LimitLength(velocities[j] + moveJ, _settings.MaxSpeed);
                    }
                    else if (canMoveI)
                    {
                        positions[i] += correction;
                        velocities[i] = LimitLength(velocities[i] + correction, _settings.MaxSpeed);
                    }
                    else if (canMoveJ)
                    {
                        positions[j] -= correction;
                        velocities[j] = LimitLength(velocities[j] - correction, _settings.MaxSpeed);
                    }
                }
            }
        }
    }

    private bool CanOccupy(Vector2 position)
    {
        const float margin = 1f;

        if (position.X < margin || position.X > _settings.WorldWidth - margin ||
            position.Y < margin || position.Y > _settings.WorldHeight - margin)
        {
            return false;
        }

        return Obstacles.All(obstacle => !obstacle.Contains(position, 0.8f));
    }

    private bool HasReachableRoute()
    {
        const float cellSize = 1.5f;
        var columns = (int)MathF.Ceiling(_settings.WorldWidth / cellSize);
        var rows = (int)MathF.Ceiling(_settings.WorldHeight / cellSize);
        var start = ToGrid(new Vector2(6f, _settings.WorldHeight / 2f));
        var target = ToGrid(Target);
        var queue = new Queue<(int X, int Y)>();
        var visited = new bool[columns, rows];

        if (!IsInsideGrid(start, columns, rows) || !IsInsideGrid(target, columns, rows))
            return false;

        queue.Enqueue(start);
        visited[start.X, start.Y] = true;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == target)
                return true;

            foreach (var direction in Directions)
            {
                var next = (current.X + direction.X, current.Y + direction.Y);
                if (!IsInsideGrid(next, columns, rows) || visited[next.Item1, next.Item2])
                    continue;

                var worldPosition = new Vector2(next.Item1 * cellSize, next.Item2 * cellSize);
                if (!CanOccupy(worldPosition))
                    continue;

                visited[next.Item1, next.Item2] = true;
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private bool CanCompleteSimulation()
    {
        const int validationStepLimit = 1500;
        const int validationTrialCount = 3;

        for (var trial = 0; trial < validationTrialCount; trial++)
        {
            Reset();

            for (var step = 0; step < validationStepLimit && !IsFinished; step++)
                Step();

            if (!IsFinished)
                return false;
        }

        return true;
    }

    private (int X, int Y) ToGrid(Vector2 position) =>
        ((int)(position.X / 1.5f), (int)(position.Y / 1.5f));

    private static bool IsInsideGrid((int X, int Y) point, int columns, int rows) =>
        point.X >= 0 && point.X < columns && point.Y >= 0 && point.Y < rows;

    private static readonly (int X, int Y)[] Directions =
    [
        (1, 0),
        (-1, 0),
        (0, 1),
        (0, -1)
    ];

    private Vector2 CalculateObstacleAvoidance(
        int agentIndex,
        Vector2 position,
        Vector2 velocity)
    {
        var result = Vector2.Zero;
        var movementDirection = SafeNormalize(velocity);

        foreach (var obstacle in Obstacles)
        {
            var route = CalculateObstacleRoute(agentIndex, position, obstacle);
            if (route != Vector2.Zero)
            {
                result += SafeNormalize(route - position) * (_settings.ObstacleCoefficient * 1.4f);
            }

            var nearest = obstacle.NearestPoint(position);
            var away = position - nearest;
            var distance = away.Length();

            // Если агент находится напротив грани, NearestPoint может совпасть
            // с самим агентом. Тогда используем направление от центра препятствия.
            if (distance < 0.001f)
            {
                away = position - obstacle.Center;
                distance = Math.Max(away.Length(), 0.001f);
            }

            if (distance < _settings.ObstacleAvoidanceRadius)
            {
                var normalizedAway = SafeNormalize(away);
                var strength =
                    (_settings.ObstacleAvoidanceRadius - distance) /
                    _settings.ObstacleAvoidanceRadius;

                result += normalizedAway * strength * _settings.ObstacleCoefficient;

                // Касательная компонента помогает не "залипнуть" перед стеной.
                // Направление выбирается так, чтобы в среднем идти в сторону цели.
                var tangentA = new Vector2(-normalizedAway.Y, normalizedAway.X);
                var tangentB = -tangentA;

                var candidateA = Vector2.Dot(tangentA, Target - position);
                var candidateB = Vector2.Dot(tangentB, Target - position);
                var tangent = candidateA >= candidateB ? tangentA : tangentB;

                result += tangent * strength * 0.42f;
            }

            // Опережающая проверка: реагируем до непосредственного контакта.
            var lookAhead = position + movementDirection * 4.5f;
            if (obstacle.Contains(lookAhead, 1.2f))
            {
                var centerAway = SafeNormalize(position - obstacle.Center);
                result += centerAway * (_settings.ObstacleCoefficient * 0.8f);
            }
        }

        return result;
    }

    private Vector2 CalculateObstacleRoute(
        int agentIndex,
        Vector2 position,
        RectangleObstacle obstacle)
    {
        const float margin = 1.4f;

        if (position.X >= obstacle.Left - margin ||
            Target.X <= obstacle.Right + margin ||
            !SegmentIntersectsRectangle(position, Target, obstacle, margin))
        {
            return Vector2.Zero;
        }

        var topLeft = new Vector2(obstacle.Left - margin, obstacle.Top - margin);
        var topRight = new Vector2(obstacle.Right + margin, obstacle.Top - margin);
        var bottomLeft = new Vector2(obstacle.Left - margin, obstacle.Bottom + margin);
        var bottomRight = new Vector2(obstacle.Right + margin, obstacle.Bottom + margin);

        var topRouteLength =
            Vector2.Distance(position, topLeft) +
            Vector2.Distance(topLeft, topRight) +
            Vector2.Distance(topRight, Target);
        var bottomRouteLength =
            Vector2.Distance(position, bottomLeft) +
            Vector2.Distance(bottomLeft, bottomRight) +
            Vector2.Distance(bottomRight, Target);

        var useTopRoute = topRouteLength < bottomRouteLength ||
            (Math.Abs(topRouteLength - bottomRouteLength) < 0.001f && agentIndex % 2 == 0);

        return position.X < obstacle.Left
            ? useTopRoute ? topLeft : bottomLeft
            : useTopRoute ? topRight : bottomRight;
    }

    private static bool SegmentIntersectsRectangle(
        Vector2 start,
        Vector2 end,
        RectangleObstacle obstacle,
        float margin)
    {
        var minX = obstacle.Left - margin;
        var maxX = obstacle.Right + margin;
        var minY = obstacle.Top - margin;
        var maxY = obstacle.Bottom + margin;
        var direction = end - start;
        var entry = 0f;
        var exit = 1f;

        return UpdateSegmentSlab(start.X, direction.X, minX, maxX, ref entry, ref exit) &&
            UpdateSegmentSlab(start.Y, direction.Y, minY, maxY, ref entry, ref exit);
    }

    private static bool UpdateSegmentSlab(
        float start,
        float direction,
        float min,
        float max,
        ref float entry,
        ref float exit)
    {
        if (Math.Abs(direction) < 0.0001f)
            return start >= min && start <= max;

        var first = (min - start) / direction;
        var second = (max - start) / direction;
        if (first > second)
            (first, second) = (second, first);

        entry = Math.Max(entry, first);
        exit = Math.Min(exit, second);
        return entry <= exit;
    }

    private void ResolveObstaclePenetration(
        Vector2 oldPosition,
        ref Vector2 newPosition,
        ref Vector2 newVelocity)
    {
        foreach (var obstacle in Obstacles)
        {
            if (!obstacle.Contains(newPosition, 0.8f))
                continue;

            // Самый безопасный вариант при пересечении: отменить шаг и
            // слегка повернуть скорость вдоль препятствия.
            newPosition = oldPosition;

            var away = SafeNormalize(oldPosition - obstacle.Center);
            var tangent = new Vector2(-away.Y, away.X);

            if (Vector2.Dot(tangent, Target - oldPosition) < 0)
                tangent = -tangent;

            newVelocity =
                LimitLength(
                    tangent * (_settings.MaxSpeed * 0.65f) +
                    away * 0.25f,
                    _settings.MaxSpeed);
        }
    }

    private void ResolveWorldBounds(ref Vector2 position, ref Vector2 velocity)
    {
        const float margin = 1f;

        if (position.X < margin)
        {
            position.X = margin;
            velocity.X = Math.Abs(velocity.X);
        }
        else if (position.X > _settings.WorldWidth - margin)
        {
            position.X = _settings.WorldWidth - margin;
            velocity.X = -Math.Abs(velocity.X);
        }

        if (position.Y < margin)
        {
            position.Y = margin;
            velocity.Y = Math.Abs(velocity.Y);
        }
        else if (position.Y > _settings.WorldHeight - margin)
        {
            position.Y = _settings.WorldHeight - margin;
            velocity.Y = -Math.Abs(velocity.Y);
        }
    }

    private float NextFloat(float min, float max) =>
        min + (float)_random.NextDouble() * (max - min);

    private RectangleObstacle CreateRandomObstacle(float minX, float maxX)
    {
        var width = NextFloat(3f, Math.Min(6f, maxX - minX));
        var height = NextFloat(3f, 8f);
        var x = NextFloat(minX, maxX - width);
        var y = NextFloat(8f, _settings.WorldHeight - height - 8f);

        return new RectangleObstacle(x, y, width, height);
    }

    private static Vector2 SafeNormalize(Vector2 vector)
    {
        var length = vector.Length();
        return length <= 0.0001f ? Vector2.Zero : vector / length;
    }

    private static Vector2 LimitLength(Vector2 vector, float maxLength)
    {
        var length = vector.Length();
        if (length <= maxLength || length <= 0.0001f)
            return vector;

        return vector / length * maxLength;
    }
}
