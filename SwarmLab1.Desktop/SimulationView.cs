using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SwarmLab1.Core;

namespace SwarmLab1.Desktop;

public sealed class SimulationView : Control
{
    public SwarmSimulation? Simulation { get; set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var simulation = Simulation;
        if (simulation is null || Bounds.Width <= 1 || Bounds.Height <= 1)
            return;

        var settings = simulation.Settings;

        var scaleX = Bounds.Width / settings.WorldWidth;
        var scaleY = Bounds.Height / settings.WorldHeight;
        var scale = Math.Min(scaleX, scaleY);

        var worldWidthPx = settings.WorldWidth * scale;
        var worldHeightPx = settings.WorldHeight * scale;

        var offsetX = (Bounds.Width - worldWidthPx) / 2.0;
        var offsetY = (Bounds.Height - worldHeightPx) / 2.0;

        Point ToScreen(System.Numerics.Vector2 p) =>
            new(offsetX + p.X * scale, offsetY + p.Y * scale);

        var worldRect = new Rect(offsetX, offsetY, worldWidthPx, worldHeightPx);
        context.FillRectangle(Brushes.WhiteSmoke, worldRect);
        context.DrawRectangle(new Pen(Brushes.DimGray, 1), worldRect);

        // Препятствия.
        foreach (var obstacle in simulation.Obstacles)
        {
            var rect = new Rect(
                offsetX + obstacle.X * scale,
                offsetY + obstacle.Y * scale,
                obstacle.Width * scale,
                obstacle.Height * scale);

            context.FillRectangle(Brushes.DarkSlateGray, rect);
        }

        // Точка сборки.
        var target = ToScreen(simulation.Target);
        var targetRadius = settings.TargetRadius * scale;

        context.DrawEllipse(
            Brushes.IndianRed,
            new Pen(Brushes.DarkRed, 2),
            target,
            targetRadius,
            targetRadius);

        // Траектории.
        foreach (var agent in simulation.Agents)
        {
            if (agent.ReachedTarget)
                continue;

            if (agent.Trail.Count < 2)
                continue;

            var geometry = new StreamGeometry();

            using (var geometryContext = geometry.Open())
            {
                geometryContext.BeginFigure(ToScreen(agent.Trail[0]), false);

                for (var i = 1; i < agent.Trail.Count; i++)
                    geometryContext.LineTo(ToScreen(agent.Trail[i]));
            }

            context.DrawGeometry(
                null,
                new Pen(Brushes.SteelBlue, 0.8),
                geometry);
        }

        // Агенты.
        foreach (var agent in simulation.Agents)
        {
            if (agent.ReachedTarget)
                continue;

            var center = ToScreen(agent.Position);
            var radius = Math.Max(3.5, 0.8 * scale);

            context.DrawEllipse(
                Brushes.DodgerBlue,
                new Pen(Brushes.Navy, 1),
                center,
                radius,
                radius);
        }
    }
}
