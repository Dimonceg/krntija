using Krntija.Core.Items;
using Krntija.Core.World;

namespace Krntija.Core.WorldGen;

/// <summary>
/// Планировщик мира: по сиду и номеру куска говорит, что и где стоит.
/// Чистые данные, без Godot. Один и тот же сид даёт один и тот же план
/// в любом порядке вызовов и у любого игрока.
/// </summary>
public static partial class WorldGenerator
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

    /// <summary>
    /// Независимые потоки ГПСЧ одного места: у каждой цели своя последовательность
    /// PCG. Раньше потоки различались смещением номера места (+1, +3, +11), и
    /// ГПСЧ одного места совпадал с ГПСЧ другого (тип места 0 = основной поток места 2).
    /// </summary>
    private enum Stream : ulong
    {
        Count = 1,
        Place = 2,
        Kind = 3,
        Loot = 4,
        Decor = 5,
    }

    /// <summary>Номер «места» для решений уровня куска (сколько мест в нём).</summary>
    private const int ChunkLevel = -1;

    private static Pcg32 Rng(ulong worldSeed, int chunkIndex, int placeIndex, Stream stream) =>
        new(Hash.Mix(worldSeed, chunkIndex, placeIndex), (ulong)stream);

    /// <summary>План куска. Отрицательные куски не существуют.</summary>
    public static ChunkPlan Plan(ulong worldSeed, int chunkIndex)
    {
        if (chunkIndex < 0) throw new ArgumentOutOfRangeException(nameof(chunkIndex), "куски не бывают отрицательными");

        double startS = chunkIndex * ChunkLength;
        double endS = startS + ChunkLength;

        var places = new List<PlacePlan>();
        var curve = Curve(worldSeed);
        int count = PlaceCount(worldSeed, chunkIndex);

        for (int placeIndex = 0; placeIndex < count; placeIndex++)
        {
            var place = Place(worldSeed, chunkIndex, placeIndex);
            if (place is null) continue;

            // Места проверяются формой: пересекающееся место в план не попадает.
            bool clashes = places.Any(other => PlaceGeometry.Overlaps(other, place, curve));
            if (!clashes) places.Add(place);
        }

        return new ChunkPlan(chunkIndex, startS, endS, Tier(startS), places);
    }

    /// <summary>Сколько мест в куске: стартовые куски дают экономику и блокпост.</summary>
    public static int PlaceCount(ulong worldSeed, int chunkIndex)
    {
        if (chunkIndex == 0) return 3;   // дом игрока и пара находок рядом
        if (chunkIndex == 1) return 2;   // заправка
        if (chunkIndex == 2) return 2;   // блокпост

        var rng = Rng(worldSeed, chunkIndex, ChunkLevel, Stream.Count);
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
        var rng = Rng(worldSeed, chunkIndex, placeIndex, Stream.Place);

        PlaceKind kind = PickKind(worldSeed, chunkIndex, placeIndex, out double lateral, out double alongInChunk, out bool onRoad);
        double s = startS + alongInChunk;

        double heading = curve.Heading(s);
        double yaw = heading + rng.Range(-0.35, 0.35);
        int variantSeed = (int)(rng.NextUInt() & 0x7fffffff);

        string id = PlaceId(chunkIndex, placeIndex);
        int tier = Tier(s);
        var loot = BuildLoot(worldSeed, chunkIndex, placeIndex, kind, tier);
        var uses = BuildUses(id, kind, tier);
        var hazards = BuildHazards(worldSeed, chunkIndex, placeIndex, kind, tier);

        return new PlacePlan(id, kind, s, lateral, yaw, variantSeed, onRoad,
            FootprintFor(kind), loot, uses, hazards);
    }

    public static string PlaceId(int chunkIndex, int placeIndex) => $"c{chunkIndex}/p{placeIndex}";

    /// <summary>Габарит по типу места: полуширина и полуглубина в осях постройки.</summary>
    public static Footprint FootprintFor(PlaceKind kind) => kind switch
    {
        PlaceKind.House => new Footprint(4.0, 5.5),
        PlaceKind.Abandoned => new Footprint(5.0, 4.0),
        PlaceKind.GasStation => new Footprint(9.0, 7.0),
        PlaceKind.Village => new Footprint(14.0, 11.0),
        PlaceKind.Checkpoint => new Footprint(5.0, 2.0),
        PlaceKind.Ruins => new Footprint(6.0, 5.0),
        PlaceKind.Farm => new Footprint(11.0, 8.0),
        PlaceKind.Barn => new Footprint(7.0, 6.0),
        PlaceKind.Shed => new Footprint(2.6, 2.0),
        PlaceKind.Well => new Footprint(2.0, 2.0),
        _ => new Footprint(4.0, 4.0),
    };

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
        var kindRng = Rng(worldSeed, chunkIndex, placeIndex, Stream.Kind);
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
        var rng = Rng(worldSeed, chunkIndex, placeIndex, Stream.Loot);
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

    private static List<UsePlan> BuildUses(string placeId, PlaceKind kind, int tier)
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
                Add("talk", 2.5, 0, 1.5);
                Add("talk", -2.0, 0, -3.0);
                break;
            case PlaceKind.Checkpoint:
                Add("gate", 0, 0, 0);
                // Закрытый боковой выезд: открывается за деньги, как в старом мире.
                Add("exit", 6, 0, 0);
                break;
            case PlaceKind.Barn:
                Add("dog", 2.5, 0, 2.5);
                if (ThreatRules.WolvesAt(tier) > 0) Add("wolves", -2.5, 0, 3.5);
                break;
            case PlaceKind.Ruins:
                if (ThreatRules.HasCollapse(tier)) Add("collapse", 0, 0, 1.5);
                break;
            case PlaceKind.House:
                // Двери, сундук и кровать дома игрока строит PlayerHouse: в плане их нет.
                break;
            case PlaceKind.Abandoned:
                // В брошенном доме только ящик с лутом: сундук — это склад дома игрока.
                Add("crate", -3, 0, -2);
                break;
            case PlaceKind.Well:
                Add("drink", 0, 0, 1.4);
                break;
        }

        return uses;
    }

    // ------------------------------------------------------------ дорожные угрозы

    /// <summary>
    /// Угрозы у места: яма на подъезде, корова у фермы или амбара, попутчик у села.
    /// Чем опаснее тир, тем больше шансов, что у места что-то случится.
    /// </summary>
    private static List<HazardPlan> BuildHazards(ulong worldSeed, int chunkIndex, int placeIndex,
        PlaceKind kind, int tier)
    {
        var result = new List<HazardPlan>();
        var rng = new Pcg32(Hash.Mix(worldSeed, chunkIndex, placeIndex + 23));

        if (!rng.Chance(ThreatRules.DangerChance(tier))) return result;

        int count = 1 + (rng.Chance(0.25 + tier * 0.05) ? 1 : 0);

        for (int i = 0; i < count; i++)
        {
            HazardKind hazard = kind switch
            {
                PlaceKind.Farm or PlaceKind.Barn => rng.Chance(0.6) ? HazardKind.Cow : HazardKind.Pothole,
                PlaceKind.Village => rng.Chance(0.6) ? HazardKind.Hitchhiker : HazardKind.Pothole,
                _ => HazardKind.Pothole,
            };

            double amount = hazard switch
            {
                HazardKind.Pothole => Math.Round(0.02 + tier * 0.004, 3),
                HazardKind.Hitchhiker => 10 + tier * 5,
                _ => 0,
            };

            result.Add(new HazardPlan(
                $"{PlaceId(chunkIndex, placeIndex)}/h{i}",
                hazard,
                Math.Round(rng.Range(-3.0, 3.0), 2),
                Math.Round(rng.Range(2.0, 7.0), 2),
                amount));
        }

        return result;
    }
}
