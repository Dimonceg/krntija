using Godot;

namespace Krntija.Game;

/// <summary>Камера, которая висит за целью и мягко догоняет её.</summary>
public partial class ChaseCamera : Camera3D
{
    public Node3D? Target { get; set; }

    public float Distance { get; set; } = 7.5f;
    public float Height { get; set; } = 3.2f;
    public float LookHeight { get; set; } = 0.9f;

    public override void _PhysicsProcess(double delta)
    {
        if (Target is null) return;

        Vector3 desired = Target.GlobalPosition + Target.GlobalBasis.Z * Distance + Vector3.Up * Height;
        float t = Mathf.Clamp((float)delta * 5f, 0f, 1f);

        GlobalPosition = GlobalPosition.Lerp(desired, t);
        LookAt(Target.GlobalPosition + Vector3.Up * LookHeight, Vector3.Up);
    }
}
