using System.Text.Json;
using System.Text.Json.Serialization;
using Krntija.Core.Economy;
using Krntija.Core.Items;
using Krntija.Core.Parts;
using Krntija.Core.Survival;
using Krntija.Core.Vehicle;
using Krntija.Core.World;

namespace Krntija.Core;

/// <summary>
/// Одна игровая сессия: машина, игрок, инвентарь, часы, ГПСЧ.
/// Это тот самый агрегат, который в коопе станет авторитетом на хосте.
/// </summary>
public sealed class GameSession
{
    public GameSession(
        VehicleState vehicle,
        SurvivalState survival,
        Inventory inventory,
        GameClock clock,
        Pcg32 rng,
        Wallet? wallet = null,
        Inventory? trunk = null,
        Inventory? home = null)
    {
        Vehicle = vehicle;
        Systems = new VehicleSystems(vehicle);
        DriveTrain = new DriveTrain(vehicle.Spec);
        Survival = survival;
        Inventory = inventory;
        Clock = clock;
        Rng = rng;
        Wallet = wallet ?? new Wallet();
        Trunk = trunk ?? new Inventory(slots: 12, maxWeightKg: 60);
        Home = home ?? new Inventory(slots: 24, maxWeightKg: 200);
    }

    public VehicleState Vehicle { get; }
    public VehicleSystems Systems { get; }
    public DriveTrain DriveTrain { get; }
    public SurvivalState Survival { get; }
    public Inventory Inventory { get; }

    /// <summary>Багажник: второе хранилище, в него кладут тяжёлое.</summary>
    public Inventory Trunk { get; }

    /// <summary>Склад в доме игрока: сюда можно сгрузить всё лишнее.</summary>
    public Inventory Home { get; }

