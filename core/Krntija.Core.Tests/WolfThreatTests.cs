using Krntija.Core.WorldGen;
using Xunit;

namespace Krntija.Core.Tests;

public class WolfThreatTests
{
    [Fact]
    public void WolfDamageGrowsWithTierAndStaysBounded()
    {
        double previous = 0;

        for (int tier = 0; tier <= WorldGenerator.MaxTier; tier++)
        {
            double damage = ThreatRules.WolfDamage(tier);
            Assert.True(damage >= previous, $"урон упал на тире {tier}");
            Assert.InRange(damage, 0.03, 0.12);
            previous = damage;
        }

        Assert.True(ThreatRules.WolfDamage(5) > ThreatRules.WolfDamage(0));
    }
}
