using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using SwarmLab2.Core;
using SwarmLab2.Core.Models;

namespace SwarmLab2.Desktop;

public sealed class SimulationView : Control
{
    private static readonly IBrush BackgroundBrush =
        new SolidColorBrush(Color.Parse("#0D1117"));
    private static readonly IPen GridPen =
        new Pen(new SolidColorBrush(Color.Parse("#26303B")), 1);
    private static readonly IPen BoundsPen =
        new Pen(new SolidColorBrush(Color.Parse("#55606D")), 1.2);
    private static readonly IPen ObstaclePen =
        new Pen(new SolidColorBrush(Color.Parse("#E5A449")), 1.7);
    private static readonly IBrush AgentBrush =
        new SolidColorBrush(Color.Parse("#5EC8F8"));
    private static readonly IPen TrailPen =
        new Pen(new SolidColorBrush(Color.Parse("#487C98")), 1.0);
    private static readonly IBrush TargetBrush =
        new SolidColorBrush(Color.Parse("#E96868"), 0.22);
    private static readonly IPen TargetPen =
        new Pen(new SolidColorBrush(Color.Parse("#FF6B6B")), 2.0);
    private static readonly IPen XAxisPen =
        new Pen(new SolidColorBrush(Color.Parse("#E96B6B")), 2);
    private static readonly IPen YAxisPen =
        new Pen(new SolidColorBrush(Color.Parse("#65C77A")), 2);
    private static readonly IPen ZAxisPen =
        new Pen(new SolidColorBrush(Color.Parse("#6A9BEA")), 2);

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private double _lastFrameSeconds;
    private bool _rotating;
    private bool _panning;
    private Point _lastPointer;

    private double _yaw = -0.72;
    private double _pitch = 0.72;
    private double _zoom = 1.0;
    private double _panX;
    private double _panY;

    public SimulationView()
    {
        ClipToBounds = true;
        Focusable = true;

        var settings = new SimulationSettings();
        Simulation = new SwarmSimulation(settings);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
    }

    public SwarmSimulation Simulation { get; }
    public bool IsRunning { get; private set; } = true;

    public event Action? StatsChanged;

    public void Start()
    {
        IsRunning = true;
        _lastFrameSeconds = _clock.Elapsed.TotalSeconds;
    }

    public void Pause() => IsRunning = false;

    public void Reset(int agentCount)
    {
        Simulation.Reset(agentCount);
        _lastFrameSeconds = _clock.Elapsed.TotalSeconds;
        InvalidateVisual();
        StatsChanged?.Invoke();
    }

    public void ResetCamera()
    {
        _yaw = -0.72;
        _pitch = 0.72;
        _zoom = 1.0;
        _panX = 0;
        _panY = 0;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(BackgroundBrush, Bounds);

        if (Bounds.Width < 10 || Bounds.Height < 10)
            return;

        DrawGroundGrid(context);
        DrawWorldBounds(context);
        DrawAxes(context);
        DrawObstacles(context);
        DrawTrails(context);
        DrawTarget(context);
        DrawAgents(context);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var delta = _lastFrameSeconds <= 0 ? 1.0 / 60.0 : now - _lastFrameSeconds;
        _lastFrameSeconds = now;

        if (IsRunning)
            Simulation.Step((float)Math.Min(delta, 0.05));

        InvalidateVisual();
        StatsChanged?.Invoke();
    }

    private void DrawGroundGrid(DrawingContext context)
    {
        var size = Simulation.Settings.WorldSize;

        for (var x = 0f; x <= size.X; x += 10f)
            DrawWorldLine(context, new Vector3(x, 0f, 0f), new Vector3(x, size.Y, 0f), GridPen);

        for (var y = 0f; y <= size.Y; y += 10f)
            DrawWorldLine(context, new Vector3(0f, y, 0f), new Vector3(size.X, y, 0f), GridPen);
    }

    private void DrawWorldBounds(DrawingContext context)
    {
        var s = Simulation.Settings.WorldSize;
        var corners = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(s.X, 0, 0),
            new Vector3(s.X, s.Y, 0),
            new Vector3(0, s.Y, 0),
            new Vector3(0, 0, s.Z),
            new Vector3(s.X, 0, s.Z),
            new Vector3(s.X, s.Y, s.Z),
            new Vector3(0, s.Y, s.Z)
        };