    /// <summary>Установленные улучшения машины и дома.</summary>
    public HashSet<string> Upgrades { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Рекорд дальности от дома, метры. Часть прогресса, а не счётчик сессии.</summary>
    public double BestDistanceMeters { get; private set; }

    /// <summary>Станция вдоль дороги: расстояние от дома по осевой линии, метры.</summary>
    public double RoadS { get; private set; }

    /// <summary>Тир мира на текущей станции: от километража по дороге, а не от одометра.</summary>
    public int Tier => WorldGen.WorldGenerator.Tier(RoadS);

    /// <summary>Сцена сообщает станцию каждый кадр; тир и рекорд считаются здесь.</summary>
    public void StepRoad(double s)
    {
        RoadS = Math.Max(0, s);
        if (RoadS > BestDistanceMeters) BestDistanceMeters = RoadS;
    }

    public GameClock Clock { get; }
    public Pcg32 Rng { get; }
    public Wallet Wallet { get; }
    public WeatherState Weather { get; } = new();

    /// <summary>Изменения мира: лут, места, шлагбаумы, выезды. Сохраняются.</summary>
    public WorldState WorldState { get; } = new();

    public static GameSession NewGame(ulong seed = 20261023, CarSpec? car = null)
    {
        var inventory = new Inventory();
        inventory.TryAdd(ItemIds.Wrench);
        inventory.TryAdd(ItemIds.DuctTape, 2);
        inventory.TryAdd("spark_plug", 2);
        inventory.TryAdd(ItemIds.Bread, 2);
        inventory.TryAdd(ItemIds.Water, 2);
        inventory.TryAdd(ItemIds.JerryCan, 1, liters: 15);

        return new GameSession(
            VehicleState.CreateDefault(car),
            new SurvivalState(),
            inventory,
            new GameClock(),
            new Pcg32(seed),
            new Wallet(120));
    }

    /// <summary>
    /// Поставить улучшение. Возвращает текст для игрока или null, если нельзя.
    /// </summary>
    public string? ApplyUpgrade(string id)
    {
        if (!UpgradeCatalog.TryGet(id, out var def)) return null;
        if (Upgrades.Contains(id)) return $"{def.Name}: уже установлено";

        switch (id)
        {
            case UpgradeCatalog.FuelTank:
                Vehicle.Fuel.RaiseCapacity(6);
                break;
            case UpgradeCatalog.Springs:
                Trunk.RaiseCapacity(15);
                break;
            case UpgradeCatalog.Toolbox:
                Inventory.RaiseCapacity(10);
                break;
            case UpgradeCatalog.RoofRack:
                Trunk.RaiseCapacity(25);
                break;
            case UpgradeCatalog.Engine:
                Vehicle.EnginePowerFactor *= UpgradeEffects.EnginePower;
                break;
            case UpgradeCatalog.Bullbar:
                Vehicle.CrashDamageFactor *= UpgradeEffects.CrashDamage;
                break;
            case UpgradeCatalog.Headlights:
                // Дальность света читает сцена; число живёт в ядре.
                break;
        }

        Upgrades.Add(id);
        return $"{def.Name}: установлено ({def.Description})";
    }

    /// <summary>Вспомнить уже установленное улучшение при загрузке: эффекты в сейве.</summary>
    public void RememberUpgrade(string id) => Upgrades.Add(id);

    /// <summary>
    /// После загрузки сейва вернуть эффекты: в сейве лежат только id улучшений,
    /// а прибавки к баку, багажнику, мощности и броне надо применить снова.
    /// </summary>
    public void ReapplyUpgrades()
    {
        var installed = Upgrades.ToList();

        foreach (string id in installed)
        {
            switch (id)
            {
                // Бак приходит из сейва вместе с вместимостью: второй раз не прибавляем.
                case UpgradeCatalog.Springs:
                    Trunk.RaiseCapacity(15);
                    break;
                case UpgradeCatalog.Toolbox:
                    Inventory.RaiseCapacity(10);
                    break;
                case UpgradeCatalog.RoofRack:
                    Trunk.RaiseCapacity(25);
                    break;
                case UpgradeCatalog.Engine:
                    Vehicle.EnginePowerFactor *= UpgradeEffects.EnginePower;
                    break;
                case UpgradeCatalog.Bullbar:
                    Vehicle.CrashDamageFactor *= UpgradeEffects.CrashDamage;
                    break;
            }
        }
    }

    public void Step(double dt, in VehicleSample sample, in DriveInput input, bool reversing = false)
    {
        if (dt <= 0) return;

        Clock.Advance(dt);

        // Дорога и погода задают множители, закон расхода остаётся законом машины.
        Vehicle.FuelMultiplier = Journey.FuelFactor(Tier, Weather.RainIntensity);
        Vehicle.WearMultiplier = Journey.WearFactor(Tier, Weather.RainIntensity);

        Systems.Step(dt, sample, input);
        DriveTrain.Update(sample.SpeedMps, input.Throttle, dt, reversing);
        Survival.Step(dt * Clock.TimeScale / 3600.0, sample.AmbientC);
        Weather.Update(dt * Clock.TimeScale / 60.0, Rng);
    }

    // ---------- сохранение ----------

    public SaveData ToSave() => new()
    {
        Version = SaveData.CurrentVersion,
        SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        SpecId = Vehicle.Spec.Id,
        Money = Wallet.Money,
        ClockMinutes = Clock.TotalMinutes,
        FuelLiters = Vehicle.Fuel.Liters,
        FuelCapacityLiters = Vehicle.Fuel.CapacityLiters,
        BatteryCharge = Vehicle.BatteryCharge,
        EngineTempC = Vehicle.EngineTempC,
        EngineRunning = Vehicle.EngineRunning,
        OdometerKm = Vehicle.OdometerKm,
        OilLevel = Vehicle.OilLevel,
        Parts = Vehicle.Parts.Values
            .Select(p => new PartSave { Slot = p.Slot.ToString(), DefId = p.Definition.Id, Condition = p.Condition })
            .ToList(),
        Health = Survival.Health,
        Hunger = Survival.Hunger,
        Thirst = Survival.Thirst,
        Fatigue = Survival.Fatigue,
        Items = Inventory.Items
            .Select(i => new ItemSave { DefId = i.DefId, Count = i.Count, Condition = i.Condition, Liters = i.Liters })
            .ToList(),
        TrunkItems = Trunk.Items
            .Select(i => new ItemSave { DefId = i.DefId, Count = i.Count, Condition = i.Condition, Liters = i.Liters })
            .ToList(),
        UpgradeIds = Upgrades.ToList(),
        BestDistanceMeters = BestDistanceMeters,
        HomeItems = Home.Items
            .Select(i => new ItemSave { DefId = i.DefId, Count = i.Count, Condition = i.Condition, Liters = i.Liters })
            .ToList(),
        RngState = Rng.State,
        WeatherKind = Weather.Kind.ToString(),
        Wetness = Weather.Wetness,
        RainIntensity = Weather.RainIntensity,
        WeatherTimer = Weather.MinutesUntilChange,
        WorldDone = WorldState.Done.OrderBy(id => id, StringComparer.Ordinal).ToList(),
    };

    public static GameSession FromSave(SaveData data)
    {
        var vehicle = new VehicleState
        {
            Spec = CarCatalog.Get(data.SpecId),
            Fuel = new FuelTank(data.FuelCapacityLiters <= 0 ? 36 : data.FuelCapacityLiters, data.FuelLiters),
            BatteryCharge = data.BatteryCharge,
            EngineRunning = data.EngineRunning,
            EngineTempC = data.EngineTempC,
            OdometerKm = data.OdometerKm,
            OilLevel = data.OilLevel,
        };

        foreach (var part in data.Parts)
        {
            if (!Enum.TryParse<PartSlot>(part.Slot, out var slot)) continue;
            var created = PartCatalog.Create(part.DefId, slot);
            created.SetCondition(part.Condition);
            vehicle.Install(created);
        }

        var survival = new SurvivalState
        {
            Health = data.Health,
            Hunger = data.Hunger,
            Thirst = data.Thirst,
            Fatigue = data.Fatigue,
        };

        var inventory = new Inventory();
        foreach (var item in data.Items)
            inventory.TryAdd(item.DefId, item.Count, item.Condition, item.Liters);

        var clock = new GameClock();
        clock.SetTotalMinutes(data.ClockMinutes);

        var rng = new Pcg32(1);
        rng.State = data.RngState;

        var trunk = new Inventory(slots: 12, maxWeightKg: 60);
        foreach (var item in data.TrunkItems)
            trunk.TryAdd(item.DefId, item.Count, item.Condition, item.Liters);

        var home = new Inventory(slots: 24, maxWeightKg: 200);
        foreach (var item in data.HomeItems)
            home.TryAdd(item.DefId, item.Count, item.Condition, item.Liters);

        var session = new GameSession(vehicle, survival, inventory, clock, rng, new Wallet(data.Money), trunk, home);
        foreach (var upgrade in data.UpgradeIds) session.RememberUpgrade(upgrade);
        session.ReapplyUpgrades();
        session.BestDistanceMeters = data.BestDistanceMeters;

        if (Enum.TryParse<WeatherKind>(data.WeatherKind, out var kind))
            session.Weather.Restore(kind, data.Wetness, data.RainIntensity, data.WeatherTimer);

        session.WorldState.Restore(data.WorldDone);
        return session;
    }
}

public sealed class SaveData
{
    /// <summary>
    /// 2 — добавлен WorldDone. 3 — мир (World, WorldSeed), позиция машины в
    /// координатах мира, а не сцены (ADR 0002). Старые сейвы читаются, их
    /// согласует с текущим миром <see cref="SaveWorld.Prepare"/>.
    /// </summary>
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;
    public long SavedAtUnix { get; set; }

