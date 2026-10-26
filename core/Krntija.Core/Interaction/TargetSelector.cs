namespace Krntija.Core.Interaction;

/// <summary>Где игрок: от этого зависит, что вообще можно выбрать клавишей E.</summary>
public enum Stance
{
    OnFoot,
    InCarStopped,
    InCarMoving,
}

/// <summary>
/// Кандидат на действие: точка мира или сама машина. Углы и расстояния считает
/// сцена, правило выбора — здесь.
/// </summary>
/// <param name="Id">Номер у вызывающего: по нему он узнаёт победителя.</param>
/// <param name="Distance">От игрока (пешком) или от машины (в машине), метры.</param>
/// <param name="AngleDeg">Угол между взглядом камеры и направлением на цель.</param>
/// <param name="Radius">Ближе этого цель доступна и без взгляда на неё.</param>
/// <param name="Priority">Небольшой перевес при равных условиях (машина, дорожные точки).</param>
/// <param name="UsableFromCar">Можно сделать, не выходя: колонка, лавка, шлагбаум, скупщик.</param>
/// <param name="InSight">Между игроком и целью нет стены или закрытой двери (проверяет сцена).</param>
public readonly record struct Candidate(
    int Id, double Distance, double AngleDeg, double Radius, int Priority = 0, bool UsableFromCar = false,
    bool InSight = true);

/// <summary>
/// Один селектор цели для клавиши E и для подсказки на экране: они не могут
/// разойтись, потому что спрашивают одно и то же. Новая точка добавляется
/// строкой в таблицу приоритетов сцены, а не новым if.
/// </summary>
/// <remarks>
/// Цель за стеной или закрытой дверью не выбирается никогда: сначала открой дверь.
/// Пешком: если что-то в конусе взгляда — побеждает то, на что смотрят точнее
/// и что ближе (приоритет — небольшой перевес). Иначе — ближайшее в своём радиусе.
/// В машине стоя: только то, что делается из машины, ближайшее в радиусе;
/// нет такого — выход. В движении — ничего.
/// </remarks>
public static class TargetSelector
{
    /// <summary>Конус взгляда, градусы.</summary>
    public const double LookAngle = 50;

    /// <summary>Дальше этого взгляд не выбирает, метры.</summary>
    public const double LookDistance = 6.5;

    /// <summary>Сколько «стоит» единица приоритета в оценке взгляда (угол/50 + расстояние/6,5).</summary>
    public const double PriorityBias = 0.15;

    public static bool IsLooked(Candidate c) => c.AngleDeg <= LookAngle && c.Distance <= LookDistance;

    /// <summary>Победитель или null: пешком — нечего делать, в машине стоя — выйти, в движении — ничего.</summary>
    public static Candidate? Select(Stance stance, IReadOnlyList<Candidate> candidates)
    {
        switch (stance)
        {
            case Stance.InCarMoving:
                return null;

            case Stance.InCarStopped:
                return Best(candidates, c => c.UsableFromCar && c.Distance <= c.Radius, NearestScore);

            default:
                return Best(candidates, IsLooked, LookScore)
                       ?? Best(candidates, c => c.Distance <= c.Radius, NearestScore);
        }
    }

    private static double LookScore(Candidate c) =>
        c.AngleDeg / LookAngle + c.Distance / LookDistance - c.Priority * PriorityBias;

    private static double NearestScore(Candidate c) => c.Distance - c.Priority * 0.01;

    private static Candidate? Best(IReadOnlyList<Candidate> candidates, Func<Candidate, bool> eligible,
        Func<Candidate, double> score)
    {
        Candidate? best = null;
        double bestScore = double.MaxValue;
        foreach (var candidate in candidates)
        {
            if (!candidate.InSight || !eligible(candidate)) continue;
            double value = score(candidate);
            if (value >= bestScore) continue;
            best = candidate;
            bestScore = value;
        }

        return best;
    }
}
