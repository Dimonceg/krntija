namespace Krntija.Core.Survival;

/// <summary>Потребности игрока. Все шкалы 0..1, где 1 — «сыт/здоров».</summary>
public sealed class SurvivalState
{
    public double Health { get; set; } = 1.0;
    public double Hunger { get; set; } = 0.85;
    public double Thirst { get; set; } = 0.80;
    public double Fatigue { get; set; } = 0.90;

    public bool IsStarving => Hunger <= 0.001;
    public bool IsDehydrated => Thirst <= 0.001;
    public bool IsExhausted => Fatigue <= 0.001;

    /// <summary>Тик потребностей. gameHours — сколько игровых часов прошло.</summary>
    public void Step(double gameHours, double ambientC)
    {
        if (gameHours <= 0) return;

        Hunger = Math.Max(0, Hunger - 0.045 * gameHours);   // ~22 игровых часа
        Thirst = Math.Max(0, Thirst - 0.060 * gameHours);   // ~13 игровых часов
        Fatigue = Math.Max(0, Fatigue - 0.035 * gameHours); // ~26 игровых часов

        double damagePerHour = 0;
        if (IsStarving) damagePerHour += 0.020;
        if (IsDehydrated) damagePerHour += 0.040;
        if (IsExhausted) damagePerHour += 0.010;

        // На холоде без печки здоровье тоже уходит.
        if (ambientC < 0 && Thirst is > 0 and <= 0.3)
            damagePerHour += 0.010;

        if (damagePerHour > 0)
            Health = Math.Clamp(Health - damagePerHour * gameHours, 0, 1);
        else if (Health < 1 && !IsStarving && !IsDehydrated)
            Health = Math.Min(1, Health + 0.005 * gameHours); // медленно заживает
    }

    public void Eat(double nutrition) => Hunger = Math.Clamp(Hunger + nutrition, 0, 1);

    public void Drink(double hydration) => Thirst = Math.Clamp(Thirst + hydration, 0, 1);

    public void Sleep(double gameHours)
    {
        Fatigue = Math.Clamp(Fatigue + 0.12 * gameHours, 0, 1);
        Hunger = Math.Max(0, Hunger - 0.02 * gameHours);
        Thirst = Math.Max(0, Thirst - 0.025 * gameHours);
    }

    public void TakeDamage(double amount) => Health = Math.Clamp(Health - Math.Max(0, amount), 0, 1);

    public void Heal(double amount) => Health = Math.Clamp(Health + Math.Max(0, amount), 0, 1);
}
