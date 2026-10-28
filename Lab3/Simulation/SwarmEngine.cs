using System.Numerics;
using SwarmLab3.Models;
using SwarmLab3.Planning;

namespace SwarmLab3.Simulation;

public sealed record Vec3(float X, float Y, float Z)
{
    public static Vec3 From(Vector3 v) => new(v.X, v.Y, v.Z);
}

public sealed class Drone
{
    public int Id { get; init; }
    public Vector3 Offset { get; init; }
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public float Distance { get; set; }
    public int Blocked { get; set; }
    public List<Vector3> Trail { get; } = [];
}

public sealed class SwarmEngine
{
    public const float DroneRadius = 0.55f;
    private const float LeaderRadius = 1.10f;
    private const float FollowGain = 1.35f;
    private const float DroneMaxSpeed = 5.4f;
    private const float LeaderSpeed = 3.1f;
    private readonly object _sync = new();
    private Scenario _scenario = null!;
    private PlanResult _astar = null!;
    private PlanResult _theta = null!;
    private PlanResult _current = null!;
    private List<Drone> _drones = [];
    private Vector3 _leader;
    private Vector3 _leaderVelocity;
    private Vector3 _heading;
    private int _waypoint;
    private string _planner = "theta";
    private bool _running = true;
    private bool _arrived;
    private double _speed = 1.0;
    private double _time;
    private long _version;

    public SwarmEngine() => Reset("urban", "theta");

    public void Reset(string scenario, string planner)
    {
        var s = ScenarioFactory.Create(scenario);
        var pathfinder = new GridPlanner(s.World);
        var a = pathfinder.FindPath(s.Start, s.Goal, theta: false);
        var t = pathfinder.FindPath(s.Start, s.Goal, theta: true);
        var selected = planner == "astar" ? a : t;
        if (!selected.Found)
            throw new InvalidOperationException($"Не удалось найти маршрут в сценарии {scenario}");

        lock (_sync)
        {
            _scenario = s;
            _astar = a;
            _theta = t;
            _current = selected;
            _planner = planner;
            _leader = s.Start;
            _leaderVelocity = Vector3.Zero;
            _heading = Vectors.Normalized(s.Goal - s.Start);
            _waypoint = Math.Min(1, selected.Path.Count - 1);
            _running = true;
            _arrived = false;
            _time = 0;
            _speed = 1;
            _version++;
            _drones = [];
            Vector3[] offsets =
            [
                new( 0.0f,  0.0f,  0.0f),
                new(-2.8f, -1.4f,  0.10f),
                new( 2.8f, -1.4f, -0.10f),
                new(-1.45f, 2.1f,  0.70f),
                new( 1.45f, 2.1f, -0.70f)
            ];
            for (int i = 0; i < offsets.Length; ++i)
            {
                var initial = DesiredPosition(offsets[i], 1f);
                if (!s.World.IsFree(initial, DroneRadius))
                    throw new InvalidOperationException("Стартовый строй пересекает препятствие");
                _drones.Add(new Drone { Id = i + 1, Offset = offsets[i],
                    Position = initial, Trail = { initial } });
            }
        }
    }

    public void SetRunning(bool value)
    {
        lock (_sync) _running = value && !_arrived;
    }

    public void SetSpeed(double value)
    {
        lock (_sync) _speed = Math.Clamp(value, 0.25, 4);
    }

    public void Step(double realDt)
    {
        lock (_sync)
        {
            if (!_running || _arrived) return;
            double remaining = Math.Clamp(realDt, 0, 0.15) * _speed;
            while (remaining > 1e-8)
            {
                float dt = (float)Math.Min(remaining, 0.03);
                Substep(dt);
                _time += dt;
                remaining -= dt;
            }
        }
    }

