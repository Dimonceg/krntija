namespace Krntija.Core.WorldGen;

/// <summary>
/// Дворовый декор (бочки, шины, поддоны, лом, скамейки, знаки). Свой поток ГПСЧ:
/// декор не сдвигает лут, угрозы и точки взаимодействия. Не входит в
/// <see cref="PlacePlan"/> и в сейв: его строят заново по сиду, а состояния у него нет.
/// </summary>
public static partial class WorldGenerator
{
    /// <summary>Не ближе этого к оси дороги: декор не на полотне и не на обочине.</summary>
    public const double DecorRoadClearance = 7.0;

    /// <summary>Не ближе этого к точке действия, луту или угрозе: подход свободен.</summary>
    public const double DecorUseClearance = 1.6;

    /// <summary>Коридор перед входом (сторона −Z): полуширина и глубина.</summary>
    public const double EntranceHalfWidth = 1.5;
    public const double EntranceDepth = 3.0;

    private static readonly string[] YardKit =
        { "rusty_barrel", "tyre_stack", "wooden_pallet", "scrap_heap", "broken_bench", "warning_sign" };

    public static IReadOnlyList<string> DecorModels => YardKit;

    private static string[] DecorFor(PlaceKind kind) => kind switch
    {
        PlaceKind.Shed => new[] { "rusty_barrel", "tyre_stack", "wooden_pallet" },
        PlaceKind.GasStation => new[] { "rusty_barrel", "tyre_stack", "rusty_barrel" },
        PlaceKind.Abandoned => new[] { "broken_bench", "scrap_heap", "rusty_barrel" },
        PlaceKind.Farm or PlaceKind.Barn => new[] { "wooden_pallet", "scrap_heap", "rusty_barrel", "wooden_pallet" },
        PlaceKind.Ruins => new[] { "scrap_heap", "warning_sign", "scrap_heap" },
        PlaceKind.Village => new[] { "broken_bench", "rusty_barrel", "wooden_pallet" },
        _ => Array.Empty<string>(), // дом игрока, колодец, блокпост на полотне — без декора
    };

    /// <summary>Декор места. Зависит только от сида и id места.</summary>
    public static IReadOnlyList<DecorPlan> Decor(ulong worldSeed, PlacePlan place, RoadCurve curve)
    {
        var result = new List<DecorPlan>();
        var kit = DecorFor(place.Kind);
        if (kit.Length == 0 || place.OnRoad || !TryParseId(place.Id, out int chunk, out int index)) return result;

        var rng = Rng(worldSeed, chunk, index, Stream.Decor);
        int count = 2 + rng.NextInt(3);
        double halfWidth = place.Footprint.HalfWidth, halfDepth = place.Footprint.HalfDepth;

        for (int n = 0; n < count; n++)
        {
            string model = kit[rng.NextInt(kit.Length)];
            for (int attempt = 0; attempt < 6; attempt++)
            {
                double x, z, yaw = rng.Range(0, Math.Tau);
                if (model == "broken_bench")
                {
                    // Скамейка — у фасада, сбоку от входа, спиной к дому.
                    x = (rng.Chance(0.5) ? 1 : -1) * rng.Range(EntranceHalfWidth + 1.2, Math.Max(EntranceHalfWidth + 1.3, halfWidth - 0.5));
                    z = -halfDepth - 1.0;
                    yaw = 0;
                }
                else if (rng.Chance(0.5))
                {
                    x = (rng.Chance(0.5) ? 1 : -1) * (halfWidth + rng.Range(0.8, 3.0));
                    z = rng.Range(-halfDepth, halfDepth);
                }
                else
                {
                    x = rng.Range(-halfWidth, halfWidth);
                    z = halfDepth + rng.Range(0.8, 3.0);
                }

                if (!DecorSpotFree(place, curve, x, z, result)) continue;
                result.Add(new DecorPlan(model, x, z, yaw));
                break;
            }
        }

        return result;
    }

    /// <summary>Точка места (оси места) → мир: X вправо (cos, sin), Z против хода дороги (−sin, cos).</summary>
    public static (double X, double Z) PlaceToWorld(PlacePlan place, RoadCurve curve, double x, double z)
    {
        var (cx, cz) = PlaceGeometry.Center(place, curve);
        double cos = Math.Cos(place.Yaw), sin = Math.Sin(place.Yaw);
        return (cx + x * cos - z * sin, cz + x * sin + z * cos);
    }

    private static bool DecorSpotFree(PlacePlan place, RoadCurve curve, double x, double z, List<DecorPlan> placed)
    {
        if (Math.Abs(x) < EntranceHalfWidth + 0.5 && z < -place.Footprint.HalfDepth + 0.5
            && z > -place.Footprint.HalfDepth - EntranceDepth)
            return false;

        foreach (var use in place.Uses)
            if (Near(use.OffsetX, use.OffsetZ)) return false;
        foreach (var loot in place.Loot)
            if (Near(loot.OffsetX, loot.OffsetZ)) return false;
        foreach (var hazard in place.Hazards)
            if (Near(hazard.OffsetX, hazard.OffsetZ)) return false; // попутчик, корова, яма — не под бочкой
        foreach (var other in placed)
            if (Near(other.OffsetX, other.OffsetZ)) return false;

        var (wx, wz) = PlaceToWorld(place, curve, x, z);
        return Math.Abs(curve.Project(wx, wz).Lateral) >= DecorRoadClearance;

        bool Near(double ox, double oz) => (ox - x) * (ox - x) + (oz - z) * (oz - z) < DecorUseClearance * DecorUseClearance;
    }

    private static bool TryParseId(string id, out int chunk, out int place)
    {
        chunk = place = -1;
        var parts = id.Split('/');
        return parts.Length == 2 && parts[0].Length > 1 && parts[1].Length > 1
               && int.TryParse(parts[0][1..], out chunk) && int.TryParse(parts[1][1..], out place);
    }
}
