using Godot;

namespace Krntija.Game;

/// <summary>
/// Пешеход. Мышь — обзор, W/S — идти, A/D — шаг вбок, Shift — бежать.
/// </summary>
public partial class Player : CharacterBody3D
{
    private const float WalkSpeed = 3.4f;
    private const float RunSpeed = 5.8f;
    private const float Gravity = 22f;
    private const float MouseSensitivity = 0.0035f;

    public float Yaw { get; set; }

    private float _stepTimer;

    /// <summary>Спрятать тело игрока в виде от первого лица.</summary>
    public void SetModelVisible(bool visible)
    {
        foreach (var child in GetChildren())
        {
            if (child is MeshInstance3D mesh) mesh.Visible = visible;
        }
    }

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

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Input.MouseMode != Input.MouseModeEnum.Captured) return;
        if (@event is InputEventMouseMotion motion)
            Yaw -= motion.Relative.X * MouseSensitivity * GameSettings.MouseSensitivity;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        Rotation = new Vector3(0, Yaw, 0);

        float move = Input.GetActionStrength("accelerate") - Input.GetActionStrength("brake");
        float strafe = Input.GetAxis("steer_left", "steer_right");
        float speed = Input.IsActionPressed("sprint") ? RunSpeed : WalkSpeed;

        Vector3 forward = -GlobalBasis.Z;
        Vector3 right = GlobalBasis.X;

        Vector3 velocity = Velocity;
        velocity.X = forward.X * move * speed + right.X * strafe * speed * 0.8f;
        velocity.Z = forward.Z * move * speed + right.Z * strafe * speed * 0.8f;
        velocity.Y -= Gravity * dt;

        Velocity = velocity;
        MoveAndSlide();

        // Шаги: чем быстрее идёшь, тем чаще.
        bool walking = IsOnFloor() && new Vector2(Velocity.X, Velocity.Z).Length() > 0.6f;
        if (walking)
        {
            _stepTimer -= dt * (Input.IsActionPressed("sprint") ? 1.6f : 1f);
            if (_stepTimer <= 0f)
            {
                Sfx.PlayStep(GlobalPosition);
                _stepTimer = 0.5f;
            }
        }
        else
        {
            _stepTimer = 0.05f;
        }
    }

    public void Teleport(Vector3 position)
    {
        GlobalPosition = position;
        Velocity = Vector3.Zero;
    }
}
