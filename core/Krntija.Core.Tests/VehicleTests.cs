using Krntija.Core.Parts;
using Krntija.Core.Vehicle;
using Xunit;

namespace Krntija.Core.Tests;

public class VehicleTests
{
    private static VehicleState RunningCar(double fuelLiters = 30)
    {
        var car = VehicleState.CreateDefault();
        car.Fuel.SetLiters(fuelLiters);
        car.EngineRunning = true;
        return car;
    }

    private static void Run(VehicleSystems systems, double seconds, double throttle, double speedMps = 0, double dt = 1.0)
    {
        for (double t = 0; t < seconds; t += dt)
            systems.Step(dt, new VehicleSample(speedMps), new DriveInput(Throttle: throttle));
    }

    [Fact]
    public void DefaultBuild_HasAllSlotsFilled()
    {
        var car = VehicleState.CreateDefault();

        // «С завода» машина полная: слоты базового набора плюс гнёзда раскладки
        // (двери, капот, крышка багажника — ADR 0003).
        foreach (var (id, slot) in PartCatalog.StandardBuild)
            Assert.True(car.Parts.ContainsKey(slot), $"Слот {slot} пустой ({id})");
        foreach (var socket in CarLayout.Zastava101.Slots)
            Assert.True(car.Parts.ContainsKey(socket.Slot), $"Гнездо {socket.Slot} пустое");

        int expected = PartCatalog.StandardBuild.Select(p => p.Slot)
            .Concat(CarLayout.Zastava101.Slots.Select(s => s.Slot))
            .Distinct()
            .Count();
        Assert.Equal(expected, car.Parts.Count);
    }

    [Fact]
    public void Ignition_StartsTheEngine()
    {
        var car = VehicleState.CreateDefault();
        var systems = new VehicleSystems(car);

        systems.Step(1, new VehicleSample(0), new DriveInput(Ignition: true));

        Assert.True(car.EngineRunning);
        Assert.True(systems.PowerFactor > 0.5);
    }

    [Fact]
    public void DeadBattery_DoesNotStart()
    {
        var car = VehicleState.CreateDefault();
        car.BatteryCharge = 0;
        var systems = new VehicleSystems(car);

        systems.Step(1, new VehicleSample(0), new DriveInput(Ignition: true));

        Assert.False(car.EngineRunning);
        Assert.Equal(0, systems.PowerFactor);
    }

    [Fact]
    public void FullThrottle_BurnsMoreFuelThanIdle()
    {
        var idleCar = RunningCar();
        var idle = new VehicleSystems(idleCar);
        Run(idle, 600, throttle: 0);
        double idleUsed = 30 - idleCar.Fuel.Liters;

        var fastCar = RunningCar();
        var fast = new VehicleSystems(fastCar);
        Run(fast, 600, throttle: 1, speedMps: 25);
        double fastUsed = 30 - fastCar.Fuel.Liters;

        Assert.True(idleUsed > 0, "на холостых расход не может быть нулевым");
        Assert.True(fastUsed > idleUsed * 4, $"полный газ {fastUsed:F2} л должен жрать заметно больше холостых {idleUsed:F2} л");
    }

    [Fact]
    public void EmptyTank_StopsTheEngine()
    {
        var car = RunningCar(fuelLiters: 0.2);
        var systems = new VehicleSystems(car);

        Run(systems, 120, throttle: 1, speedMps: 20);

        Assert.True(car.Fuel.Liters <= 0.05);
        Assert.False(car.EngineRunning);
    }

    [Fact]
    public void BrokenRadiator_LeadsToOverheat()
    {
        var car = RunningCar();
        car.Get(PartSlot.Radiator)!.SetCondition(0.05);
        var systems = new VehicleSystems(car);

        Run(systems, 1200, throttle: 1, speedMps: 0);

        Assert.True(car.EngineTempC > 110, $"ожидали перегрев, получили {car.EngineTempC:F1} °C");
        Assert.True(systems.Overheating);
    }

    [Fact]
    public void HealthyCar_AtHighwaySpeed_DoesNotOverheat()
    {
        var car = RunningCar();
        var systems = new VehicleSystems(car);

        Run(systems, 1200, throttle: 1, speedMps: 25);

        Assert.InRange(car.EngineTempC, 40, 108);
    }

    [Fact]
    public void WornEngine_LosesPower()
    {
        var car = RunningCar();
        car.Get(PartSlot.Engine)!.SetCondition(0.1);
        var systems = new VehicleSystems(car);

        systems.Step(1, new VehicleSample(10), new DriveInput(Throttle: 1));

        Assert.True(systems.PowerFactor < 0.55, $"изношенный мотор должен терять мощность, получили {systems.PowerFactor:F2}");
    }

    [Fact]
    public void Driving_WearsTyres_AndAddsOdometer()
    {
        var car = RunningCar();
        var systems = new VehicleSystems(car);
        double before = car.Get(PartSlot.WheelFrontLeft)!.Condition;

        Run(systems, 3600, throttle: 0.5, speedMps: 20);

        Assert.True(car.OdometerKm > 60, $"одометр должен расти, получили {car.OdometerKm:F1} км");
        Assert.True(car.Get(PartSlot.WheelFrontLeft)!.Condition < before);
    }

    [Fact]
    public void Repair_RestoresCondition()
    {
        var car = RunningCar();
        var engine = car.Get(PartSlot.Engine)!;
        engine.SetCondition(0.3);
        var systems = new VehicleSystems(car);

        systems.Repair(PartSlot.Engine, 0.5);

        Assert.InRange(engine.Condition, 0.79, 0.81);
    }
}
