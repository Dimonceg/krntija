namespace Krntija.Core.WorldGen;

/// <summary>Типы мест, которые умеет ставить планировщик.</summary>
public enum PlaceKind
{
    House,
    GasStation,
    Village,
    Checkpoint,
    Ruins,
    Farm,
    Barn,
    Well,
    Shed,
    Abandoned,
}

/// <summary>Предмет в мире: что, сколько и где относительно центра места.</summary>
public sealed record LootPlan(
    string Id,
    string ItemId,
    int Count,
    double OffsetX,
    double OffsetY,
    double OffsetZ);

/// <summary>Точка взаимодействия: колонка, лавка, кровать, колодец, шлагбаум.</summary>
public sealed record UsePlan(
    string Id,
    string Kind,
    double OffsetX,
    double OffsetY,
    double OffsetZ);

/// <summary>
/// Габарит места в его собственных осях: полуширина — поперёк дороги (ось X
/// места, вправо), полуглубина — вдоль дороги (ось Z места). По нему сцена выравнивает площадку и расчищает
/// растительность, а ядро проверяет, что места не наезжают друг на друга.
/// </summary>
public sealed record Footprint(double HalfWidth, double HalfDepth)
{
    /// <summary>Радиус описанной окружности — для грубых проверок и отступа от дороги.</summary>
    public double Radius => Math.Sqrt(HalfWidth * HalfWidth + HalfDepth * HalfDepth);
}

/// <summary>Одно место в куске: где стоит, какого размера и что внутри.</summary>
public sealed record PlacePlan(
    string Id,
    PlaceKind Kind,
    double S,
    double Lateral,
    double Yaw,
    int VariantSeed,
    bool OnRoad,
    Footprint Footprint,
    IReadOnlyList<LootPlan> Loot,
    IReadOnlyList<UsePlan> Uses);

/// <summary>Кусок мира: 256 м дороги и всё, что на них стоит.</summary>
public sealed record ChunkPlan(
    int Index,
    double StartS,
    double EndS,
    int Tier,
    IReadOnlyList<PlacePlan> Places);
