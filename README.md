# Лабораторная работа №1
## Децентрализованное управление роем малоразмерных агентов

Проект реализует двумерную симуляцию движения роя агентов к общей точке сборки.

### Что реализовано

- движение каждого агента дискретными шагами;
- инерционная составляющая;
- когнитивная составляющая;
- социальная составляющая, использующая лучшего соседа;
- общая целевая точка;
- прямоугольные препятствия;
- обход препятствий;
- минимальная дистанция между агентами;
- ограничение максимальной скорости;
- визуализация траекторий;
- статистика числа итераций, доставленных агентов и средней длины пути.

Формула движения из задания реализована в `SwarmSimulation.Step()`.

Для прикладной части задания дополнительно введены три корректирующих вектора:
`goal`, `separation`, `obstacleAvoidance`.
Они нужны для явного движения к точке сборки, предотвращения столкновений и обхода препятствий.

## Требования

- .NET SDK 9.0.100 или новее (линейка 9.0.x)
- интернет при первом `dotnet restore`, чтобы скачать пакеты Avalonia

Проверка:

```bash
dotnet --version
```

Ожидается версия SDK `9.0.x`.

## Запуск

Из корня проекта:

```bash
dotnet restore
dotnet run --project SwarmLab1.Desktop
```

## Сборка

```bash
dotnet build -c Release
```

## Публикация под разные ОС

Выполнять из корня проекта.

### Windows x64

```bash
dotnet publish SwarmLab1.Desktop/SwarmLab1.Desktop.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o publish/win-x64
```

### Windows ARM64

```bash
dotnet publish SwarmLab1.Desktop/SwarmLab1.Desktop.csproj \
  -c Release \
  -r win-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o publish/win-arm64
```

### macOS Apple Silicon (M1/M2/M3/M4/M5...)

```bash
dotnet publish SwarmLab1.Desktop/SwarmLab1.Desktop.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o publish/osx-arm64
```

### macOS Intel

```bash
dotnet publish SwarmLab1.Desktop/SwarmLab1.Desktop.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o publish/osx-x64
```

### Linux x64

```bash
dotnet publish SwarmLab1.Desktop/SwarmLab1.Desktop.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o publish/linux-x64
```

После публикации готовые файлы находятся в папке `publish`.

`--self-contained true` включает среду выполнения .NET в публикацию, поэтому на компьютере,
где запускается готовая сборка, отдельно устанавливать .NET Runtime не требуется.

## Основные параметры

Они находятся в `SwarmLab1.Core/SimulationSettings.cs`.

Можно менять:

- `AgentCount` — число агентов;
- `InertiaCoefficient` — инерция;
- `CognitiveCoefficient` — когнитивный коэффициент;
- `SocialCoefficient` — социальный коэффициент;
- `MinAgentDistance` — минимальное расстояние между агентами;
- `ObstacleAvoidanceRadius` — радиус реакции на препятствие;
- `MaxSpeed` — максимальная скорость.

## Где менять карту

В `SwarmLab1.Core/SwarmSimulation.cs`:

```csharp
Target = new Vector2(92f, 35f);

Obstacles =
[
    new RectangleObstacle(31f, 5f, 10f, 34f),
    new RectangleObstacle(53f, 34f, 11f, 31f),
    new RectangleObstacle(72f, 10f, 8f, 21f)
];
```

## Что показывать на защите

1. Запустить программу.
2. Показать, что агенты идут к точке сборки.
3. Показать обход препятствий.
4. Показать, что агенты не должны занимать одну и ту же позицию.
5. Поставить симуляцию на паузу и выполнить несколько одиночных шагов.
6. Показать `SwarmSimulation.Step()` и объяснить:
   - `inertia`;
   - `cognitive`;
   - `social`;
   - `goal`;
   - `separation`;
   - `obstacleAvoidance`.
7. Показать параметры в `SimulationSettings`.
