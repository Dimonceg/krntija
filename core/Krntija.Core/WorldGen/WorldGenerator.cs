using Krntija.Core.Items;
using Krntija.Core.World;

namespace Krntija.Core.WorldGen;

/// <summary>
/// Планировщик мира: по сиду и номеру куска говорит, что и где стоит.
/// Чистые данные, без Godot. Один и тот же сид даёт один и тот же план
/// в любом порядке вызовов и у любого игрока.
/// </summary>
public static class WorldGenerator
{
    public const double ChunkLength = 256.0;

    /// <summary>Тир опасности: 0 до километра, дальше +1 каждые 3 км, максимум 10.</summary>
    public const int MaxTier = 10;

    public static int Tier(double s)
    {
        if (s < 1000) return 0;
        int tier = 1 + (int)((s - 1000) / 3000);
        return Math.Clamp(tier, 0, MaxTier);
    }

    public static RoadCurve Curve(ulong worldSeed) => new(worldSeed);

    /// <summary>План куска. Отрицательные куски не существуют.</summary>
    public static ChunkPlan Plan(ulong worldSeed, int chunkIndex)
    {
        if (chunkIndex < 0) throw new ArgumentOutOfRangeException(nameof(chunkIndex), "куски не бывают отрицательными");

        double startS = chunkIndex * ChunkLength;
        double endS = startS + ChunkLength;

        var places = new List<PlacePlan>();
        int count = PlaceCount(worldSeed, chunkIndex);

        for (int placeIndex = 0; placeIndex < count; placeIndex++)
        {
            var place = Place(worldSeed, chunkIndex, placeIndex);
            if (place is not null) places.Add(place);
        }

        return new ChunkPlan(chunkIndex, startS, endS, Tier(startS), places);
    }

    /// <summary>Сколько мест в куске: стартовые куски дают экономику и блокпост.</summary>
    public static int PlaceCount(ulong worldSeed, int chunkIndex)
    {
        if (chunkIndex == 0) return 3;   // дом игрока и пара находок рядом
        if (chunkIndex == 1) return 2;   // заправка
        if (chunkIndex == 2) return 2;   // блокпост

        var rng = new Pcg32(Hash.Mix(worldSeed, chunkIndex, 1));
        int tier = Tier(chunkIndex * ChunkLength);
        int count = 1 + (int)(rng.NextDouble() * (1 + Math.Min(tier, 3)));
        return Math.Clamp(count, 1, 3);
    }

    /// <summary>
    /// Одно место. Зависит только от (сид, кусок, номер места): добавление
    /// соседа или смена его типа не сдвигает это место.
    /// </summary>
    public static PlacePlan? Place(ulong worldSeed, int chunkIndex, int placeIndex)
    {
        if (chunkIndex < 0 || placeIndex < 0) return null;

        double startS = chunkIndex * ChunkLength;
        var curve = Curve(worldSeed);
        var rng = new Pcg32(Hash.Mix(worldSeed, chunkIndex, placeIndex + 1));

        PlaceKind kind = PickKind(worldSeed, chunkIndex, placeIndex, out double lateral, out double alongInChunk, out bool onRoad);
        double s = startS + alongInChunk;

        double heading = curve.Heading(s);
        double yaw = heading + rng.Range(-0.35, 0.35);
        int variantSeed = (int)(rng.NextUInt() & 0x7fffffff);

        string id = PlaceId(chunkIndex, placeIndex);
        var loot = BuildLoot(worldSeed, chunkIndex, placeIndex, kind, Tier(s));
        var uses = BuildUses(id, kind);

        return new PlacePlan(id, kind, s, lateral, yaw, variantSeed, onRoad, loot, uses);
    }

    public static string PlaceId(int chunkIndex, int placeIndex) => $"c{chunkIndex}/p{placeIndex}";

    // ------------------------------------------------------------ типы мест

