using Krntija.Core.Items;
using Xunit;

namespace Krntija.Core.Tests;

/// <summary>Установка улучшения рядом с машиной, где угодно (A9).</summary>
public class UpgradeInstallTests
{
    private static GameSession Session() => GameSession.NewGame();

    [Fact]
    public void InstallsFromTheBagNearTheCar()
    {
        var session = Session();
        Assert.True(session.Inventory.TryAdd(UpgradeCatalog.Toolbox, 1));

        double roomBefore = session.Inventory.MaxWeightKg;

        Assert.Equal(UpgradeInstallResult.Ok,
            UpgradeInstall.CanInstall(session, UpgradeCatalog.Toolbox, 1.5));
        Assert.Equal(UpgradeInstallResult.Ok,
            UpgradeInstall.Install(session, UpgradeCatalog.Toolbox, 1.5));

        Assert.Contains(UpgradeCatalog.Toolbox, session.Upgrades);
        Assert.Equal(0, session.Inventory.Count(UpgradeCatalog.Toolbox));
        Assert.Equal(roomBefore + 10, session.Inventory.MaxWeightKg, 3);
    }

    [Fact]
    public void InstallsFromTheTrunkWhenTheBagIsEmpty()
    {
        var session = Session();
        Assert.True(session.Trunk.TryAdd(UpgradeCatalog.RoofRack, 1));

        double roomBefore = session.Trunk.MaxWeightKg;

        Assert.Equal(UpgradeInstallResult.Ok,
            UpgradeInstall.Install(session, UpgradeCatalog.RoofRack, 2));

        Assert.Contains(UpgradeCatalog.RoofRack, session.Upgrades);
        Assert.Equal(0, session.Trunk.Count(UpgradeCatalog.RoofRack));
        Assert.Equal(roomBefore + 25, session.Trunk.MaxWeightKg, 3);
    }

    [Fact]
    public void TakesFromTheBagBeforeTheTrunk()
    {
        var session = Session();
        Assert.True(session.Inventory.TryAdd(UpgradeCatalog.Toolbox, 1));
        Assert.True(session.Trunk.TryAdd(UpgradeCatalog.Toolbox, 2));

        Assert.Equal(UpgradeInstallResult.Ok,
            UpgradeInstall.Install(session, UpgradeCatalog.Toolbox, 1));

        Assert.Equal(0, session.Inventory.Count(UpgradeCatalog.Toolbox));
        Assert.Equal(2, session.Trunk.Count(UpgradeCatalog.Toolbox));
    }

    [Fact]
    public void TooFarIsNotNearAndTheItemStays()
    {
        var session = Session();
        Assert.True(session.Inventory.TryAdd(UpgradeCatalog.Headlights, 1));

        Assert.Equal(UpgradeInstallResult.Ok,
            UpgradeInstall.CanInstall(session, UpgradeCatalog.Headlights, UpgradeInstall.NearCarMeters));
        Assert.Equal(UpgradeInstallResult.NotNear,
            UpgradeInstall.CanInstall(session, UpgradeCatalog.Headlights, UpgradeInstall.NearCarMeters + 0.01));

        Assert.Equal(UpgradeInstallResult.NotNear,
            UpgradeInstall.Install(session, UpgradeCatalog.Headlights, 9));
        Assert.Equal(1, session.Inventory.Count(UpgradeCatalog.Headlights));
        Assert.DoesNotContain(UpgradeCatalog.Headlights, session.Upgrades);
    }

    [Fact]
    public void AlreadyInstalledIsReported()
    {
        var session = Session();
        Assert.True(session.Trunk.TryAdd(UpgradeCatalog.Bullbar, 2));

        Assert.Equal(UpgradeInstallResult.Ok, UpgradeInstall.Install(session, UpgradeCatalog.Bullbar, 1));
        Assert.Equal(UpgradeInstallResult.AlreadyInstalled,
            UpgradeInstall.CanInstall(session, UpgradeCatalog.Bullbar, 1));

        // Второй раз ставить нечего: предмет остаётся лежать.
        Assert.Equal(UpgradeInstallResult.AlreadyInstalled,
            UpgradeInstall.Install(session, UpgradeCatalog.Bullbar, 1));
        Assert.Equal(1, session.Trunk.Count(UpgradeCatalog.Bullbar));
    }

    [Fact]
    public void NothingCarriedIsNotCarried()
    {
        var session = Session();

        Assert.Equal(UpgradeInstallResult.NotCarried,
            UpgradeInstall.CanInstall(session, UpgradeCatalog.FuelTank, 1));
    }

    [Fact]
    public void UnknownUpgradeIsUnknown()
    {
        Assert.Equal(UpgradeInstallResult.Unknown,
            UpgradeInstall.CanInstall(Session(), "upgrade_nope", 1));
    }

    [Fact]
    public void PendingIsTheCarriedAndNotInstalledOne()
    {
        var session = Session();
        Assert.Null(UpgradeInstall.Pending(session));

        Assert.True(session.Trunk.TryAdd(UpgradeCatalog.FuelTank, 1));
        Assert.Equal(UpgradeCatalog.FuelTank, UpgradeInstall.Pending(session));

        Assert.Equal(UpgradeInstallResult.Ok, UpgradeInstall.Install(session, UpgradeCatalog.FuelTank, 1));
        Assert.Null(UpgradeInstall.Pending(session));
    }

    [Fact]
    public void EveryUpgradeCanBeInstalledFromTheTrunk()
    {
        foreach (string id in UpgradeCatalog.All.Keys)
        {
            var session = Session();
            Assert.True(session.Trunk.TryAdd(id, 1), $"не влез в багажник: {id}");

            Assert.Equal(UpgradeInstallResult.Ok, UpgradeInstall.Install(session, id, 1));
            Assert.Contains(id, session.Upgrades);
            Assert.Equal(0, session.Trunk.Count(id));
        }
    }
}
