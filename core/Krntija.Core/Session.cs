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
        Wallet? wallet = null)
    {
        Vehicle = vehicle;
        Systems = new VehicleSystems(vehicle);
        DriveTrain = new DriveTrain(vehicle.Spec);
        Survival = survival;
        Inventory = inventory;
        Clock = clock;
        Rng = rng;
        Wallet = wallet ?? new Wallet();
    }

    public VehicleState Vehicle { get; }
    public VehicleSystems Systems { get; }
    public DriveTrain DriveTrain { get; }
    public SurvivalState Survival { get; }
    public Inventory Inventory { get; }
    public GameClock Clock { get; }
    public Pcg32 Rng { get; }
    public Wallet Wallet { get; }

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

    public void Step(double dt, in VehicleSample sample, in DriveInput input, bool reversing = false)
    {
        if (dt <= 0) return;

        Clock.Advance(dt);
        Systems.Step(dt, sample, input);
        DriveTrain.Update(sample.SpeedMps, input.Throttle, dt, reversing);
        Survival.Step(dt * Clock.TimeScale / 3600.0, sample.AmbientC);
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
        RngState = Rng.State,
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

        return new GameSession(vehicle, survival, inventory, clock, rng, new Wallet(data.Money));
    }
}

public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public long SavedAtUnix { get; set; }
    public string SpecId { get; set; } = CarCatalog.Zastava101.Id;
    public double Money { get; set; } = 120;
    public bool ObjectiveDone { get; set; }
    public double CarX { get; set; }
    public double CarY { get; set; }
    public double CarZ { get; set; }
    public double CarYaw { get; set; }
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
    public ulong RngState { get; set; }
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
