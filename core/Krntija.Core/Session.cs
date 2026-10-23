using System.Text.Json;
using System.Text.Json.Serialization;
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
        Pcg32 rng)
    {
        Vehicle = vehicle;
        Systems = new VehicleSystems(vehicle);
        Survival = survival;
        Inventory = inventory;
        Clock = clock;
        Rng = rng;
    }

    public VehicleState Vehicle { get; }
    public VehicleSystems Systems { get; }
    public SurvivalState Survival { get; }
    public Inventory Inventory { get; }
    public GameClock Clock { get; }
    public Pcg32 Rng { get; }

    public static GameSession NewGame(ulong seed = 20261023)
    {
        var inventory = new Inventory();
        inventory.TryAdd("wrench");
        inventory.TryAdd("duct_tape", 2);
        inventory.TryAdd("spark_plug", 2);
        inventory.TryAdd("bread", 2);
        inventory.TryAdd("water", 2);
        inventory.TryAdd("jerry_can");

        return new GameSession(
            VehicleState.CreateDefault(),
            new SurvivalState(),
            inventory,
            new GameClock(),
            new Pcg32(seed));
    }

    public void Step(double dt, in VehicleSample sample, in DriveInput input)
    {
        if (dt <= 0) return;

        Clock.Advance(dt);
        Systems.Step(dt, sample, input);
        Survival.Step(dt * Clock.TimeScale / 3600.0, sample.AmbientC);
    }

    // ---------- сохранение ----------

    public SaveData ToSave() => new()
    {
        Version = SaveData.CurrentVersion,
        SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
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
            Fuel = new FuelTank(data.FuelCapacityLiters <= 0 ? 45 : data.FuelCapacityLiters, data.FuelLiters),
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

        return new GameSession(vehicle, survival, inventory, clock, rng);
    }
}

public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public long SavedAtUnix { get; set; }
    public double ClockMinutes { get; set; }
    public double FuelLiters { get; set; }
    public double FuelCapacityLiters { get; set; } = 45;
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
