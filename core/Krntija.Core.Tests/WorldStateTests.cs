using Krntija.Core.World;
using Xunit;

namespace Krntija.Core.Tests;

public class WorldStateTests
{
    [Fact]
    public void MarkDone_IsTrueOnlyTheFirstTime()
    {
        var world = new WorldState();

        Assert.True(world.MarkDone(WorldState.Loot("house", 3)));
        Assert.False(world.MarkDone(WorldState.Loot("house", 3)));
        Assert.True(world.IsDone("loot:house:3"));
        Assert.False(world.IsDone(WorldState.Loot("house", 4)));
    }

    [Fact]
    public void SaveAndLoad_KeepsWhatWasDoneInTheWorld()
    {
        var session = GameSession.NewGame();
        session.WorldState.MarkDone(WorldState.Loot("poi", 0));
        session.WorldState.MarkDone(WorldState.Gate(1));
        session.WorldState.MarkDone(WorldState.Exit(2));

        var json = SaveSerializer.ToJson(session.ToSave());
        var loaded = GameSession.FromSave(SaveSerializer.FromJson(json));

        Assert.True(loaded.WorldState.IsDone("loot:poi:0"));
        Assert.True(loaded.WorldState.IsDone("gate:1"));
        Assert.True(loaded.WorldState.IsDone("exit:2"));
        Assert.Equal(3, loaded.WorldState.Done.Count);
    }

    [Fact]
    public void OldSaveWithoutWorldState_LoadsWithEmptyWorld()
    {
        // Сейв версии 1: поля worldDone ещё не было.
        const string json = """{ "version": 1, "specId": "zastava_101", "money": 50 }""";

        var loaded = GameSession.FromSave(SaveSerializer.FromJson(json));

        Assert.Empty(loaded.WorldState.Done);
        Assert.Equal(50, loaded.Wallet.Money);
    }

    [Fact]
    public void PlaceReward_IsPaidOnceEvenAfterLoad()
    {
        var session = GameSession.NewGame();
        double start = session.Wallet.Money;

        Assert.True(WorldActions.TryRewardPlace(session, 2));
        Assert.False(WorldActions.TryRewardPlace(session, 2));
        Assert.Equal(start + WorldActions.PlaceReward, session.Wallet.Money, precision: 6);

        // Раньше награды жили в сцене: после перезапуска их можно было собрать снова.
        var loaded = GameSession.FromSave(SaveSerializer.FromJson(SaveSerializer.ToJson(session.ToSave())));
        Assert.False(WorldActions.TryRewardPlace(loaded, 2));
        Assert.True(WorldActions.TryRewardPlace(loaded, 3));
    }

    [Fact]
    public void PlanIds_MatchTheFormatFromAdr0002()
    {
        Assert.Equal("loot:c12/p3/l1", WorldState.Loot("c12/p3/l1"));
        Assert.Equal("place:c4/p1", WorldState.Place("c4/p1"));
        Assert.Equal("gate:c2/p0/u0", WorldState.Use("gate", "c2/p0/u0"));
    }

    [Fact]
    public void PlanPlaceReward_IsPaidOnceAndOnlyForLandmarks()
    {
        var session = GameSession.NewGame();
        double start = session.Wallet.Money;
        var station = WorldGen.WorldGenerator.Plan(20261025, 1).Places[0];
        var well = WorldGen.WorldGenerator.Plan(20261025, 0).Places[2];

        Assert.True(WorldActions.TryRewardPlace(session, station));
        Assert.False(WorldActions.TryRewardPlace(session, station));
        Assert.False(WorldActions.TryRewardPlace(session, well));
        Assert.Equal(start + WorldActions.PlaceReward, session.Wallet.Money, precision: 6);
        Assert.True(session.WorldState.IsDone($"place:{station.Id}"));
    }
}
