using Krntija.Core;
using Krntija.Core.Items;
using Xunit;

namespace Krntija.Core.Tests;

public class CrateTests
{
    [Fact]
    public void CrateLootIsDeterministicForSameId()
    {
        var first = WorldActions.CrateLoot("crate:c5/p0/u2");
        var second = WorldActions.CrateLoot("crate:c5/p0/u2");

        Assert.Equal(first, second);
        Assert.True(first.Count > 0);
        Assert.True(ItemCatalog.TryGet(first.ItemId, out _));
    }

    [Fact]
    public void DifferentCratesGiveDifferentSeeds()
    {
        var a = WorldActions.CrateLoot("crate:c5/p0/u2");
        var b = WorldActions.CrateLoot("crate:c9/p1/u1");

        Assert.False(string.Equals(WorldGen.Hash.MixString(0x5EED, "crate:c5/p0/u2"),
            WorldGen.Hash.MixString(0x5EED, "crate:c9/p1/u1")));
        Assert.True(a.Count > 0 && b.Count > 0);
    }
}
