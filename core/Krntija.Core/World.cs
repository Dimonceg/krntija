namespace Krntija.Core.World;

/// <summary>
/// Профиль высот дороги: пологие балканские горки. Зависит только от пройденных
/// метров, поэтому дорога, обочины и точки интереса всегда согласованы между собой
/// и у всех игроков одинаковы.
/// </summary>
public static class RoadProfile
{
    public static double HeightAt(double distanceMeters) =>
        5.5 * Math.Sin(distanceMeters * 0.0042)
        + 2.4 * Math.Sin(distanceMeters * 0.011 + 1.3)
        + 1.1 * Math.Sin(distanceMeters * 0.027 + 0.7);

    /// <summary>Максимальная крутизна, м на метр пути (для проверок в тестах).</summary>
    public static double MaxSlope => 5.5 * 0.0042 + 2.4 * 0.011 + 1.1 * 0.027;
}

/// <summary>Игровые часы. По умолчанию 1 реальная секунда = 1 игровая минута.</summary>
public sealed class GameClock
{
    public const double MinutesPerDay = 1440;

    /// <summary>Сколько игровых секунд проходит за одну реальную.</summary>
    public double TimeScale { get; init; } = 60;

    public double TotalMinutes { get; private set; }

    public int Day => (int)(TotalMinutes / MinutesPerDay) + 1;

    public double HourOfDay => TotalMinutes % MinutesPerDay / 60.0;

    public bool IsNight => HourOfDay < 6.5 || HourOfDay >= 18.5;

    public void Advance(double realSeconds)
    {
        if (realSeconds <= 0) return;
        TotalMinutes += realSeconds * TimeScale / 60.0;
    }

    public void SetTotalMinutes(double minutes) => TotalMinutes = Math.Max(0, minutes);

    public override string ToString() =>
        $"День {Day}, {HourOfDay:00.0}:00";
}

/// <summary>
/// Детерминированный ГПСЧ (PCG32). Нужен, чтобы события на дороге
/// воспроизводились у всех игроков в коопе и в тестах.
/// </summary>
public sealed class Pcg32
{
    private readonly ulong _increment;

    public Pcg32(ulong seed, ulong sequence = 54)
    {
        _increment = (sequence << 1) | 1UL;
        State = 0;
        NextUInt();
        State += seed;
        NextUInt();
    }

    public ulong State { get; set; }

    public uint NextUInt()
    {
        ulong old = State;
        State = old * 6364136223846793005UL + _increment;
        uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
    }

    /// <summary>[0, 1).</summary>
    public double NextDouble() => NextUInt() * (1.0 / 4294967296.0);

    public int NextInt(int maxExclusive) =>
        maxExclusive <= 0 ? 0 : Math.Min(maxExclusive - 1, (int)(NextDouble() * maxExclusive));

    public double Range(double min, double max) => min + NextDouble() * (max - min);

    public bool Chance(double probability) => NextDouble() < probability;

    public T Pick<T>(IReadOnlyList<T> items) =>
        items.Count == 0 ? throw new ArgumentException("Пустой список", nameof(items)) : items[NextInt(items.Count)];
}
