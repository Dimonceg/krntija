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
    private const float JumpSpeed = 5.2f;
    private const float StepHeight = 0.34f;
    private const float BodyRadius = 0.34f;

    private bool _jumpRequested;

    /// <summary>Множитель скорости: 1 в норме, ниже — когда карман перегружен.</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    public float Yaw { get; set; }

    private float _stepTimer;
    private TravellerVisual? _visual;

    /// <summary>Спрятать тело игрока в виде от первого лица.</summary>
    public void SetModelVisible(bool visible)
    {
        if (_visual is not null) _visual.Visible = visible;
        foreach (var child in GetChildren())
        {
            if (child is MeshInstance3D mesh) mesh.Visible = visible;
        }
    }

    public override void _Ready()
    {
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = BodyRadius, Height = 1.78f },
            Position = new Vector3(0, 0.89f, 0),
        });

        _visual = TravellerVisual.Create();
        if (_visual is not null)
        {
            AddChild(_visual);
            return;
        }

        var cloth = new StandardMaterial3D { AlbedoColor = new Color(0.33f, 0.37f, 0.44f), Roughness = 0.95f };
        AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.34f, Height = 1.78f },
            Position = new Vector3(0, 0.89f, 0),
            MaterialOverride = cloth,
        });

        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.15f, Height = 0.30f },
            Position = new Vector3(0, 1.80f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.84f, 0.7f, 0.56f), Roughness = 0.8f },
        });
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Input.MouseMode != Input.MouseModeEnum.Captured) return;

        if (@event is InputEventMouseMotion motion)
            Yaw -= motion.Relative.X * MouseSensitivity * GameSettings.MouseSensitivity;

        // Пробел пешком — прыжок (в машине он остаётся ручником).
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
            _jumpRequested = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        Rotation = new Vector3(0, Yaw, 0);

        float move = Input.GetActionStrength("accelerate") - Input.GetActionStrength("brake");
        float strafe = Input.GetAxis("steer_left", "steer_right");
        float speed = (Input.IsActionPressed("sprint") ? RunSpeed : WalkSpeed) * SpeedMultiplier;

        Vector3 forward = -GlobalBasis.Z;
        Vector3 right = GlobalBasis.X;

        Vector3 velocity = Velocity;
        velocity.X = forward.X * move * speed + right.X * strafe * speed * 0.8f;
        velocity.Z = forward.Z * move * speed + right.Z * strafe * speed * 0.8f;
        velocity.Y -= Gravity * dt;

        if (IsOnFloor() && _jumpRequested)
        {
            velocity.Y = JumpSpeed;
            Sfx.PlayStep(GlobalPosition);
        }

        _jumpRequested = false;

        Vector3 horizontal = new(velocity.X, 0, velocity.Z);
        if (IsOnFloor() && horizontal.Length() > 0.8f && velocity.Y <= 0)
            TryStepUp(horizontal * dt);

        Velocity = velocity;
        MoveAndSlide();
        _visual?.Animate(dt, GlobalBasis.Inverse() * GetRealVelocity(), IsOnFloor());

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

    private void TryStepUp(Vector3 motion)
    {
        using var obstruction = new KinematicCollision3D();
        if (!TestMove(GlobalTransform, motion, obstruction, SafeMargin)) return;
        float floorNormal = Mathf.Cos(FloorMaxAngle);
        // A walkable slope is already handled by MoveAndSlide. It is not a stair.
        if (obstruction.GetNormal().Dot(Vector3.Up) >= floorNormal) return;

        var lift = Vector3.Up * StepHeight;
        if (TestMove(GlobalTransform, lift, safeMargin: SafeMargin)) return;
        var raised = new Transform3D(GlobalBasis, GlobalPosition + lift);
        if (TestMove(raised, motion, safeMargin: SafeMargin)) return;

        // Probe just beyond the rounded toe: immediately above a stair edge a
        // capsule sweep reports the corner normal, not the flat tread. The initial
        // sweep still requires an obstruction in THIS frame; we do not advance X/Z.
        var destination = new Transform3D(GlobalBasis,
            raised.Origin + motion.Normalized() * (BodyRadius + motion.Length()));
        using var support = new KinematicCollision3D();
        if (!TestMove(destination, Vector3.Down * (StepHeight + .02f), support, SafeMargin)) return;
        if (support.GetNormal().Dot(Vector3.Up) < floorNormal) return;
        float rise = StepHeight + support.GetTravel().Y;
        if (rise > .005f && rise <= StepHeight)
            GlobalPosition += Vector3.Up * rise;
    }
}
