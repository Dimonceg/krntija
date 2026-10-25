using Godot;
using Krntija.Core.WorldGen;

namespace Krntija.Game;

/// <summary>
/// Стая волков у места. Кусает только пешехода: в машине ты защищён.
/// Расстояние считается от самой стаи (узел стоит там, где волки), урон и
/// пауза — из ядра (<see cref="ThreatRules"/>). Живёт в куске.
/// </summary>
public partial class WolfPack : Node3D
{
    public int Tier { get; set; } = 1;

    private const float BiteRadius = 15f;
    private const float BiteCooldown = 5f;

    private float _cooldown;

    public override void _Process(double delta)
    {
        _cooldown -= (float)delta;
        if (_cooldown > 0f) return;

        // Пешеход — единственная цель: сидя в машине, игрок в безопасности.
        if (GetTree().GetFirstNodeInGroup("player") is not Node3D player) return;
        if (GlobalPosition.DistanceTo(player.GlobalPosition) > BiteRadius) return;

        if (GetTree().GetFirstNodeInGroup("car") is not Car car) return;

        var survival = car.Session.Survival;
        if (survival.Health <= 0f) return;

        survival.Health = Mathf.Max(0f, survival.Health - (float)ThreatRules.WolfDamage(Tier));
        _cooldown = BiteCooldown;
    }
}
