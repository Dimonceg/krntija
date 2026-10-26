using Krntija.Core.Interaction;
using Xunit;

namespace Krntija.Core.Tests;

/// <summary>Случаи из багов: у дома нельзя было сесть в машину, потом — выйти из неё.</summary>
public class TargetSelectorTests
{
    private const int Car = 0, Door = 1, Chest = 2, Pump = 3;

    private static Candidate CarAt(double distance, double angle) => new(Car, distance, angle, 3.5, Priority: 1);
    private static Candidate DoorAt(double distance, double angle) => new(Door, distance, angle, 3.2);

    [Fact]
    public void OnFoot_LookingAtCarNextToDoor_EntersCar()
    {
        var chosen = TargetSelector.Select(Stance.OnFoot, new[] { CarAt(2.5, 8), DoorAt(1.8, 70) });
        Assert.Equal(Car, chosen?.Id);
    }

    [Fact]
    public void OnFoot_LookingAtDoorNextToCar_OpensDoor()
    {
        var chosen = TargetSelector.Select(Stance.OnFoot, new[] { CarAt(2.5, 80), DoorAt(1.8, 5) });
        Assert.Equal(Door, chosen?.Id);
    }

    [Fact]
    public void OnFoot_LookingAway_TakesNearestInRadius()
    {
        var chosen = TargetSelector.Select(Stance.OnFoot, new[] { CarAt(3.0, 160), DoorAt(1.5, 170) });
        Assert.Equal(Door, chosen?.Id);

        Assert.Null(TargetSelector.Select(Stance.OnFoot, new[] { CarAt(9, 160), DoorAt(8, 170) }));
    }

    [Fact]
    public void InCar_NextToHouse_ExitsInsteadOfOpeningDoorOrChest()
    {
        var around = new[] { DoorAt(2.0, 3), new Candidate(Chest, 2.5, 10, 3) };
        Assert.Null(TargetSelector.Select(Stance.InCarStopped, around));
    }

    [Fact]
    public void InCar_AtPump_Refuels_AndMovingDoesNothing()
    {
        var around = new[] { DoorAt(2.0, 3), new Candidate(Pump, 3.0, 90, 4.5, UsableFromCar: true) };
        Assert.Equal(Pump, TargetSelector.Select(Stance.InCarStopped, around)?.Id);
        Assert.Null(TargetSelector.Select(Stance.InCarMoving, around));
    }

    [Fact]
    public void Priority_OnlyBreaksNearTies()
    {
        // Почти одинаково: перевес машины решает.
        var tie = TargetSelector.Select(Stance.OnFoot, new[] { CarAt(3.0, 20), DoorAt(3.0, 22) });
        Assert.Equal(Car, tie?.Id);

        // Смотрим прямо на дверь, машина сбоку на краю конуса — дверь.
        var clear = TargetSelector.Select(Stance.OnFoot, new[] { CarAt(3.0, 45), DoorAt(3.0, 2) });
        Assert.Equal(Door, clear?.Id);
    }
}
