namespace Krntija.Core.World;

public enum WeatherKind
{
    Clear,
    Cloudy,
    Rain,
}

/// <summary>
/// Погода меняется сама: ясно → облачно → дождь → облачно. Дождь делает
/// асфальт мокрым (падает сцепление) и постепенно высыхает.
/// </summary>
public sealed class WeatherState
{
    public WeatherKind Kind { get; private set; } = WeatherKind.Clear;

    /// <summary>0..1, насколько дорога мокрая. Сохраняется после дождя.</summary>
    public double Wetness { get; private set; }

    /// <summary>0..1, сила дождя прямо сейчас.</summary>
    public double RainIntensity { get; private set; }

    public double MinutesUntilChange { get; private set; } = 5;

    /// <summary>Множитель сцепления с дорогой: мокрое полотно держит хуже.</summary>
    public double GripFactor => 1.0 - 0.22 * Wetness;

    public void Update(double gameMinutes, Pcg32 rng)
    {
        if (gameMinutes <= 0) return;

        MinutesUntilChange -= gameMinutes;
        if (MinutesUntilChange <= 0)
            Change(rng);

        bool raining = Kind == WeatherKind.Rain;

        RainIntensity = raining
            ? Math.Min(1, RainIntensity + gameMinutes * 0.6)
            : Math.Max(0, RainIntensity - gameMinutes * 0.4);

        Wetness = raining
            ? Math.Min(1, Wetness + gameMinutes * 0.25)
            : Math.Max(0, Wetness - gameMinutes * 0.04);
    }

    /// <summary>Для сейва: восстановить состояние как было.</summary>
    public void Restore(WeatherKind kind, double wetness, double rain, double minutesUntilChange)
    {
        Kind = kind;
        Wetness = Math.Clamp(wetness, 0, 1);
        RainIntensity = Math.Clamp(rain, 0, 1);
        MinutesUntilChange = Math.Max(0, minutesUntilChange);
    }

    private void Change(Pcg32 rng)
    {
        Kind = Kind switch
        {
            WeatherKind.Clear => WeatherKind.Cloudy,
            WeatherKind.Cloudy => rng.Chance(0.5) ? WeatherKind.Rain : WeatherKind.Clear,
            _ => WeatherKind.Cloudy,
        };

        MinutesUntilChange = rng.Range(3, 8);
    }
}
