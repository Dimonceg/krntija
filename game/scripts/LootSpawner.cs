using System.Collections.Generic;
using Godot;
using Krntija.Core.Items;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>
/// Расставляет лут по миру: у каждой точки интереса — своя таблица,
/// в доме игрока — стартовый набор. Всё по сиду, значит воспроизводимо.
/// </summary>
public static class LootSpawner
{
    private const ulong Seed = 20261024;

    /// <summary>Мировые точки лута у точек интереса: (позиция, id предмета, количество).</summary>
    public static List<(Vector3 Position, string ItemId, int Count)> PlanLandmarks(
        IReadOnlyList<RoadMeshBuilder.PointOfInterest> points)
    {
        var result = new List<(Vector3, string, int)>();

        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];
            LootTable table = point.Segment.Feature switch
            {
                RoadFeature.GasStation => LootTables.GasStation,
                RoadFeature.Village => LootTables.Village,
                RoadFeature.Ruins => LootTables.Ruins,
                RoadFeature.Farm => LootTables.Farm,
                _ => LootTables.Roadside,
            };

            var rng = new Pcg32(Seed + (ulong)i * 17, sequence: 9);
            var loot = LootRoll.Roll(table, rng);
            var right = new Vector3(Mathf.Cos(point.Heading), 0, Mathf.Sin(point.Heading));

            for (int n = 0; n < loot.Count; n++)
            {
                // Раскладываем вокруг места, чтобы не лежали в одной точке.
                float along = (float)rng.Range(-3.5, 3.5);
                float across = (float)rng.Range(-2.5, 2.5);
                Vector3 spot = point.Position - right * (14f + across) + right * 0f;
                spot += new Vector3(-Mathf.Sin(point.Heading), 0, Mathf.Cos(point.Heading)) * along;
                spot.Y = TerrainBuilder.SurfaceHeight(point.Position.Y, -14f, spot);

                result.Add((spot, loot[n].DefId, loot[n].Count));
            }
        }

        return result;
    }

    /// <summary>Кладка внутри дома: кухня, гараж, чердак.</summary>
    public static List<(Vector3 Position, string ItemId, int Count)> PlanHouse(Node3D house)
    {
        var result = new List<(Vector3, string, int)>();
        var rng = new Pcg32(Seed + 900, sequence: 13);
        var loot = LootRoll.Roll(LootTables.House, rng);

        // Локальные точки дома: кухня, гараж, гостиная, чердак.
        Vector3[] spots =
        {
            new(-4.6f, 1.05f, 3.2f),
            new(4.3f, 1.05f, -0.6f),
            new(-1.6f, 0.25f, 0.9f),
            new(1.0f, 3.05f, -1.4f),
            new(-3.8f, 0.25f, 1.6f),
        };

        for (int i = 0; i < loot.Count && i < spots.Length; i++)
        {
            Vector3 position = house.ToGlobal(spots[i] + new Vector3(
                (float)rng.Range(-0.3, 0.3), 0, (float)rng.Range(-0.3, 0.3)));
            result.Add((position, loot[i].DefId, loot[i].Count));
        }

        return result;
    }
}
