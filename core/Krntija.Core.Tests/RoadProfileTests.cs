using Krntija.Core.World;
using Xunit;

namespace Krntija.Core.Tests;

public class RoadProfileTests
{
    [Fact]
    public void Height_IsDeterministic()
    {
        for (double d = 0; d < 3000; d += 137)
            Assert.Equal(RoadProfile.HeightAt(d), RoadProfile.HeightAt(d), 9);
    }

    [Fact]
    public void Height_StaysWithinReasonableRange()
    {
        for (double d = 0; d < 5000; d += 5)
            Assert.InRange(RoadProfile.HeightAt(d), -9, 9);
    }

    [Fact]
    public void Profile_IsContinuous()
    {
        double previous = RoadProfile.HeightAt(0);

        for (double d = 4; d < 5000; d += 4)
        {
            double current = RoadProfile.HeightAt(d);
            Assert.True(Math.Abs(current - previous) < 0.5, $"разрыв на {d} м: {previous:F2} -> {current:F2}");
            previous = current;
        }
    }

    [Fact]
    public void Slope_IsGentleEnoughToDrive()
    {
        double maxStep = 0;
        for (double d = 0; d < 5000; d += 1)
            maxStep = Math.Max(maxStep, Math.Abs(RoadProfile.HeightAt(d + 1) - RoadProfile.HeightAt(d)));

        Assert.True(maxStep < 0.12, $"склон слишком крутой: {maxStep:F3} м на метр");
        Assert.True(RoadProfile.MaxSlope < 0.12);
    }
}
