using Godot;
using Krntija.Core;
using Krntija.Core.Vehicle;

namespace Krntija.Game;

/// <summary>
/// Машина на raycast-подвеске. Физику твёрдого тела считает Godot,
/// а всё «железо» (расход, нагрев, износ) — Krntija.Core.
/// </summary>
public partial class Car : RigidBody3D
{
    private const float WheelRadius = 0.34f;
    private const float RestLength = 0.40f;
    private const float SpringStiffness = 26000f;
    private const float SpringDamping = 3000f;
    private const float MaxBrakeForce = 11000f;
    private const float MaxSteerRad = 0.55f;

    private static readonly Vector3[] Anchors =
    {
        new(-0.86f, 0f, -1.35f), // перед лево
        new(0.86f, 0f, -1.35f),  // перед право
        new(-0.86f, 0f, 1.35f),  // зад лево
        new(0.86f, 0f, 1.35f),   // зад право
    };

    private readonly MeshInstance3D[] _wheels = new MeshInstance3D[4];

    public GameSession Session { get; private set; } = null!;
    public bool Autopilot { get; set; }

    /// <summary>За рулём ли игрок. Когда вышел — машина просто стоит и молотит на холостых.</summary>
    public bool Driven { get; set; } = true;

    public int GroundedWheels { get; private set; }

    public bool DebugForces { get; set; }

    private int _physicsFrames;
    private SpotLight3D? _headlightLeft;
    private SpotLight3D? _headlightRight;
    private EngineAudio? _audio;

    public float Kmh => LinearVelocity.Length() * 3.6f;
    public float SpeedMps => LinearVelocity.Length();

    public string GearLabel
    {
        get
        {
            float kmh = Kmh;
            if (kmh < 1) return "N";
            if (kmh < 25) return "1";
            if (kmh < 50) return "2";
            if (kmh < 80) return "3";
            return "4";
        }
    }

    public static Car Build(GameSession session)
    {
        var car = new Car
        {
            Session = session,
            Mass = (float)session.Vehicle.TotalMassKg,
            CenterOfMassMode = CenterOfMassModeEnum.Custom,
            CenterOfMass = new Vector3(0, -0.45f, 0),
            LinearDamp = 0.02f,
            AngularDamp = 3.5f,
            ContinuousCd = true,
            ContactMonitor = true,
            MaxContactsReported = 8,
        };

        car.BuildBody();
        return car;
    }

