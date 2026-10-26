using Krntija.Core.Parts;
using Krntija.Core.Vehicle;
using Xunit;

namespace Krntija.Core.Tests;

/// <summary>Правила сборки и разборки машины руками (ADR 0003, п. 1–2).</summary>
public class AssemblyTests
{
    private static VehicleState Zastava() => VehicleState.CreateDefault(CarCatalog.Zastava101);

    private static VehicleState Golf() => VehicleState.CreateDefault(CarCatalog.Golf2);

    private static PartDefinition Def(string id) => PartCatalog.All[id];

    /// <summary>Открыть капот: без этого моторный отсек закрыт (ADR 0003, п. 1).</summary>
    private static void OpenHood(VehicleState car) =>
        Assert.True(AssemblyRules.SetOpen(car, PartSlot.Hood, true), "капот должен открываться");

    [Fact]
    public void SlotIds_RoundTrip()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var slot in Enum.GetValues<PartSlot>())
        {
            string id = PartSlotIds.Of(slot);
            Assert.False(string.IsNullOrWhiteSpace(id), $"у слота {slot} нет id");
            Assert.True(seen.Add(id), $"id «{id}» повторяется");

            Assert.True(PartSlotIds.TryParse(id, out var back), $"id «{id}» не читается обратно");
            Assert.Equal(slot, back);
        }
    }

    [Fact]
    public void SlotIds_MatchTheAdrTable()
    {
        Assert.Equal("engine", PartSlotIds.Of(PartSlot.Engine));
        Assert.Equal("wheel_rr", PartSlotIds.Of(PartSlot.WheelRearRight));
        Assert.Equal("door_fl", PartSlotIds.Of(PartSlot.DoorFrontLeft));
        Assert.Equal("hood", PartSlotIds.Of(PartSlot.Hood));
        Assert.Equal("trunk", PartSlotIds.Of(PartSlot.TrunkLid));

        Assert.True(PartSlotIds.TryParse("hood", out var hood));
        Assert.Equal(PartSlot.Hood, hood);
        Assert.False(PartSlotIds.TryParse("было_бы_смешно", out _));
    }

    [Fact]
    public void PanelParts_BelongToTheirCar()
    {
        PartSlot[] panels =
        {
            PartSlot.Hood, PartSlot.TrunkLid,
            PartSlot.DoorFrontLeft, PartSlot.DoorFrontRight,
            PartSlot.DoorRearLeft, PartSlot.DoorRearRight,
        };

        foreach (var slot in panels)
        {
            var def = PartCatalog.PanelFor("zastava_101", slot);
            Assert.NotNull(def);
            Assert.Equal(slot, def!.Slot);
            Assert.Equal("zastava_101", def.SpecId);
            Assert.True(def.MassKg > 0, $"у {def.Id} должна быть масса");
            Assert.True(def.Price > 0, $"у {def.Id} должна быть цена");
        }

        Assert.Null(PartCatalog.PanelFor("golf_2", PartSlot.Hood));
    }

    [Fact]
    public void DefaultCar_HasEverySocketFilled()
    {
        var car = Zastava();

        foreach (var spec in CarLayout.Zastava101.Slots)
            Assert.True(car.Parts.ContainsKey(spec.Slot), $"гнездо {spec.Slot} пустое");
    }

    [Fact]
    public void EmptySocket_AcceptsMatchingPart()
    {
        var car = Zastava();
        Assert.NotNull(AssemblyRules.Remove(car, PartSlot.DoorRearRight));

        Assert.Equal(AssemblyResult.Ok, AssemblyRules.CanInstall(car, PartSlot.DoorRearRight, Def("door_rr_101")));
    }

    [Fact]
    public void OccupiedSocket_IsOccupied()
    {
        var car = Zastava();

        Assert.Equal(AssemblyResult.Occupied, AssemblyRules.CanInstall(car, PartSlot.DoorFrontLeft, Def("door_fl_101")));
        Assert.Equal(AssemblyResult.Occupied, AssemblyRules.Install(car, new Part(Def("door_fl_101"))));
    }

    [Fact]
    public void ForeignPart_IsWrongPart()
    {
        var car = Zastava();
        Assert.NotNull(AssemblyRules.Remove(car, PartSlot.DoorRearRight));

        // Дверь передняя в гнездо задней двери не встаёт.
        Assert.Equal(AssemblyResult.WrongPart, AssemblyRules.CanInstall(car, PartSlot.DoorRearRight, Def("door_fl_101")));

        // Дверь чужой машины тоже.
        var foreign = new PartDefinition("door_rr_golf", PartSlot.DoorRearRight, "Дверь Golf", 18, 45, 0.02, "golf_2");
        Assert.Equal(AssemblyResult.WrongPart, AssemblyRules.CanInstall(car, PartSlot.DoorRearRight, foreign));
    }

    [Fact]
    public void ClosedHood_NeedsAccess()
    {
        var car = Zastava();

        Assert.False(AssemblyRules.HasEngineBayAccess(car), "капот установлен и закрыт");
        Assert.Equal(AssemblyResult.NeedsAccess, AssemblyRules.CanRemove(car, PartSlot.Engine));

        Assert.Null(AssemblyRules.Remove(car, PartSlot.Engine));
        Assert.True(car.Parts.ContainsKey(PartSlot.Engine), "закрытый капот ничего не снял");

        // Пустое гнездо под капотом: ставить нельзя, пока капот закрыт.
        OpenHood(car);
        Assert.NotNull(AssemblyRules.Remove(car, PartSlot.Engine));
        Assert.True(AssemblyRules.SetOpen(car, PartSlot.Hood, false));
        Assert.Equal(AssemblyResult.NeedsAccess, AssemblyRules.CanInstall(car, PartSlot.Engine, Def("engine_1_6")));

        // А если гнездо занято, подсказка — «занято», а не «открой капот».
        OpenHood(car);
        Assert.Equal(AssemblyResult.Ok, AssemblyRules.Install(car, PartSlot.Engine, new Part(Def("engine_1_6"))));
        Assert.True(AssemblyRules.SetOpen(car, PartSlot.Hood, false));
        Assert.Equal(AssemblyResult.Occupied, AssemblyRules.CanInstall(car, PartSlot.Engine, Def("engine_1_6")));
    }

    [Fact]
    public void OpenHood_GivesAccess()
    {
        var car = Zastava();
        OpenHood(car);

        Assert.True(AssemblyRules.HasEngineBayAccess(car));
        var engine = AssemblyRules.Remove(car, PartSlot.Engine);
        Assert.NotNull(engine);
        Assert.Equal(AssemblyResult.Ok, AssemblyRules.Install(car, PartSlot.Engine, engine!));
    }

    [Fact]
    public void RemovedHood_GivesAccess()
    {
        var car = Zastava();

        var hood = AssemblyRules.Remove(car, PartSlot.Hood);
        Assert.NotNull(hood);
        Assert.True(AssemblyRules.HasEngineBayAccess(car), "снятый капот — тоже доступ");

        Assert.NotNull(AssemblyRules.Remove(car, PartSlot.Engine));
        Assert.Equal(AssemblyResult.Ok, AssemblyRules.Install(car, PartSlot.Hood, hood!));
    }

    [Fact]
    public void EmptySocket_ReportsEmpty()
    {
        var car = Zastava();
        Assert.NotNull(AssemblyRules.Remove(car, PartSlot.TrunkLid));

        Assert.Equal(AssemblyResult.Empty, AssemblyRules.CanRemove(car, PartSlot.TrunkLid));
        Assert.Null(AssemblyRules.Remove(car, PartSlot.TrunkLid));
    }

    [Fact]
    public void NonPhysicalSlot_IsNotRemovable()
    {
        var car = Zastava();

        Assert.Equal(AssemblyResult.NotRemovable, AssemblyRules.CanRemove(car, PartSlot.Gearbox));
        Assert.Null(AssemblyRules.Remove(car, PartSlot.Gearbox));
        Assert.True(car.Parts.ContainsKey(PartSlot.Gearbox), "КПП остаётся на месте");
        Assert.False(AssemblyRules.HasSocket(car, PartSlot.Gearbox));
    }

    [Fact]
    public void RemovedPart_KeepsConditionAndGoesBack()
    {
        var car = Zastava();
        car.Parts[PartSlot.Engine].SetCondition(0.42);
        OpenHood(car);

        var engine = AssemblyRules.Remove(car, PartSlot.Engine);
        Assert.NotNull(engine);
        Assert.Equal(0.42, engine!.Condition, 3);
        Assert.False(car.Parts.ContainsKey(PartSlot.Engine), "снятой детали в машине нет");

        Assert.Equal(AssemblyResult.Ok, AssemblyRules.Install(car, PartSlot.Engine, engine));
        Assert.Equal(0.42, car.Parts[PartSlot.Engine].Condition, 3);
    }

    [Fact]
    public void WheelFitsAnyWheelSocket()
    {
        var car = Zastava();
        var wheel = AssemblyRules.Remove(car, PartSlot.WheelFrontLeft);
        Assert.NotNull(wheel);
        wheel!.Wear(0.3);
        Assert.NotNull(AssemblyRules.Remove(car, PartSlot.WheelRearLeft));

        Assert.Equal(AssemblyResult.Ok, AssemblyRules.Install(car, PartSlot.WheelRearLeft, wheel));
        Assert.False(car.Parts.ContainsKey(PartSlot.WheelFrontLeft));
        Assert.Equal(PartSlot.WheelRearLeft, car.Parts[PartSlot.WheelRearLeft].Slot);
        Assert.Equal(wheel.Condition, car.Parts[PartSlot.WheelRearLeft].Condition, 3);
    }

    [Fact]
    public void OnlyPanelsOpen()
    {
        var car = Zastava();

        Assert.False(AssemblyRules.SetOpen(car, PartSlot.Engine, true), "двигатель не панель");
        Assert.False(AssemblyRules.IsOpen(car, PartSlot.Engine));

        Assert.True(AssemblyRules.SetOpen(car, PartSlot.DoorFrontLeft, true));
        Assert.True(AssemblyRules.IsOpen(car, PartSlot.DoorFrontLeft));

        Assert.True(AssemblyRules.SetOpen(car, PartSlot.DoorFrontLeft, false));
        Assert.False(AssemblyRules.IsOpen(car, PartSlot.DoorFrontLeft));
    }

    [Fact]
    public void RemovedPanel_IsNotOpen()
    {
        var car = Zastava();
        AssemblyRules.SetOpen(car, PartSlot.TrunkLid, true);

        Assert.NotNull(AssemblyRules.Remove(car, PartSlot.TrunkLid));
        Assert.False(AssemblyRules.IsOpen(car, PartSlot.TrunkLid), "снятая панель не открыта");
        Assert.False(AssemblyRules.SetOpen(car, PartSlot.TrunkLid, true), "снятую панель не открыть");
    }

    [Fact]
    public void InstalledPanel_IsClosed()
    {
        var car = Zastava();
        var lid = AssemblyRules.Remove(car, PartSlot.TrunkLid);
        Assert.NotNull(lid);

        Assert.Equal(AssemblyResult.Ok, AssemblyRules.Install(car, PartSlot.TrunkLid, lid!));
        Assert.False(AssemblyRules.IsOpen(car, PartSlot.TrunkLid), "поставленная панель закрыта");
    }

    [Fact]
    public void WholeCar_Starts()
    {
        var car = Zastava();

        Assert.True(AssemblyRules.CanStart(car));
        var systems = new VehicleSystems(car);
        systems.Step(1, new VehicleSample(0), new DriveInput(Ignition: true));
        Assert.True(car.EngineRunning);
    }

    [Fact]
    public void NoEngine_DoesNotStart()
    {
        var car = Zastava();
        OpenHood(car);
        Assert.NotNull(AssemblyRules.Remove(car, PartSlot.Engine));

        Assert.False(AssemblyRules.CanStart(car));
        var systems = new VehicleSystems(car);
        systems.Step(1, new VehicleSample(0), new DriveInput(Ignition: true));
        Assert.False(car.EngineRunning, "без двигателя не заводится");
    }

    [Fact]
    public void NoBattery_DoesNotStart()
    {
        var car = Zastava();
        OpenHood(car);
        Assert.NotNull(AssemblyRules.Remove(car, PartSlot.Battery));

        Assert.False(AssemblyRules.CanStart(car));
        var systems = new VehicleSystems(car);
        systems.Step(1, new VehicleSample(0), new DriveInput(Ignition: true));
        Assert.False(car.EngineRunning, "без аккумулятора не заводится");
    }

    [Fact]
    public void NoRadiator_GivesOverheatFactorFour()
    {
        var car = Zastava();
        Assert.Equal(1, AssemblyRules.OverheatFactor(car), 3);

        OpenHood(car);
        Assert.NotNull(AssemblyRules.Remove(car, PartSlot.Radiator));

        Assert.Equal(4, AssemblyRules.OverheatFactor(car), 3);
    }

    [Fact]
    public void NoRadiator_EngineHeatsFaster()
    {
        var withRadiator = Zastava();
        var without = Zastava();
        OpenHood(without);
        Assert.NotNull(AssemblyRules.Remove(without, PartSlot.Radiator));

        Heat(withRadiator, seconds: 30);
        Heat(without, seconds: 30);

        Assert.True(without.EngineTempC > withRadiator.EngineTempC + 15,
            $"без радиатора {without.EngineTempC:F1} °C, с радиатором {withRadiator.EngineTempC:F1} °C");
    }

    [Fact]
    public void CarrySpeedDropsWithMass()
    {
        Assert.Equal(1.0, AssemblyRules.CarrySpeedFactor(0), 3);
        Assert.Equal(0.95, AssemblyRules.CarrySpeedFactor(10), 3);
        Assert.True(AssemblyRules.CarrySpeedFactor(140) < AssemblyRules.CarrySpeedFactor(18),
            "двигатель тяжелее двери — и нести его медленнее");

        Assert.Equal(AssemblyRules.MinCarrySpeedFactor, AssemblyRules.CarrySpeedFactor(500), 3);
        Assert.Equal(AssemblyRules.MinCarrySpeedFactor, AssemblyRules.CarrySpeedFactor(5000), 3);
    }

    [Fact]
    public void SnapPicksNearestFreeSocket()
    {
        var car = Zastava();
        Assert.NotNull(AssemblyRules.Remove(car, PartSlot.DoorFrontLeft));

        var target = AssemblyRules.SnapTarget(car, Def("door_fl_101"), new[]
        {
            (PartSlot.DoorFrontRight, 0.10), // занято
            (PartSlot.DoorRearLeft, 0.90),   // далеко
            (PartSlot.DoorFrontLeft, 0.40),  // свободно и близко
        });

        Assert.Equal(PartSlot.DoorFrontLeft, target);
    }

    [Fact]
    public void SnapIgnoresOccupiedAndFarSockets()
    {
        var car = Zastava();

        var target = AssemblyRules.SnapTarget(car, Def("door_fl_101"), new[]
        {
            (PartSlot.DoorFrontRight, 0.10),
            (PartSlot.DoorRearLeft, 0.90),
        });

        Assert.Null(target);
    }

    [Fact]
    public void SnapRespectsClosedHood()
    {
        var car = Zastava();
        OpenHood(car);
        Assert.NotNull(AssemblyRules.Remove(car, PartSlot.Engine));

        AssemblyRules.SetOpen(car, PartSlot.Hood, false);
        Assert.Null(AssemblyRules.SnapTarget(car, Def("engine_1_6"), new[] { (PartSlot.Engine, 0.2) }));

        OpenHood(car);
        Assert.Equal(PartSlot.Engine,
            AssemblyRules.SnapTarget(car, Def("engine_1_6"), new[] { (PartSlot.Engine, 0.2) }));
    }

    [Fact]
    public void AimedSlotPicksWhatThePlayerLooksAt()
    {
        var target = AssemblyRules.AimedSlot(new[]
        {
            (PartSlot.Hood, 22.0, 1.4),
            (PartSlot.DoorFrontLeft, 8.0, 1.1),
            (PartSlot.Engine, 3.0, 9.0),      // далеко: рукой не достать
            (PartSlot.TrunkLid, 60.0, 1.0),   // мимо взгляда
        });

        Assert.Equal(PartSlot.DoorFrontLeft, target);
    }

    [Fact]
    public void AimedSlotIsNullWhenNothingIsInReach()
    {
        Assert.Null(AssemblyRules.AimedSlot(Array.Empty<(PartSlot, double, double)>()));
        Assert.Null(AssemblyRules.AimedSlot(new[]
        {
            (PartSlot.Hood, 5.0, AssemblyRules.AimDistance + 0.5),
            (PartSlot.DoorFrontLeft, AssemblyRules.AimAngle + 5, 1.0),
        }));
    }

    [Fact]
    public void CarWithoutLayout_IsNotRemovable()
    {
        var car = Golf();
        Assert.Null(CarLayout.For(car.Spec.Id));

        Assert.Equal(AssemblyResult.NotRemovable, AssemblyRules.CanRemove(car, PartSlot.WheelFrontLeft));
        Assert.Equal(AssemblyResult.NotRemovable, AssemblyRules.CanInstall(car, PartSlot.WheelFrontLeft, Def("wheel_steel")));
        Assert.Null(AssemblyRules.Remove(car, PartSlot.WheelFrontLeft));
        Assert.False(AssemblyRules.SetOpen(car, PartSlot.DoorFrontLeft, true));
        Assert.False(AssemblyRules.HasSocket(car, PartSlot.Engine));
        Assert.Null(AssemblyRules.SnapTarget(car, Def("wheel_steel"), new[] { (PartSlot.WheelFrontLeft, 0.1) }));

        // «Работает как раньше»: машина заводится и едет, хотя гнёзд у неё нет.
        var systems = new VehicleSystems(car);
        systems.Step(1, new VehicleSample(0), new DriveInput(Ignition: true));
        Assert.True(car.EngineRunning, "машина без раскладки должна заводиться как раньше");
        Assert.True(systems.PowerFactor > 0.5);
    }

    private static void Heat(VehicleState car, double seconds)
    {
        var systems = new VehicleSystems(car);
        for (double t = 0; t < seconds; t += 1)
            systems.Step(1, new VehicleSample(0), new DriveInput(Ignition: true, Throttle: 1));
    }
}
