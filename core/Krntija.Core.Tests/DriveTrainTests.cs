using Krntija.Core.Vehicle;
using Xunit;

namespace Krntija.Core.Tests;

public class DriveTrainTests
{
    private static DriveTrain Settled(CarSpec spec, double speedMps, double seconds = 3)
    {
        var box = new DriveTrain(spec);
        for (double t = 0; t < seconds; t += 1.0 / 60)
            box.Update(speedMps, throttle: 0.8, dt: 1.0 / 60);
        return box;
    }

    [Fact]
    public void AtStandstill_EngineIdlesInFirstGear()
    {
        var box = Settled(CarCatalog.Zastava101, speedMps: 0);

        Assert.Equal(1, box.Gear);
        Assert.InRange(box.Rpm, 790, 900);
    }

    [Fact]
    public void Rpm_GrowsWithSpeed()
    {
        var slow = Settled(CarCatalog.Zastava101, speedMps: 4);
        var fast = Settled(CarCatalog.Zastava101, speedMps: 9);

        Assert.True(fast.Rpm > slow.Rpm, $"{fast.Rpm:F0} должно быть больше {slow.Rpm:F0}");
    }

    [Fact]
    public void TopSpeed_ReachesTopGear()
    {
        var spec = CarCatalog.Zastava101;
        var box = Settled(spec, speedMps: spec.TopSpeedMps, seconds: 5);

        Assert.Equal(spec.GearRatios.Count, box.Gear);
        Assert.InRange(box.Rpm, 800, spec.MaxRpm);
    }

    [Fact]
    public void SlowingDown_ShiftsBackDown()
    {
        var box = Settled(CarCatalog.Zastava101, speedMps: 30, seconds: 5);
        int topGear = box.Gear;

        for (double t = 0; t < 6; t += 1.0 / 60)
            box.Update(speedMps: 3, throttle: 0.1, dt: 1.0 / 60);

        Assert.True(box.Gear < topGear, $"передача должна упасть с {topGear}, а стала {box.Gear}");
    }

    [Fact]
    public void DifferentCars_HaveDifferentMass()
    {
        var zastava = VehicleState.CreateDefault(CarCatalog.Zastava101);
        var vaz = VehicleState.CreateDefault(CarCatalog.Vaz2106);

        Assert.True(vaz.TotalMassKg > zastava.TotalMassKg);
        Assert.Equal(CarCatalog.Vaz2106.FuelCapacityL, vaz.Fuel.CapacityLiters, 3);
    }
}
