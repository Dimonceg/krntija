using Krntija.Core;
using Krntija.Core.Items;
using Xunit;

namespace Krntija.Core.Tests;

public class UpgradeEffectTests
{
    [Fact]
    public void RoofRackGivesTwentyFiveKilograms()
    {
        var session = GameSession.NewGame();
        double before = session.Trunk.MaxWeightKg;

        session.ApplyUpgrade(UpgradeCatalog.RoofRack);

        Assert.Equal(before + 25, session.Trunk.MaxWeightKg, 3);
        Assert.Equal(25, UpgradeEffects.RoofRackKg, 3);
    }

    [Fact]
    public void EngineAndBullbarChangeVehicleNumbers()
    {
        var session = GameSession.NewGame();
        Assert.Equal(1.0, session.Vehicle.EnginePowerFactor, 6);
        Assert.Equal(1.0, session.Vehicle.CrashDamageFactor, 6);

        session.ApplyUpgrade(UpgradeCatalog.Engine);
        session.ApplyUpgrade(UpgradeCatalog.Bullbar);

        Assert.Equal(UpgradeEffects.EnginePower, session.Vehicle.EnginePowerFactor, 6);
        Assert.Equal(UpgradeEffects.CrashDamage, session.Vehicle.CrashDamageFactor, 6);
        Assert.True(session.Vehicle.EnginePowerFactor > 1);
        Assert.True(session.Vehicle.CrashDamageFactor < 1);
    }

    [Fact]
    public void LightsRangeIsANumberInCore()
    {
        Assert.True(UpgradeEffects.LightsRange(true) > UpgradeEffects.LightsRange(false));
        Assert.True(UpgradeEffects.LightsRange(false) > 0);
    }

    [Fact]
    public void UpgradesStillWorkAfterSaveAndLoad()
    {
        var session = GameSession.NewGame();
        session.ApplyUpgrade(UpgradeCatalog.RoofRack);
        session.ApplyUpgrade(UpgradeCatalog.Engine);
        session.ApplyUpgrade(UpgradeCatalog.Bullbar);

        double trunk = session.Trunk.MaxWeightKg;

        var loaded = GameSession.FromSave(session.ToSave());

        Assert.Equal(trunk, loaded.Trunk.MaxWeightKg, 3);
        Assert.Equal(UpgradeEffects.EnginePower, loaded.Vehicle.EnginePowerFactor, 6);
        Assert.Equal(UpgradeEffects.CrashDamage, loaded.Vehicle.CrashDamageFactor, 6);
    }
}