    private void Substep(float dt)
    {
        if (_waypoint < _current.Path.Count)
        {
            var target = _current.Path[_waypoint];
            var diff = target - _leader;
            var old = _leader;
            _leader = Vectors.MoveTowards(_leader, target, LeaderSpeed * dt);
            _leaderVelocity = (_leader - old) / dt;
            if (diff.LengthSquared() <= (LeaderSpeed * dt + 0.02f) * (LeaderSpeed * dt + 0.02f))
                _waypoint++;
        }
        else _leaderVelocity = Vector3.Zero;

        var horizontal = new Vector3(_leaderVelocity.X, _leaderVelocity.Y, 0);
        if (horizontal.LengthSquared() > 1e-5f)
            _heading = Vector3.Normalize(horizontal);

        float clearance = _scenario.World.Clearance(_leader);
        float compact = Math.Clamp((clearance - 0.4f) / 3.5f, 0.52f, 1f);

        foreach (var drone in _drones)
        {
            var desiredPosition = DesiredPosition(drone.Offset, compact);
            var targetVelocity = _leaderVelocity + FollowGain * (desiredPosition - drone.Position);
            targetVelocity += ObstacleForce(drone.Position);
            targetVelocity += SeparationForce(drone);
            targetVelocity = Vectors.Limit(targetVelocity, DroneMaxSpeed);

            // Управление скоростью: ограничение ускорения и дискретный шаг интеграции.
            drone.Velocity = Vectors.MoveTowards(drone.Velocity, targetVelocity, 11.5f * dt);
            var velocity = drone.Velocity;
            var newPosition = drone.Position + velocity * dt;
            if (!CanMove(drone, newPosition))
            {
                // Скользящее движение вдоль препятствия/другого дрона.
                Vector3[] components =
                [
                    new(drone.Velocity.X, 0, 0),
                    new(0, drone.Velocity.Y, 0),
                    new(0, 0, drone.Velocity.Z),
                    new(drone.Velocity.X, drone.Velocity.Y, 0),
                    new(drone.Velocity.X, 0, drone.Velocity.Z),
                    new(0, drone.Velocity.Y, drone.Velocity.Z)
                ];
                bool moved = false;
                foreach (var component in components.OrderByDescending(c => c.LengthSquared()))
                {
                    var trial = drone.Position + component * dt;
                    if (!CanMove(drone, trial)) continue;
                    newPosition = trial;
                    drone.Velocity = component;
                    moved = true;
                    break;
                }
                if (!moved)
                {
                    drone.Velocity = Vector3.Zero;
                    drone.Blocked++;
                    newPosition = drone.Position;
                }
            }
            drone.Distance += Vector3.Distance(drone.Position, newPosition);
            drone.Position = newPosition;
            if (drone.Trail.Count == 0 || Vector3.Distance(drone.Trail[^1], newPosition) > 0.55f)
            {
                drone.Trail.Add(newPosition);
                if (drone.Trail.Count > 450) drone.Trail.RemoveAt(0);
            }
        }

        // БПЛА остаются в строю у финиша, а не исчезают (в отличие от лабораторной № 1).
        if (_waypoint >= _current.Path.Count && _drones.All(d =>
            Vector3.Distance(d.Position, DesiredPosition(d.Offset, 1f)) < 0.65f &&
            d.Velocity.LengthSquared() < 0.1f))
        {
            _arrived = true;
            _running = false;
        }
    }

    private bool CanMove(Drone drone, Vector3 candidate)
    {
        if (!_scenario.World.LineOfSight(drone.Position, candidate, DroneRadius)) return false;
        foreach (var other in _drones)
            if (other.Id != drone.Id && Vector3.DistanceSquared(candidate, other.Position) < 1.12f * 1.12f)
                return false;
        return true;
    }

    private Vector3 DesiredPosition(Vector3 offset, float compact)
    {
        var forward = new Vector3(_heading.X, _heading.Y, 0);
        if (forward.LengthSquared() < 0.01f) forward = Vector3.UnitY;
        forward = Vector3.Normalize(forward);
        var side = new Vector3(-forward.Y, forward.X, 0);
        return _leader + (side * offset.X + forward * offset.Y) * compact
            + Vector3.UnitZ * offset.Z;
    }

