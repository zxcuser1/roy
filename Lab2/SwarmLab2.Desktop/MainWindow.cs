using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SwarmLab2.Desktop;

public sealed class MainWindow : Window
{
    private readonly SimulationView _simulationView;
    private readonly Button _runButton;
    private readonly Slider _agentCountSlider;
    private readonly TextBlock _agentCountValue;
    private readonly TextBlock _activeValue;
    private readonly TextBlock _reachedValue;

    public MainWindow()
    {
        Title = "SwarmLab2 — 3D swarm simulation";
        Width = 1220;
        Height = 820;
        MinWidth = 900;
        MinHeight = 600;

        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("270,*")
        };

        var sidebar = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#343A46")),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 12
            }
        };

        var panel = (StackPanel)sidebar.Child;

        panel.Children.Add(new TextBlock
        {
            Text = "Лабораторная работа 2",
            FontSize = 22,
            FontWeight = FontWeight.Bold
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Рой агентов в трёхмерном пространстве",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#A9B0BC"))
        });

        panel.Children.Add(new Separator());

        _runButton = new Button
        {
            Content = "Пауза",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _runButton.Click += (_, _) => ToggleRunning();
        panel.Children.Add(_runButton);

        var resetButton = new Button
        {
            Content = "Перезапустить",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        panel.Children.Add(resetButton);

        var cameraButton = new Button
        {
            Content = "Сбросить камеру",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        panel.Children.Add(cameraButton);

        panel.Children.Add(new Separator());
        panel.Children.Add(new TextBlock
        {
            Text = "Количество агентов"
        });

        _agentCountSlider = new Slider
        {
            Minimum = 10,
            Maximum = 100,
            Value = 40,
            TickFrequency = 5,
            IsSnapToTickEnabled = true
        };
        panel.Children.Add(_agentCountSlider);

        _agentCountValue = new TextBlock
        {
            Text = "40",
            Foreground = new SolidColorBrush(Color.Parse("#A9B0BC"))
        };
        panel.Children.Add(_agentCountValue);

        panel.Children.Add(new Separator());
        panel.Children.Add(new TextBlock
        {
            Text = "Состояние",
            FontWeight = FontWeight.SemiBold
        });

        _activeValue = new TextBlock();
        _reachedValue = new TextBlock();
        panel.Children.Add(_activeValue);
        panel.Children.Add(_reachedValue);

        panel.Children.Add(new Separator());
        panel.Children.Add(new TextBlock
        {
            Text = "Управление камерой",
            FontWeight = FontWeight.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = "ЛКМ + движение — вращение\nПКМ + движение — сдвиг\nКолесо — масштаб",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#A9B0BC"))
        });

        panel.Children.Add(new Separator());
        panel.Children.Add(new TextBlock
        {
            Text = "Синие точки — активные агенты. Красная сфера — цель. Оранжевые каркасы — объёмные препятствия.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#A9B0BC"))
        });

        _simulationView = new SimulationView();
        _simulationView.StatsChanged += UpdateStats;

        resetButton.Click += (_, _) =>
        {
            _simulationView.Reset((int)Math.Round(_agentCountSlider.Value));
            UpdateStats();
        };

        cameraButton.Click += (_, _) => _simulationView.ResetCamera();

        _agentCountSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                _agentCountValue.Text = ((int)Math.Round(_agentCountSlider.Value)).ToString();
        };

        Grid.SetColumn(sidebar, 0);
        Grid.SetColumn(_simulationView, 1);
        root.Children.Add(sidebar);
        root.Children.Add(_simulationView);

        Content = root;
        UpdateStats();
    }

    private void ToggleRunning()
    {
        if (_simulationView.IsRunning)
        {
            _simulationView.Pause();
            _runButton.Content = "Продолжить";
        }
        else
        {
            _simulationView.Start();
            _runButton.Content = "Пауза";
        }
    }

    private void UpdateStats()
    {
        _activeValue.Text = $"В полёте: {_simulationView.Simulation.ActiveCount}";
        _reachedValue.Text = $"Достигли цели: {_simulationView.Simulation.ReachedCount}";
    }
}
