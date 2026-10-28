using System.Numerics;
using SwarmLab3.Models;

namespace SwarmLab3.Simulation;

public sealed record Scenario(string Id, string Name, World World, Vector3 Start, Vector3 Goal);

public static class ScenarioFactory
{
    public static readonly string[] Names = ["urban", "corridor", "open"];

    public static Scenario Create(string id)
    {
        var world = new World();
        static Obstacle Box(string name, float x, float y, float w, float l, float h) =>
            new(name, new Vector3(x, y, 0), new Vector3(x + w, y + l, h));

        switch (id)
        {
            case "corridor":
                world.Obstacles.AddRange([
                    Box("Левая перегородка", 18, 13, 17, 54, 10.5f),
                    Box("Правая перегородка", 45, 13, 17, 54, 10.5f),
                    Box("Низкий барьер", 36.5f, 33, 7, 8, 3.4f),
                    Box("Выходная стенка слева", 15, 70, 22, 5, 7),
                    Box("Входной выступ", 2, 17, 10, 7, 4)
                ]);
                return new Scenario(id, "Узкий коридор", world,
                    new Vector3(40, 6, 5.1f), new Vector3(40, 74, 5.1f));
            case "open":
                world.Obstacles.AddRange([
                    Box("Столб 1", 24, 25, 5, 5, 7),
                    Box("Столб 2", 45, 42, 6, 6, 8),
                    Box("Барьер", 29, 58, 11, 3, 4)
                ]);
                return new Scenario(id, "Открытая площадка", world,
                    new Vector3(7, 7, 5), new Vector3(74, 74, 5));
            default:
                world.Obstacles.AddRange([
                    Box("Западный блок", 17, 12, 6, 32, 8.5f),
                    Box("Южная стенка", 34, 1, 5, 33, 10),
                    Box("Центральный блок", 33, 45, 17, 7, 7.6f),
                    Box("Восточный блок", 58, 26, 7, 38, 10),
                    Box("Низкая перекладина", 12, 58, 21, 5, 4),
                    Box("Высокий пилон", 45, 13, 7, 11, 11),
                    Box("Восточный остров", 67, 9, 6, 12, 5),
                    Box("Северная стенка", 48, 70, 19, 5, 7)
                ]);
                return new Scenario(id, "Городские препятствия", world,
                    new Vector3(7, 7, 5), new Vector3(74, 74, 5));
        }
    }
}
