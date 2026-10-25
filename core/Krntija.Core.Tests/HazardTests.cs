using Krntija.Core.WorldGen;
using Xunit;

namespace Krntija.Core.Tests;

public class HazardTests
{
    private const ulong Seed = 20261025;

    [Fact]
    public void HazardsAreDeterministicAndHavePlanIds()
    {
        for (int chunk = 0; chunk < 10; chunk++)
        {
            var first = WorldGenerator.Plan(Seed, chunk);
            var second = WorldGenerator.Plan(Seed, chunk);

            for (int i = 0; i < first.Places.Count; i++)
            {
                var a = first.Places[i].Hazards;
                var b = second.Places[i].Hazards;

                Assert.Equal(a.Count, b.Count);
                for (int h = 0; h < a.Count; h++)
                {
                    Assert.Equal(a[h], b[h]);
                    Assert.Matches(new System.Text.RegularExpressions.Regex(@"^c\d+/p\d+/h\d+$"), a[h].Id);
                    Assert.True(a[h].Amount >= 0);
                }
            }
        }
    }

    [Fact]
    public void DangerousTiersProduceMoreHazards()
    {
        int early = CountHazards(0, 8);
        int late = CountHazards(40, 48);

        Assert.True(late > early, $"ранние куски: {early}, дальние: {late}");
    }

    [Fact]
    public void StartChunkIsQuiet()
    {
        var start = WorldGenerator.Plan(Seed, 0);
        int hazards = start.Places.Sum(place => place.Hazards.Count);

        // На старте шанс опасности минимальный (10 %), но не нулевой по правилам;
        // проверяем, что заведомо меньше, чем на дальних кусках.
        Assert.True(hazards <= 1, $"на старте {hazards} угроз");
    }

    private static int CountHazards(int fromChunk, int toChunk)
    {
        int total = 0;
        for (int chunk = fromChunk; chunk < toChunk; chunk++)
            total += WorldGenerator.Plan(Seed, chunk).Places.Sum(place => place.Hazards.Count);
        return total;
    }
}
