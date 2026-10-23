using Godot;

namespace Krntija.Game;

/// <summary>Камера, которая висит за машиной и мягко догоняет её.</summary>
public partial class ChaseCamera : Camera3D
{
    public Node3D? Target { get; set; }

    public override void _PhysicsProcess(double delta)
    {
        if (Target is null) return;

        Vector3 desired = Target.GlobalPosition + Target.GlobalBasis.Z * 7.5f + Vector3.Up * 3.2f;
        float t = Mathf.Clamp((float)delta * 5f, 0f, 1f);

        GlobalPosition = GlobalPosition.Lerp(desired, t);
        LookAt(Target.GlobalPosition + Vector3.Up * 0.9f, Vector3.Up);
    }
}
