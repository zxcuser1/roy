using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SwarmLab1.Core;

namespace SwarmLab1.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private SwarmSimulation _simulation;
    private bool _running;

    public MainWindow()
    {
        InitializeComponent();

        _simulation = CreateSimulation();
        SimulationView.Simulation = _simulation;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(25)
        };

        _timer.Tick += (_, _) =>
        {
            if (!_running)
                return;

            _simulation.Step();
            SimulationView.InvalidateVisual();
            UpdateStats();

            if (_simulation.IsFinished)
            {
                _running = false;
                StartPauseButton.Content = "Старт";
            }
        };

        _timer.Start();
        UpdateStats();
    }

    private void StartPauseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _running = !_running;
        StartPauseButton.Content = _running ? "Пауза" : "Старт";
    }

    private void NewEnvironmentButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _running = false;
        StartPauseButton.Content = "Старт";

        _simulation.RandomizeEnvironment();
        SimulationView.InvalidateVisual();
        UpdateStats();
    }

    private static SwarmSimulation CreateSimulation() =>
        new(seed: Random.Shared.Next());

    private void UpdateStats()
    {
        StatsText.Text =
            $"Итерация: {_simulation.Iteration}   " +
            $"Доставлено: {_simulation.ReachedCount}/{_simulation.Agents.Count}   " +
            $"Средний путь: {_simulation.AveragePathLength:F1}";
    }
}