        DrawBoxEdges(context, corners, BoundsPen);
    }

    private void DrawAxes(DrawingContext context)
    {
        DrawWorldLine(context, Vector3.Zero, new Vector3(16, 0, 0), XAxisPen);
        DrawWorldLine(context, Vector3.Zero, new Vector3(0, 16, 0), YAxisPen);
        DrawWorldLine(context, Vector3.Zero, new Vector3(0, 0, 16), ZAxisPen);
    }

    private void DrawObstacles(DrawingContext context)
    {
        foreach (var obstacle in Simulation.Obstacles
                     .OrderByDescending(o => Project(o.Center).Depth))
        {
            var min = obstacle.Min;
            var max = obstacle.Max;
            var corners = new[]
            {
                new Vector3(min.X, min.Y, min.Z),
                new Vector3(max.X, min.Y, min.Z),
                new Vector3(max.X, max.Y, min.Z),
                new Vector3(min.X, max.Y, min.Z),
                new Vector3(min.X, min.Y, max.Z),
                new Vector3(max.X, min.Y, max.Z),
                new Vector3(max.X, max.Y, max.Z),
                new Vector3(min.X, max.Y, max.Z)
            };

            DrawBoxEdges(context, corners, ObstaclePen);
        }
    }

    private void DrawTrails(DrawingContext context)
    {
        foreach (var agent in Simulation.Agents)
        {
            if (agent.Trail.Count < 2)
                continue;

            for (var i = 1; i < agent.Trail.Count; i++)
                DrawWorldLine(context, agent.Trail[i - 1], agent.Trail[i], TrailPen);
        }
    }

    private void DrawTarget(DrawingContext context)
    {
        var projected = Project(Simulation.Settings.Target);
        var radius = Math.Clamp(
            Simulation.Settings.TargetRadius * projected.Scale,
            5,
            22);

        context.DrawEllipse(
            TargetBrush,
            TargetPen,
            projected.Point,
            radius,
            radius);

        var verticalTop = Project(
            Simulation.Settings.Target + Vector3.UnitZ * Simulation.Settings.TargetRadius);
        var verticalBottom = Project(
            Simulation.Settings.Target - Vector3.UnitZ * Simulation.Settings.TargetRadius);
        context.DrawLine(TargetPen, verticalTop.Point, verticalBottom.Point);
    }

    private void DrawAgents(DrawingContext context)
    {
        foreach (var agent in Simulation.Agents
                     .Where(a => !a.ReachedTarget)
                     .OrderByDescending(a => Project(a.Position).Depth))
        {
            var projected = Project(agent.Position);
            var radius = Math.Clamp(3.2 * projected.Perspective, 2.2, 6.2);

            context.DrawEllipse(
                AgentBrush,
                null,
                projected.Point,
                radius,
                radius);
        }
    }

    private void DrawBoxEdges(DrawingContext context, IReadOnlyList<Vector3> c, IPen pen)
    {
        var edges = new (int A, int B)[]
        {
            (0,1), (1,2), (2,3), (3,0),
            (4,5), (5,6), (6,7), (7,4),
            (0,4), (1,5), (2,6), (3,7)
        };

        foreach (var (a, b) in edges)
            DrawWorldLine(context, c[a], c[b], pen);
    }

    private void DrawWorldLine(DrawingContext context, Vector3 a, Vector3 b, IPen pen)
    {
        var pa = Project(a);
        var pb = Project(b);
        context.DrawLine(pen, pa.Point, pb.Point);
    }

    private ProjectedPoint Project(Vector3 world)
    {
        var worldCenter = Simulation.Settings.WorldSize / 2f;
        var p = world - worldCenter;

        var cosYaw = Math.Cos(_yaw);
        var sinYaw = Math.Sin(_yaw);
        var x1 = p.X * cosYaw - p.Y * sinYaw;
        var y1 = p.X * sinYaw + p.Y * cosYaw;
        var z1 = p.Z;

        var cosPitch = Math.Cos(_pitch);
        var sinPitch = Math.Sin(_pitch);
        var y2 = y1 * cosPitch - z1 * sinPitch;
        var z2 = y1 * sinPitch + z1 * cosPitch;

        const double cameraDistance = 220.0;
        var denominator = Math.Max(45.0, cameraDistance + y2);
        var perspective = cameraDistance / denominator;

        var baseScale = Math.Min(
            Bounds.Width / Math.Max(1.0, Simulation.Settings.WorldSize.X),
            Bounds.Height / Math.Max(1.0, Simulation.Settings.WorldSize.Z)) * 0.70 * _zoom;

        var scale = baseScale * perspective;
        var screenX = Bounds.Width / 2.0 + _panX + x1 * scale;
        var screenY = Bounds.Height / 2.0 + _panY - z2 * scale;

        return new ProjectedPoint(
            new Point(screenX, screenY),
            y2,
            scale,
            perspective);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        _lastPointer = e.GetPosition(this);

        var properties = e.GetCurrentPoint(this).Properties;
        _rotating = properties.IsLeftButtonPressed;
        _panning = properties.IsRightButtonPressed;

        if (_rotating || _panning)
            e.Pointer.Capture(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_rotating && !_panning)
            return;

        var current = e.GetPosition(this);
        var dx = current.X - _lastPointer.X;
        var dy = current.Y - _lastPointer.Y;
        _lastPointer = current;

        if (_rotating)
        {
            _yaw += dx * 0.010;
            _pitch = Math.Clamp(_pitch - dy * 0.010, -1.35, 1.35);
        }

        if (_panning)
        {
            _panX += dx;
            _panY += dy;
        }

        InvalidateVisual();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _rotating = false;
        _panning = false;
        e.Pointer.Capture(null);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _zoom *= Math.Pow(1.12, e.Delta.Y);
        _zoom = Math.Clamp(_zoom, 0.45, 2.8);
        InvalidateVisual();
    }

    private readonly record struct ProjectedPoint(
        Point Point,
        double Depth,
        double Scale,
        double Perspective);
}
