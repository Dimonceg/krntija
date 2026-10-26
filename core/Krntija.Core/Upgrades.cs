namespace Krntija.Core.Items;

/// <summary>Улучшения машины, которые ставятся на верстаке в доме.</summary>
public sealed record UpgradeDef(string Id, string Name, string Description, int Cost);

public static class UpgradeCatalog
{
    public const string FuelTank = "upgrade_tank";
    public const string Springs = "upgrade_springs";
    public const string Toolbox = "upgrade_toolbox";
    public const string Headlights = "upgrade_headlights";
    public const string Bullbar = "upgrade_bullbar";
    public const string Engine = "upgrade_engine";
    public const string RoofRack = "upgrade_roofrack";

    private static readonly Dictionary<string, UpgradeDef> Items = new(StringComparer.OrdinalIgnoreCase)
    {
        [FuelTank] = new(FuelTank, "Увеличенный бак", "+6 л к баку", 40),
        [Springs] = new(Springs, "Усиленные рессоры", "+15 кг в багажник", 35),
        [Toolbox] = new(Toolbox, "Ящик с инструментом", "+10 кг в карман", 30),
        [Headlights] = new(Headlights, "Яркие фары", "Дальше видно ночью", 45),
        [Bullbar] = new(Bullbar, "Кенгурятник", "Меньше урона от ударов", 50),
        [Engine] = new(Engine, "Форсированный двигатель", "Лучше тянет на подъёмах", 70),
        [RoofRack] = new(RoofRack, "Багажник на крышу", "+25 кг в багажник", 55),
    };

    public static IReadOnlyDictionary<string, UpgradeDef> All => Items;

    public static bool TryGet(string id, out UpgradeDef def) => Items.TryGetValue(id, out def!);
}