    /// <summary>В каком мире сделан сейв: <see cref="SaveWorld.Chunks"/> или <see cref="SaveWorld.Legacy"/>.</summary>
    public string World { get; set; } = SaveWorld.Legacy;

    public ulong WorldSeed { get; set; }
    public string SpecId { get; set; } = CarCatalog.Zastava101.Id;
    public double Money { get; set; } = 120;
    public bool ObjectiveDone { get; set; }
    public double CarX { get; set; }
    public double CarY { get; set; }
    public double CarZ { get; set; }
    public double CarYaw { get; set; }
    public string WeatherKind { get; set; } = "Clear";
    public double Wetness { get; set; }
    public double RainIntensity { get; set; }
    public double WeatherTimer { get; set; } = 5;
    public double ClockMinutes { get; set; }
    public double FuelLiters { get; set; }
    public double FuelCapacityLiters { get; set; } = 36;
    public double BatteryCharge { get; set; }
    public double EngineTempC { get; set; }
    public bool EngineRunning { get; set; }
    public double OdometerKm { get; set; }
    public double OilLevel { get; set; } = 1;
    public List<PartSave> Parts { get; set; } = new();
    public double Health { get; set; } = 1;
    public double Hunger { get; set; } = 0.85;
    public double Thirst { get; set; } = 0.8;
    public double Fatigue { get; set; } = 0.9;
    public List<ItemSave> Items { get; set; } = new();
    public List<ItemSave> TrunkItems { get; set; } = new();
    public List<ItemSave> HomeItems { get; set; } = new();
    public List<string> UpgradeIds { get; set; } = new();

    /// <summary>Рекорд дальности по дороге, метры.</summary>
    public double BestDistanceMeters { get; set; }

    public ulong RngState { get; set; }
    public List<string> WorldDone { get; set; } = new();
}

public sealed class PartSave
{
    public string Slot { get; set; } = "";
    public string DefId { get; set; } = "";
    public double Condition { get; set; } = 1;
}

public sealed class ItemSave
{
    public string DefId { get; set; } = "";
    public int Count { get; set; } = 1;
    public double Condition { get; set; } = 1;
    public double Liters { get; set; }
}

public static class SaveSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string ToJson(SaveData data) => JsonSerializer.Serialize(data, Options);

    public static SaveData FromJson(string json) =>
        JsonSerializer.Deserialize<SaveData>(json, Options)
        ?? throw new InvalidDataException("Пустой или битый сейв");

    public static void SaveFile(string path, SaveData data)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, ToJson(data));
    }

    public static SaveData LoadFile(string path) => FromJson(File.ReadAllText(path));
}