    private void BuildBody()
    {
        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.5f, 0.5f, 4.2f) },
            Position = new Vector3(0, 0.10f, 0),
        });

        var paint = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.42f, 0.22f), Roughness = 0.75f };

        var body = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.5f, 0.5f, 4.2f) },
            Position = new Vector3(0, 0.10f, 0),
            MaterialOverride = paint,
        };
        AddChild(body);

        var cabin = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.34f, 0.55f, 1.9f) },
            Position = new Vector3(0, 0.58f, 0.15f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.38f, 0.2f), Roughness = 0.8f },
        };
        AddChild(cabin);

        var rubber = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.29f, 0.28f), Roughness = 1.0f };
        var hubMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.6f, 0.58f), Metallic = 0.5f, Roughness = 0.55f };

        for (int i = 0; i < Anchors.Length; i++)
        {
            var wheel = new MeshInstance3D
            {
                Mesh = new CylinderMesh { Height = 0.24f, TopRadius = WheelRadius, BottomRadius = WheelRadius },
                MaterialOverride = rubber,
                Position = Anchors[i] - Vector3.Up * RestLength,
                Rotation = new Vector3(0, 0, Mathf.Pi / 2),
            };
            AddChild(wheel);

            // Колпак чуть шире шины, чтобы колесо читалось в тени.
            wheel.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { Height = 0.26f, TopRadius = 0.17f, BottomRadius = 0.17f },
                MaterialOverride = hubMaterial,
            });

            _wheels[i] = wheel;
        }

        AddHeadlights();

        _audio = new EngineAudio();
        AddChild(_audio);
    }

    private void AddHeadlights()
    {
        _headlightLeft = CreateHeadlight(new Vector3(-0.52f, 0.42f, -2.02f));
        _headlightRight = CreateHeadlight(new Vector3(0.52f, 0.42f, -2.02f));
    }

    private SpotLight3D CreateHeadlight(Vector3 position)
    {
        var light = new SpotLight3D
        {
            Position = position,
            RotationDegrees = new Vector3(-3, 0, 0),
            SpotRange = 70,
            SpotAngle = 42,
            LightEnergy = 0,
            LightColor = new Color(1f, 0.93f, 0.72f),
        };
        AddChild(light);
        return light;
    }

    private void UpdateHeadlights()
    {
        float energy = Session.Clock.IsNight ? 8f : 0f;
        if (_headlightLeft is not null) _headlightLeft.LightEnergy = energy;
        if (_headlightRight is not null) _headlightRight.LightEnergy = energy;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Session is null) return;

        float dt = (float)delta;
        _physicsFrames++;

        var input = Autopilot
            ? new DriveInput(Ignition: true, Throttle: 1.0)
            : ReadInput();

        var sample = new VehicleSample(LinearVelocity.Length(), AmbientC: 20);
        Session.Step(dt, sample, input);
        UpdateHeadlights();

        if (_audio is not null)
        {
            _audio.Rpm = Session.DriveTrain.Rpm;
            _audio.SpeedMps = SpeedMps;
            _audio.Running = Session.Vehicle.EngineRunning;
        }

        ApplyForces(dt, input);
        SpinWheels(dt);
    }

    private DriveInput ReadInput() => Driven
        ? new DriveInput(
            Ignition: Input.IsActionJustPressed("ignition"),
            Throttle: Input.GetActionStrength("accelerate"),
            Brake: Input.GetActionStrength("brake"),
            Steer: Input.GetAxis("steer_left", "steer_right"),
            Handbrake: Input.IsActionPressed("handbrake"))
        : default;

    private void ApplyForces(float dt, in DriveInput input)
    {
        Vector3 up = GlobalBasis.Y.Normalized();
        Vector3 forward = -GlobalBasis.Z.Normalized();
        Vector3 right = GlobalBasis.X.Normalized();
        Vector3 com = GlobalPosition;

        float mass = Mass;
        float grip = 10f * (float)Session.Systems.GripFactor;
        int contacts = 0;

        var space = GetWorld3D().DirectSpaceState;
        float steer = (float)input.Steer * MaxSteerRad;
        float throttle = (float)input.Throttle * (float)Session.Systems.PowerFactor;
        float brake = (float)input.Brake * (float)Session.Systems.BrakeFactor;

        // Мощность берём из модели: «Запорожец» и «шестёрка» едут по-разному.
        var spec = Session.Vehicle.Spec;
        float maxSpeed = (float)spec.TopSpeedMps;
        float powerW = (float)(spec.PowerKw * 1000);
        float driveForce = Mathf.Min(powerW / Mathf.Max(SpeedMps, 4f), 8000f);

        for (int i = 0; i < Anchors.Length; i++)
        {
            Vector3 anchor = ToGlobal(Anchors[i]);
            var query = PhysicsRayQueryParameters3D.Create(anchor, anchor - up * (RestLength + WheelRadius));
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
            var hit = space.IntersectRay(query);

            if (hit.Count == 0)
            {
                _wheels[i].Position = Anchors[i] - Vector3.Up * RestLength;
                continue;
            }

            contacts++;
            Vector3 point = (Vector3)hit["position"];
            float distance = anchor.DistanceTo(point);
            float compression = Mathf.Max(0f, RestLength + WheelRadius - distance);

            Vector3 contactVelocity = LinearVelocity + AngularVelocity.Cross(point - com);
            float closing = -LinearVelocity.Dot(up);
            float springForce = SpringStiffness * compression + SpringDamping * closing;
            springForce = Mathf.Clamp(springForce, 0f, 90000f);

            if (DebugForces && _physicsFrames < 60 && i == 0)
                GD.Print($"[f{_physicsFrames}] y={GlobalPosition.Y:F3} vy={LinearVelocity.Y:F2} comp={compression:F3} force={springForce:F0}");

            ApplyForce(up * springForce, point - com);

            // Шины держат поперёк своего качения; передние повёрнуты рулём.
            Vector3 wheelRight = right;
            if (i < 2) wheelRight = right.Rotated(up, -steer);

            float lateralSpeed = contactVelocity.Dot(wheelRight);
            float lateralForce = -lateralSpeed * grip * mass * 0.25f;
            lateralForce = Mathf.Clamp(lateralForce, -mass * 12f, mass * 12f);
            ApplyForce(wheelRight * lateralForce, point - com);

            // Тяга — на заднюю ось, но без пары сил: иначе машина встаёт на дыбы.
            if (i >= 2 && throttle > 0.01f && SpeedMps < maxSpeed)
            {
                ApplyForce(forward * throttle * driveForce);
            }

            // Тормоз — на все колёса.
            if (brake > 0.01f && SpeedMps > 0.3f)
            {
                Vector3 back = -LinearVelocity.Normalized();
                ApplyForce(back * brake * MaxBrakeForce * 0.25f, point - com);
            }

            _wheels[i].Position = ToLocal(point) + Vector3.Up * WheelRadius;
        }

        GroundedWheels = contacts;

        // Небольшая прижимная сила на скорости — машина перестаёт козлить.
        ApplyForce(-up * Mathf.Min(SpeedMps * 45f, 2500f));

        if (contacts == 0)
            ApplyTorque(-AngularVelocity * mass * 0.05f);
    }

    private void SpinWheels(float dt)
    {
        float spin = SpeedMps / WheelRadius * dt;
        foreach (var wheel in _wheels)
            wheel.RotateX(-spin);
    }
}
