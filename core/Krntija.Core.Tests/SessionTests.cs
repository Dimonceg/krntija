using Krntija.Core.Parts;
using Krntija.Core.Survival;
using Krntija.Core.Vehicle;
using Krntija.Core.World;
using Xunit;

namespace Krntija.Core.Tests;

public class SessionTests
{
    [Fact]
    public void NewGame_HasCarDriverAndStarterKit()
    {
        var session = GameSession.NewGame();

        Assert.True(session.Vehicle.EngineRunning == false);
        Assert.Equal(1.0, session.Survival.Health);
        Assert.True(session.Inventory.Count("wrench") == 1);
        Assert.Equal(1, session.Clock.Day);
    }

    [Fact]
    public void Clock_RunsAtConfiguredScale()
    {
        var session = GameSession.NewGame();

        // 60 реальных секунд при TimeScale = 60 → 1 игровой час.
        session.Step(60, new VehicleSample(10), new DriveInput());

        Assert.InRange(session.Clock.HourOfDay, 0.9, 1.1);
    }

    [Fact]
    public void SaveRoundTrip_PreservesEverythingThatMatters()
    {
        var session = GameSession.NewGame(seed: 777);
        for (int i = 0; i < 300; i++)
            session.Step(1, new VehicleSample(15), new DriveInput(Ignition: i == 0, Throttle: 0.6));

        session.Vehicle.Get(PartSlot.Engine)!.SetCondition(0.42);
        session.Inventory.TryAdd("bread", 1);

        var json = SaveSerializer.ToJson(session.ToSave());
        var restored = GameSession.FromSave(SaveSerializer.FromJson(json));

        Nearly(session.Clock.TotalMinutes, restored.Clock.TotalMinutes);
        Nearly(session.Vehicle.Fuel.Liters, restored.Vehicle.Fuel.Liters);
        Nearly(session.Vehicle.EngineTempC, restored.Vehicle.EngineTempC);
        Nearly(session.Vehicle.OdometerKm, restored.Vehicle.OdometerKm);
        Nearly(0.42, restored.Vehicle.Get(PartSlot.Engine)!.Condition);
        Nearly(session.Survival.Hunger, restored.Survival.Hunger);
        Assert.Equal(session.Inventory.Count("bread"), restored.Inventory.Count("bread"));
        Assert.Equal(session.Rng.State, restored.Rng.State);
        Assert.Equal(session.Wallet.Money, restored.Wallet.Money, 6);
        Assert.Equal(session.Vehicle.Parts.Count, restored.Vehicle.Parts.Count);
    }

    [Fact]
    public void SaveFile_RoundTripsThroughDisk()
    {
        var session = GameSession.NewGame();
        var path = Path.Combine(AppContext.BaseDirectory, "test-saves", $"save-{Guid.NewGuid():N}.json");

        try
        {
            SaveSerializer.SaveFile(path, session.ToSave());
            Assert.True(File.Exists(path));

            var loaded = SaveSerializer.LoadFile(path);
            Assert.Equal(session.Vehicle.Fuel.Liters, loaded.FuelLiters, 6);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new Pcg32(12345);
        var b = new Pcg32(12345);
        var c = new Pcg32(54321);

        var seqA = Enumerable.Range(0, 32).Select(_ => a.NextUInt()).ToArray();
        var seqB = Enumerable.Range(0, 32).Select(_ => b.NextUInt()).ToArray();
        var seqC = Enumerable.Range(0, 32).Select(_ => c.NextUInt()).ToArray();

        Assert.Equal(seqA, seqB);
        Assert.NotEqual(seqA, seqC);
    }

    [Fact]
    public void HungerAndThirst_FallOverTime_AndHealthFollows()
    {
        var survival = new SurvivalState { Thirst = 0.01 };

        survival.Step(gameHours: 10, ambientC: 20);

        Assert.True(survival.Thirst <= 0.001);
        Assert.True(survival.Health < 1.0, "без воды здоровье должно падать");
    }

    [Fact]
    public void SaveV3_IsReadAsWholeCarWithPanels()
    {
        // ADR 0003, п. 4: в v3 отсутствующий слот значил «слот не сохраняется»,
        // поэтому старый сейв читается как полностью собранная машина.
        var path = Path.Combine(AppContext.BaseDirectory, "Saves", "save_v3.json");
        var data = SaveSerializer.LoadFile(path);

        Assert.Equal(3, data.Version);
        Assert.DoesNotContain(data.Parts, p => p.Slot == nameof(PartSlot.Hood));

        var car = GameSession.FromSave(data).Vehicle;

        foreach (var socket in CarLayout.Zastava101.Slots)
            Assert.True(car.Parts.ContainsKey(socket.Slot), $"гнездо {socket.Slot} должно быть занято после загрузки v3");
        Assert.Equal(PartCatalog.StandardBuild.Select(p => p.Slot)
            .Concat(CarLayout.Zastava101.Slots.Select(s => s.Slot)).Distinct().Count(), car.Parts.Count);
        Assert.Equal(0.86, car.Get(PartSlot.Engine)!.Condition, 3);
    }

    [Fact]
    public void SaveV4_KeepsRemovedPanelsRemoved()
    {
        // С v4 «нет слота в Parts» — это «детали нет», и доставлять её нельзя.
        var data = new SaveData { Version = 4, SpecId = CarCatalog.Zastava101.Id };
        data.Parts.Add(new PartSave { Slot = nameof(PartSlot.Engine), DefId = "engine_1_6", Condition = 0.5 });

        var car = GameSession.FromSave(data).Vehicle;

        Assert.True(car.Parts.ContainsKey(PartSlot.Engine));
        Assert.False(car.Parts.ContainsKey(PartSlot.Hood), "снятый капот в v4 остаётся снятым");
        Assert.False(car.Parts.ContainsKey(PartSlot.DoorFrontLeft));
    }

    private static void Nearly(double expected, double actual, double tolerance = 1e-5) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"ожидали {expected}, получили {actual}");
}