    private static PlaceKind PickKind(ulong worldSeed, int chunkIndex, int placeIndex, out double lateral,
        out double alongInChunk, out bool onRoad)
    {
        onRoad = false;

        if (chunkIndex == 0 && placeIndex == 0)
        {
            lateral = -14;
            alongInChunk = 24;
            return PlaceKind.House;
        }

        if (chunkIndex == 0 && placeIndex == 1)
        {
            lateral = 28;
            alongInChunk = 96;
            return PlaceKind.Shed;
        }

        if (chunkIndex == 0 && placeIndex == 2)
        {
            lateral = -40;
            alongInChunk = 190;
            return PlaceKind.Well;
        }

        if (chunkIndex == 1 && placeIndex == 0)
        {
            lateral = -20;
            alongInChunk = 92;
            return PlaceKind.GasStation;
        }

        if (chunkIndex == 2 && placeIndex == 0)
        {
            lateral = 0;
            alongInChunk = 64;
            onRoad = true;
            return PlaceKind.Checkpoint;
        }

        // Дальше — обычные места: тип и сторону выбирает ГПСЧ этого места.
        var kindRng = new Pcg32(Hash.Mix(worldSeed, chunkIndex, placeIndex + 3));
        PlaceKind[] kinds =
        {
            PlaceKind.Village, PlaceKind.Ruins, PlaceKind.Farm,
            PlaceKind.Barn, PlaceKind.Shed, PlaceKind.Abandoned, PlaceKind.Well,
        };

        var kind = kinds[kindRng.NextInt(kinds.Length)];
        double side = kindRng.Chance(0.5) ? 1 : -1;
        lateral = side * kindRng.Range(30, 90);
        // Слоты вдоль куска: места не наезжают друг на друга.
        alongInChunk = 48 + placeIndex * 80 + kindRng.Range(-6, 6);
        return kind;
    }

    // ------------------------------------------------------------ лут

    private static List<LootPlan> BuildLoot(ulong worldSeed, int chunkIndex, int placeIndex,
        PlaceKind kind, int tier)
    {
        var rng = new Pcg32(Hash.Mix(worldSeed, chunkIndex, placeIndex + 11));
        var table = TableFor(kind);

        var result = new List<LootPlan>();
        var rolled = LootRoll.Roll(table, rng);

        // Дальше от дома — щедрее: на высоких тирах добавляется ещё один заход.
        if (tier >= 3)
        {
            var extra = LootRoll.Roll(table, rng);
            rolled.AddRange(extra);
        }

        for (int i = 0; i < rolled.Count; i++)
        {
            double angle = rng.Range(0, Math.Tau);
            double distance = rng.Range(2.0, 6.0);

            result.Add(new LootPlan(
                $"{PlaceId(chunkIndex, placeIndex)}/l{i}",
                rolled[i].DefId,
                rolled[i].Count,
                Math.Cos(angle) * distance,
                0.2,
                Math.Sin(angle) * distance));
        }

        return result;
    }

    private static LootTable TableFor(PlaceKind kind) => kind switch
    {
        PlaceKind.GasStation => LootTables.GasStation,
        PlaceKind.Village => LootTables.Village,
        PlaceKind.Ruins => LootTables.Ruins,
        PlaceKind.Farm => LootTables.Farm,
        PlaceKind.House or PlaceKind.Abandoned => LootTables.House,
        _ => LootTables.Roadside,
    };

    // ------------------------------------------------------------ взаимодействия

    private static List<UsePlan> BuildUses(string placeId, PlaceKind kind)
    {
        var uses = new List<UsePlan>();

        void Add(string kindName, double x, double y, double z) =>
            uses.Add(new UsePlan($"{placeId}/u{uses.Count}", kindName, x, y, z));

        switch (kind)
        {
            case PlaceKind.GasStation:
                Add("pump", 4, 0, 6);
                Add("shop", -4, 0, -6);
                break;
            case PlaceKind.Village:
                Add("sell", 0, 0, 8);
                Add("sleep", -6, 0, -4);
                break;
            case PlaceKind.Checkpoint:
                Add("gate", 0, 0, 0);
                break;
            case PlaceKind.House:
            case PlaceKind.Abandoned:
                Add("door", 0, 0, 3.2);
                Add("chest", -3, 0, -2);
                Add("bed", 3, 0, -2);
                break;
            case PlaceKind.Well:
                Add("drink", 0, 0, 1.4);
                break;
        }

        return uses;
    }
}
