namespace SsaLab4.Core;

public readonly record struct V3(double X, double Y, double Z)
{
    public static V3 operator +(V3 a, V3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static V3 operator -(V3 a, V3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static V3 operator *(V3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    public static V3 operator /(V3 a, double k) => new(a.X / k, a.Y / k, a.Z / k);
    public double Norm() => Math.Sqrt(X * X + Y * Y + Z * Z);
    public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static double Distance(V3 a, V3 b) => (a - b).Norm();
    public double At(int dim) => dim switch { 0 => X, 1 => Y, 2 => Z, _ => throw new IndexOutOfRangeException() };
    public V3 With(int dim, double val) => dim switch {
        0 => this with { X = val }, 1 => this with { Y = val }, 2 => this with { Z = val },
        _ => throw new IndexOutOfRangeException()
    };
}

public record Box(V3 Min, V3 Max);
public record Circle(V3 Center, double Radius);
public record Pair(V3 Start, V3 Goal);
public record MapData(int Id, string Name, bool Is3D, V3 Min, V3 Max,
    List<Box> Boxes, List<Circle> Circles, List<Pair> Agents);

public sealed class RunRequest
{
    public int MapId { get; set; } = 0;
    public int Population { get; set; } = 55;
    public int Iterations { get; set; } = 130;
    public int Seed { get; set; } = 42;
    public double Speed { get; set; } = 2;
    public double Separation { get; set; } = 0.4;
    public bool Smooth { get; set; } = true;
    public bool RepairInvalidStarts { get; set; } = true;
}

public record RouteResult(int Id, V3 Start, V3 Goal, List<V3> InitialPath,
    List<V3> RawPath, List<V3> Path, double StartDelay,
    double Length, double InitialLength, double Cost, int Turns, bool CollisionFree,
    List<double> Convergence, string Note);

public record RunResult(MapData Map, List<RouteResult> Routes, double ElapsedMs,
    bool AllSafe, bool AgentsSeparated, double Separation,
    int Iterations, int Population, string Method, List<string> Warnings);
