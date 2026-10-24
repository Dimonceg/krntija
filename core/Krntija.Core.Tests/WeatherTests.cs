using Krntija.Core.World;
using Xunit;

namespace Krntija.Core.Tests;

public class WeatherTests
{
    [Fact]
    public void WeatherChangesOverTime()
    {
        var weather = new WeatherState();
        var rng = new Pcg32(42);
        var seen = new HashSet<WeatherKind>();

        for (double minute = 0; minute < 240; minute += 0.5)
        {
            weather.Update(0.5, rng);
            seen.Add(weather.Kind);
        }

        Assert.Contains(WeatherKind.Cloudy, seen);
        Assert.Contains(WeatherKind.Rain, seen);
    }

    [Fact]
    public void RainMakesRoadWet_AndWetRoadGripsWorse()
    {
        var weather = new WeatherState();
        var rng = new Pcg32(7);
        weather.Restore(WeatherKind.Rain, wetness: 0, rain: 1, minutesUntilChange: 60);

        for (int i = 0; i < 240; i++)
            weather.Update(0.25, rng); // час игрового времени

        Assert.True(weather.Wetness > 0.9, $"мокрость {weather.Wetness:F2}");
        Assert.True(weather.GripFactor < 0.85, $"сцепление {weather.GripFactor:F2}");
    }

    [Fact]
    public void ClearWeatherDriesTheRoad()
    {
        var weather = new WeatherState();
        var rng = new Pcg32(3);
        weather.Restore(WeatherKind.Clear, wetness: 1, rain: 0, minutesUntilChange: 600);

        for (int i = 0; i < 1200; i++)
            weather.Update(1, rng);

        Assert.True(weather.Wetness < 0.05, $"мокрость {weather.Wetness:F2}");
        Assert.InRange(weather.GripFactor, 0.99, 1.0);
    }

    [Fact]
    public void SameSeed_SameWeather()
    {
        var a = new WeatherState();
        var b = new WeatherState();
        var ra = new Pcg32(99);
        var rb = new Pcg32(99);

        for (int i = 0; i < 500; i++)
        {
            a.Update(0.2, ra);
            b.Update(0.2, rb);
        }

        Assert.Equal(a.Kind, b.Kind);
        Assert.Equal(a.Wetness, b.Wetness, 6);
    }
}
