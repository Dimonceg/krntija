using Krntija.Core;
using Krntija.Core.Economy;
using Krntija.Core.Items;
using Xunit;

namespace Krntija.Core.Tests;

public class LootTradeTests
{
    [Fact]
    public void SellingJunkRemovesItAndPaysHalfValue()
    {
        var bag = new Inventory();
        var wallet = new Wallet(0);
        bag.TryAdd("cassette", 4);   // 5 € за штуку
        bag.TryAdd("bread", 2);      // еда не хлам

        var (count, money) = TradeService.SellJunk(bag, wallet);

        Assert.Equal(4, count);
        Assert.Equal(10, money, 2);
        Assert.Equal(0, bag.Count("cassette"));
        Assert.Equal(2, bag.Count("bread"));
        Assert.Equal(10, wallet.Money, 2);
    }

    [Fact]
    public void NothingToSellWhenNoJunk()
    {
        var bag = new Inventory();
        var wallet = new Wallet(0);
        bag.TryAdd("bread", 1);

        var (count, money) = TradeService.SellJunk(bag, wallet);

        Assert.Equal(0, count);
        Assert.Equal(0, money, 2);
        Assert.Equal(1, bag.Count("bread"));
    }

    [Fact]
    public void TrunkSurvivesSaveRoundTrip()
    {
        var session = GameSession.NewGame();
        session.Trunk.TryAdd("spare_tire");
        session.Trunk.TryAdd("cassette", 3);

        var restored = GameSession.FromSave(session.ToSave());

        Assert.True(restored.Trunk.Count("spare_tire") == 1);
        Assert.True(restored.Trunk.Count("cassette") == 3);
        Assert.True(restored.Inventory.Count("bread") == 2);
    }
}
