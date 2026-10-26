using System.Collections.Generic;
using Godot;
using Krntija.Core.WorldGen;

namespace Krntija.Game;

/// <summary>
/// Стая волков: патрулирует вокруг места, гонится за пешеходом и кусает его.
/// В машине игрок защищён. Анимации (idle/walk/run/attack) приходят из wolf.glb,
/// урон и пауза — из ядра (<see cref="ThreatRules"/>). Живёт в куске.
/// </summary>
public partial class WolfPack : Node3D
{
    public int Tier { get; set; } = 1;

    private const float ChaseRadius = 20f;
    private const float BiteRadius = 2.2f;
    private const float BiteCooldown = 1.6f;
    private const float PatrolRadius = 4f;
    private const float PatrolSpeed = 1.1f;
    private const float ChaseSpeed = 2.6f;

    private readonly List<AnimationPlayer> _animators = new();

    private Vector3 _origin;
    private float _patrolAngle;
    private float _biteTimer;
    private string _clip = "idle";

    public override void _Ready()
    {
        _origin = GlobalPosition;
        _patrolAngle = GlobalPosition.X * 0.37f;
        CollectAnimators(this);
        Play("idle");
    }

    private void CollectAnimators(Node node)
    {
        if (node is AnimationPlayer player) _animators.Add(player);

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode) CollectAnimators(childNode);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _biteTimer -= dt;

        var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        bool chasing = player is not null && GlobalPosition.DistanceTo(player.GlobalPosition) < ChaseRadius;

        if (chasing)
        {
            Vector3 toPlayer = player!.GlobalPosition - GlobalPosition;
            toPlayer.Y = 0;

            if (toPlayer.Length() > BiteRadius)
            {
                MoveTowards(toPlayer.Normalized(), ChaseSpeed * dt);
                Play("run");
            }
            else
            {
                Face(toPlayer);
                Play("attack");
                if (_biteTimer <= 0f) Bite();
            }

            return;
        }

        // Патруль по кругу вокруг места, куда стаю поставили.
        _patrolAngle += dt * 0.5f;
        var target = _origin + new Vector3(Mathf.Cos(_patrolAngle), 0, Mathf.Sin(_patrolAngle)) * PatrolRadius;
        Vector3 toTarget = target - GlobalPosition;
        toTarget.Y = 0;

        if (toTarget.Length() > 0.4f)
        {
            MoveTowards(toTarget.Normalized(), PatrolSpeed * dt);
            Play("walk");
        }
        else
        {
            Play("idle");
        }
    }

    private void MoveTowards(Vector3 direction, float step)
    {
        GlobalPosition += direction * step;
        Face(direction);
    }

    private void Face(Vector3 direction)
    {
        if (direction.LengthSquared() < 0.0001f) return;
        Rotation = new Vector3(0, Mathf.Atan2(direction.X, direction.Z), 0);
    }

    private void Bite()
    {
        _biteTimer = BiteCooldown;

        if (GetTree().GetFirstNodeInGroup("car") is not Car car) return;

        var survival = car.Session.Survival;
        if (survival.Health <= 0f) return;

        survival.Health = Mathf.Max(0f, survival.Health - (float)ThreatRules.WolfDamage(Tier));
    }

    private void Play(string clip)
    {
        if (clip == _clip) return;
        _clip = clip;

        foreach (var player in _animators)
        {
            if (!player.HasAnimation(clip)) continue;
            player.Play(clip);
        }
    }
}
