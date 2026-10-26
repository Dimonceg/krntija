using Krntija.Core;
using Krntija.Core.Items;
using Xunit;

namespace Krntija.Core.Tests;

public class CrashTests
{
    [Fact]
    public void GentleBrakingIsNotACrash()
    {
        Assert.Equal(0, CrashRules.BodyWear(1.0));
        Assert.Equal(0, CrashRules.BodyWear(CrashRules.ThresholdMps - 0.1));
    }

    [Fact]
    public void HarderImpactWearsMoreButStaysBounded()
    {
        double soft = CrashRules.BodyWear(5);
        double hard = CrashRules.BodyWear(20);

        Assert.True(soft > 0);
        Assert.True(hard > soft);
        Assert.True(hard <= 0.35);
    }

    [Fact]
    public void BullbarReducesImpactWear()
    {
        double bare = CrashRules.BodyWear(15, 1.0);
        double protectedCar = CrashRules.BodyWear(15, UpgradeEffects.CrashDamage);

        Assert.True(protectedCar < bare);
        Assert.Equal(bare * UpgradeEffects.CrashDamage, protectedCar, 6);
    }

}
