using Krntija.Core.Parts;
using Krntija.Core.Vehicle;
using Xunit;

namespace Krntija.Core.Tests;

/// <summary>Старт в гараже: что снято и что лежит рядом (ADR 0003, п. 5).</summary>
public class StartScenarioTests
{
    private static GameSession Zastava() => GameSession.NewGame(car: CarCatalog.Zastava101);

    [Fact]
    public void PlanListsTheAdvertisedSlots()
    {
        var plan = StartScenario.For(CarCatalog.Zastava101.Id);

        Assert.NotNull(plan);
        Assert.Equal(new[]
        {
            PartSlot.Engine, PartSlot.Battery, PartSlot.Radiator,
            PartSlot.Hood, PartSlot.TrunkLid, PartSlot.DoorFrontLeft,
        }, plan!.EmptySlots);
        Assert.Equal(plan.EmptySlots.Count, plan.LooseParts.Count);
        Assert.False(plan.IsEmpty);
    }

    [Fact]
    public void EveryPlacementFitsItsSocket()
    {
        var session = Zastava();

        foreach (var placement in StartScenario.ZastavaGarage.LooseParts)
        {
            Assert.True(PartCatalog.All.TryGetValue(placement.DefId, out var def),
                $"деталь {placement.DefId} должна быть в каталоге");
            Assert.Equal(placement.Slot, def!.Slot);
        }

        // И каждая снятая деталь действительно встаёт в своё гнездо по правилам.
        StartScenario.Apply(session, 0, 0, 0, 0);
        foreach (var loose in session.LooseParts.Items)
        {
            var result = AssemblyRules.CanInstall(session.Vehicle, loose.Definition!.Slot, loose.Definition);
            Assert.Equal(AssemblyResult.Ok, result);
        }
    }

    [Fact]
    public void ApplyTakesPartsOffAndPutsThemAroundTheCar()
    {
        var session = Zastava();
        var car = session.Vehicle;

        var plan = StartScenario.Apply(session, 10, 0.5, -20, 0);

        Assert.NotNull(plan);
        Assert.False(car.Parts.ContainsKey(PartSlot.Engine));
        Assert.False(car.Parts.ContainsKey(PartSlot.Battery));
        Assert.False(car.Parts.ContainsKey(PartSlot.Radiator));
        Assert.False(car.Parts.ContainsKey(PartSlot.Hood));
        Assert.False(car.Parts.ContainsKey(PartSlot.TrunkLid));
        Assert.False(car.Parts.ContainsKey(PartSlot.DoorFrontLeft));

        // Колёса и остальное на месте — машину можно толкать и домкратить.
        foreach (var wheel in PartCatalog.Wheels)
            Assert.True(car.Parts.ContainsKey(wheel), $"колесо {wheel} должно остаться");
        Assert.True(car.Parts.ContainsKey(PartSlot.Gearbox));

        Assert.Equal(6, session.LooseParts.Count);
        var hood = session.LooseParts.Find("part:garage:3");
        Assert.NotNull(hood);
        Assert.Equal("hood_101", hood!.DefId);
        Assert.Equal(1.0, hood.Condition, 3);
        Assert.True(hood.X < 10, "капот лежит слева от машины");
        Assert.Equal(0.5 + 0.15, hood.Y, 3);
    }

    [Fact]
    public void ApplyRotatesOffsetsWithTheCar()
    {
        var session = Zastava();
        StartScenario.Apply(session, 0, 0, 0, Math.PI / 2);

        // При повороте на 90° смещение в осях машины (−1,8; −2,3) → в мире (−2,3; +1,8).
        var battery = session.LooseParts.Find("part:garage:1");
        Assert.NotNull(battery);
        Assert.Equal(-2.3, battery!.X, 6);
        Assert.Equal(1.8, battery.Z, 6);
    }

    [Fact]
    public void LoosePartIdsAreTheGarageOnes()
    {
        var session = Zastava();
        StartScenario.Apply(session, 0, 0, 0, 0);

        for (int i = 0; i < 6; i++)
        {
            var part = session.LooseParts.Find($"part:garage:{i}");
            Assert.NotNull(part);
            Assert.Equal(StartScenario.ZastavaGarage.LooseParts[i].DefId, part!.DefId);
        }
    }

    [Fact]
    public void CarCanBeAssembledFromTheGarage()
    {
        var session = Zastava();
        var car = session.Vehicle;
        Assert.False(StartScenario.NeedsAssembly(car), "новая сессия ещё полная: старт применяет сцена по якорю");

        StartScenario.Apply(session, 0, 0, 0, 0);

        Assert.True(StartScenario.NeedsAssembly(car));
        Assert.Equal(6, StartScenario.MissingCount(car));

        // Детали ставятся по одной — как игрок руками: капот снят, доступ под капот есть.
        foreach (var loose in session.LooseParts.Items.ToList())
        {
            var part = loose.ToPart();
            Assert.NotNull(part);
            var slot = part!.Slot;
            Assert.Equal(AssemblyResult.Ok, AssemblyRules.Install(car, slot, part));
            Assert.True(session.LooseParts.Remove(loose.Id));
        }

        Assert.False(StartScenario.NeedsAssembly(car));
        Assert.Equal(0, StartScenario.MissingCount(car));
        Assert.True(AssemblyRules.CanStart(car), "собранная машина заводится");
    }

    [Fact]
    public void CarWithoutLayoutStartsAsBefore()
    {
        var session = GameSession.NewGame(car: CarCatalog.Golf2);

        Assert.Null(StartScenario.For(session.Vehicle.Spec.Id));
        Assert.Null(StartScenario.Apply(session, 0, 0, 0, 0));
        Assert.Equal(0, session.LooseParts.Count);
        Assert.False(StartScenario.NeedsAssembly(session.Vehicle));
        Assert.True(session.Vehicle.Parts.ContainsKey(PartSlot.Engine));
    }

    [Fact]
    public void FullCarNeedsNoAssembly()
    {
        var car = VehicleState.CreateDefault();

        Assert.False(StartScenario.NeedsAssembly(car));
        Assert.Null(StartScenario.For("golf_2"));

        AssemblyRules.Remove(car, PartSlot.DoorRearRight);
        Assert.True(StartScenario.NeedsAssembly(car));
        Assert.Equal(1, StartScenario.MissingCount(car));
    }
}