    private Vector3 ObstacleForce(Vector3 position)
    {
        const float range = 5.0f;
        Vector3 force = Vector3.Zero;
        foreach (var obstacle in _scenario.World.Obstacles)
        {
            float d = obstacle.Distance(position);
            if (d >= range) continue;
            d = MathF.Max(d, 0.35f);
            float value = 1.9f * (1 / d - 1 / range) / (d * d);
            force += obstacle.DirectionAway(position) * MathF.Min(value, 10);
        }
        // Отталкивание от границ мира.
        float[] distances = [position.X, World.Width - position.X,
            position.Y, World.Length - position.Y, position.Z, World.Height - position.Z];
        Vector3[] directions = [Vector3.UnitX, -Vector3.UnitX,
            Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        for (int i = 0; i < distances.Length; i++)
        {
            float d = MathF.Max(0.35f, distances[i]);
            if (d < 2.5f) force += directions[i] *
                MathF.Min(7, 1.2f * (1 / d - 1 / 2.5f) / (d * d));
        }
        return Vectors.Limit(force, 10f);
    }

    private Vector3 SeparationForce(Drone drone)
    {
        const float range = 3.4f;
        Vector3 force = Vector3.Zero;
        foreach (var other in _drones)
        {
            if (other.Id == drone.Id) continue;
            var delta = drone.Position - other.Position;
            float d = delta.Length();
            if (d >= range || d < 1e-5f) continue;
            float safeD = MathF.Max(d, 0.6f);
            force += delta / d * MathF.Min(9,
                4.5f * (1 / safeD - 1 / range) / (safeD * safeD));
        }
        return Vectors.Limit(force, 9);
    }

    public object Snapshot()
    {
        lock (_sync)
        {
            return new
            {
                version = _version,
                scenario = _scenario.Id,
                scenarioName = _scenario.Name,
                planner = _planner,
                running = _running,
                arrived = _arrived,
                simulationTime = Math.Round(_time, 2),
                speed = _speed,
                world = new { width = World.Width, length = World.Length, height = World.Height },
                start = Vec3.From(_scenario.Start),
                goal = Vec3.From(_scenario.Goal),
                leader = Vec3.From(_leader),
                leaderVelocity = Vec3.From(_leaderVelocity),
                currentWaypoint = _waypoint,
                obstacles = _scenario.World.Obstacles.Select(o => new
                {
                    name = o.Name, min = Vec3.From(o.Min), max = Vec3.From(o.Max)
                }).ToArray(),
                path = _current.Path.Select(Vec3.From).ToArray(),
                astarPath = _astar.Path.Select(Vec3.From).ToArray(),
                thetaPath = _theta.Path.Select(Vec3.From).ToArray(),
                drones = _drones.Select(d => new
                {
                    id = d.Id,
                    position = Vec3.From(d.Position),
                    velocity = Vec3.From(d.Velocity),
                    target = Vec3.From(DesiredPosition(d.Offset,
                        Math.Clamp((_scenario.World.Clearance(_leader) - 0.4f) / 3.5f, 0.52f, 1f))),
                    distance = Math.Round(d.Distance, 2),
                    blockedSteps = d.Blocked,
                    trail = d.Trail.Select(Vec3.From).ToArray()
                }).ToArray(),
                metrics = new
                {
                    astar = Metrics(_astar),
                    theta = Metrics(_theta),
                    dronesDistance = Math.Round(_drones.Sum(d => d.Distance), 1),
                    blockedSteps = _drones.Sum(d => d.Blocked),
                    leaderDistanceRemaining = Math.Round(Vector3.Distance(_leader, _scenario.Goal), 2)
                }
            };
        }
    }

    private static object Metrics(PlanResult p) => new
    {
        found = p.Found, expanded = p.Expanded,
        milliseconds = Math.Round(p.Milliseconds, 1),
        length = Math.Round(p.Distance, 2),
        turns = p.Turns, waypoints = p.Path.Count
    };

    // Отдельная точка доступа для автоматизированных проверок.
    public bool IsSafe()
    {
        lock (_sync)
        {
            foreach (var drone in _drones)
            {
                if (!_scenario.World.IsFree(drone.Position, DroneRadius - 1e-3f)) return false;
                foreach (var other in _drones)
                    if (other.Id != drone.Id && Vector3.Distance(drone.Position, other.Position) < 1.10f)
                        return false;
            }
            return _scenario.World.IsFree(_leader, LeaderRadius - 1e-3f);
        }
    }
}
