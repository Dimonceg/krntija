using Krntija.Core;
using Xunit;

namespace Krntija.Core.Tests;

public class ConsumptionTests
{
    [Fact]
    public void TierZeroDryChangesNothing()
    {
        Assert.Equal(1.0, Journey.FuelFactor(0, 0), 6);
        Assert.Equal(1.0, Journey.WearFactor(0, 0), 6);
    }

    [Fact]
    public void TierThreeInRainUsesFormulaMultiplier()
    {
        double expectedFuel = Journey.LitersPerKm(3, 1) / Journey.LitersPerKm(0, 0);
        double expectedWear = Journey.WearPerKm(3, 1) / Journey.WearPerKm(0, 0);

        Assert.Equal(expectedFuel, Journey.FuelFactor(3, 1), 6);
        Assert.Equal(expectedWear, Journey.WearFactor(3, 1), 6);
        Assert.True(expectedFuel > 1.5, $"на тире 3 в дождь расход всего ×{expectedFuel:F2}");
    }

    [Fact]
    public void MultipliersGrowWithDistanceAndRain()
    {
        Assert.True(Journey.FuelFactor(3, 0) > Journey.FuelFactor(0, 0));
        Assert.True(Journey.FuelFactor(0, 1) > Journey.FuelFactor(0, 0));
        Assert.True(Journey.WearFactor(5, 0) > Journey.WearFactor(0, 0));
    }
}
