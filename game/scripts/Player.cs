using Godot;

namespace Krntija.Game;

/// <summary>
/// Пешеход. Пока без мыши: A/D поворачивают, W/S идут, Shift — бег.
/// Этого хватает, чтобы выйти из машины, дойти до колонки и вернуться.
/// </summary>
public partial class Player : CharacterBody3D
{
    private const float WalkSpeed = 3.4f;
    private const float RunSpeed = 5.8f;
    private const float TurnSpeed = 2.6f;
    private const float Gravity = 22f;

    public float Yaw { get; set; }

    public override void _Ready()
    {
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.34f, Height = 1.7f },
            Position = new Vector3(0, 0.85f, 0),
        });

        var cloth = new StandardMaterial3D { AlbedoColor = new Color(0.33f, 0.37f, 0.44f), Roughness = 0.95f };
        AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.34f, Height = 1.7f },
            Position = new Vector3(0, 0.85f, 0),
            MaterialOverride = cloth,
        });

        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.17f, Height = 0.34f },
            Position = new Vector3(0, 1.78f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.84f, 0.7f, 0.56f), Roughness = 0.8f },
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        Yaw -= Input.GetAxis("steer_left", "steer_right") * TurnSpeed * dt;
        Rotation = new Vector3(0, Yaw, 0);

        float move = Input.GetActionStrength("accelerate") - Input.GetActionStrength("brake");
        float speed = Input.IsActionPressed("handbrake") ? RunSpeed : WalkSpeed;
        Vector3 forward = -GlobalBasis.Z;

        Vector3 velocity = Velocity;
        velocity.X = forward.X * move * speed;
        velocity.Z = forward.Z * move * speed;
        velocity.Y -= Gravity * dt;

        Velocity = velocity;
        MoveAndSlide();
    }

    public void Teleport(Vector3 position)
    {
        GlobalPosition = position;
        Velocity = Vector3.Zero;
    }
}
