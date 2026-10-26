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
    public void NewGame_StartsInTheMorning()
    {
        var session = GameSession.NewGame();

        Assert.Equal(8, session.Clock.Hour);
        Assert.Equal(0, session.Clock.Minute);
        Assert.Equal("День 1, 08:00", session.Clock.ToString());
        Assert.False(session.Clock.IsNight, "утром не темно");
    }

    [Fact]
    public void Clock_PrintsHoursAndMinutes()
    {
        var clock = new GameClock();

        clock.SetTotalMinutes(18 * 60 + 45);
        Assert.Equal("День 1, 18:45", clock.ToString());

        clock.SetTotalMinutes(0);
        Assert.Equal("День 1, 00:00", clock.ToString());

        clock.SetTotalMinutes(25 * 60 + 5);
        Assert.Equal("День 2, 01:05", clock.ToString());

        // Полминуты не превращаются в «:00» — показываем целые минуты.
        clock.SetTotalMinutes(12 * 60 + 59.7);
        Assert.Equal("День 1, 12:59", clock.ToString());
    }

    [Fact]
    public void Clock_RunsAtConfiguredScale()
    {
        var session = GameSession.NewGame();

        // 60 реальных секунд при TimeScale = 60 → 1 игровой час от старта в 08:00.
        session.Step(60, new VehicleSample(10), new DriveInput());

        Assert.InRange(session.Clock.HourOfDay, 8.9, 9.1);
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
        Assert.Empty(data.LooseParts);

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

    [Fact]
    public void SaveV4_TakenPartLiesInTheWorldAfterLoad()
    {
        // «Снял → положил → сохранил → загрузил → всё на месте» (ADR 0003, п. 3–4).
        var session = GameSession.NewGame();
        var car = session.Vehicle;

        AssemblyRules.SetOpen(car, PartSlot.Hood, true);
        var hood = AssemblyRules.Remove(car, PartSlot.Hood);
        Assert.NotNull(hood);
        hood!.SetCondition(0.33);

        var loose = session.LooseParts.Add(
            hood.Definition.Id, hood.Condition, 1.5, 0.2, 2.5, 0.3, LooseParts.Garage);
        Assert.True(AssemblyRules.SetOpen(car, PartSlot.DoorFrontLeft, true));

        var data = SaveSerializer.FromJson(SaveSerializer.ToJson(session.ToSave()));

        Assert.Equal(4, data.Version);
        Assert.False(data.Parts.Any(p => p.Slot == nameof(PartSlot.Hood)), "снятого капота в сейве нет");
        Assert.Contains(nameof(PartSlot.DoorFrontLeft), data.OpenSlots);
        Assert.Single(data.LooseParts);

        var loaded = GameSession.FromSave(data);
        Assert.False(loaded.Vehicle.Parts.ContainsKey(PartSlot.Hood), "капот остался снятым");
        Assert.True(AssemblyRules.IsOpen(loaded.Vehicle, PartSlot.DoorFrontLeft), "дверь осталась открытой");

        var found = loaded.LooseParts.Find(loose.Id);
        Assert.NotNull(found);
        Assert.Equal(loose.Id, found!.Id);
        Assert.Equal("hood_101", found.DefId);
        Assert.Equal(0.33, found.Condition, 3);
        Assert.Equal(1.5, found.X, 3);
        Assert.Equal(0.2, found.Y, 3);
        Assert.Equal(2.5, found.Z, 3);
        Assert.Equal(0.3, found.Yaw, 3);

        // И обратно на машину — с тем же износом.
        Assert.Equal(AssemblyResult.Ok, AssemblyRules.Install(loaded.Vehicle, PartSlot.Hood, found.ToPart()!));
        Assert.True(loaded.LooseParts.Remove(found.Id));
        Assert.Equal(0.33, loaded.Vehicle.Parts[PartSlot.Hood].Condition, 3);
    }

    [Fact]
    public void SaveV4Sample_GarageAssemblyInProgress()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Saves", "save_v4.json");
        var data = SaveSerializer.LoadFile(path);
        Assert.Equal(4, data.Version);

        var session = GameSession.FromSave(data);
        var car = session.Vehicle;

        // Снято на старте (ADR 0003, п. 5): двигатель, аккумулятор, капот, дверь.
        Assert.False(car.Parts.ContainsKey(PartSlot.Engine));
        Assert.False(car.Parts.ContainsKey(PartSlot.Battery));
        Assert.False(car.Parts.ContainsKey(PartSlot.Hood));
        Assert.False(car.Parts.ContainsKey(PartSlot.DoorFrontLeft));
        Assert.True(car.Parts.ContainsKey(PartSlot.Radiator), "радиатор на месте");

        Assert.Equal(4, session.LooseParts.Count);
        var engine = session.LooseParts.Find("part:garage:0");
        Assert.NotNull(engine);
        Assert.Equal("engine_1_6", engine!.DefId);
        Assert.Equal(0.78, engine.Condition, 3);
        Assert.True(AssemblyRules.IsOpen(car, PartSlot.DoorFrontRight), "открытая дверь из сейва");
        Assert.False(AssemblyRules.CanStart(car), "без двигателя и аккумулятора не завестись");

        // Собрать из деталей гаража: капот снят, доступ под капот есть.
        Assert.Equal(AssemblyResult.Ok, AssemblyRules.Install(car, PartSlot.Engine, engine.ToPart()!));
        var battery = session.LooseParts.Find("part:garage:1");
        Assert.NotNull(battery);
        Assert.Equal(AssemblyResult.Ok, AssemblyRules.Install(car, PartSlot.Battery, battery!.ToPart()!));

        Assert.True(AssemblyRules.CanStart(car), "с двигателем и аккумулятором заводится");
    }

    [Fact]
    public void SaveV4_SkipsUnknownPartsAndPanels()
    {
        var data = new SaveData();
        data.Parts.Add(new PartSave { Slot = nameof(PartSlot.Engine), DefId = "engine_1_6" });
        data.LooseParts.Add(new LoosePartSave { Id = "part:world:0", DefId = "нет_такой_детали", X = 1 });
        data.LooseParts.Add(new LoosePartSave { Id = "", DefId = "hood_101" });
        data.OpenSlots.Add("НетТакогоСлота");
        data.OpenSlots.Add(nameof(PartSlot.Hood));

        var session = GameSession.FromSave(data);

        Assert.Equal(0, session.LooseParts.Count);
        Assert.Empty(session.Vehicle.OpenSlots); // капот не установлен — и не открыт
        Assert.True(session.Vehicle.Parts.ContainsKey(PartSlot.Engine));
    }

    private static void Nearly(double expected, double actual, double tolerance = 1e-5) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"ожидали {expected}, получили {actual}");
}
