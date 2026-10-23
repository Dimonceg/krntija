namespace Krntija.Core.World;

/// <summary>Что стоит у дороги. Пока — только метки, наполнение появится в M2.</summary>
public enum RoadFeature
{
    None,
    GasStation,
    Village,
    Ruins,
    Farm,
    Checkpoint,
}

public sealed record RoadSegment(
    int Index,
    double StartM,
    double LengthM,
    double Curvature,
    RoadFeature Feature,
    string Title)
{
    public double EndM => StartM + LengthM;
    public bool IsCurve => Math.Abs(Curvature) > 0.01;
    public bool HasFeature => Feature != RoadFeature.None;
}

/// <summary>План дороги: где поворот, где заправка, где село. Чистые данные.</summary>
public sealed class RoadPlan
{
    public RoadPlan(IReadOnlyList<RoadSegment> segments)
    {
        Segments = segments;
        TotalMeters = segments.Count == 0 ? 0 : segments[^1].EndM;
    }

    public IReadOnlyList<RoadSegment> Segments { get; }
    public double TotalMeters { get; }

    public IEnumerable<RoadSegment> Features => Segments.Where(s => s.HasFeature);

    public RoadSegment? FirstWithFeature(RoadFeature feature) =>
        Segments.FirstOrDefault(s => s.Feature == feature);
}

/// <summary>
/// Генератор дороги. Детерминированный: один и тот же seed даёт одну и ту же
/// трассу — это нужно и для тестов, и для коопа, где карта должна совпадать
/// у всех игроков.
/// </summary>
public static class RoadPlanner
{
    public const double DefaultSegmentMeters = 180;

    /// <summary>Сколько радиан поворота набегает на метр при кривизне 1.0.</summary>
    private const double CurvatureToRadiansPerMeter = 0.004;

    public static RoadPlan Generate(
        ulong seed,
        double totalMeters = 3000,
        double segmentMeters = DefaultSegmentMeters)
    {
        if (totalMeters <= 0) throw new ArgumentOutOfRangeException(nameof(totalMeters));
        if (segmentMeters < 40) throw new ArgumentOutOfRangeException(nameof(segmentMeters));

        var rng = new Pcg32(seed, sequence: 7);
        var segments = new List<RoadSegment>();

        double start = 0;
        int index = 0;
        double nextFeatureAt = 450;
        int featuresPlaced = 0;

        while (start < totalMeters)
        {
            RoadFeature feature = RoadFeature.None;
            string title = "";

            if (start >= nextFeatureAt)
            {
                (feature, title) = PickFeature(ref featuresPlaced, rng);
                nextFeatureAt = start + rng.Range(420, 720);
            }

            double curvature = rng.Chance(0.45)
                ? 0
                : Math.Round(rng.Range(0.35, 0.9) * (rng.Chance(0.5) ? 1 : -1), 2);

            double length = segmentMeters + rng.Range(-35, 35);

            segments.Add(new RoadSegment(index, start, length, curvature, feature, title));
            start += length;
            index++;
        }

        return new RoadPlan(segments);
    }

    private static (RoadFeature Feature, string Title) PickFeature(ref int placed, Pcg32 rng)
    {
        placed++;

        // Первые две точки всегда одинаковые: сначала заправка, потом блокпост.
        // Так игрок гарантированно встречает и экономику, и шлагбаум.
        if (placed == 1) return (RoadFeature.GasStation, "Бензинска пумпа");
        if (placed == 2) return (RoadFeature.Checkpoint, "Блокпост");

        var options = new (RoadFeature Feature, string Title)[]
        {
            (RoadFeature.Village, "Село"),
            (RoadFeature.Ruins, "Рушевине"),
            (RoadFeature.Farm, "Фарма"),
            (RoadFeature.Checkpoint, "Блокпост"),
            (RoadFeature.GasStation, "Бензинска пумпа"),
        };

        return rng.Pick(options);
    }

    /// <summary>Шаг поворота для построения геометрии: радианы на метр.</summary>
    public static double RadiansPerMeter(double curvature) => curvature * CurvatureToRadiansPerMeter;
}
