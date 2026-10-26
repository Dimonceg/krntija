using Krntija.Core;
using Xunit;

namespace Krntija.Core.Tests;

public class RoadStationTests
{
    [Fact]
    public void DrivingBackAndForthNearHomeKeepsRecordButNotTier()
    {
        var session = GameSession.NewGame();

        session.StepRoad(200);
        Assert.Equal(0, session.Tier);
        Assert.Equal(200, session.BestDistanceMeters);

        session.StepRoad(3000);
        Assert.Equal(1, session.Tier);

        // Вернулись к дому: тир мира снова нулевой, а рекорд остался.
        session.StepRoad(0);
        Assert.Equal(0, session.Tier);
        Assert.Equal(3000, session.BestDistanceMeters);
    }
}
