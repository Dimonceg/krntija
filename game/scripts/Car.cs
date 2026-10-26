using System.Linq;
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
    private float WheelRadius = 0.36f;
    private const float RestLength = 0.40f;
    private const float SpringStiffness = 26000f;
    private const float SpringDamping = 3000f;
    private const float MaxBrakeForce = 11000f;
    private const float MaxSteerRad = 0.55f;
    private const float MaxReverseSpeed = 8f;

    private Vector3[] Anchors =
    {
        new(-0.86f, 0f, -1.45f), // перед лево
        new(0.86f, 0f, -1.45f),  // перед право
        new(-0.86f, 0f, 1.45f),  // зад лево
        new(0.86f, 0f, 1.45f),   // зад право
    };

    private readonly Node3D[] _wheels = new Node3D[4];
    private readonly Node3D[] _wheelVisuals = new Node3D[4];

    /// <summary>Корень кузова с навесами улучшений; null — процедурная заглушка.</summary>
    private CarUpgradeVisuals? _body;

    /// <summary>Навесы на кузове: для проверок.</summary>
    public CarUpgradeVisuals? BodyVisuals => _body;

    /// <summary>Кузов из деталей (ADR 0003); null — у машины нет раскладки гнёзд.</summary>
    private CarAssemblyVisual? _assembly;

    /// <summary>Детали кузова по гнёздам: состояние, открывание, узлы деталей.</summary>
    public CarAssemblyVisual? Assembly => _assembly;

    private bool WheelInstalled(int index) =>
        _assembly is null || _assembly.State.IsInstalled(CarAssemblyVisual.WheelSockets[index]);

    /// <summary>
    /// Мировой трансформ гнезда (закрытое положение): к нему примагничивается деталь в
    /// руках. Колёса — точка крепления на оси в покое. Null — у машины нет такого гнезда.
    /// </summary>
    public Transform3D? SocketGlobal(string socket)
    {
        if (_assembly is null) return null;

        int wheel = System.Array.IndexOf(CarAssemblyVisual.WheelSockets, socket);
        return wheel >= 0
            ? GlobalTransform * new Transform3D(Basis.Identity, Anchors[wheel] - Vector3.Up * RestLength)
            : _assembly.SocketGlobal(socket);
    }

    /// <summary>Колёса не под корнем кузова: рессоры их не поднимают. Для проверок.</summary>
    public bool WheelsOutsideBody => _body is null || _wheelVisuals.All(wheel => !_body.IsAncestorOf(wheel));
    private bool _modelBody;

    public GameSession Session { get; private set; } = null!;
    public bool Autopilot { get; set; }

    /// <summary>Подменить модель кузова для съёмки/сравнения вариантов.</summary>
    public static string? ModelOverride { get; set; }

    /// <summary>За рулём ли игрок. Когда вышел — машина просто стоит и молотит на холостых.</summary>
    public bool Driven { get; set; } = true;

    /// <summary>Принудительный ввод для автотестов: заменяет клавиатуру.</summary>
    public DriveInput? ForcedInput { get; set; }

    /// <summary>Осевая дороги для автопилота (тесты и будущий трафик).</summary>
    public System.Collections.Generic.List<Vector3>? Path { get; set; }

    /// <summary>Точка впереди на дороге для автопилота (мир кусками). Важнее <see cref="Path"/>.</summary>
    public System.Func<Vector3, Vector3>? PathTarget { get; set; }

    private int _pathIndex;

    public int GroundedWheels { get; private set; }

    /// <summary>Ручник нажат — для индикатора в HUD.</summary>
    public bool HandbrakeActive { get; private set; }

    private bool _handbrakeOn;

    /// <summary>Ручник взведён (стояночный режим).</summary>
    public bool HandbrakeOn => _handbrakeOn;

    /// <summary>Стояночный тормоз: на старте взведён, чтобы машина не катилась.</summary>
    public void SetHandbrake(bool on) => _handbrakeOn = on;

    /// <summary>Мокрое полотно держит хуже: множитель приходит из погоды.</summary>
    public float GripScale { get; set; } = 1f;

    public bool DebugForces { get; set; }

    private int _physicsFrames;
    private float _lastSpeed;
    private SpotLight3D? _headlightLeft;
    private SpotLight3D? _headlightRight;
    private EngineAudio? _audio;

    public float Kmh => LinearVelocity.Length() * 3.6f;
    public float SpeedMps => LinearVelocity.Length();

    /// <summary>Габарит кузова в осях машины: низ — порог, верх — крыша.</summary>
    public Aabb BodyBounds { get; private set; } = new(new Vector3(-1f, -0.5f, -2f), new Vector3(2f, 1.4f, 4f));

    /// <summary>
    /// Глаз водителя в осях машины: слева (у нас левый руль), на уровне глаз,
    /// чуть впереди середины салона. Считается от кузова, поэтому подходит любой
    /// машине с моделью, а не только Zastava.
    /// </summary>
    public Vector3 DriverEyeLocal => new(
        -BodyBounds.Size.X * 0.18f,
        BodyBounds.Position.Y + BodyBounds.Size.Y * 0.83f,
        -BodyBounds.Size.Z * 0.03f);

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
        car.AddToGroup("car");
        return car;
    }

    private void BuildBody()
    {
        // Геометрия колёс берётся из спеки: у каждой машины своя база и колея.
        var spec = Session.Vehicle.Spec;
        _modelBody = spec.WheelbaseM > 0 && spec.TrackM > 0;

        if (_modelBody)
        {
            WheelRadius = (float)spec.WheelRadiusM;

            float halfTrack = (float)(spec.TrackM / 2);
            float halfBase = (float)(spec.WheelbaseM / 2);
            Anchors = new[]
            {
                new Vector3(-halfTrack, 0, -halfBase), new Vector3(halfTrack, 0, -halfBase),
                new Vector3(-halfTrack, 0, halfBase), new Vector3(halfTrack, 0, halfBase),
            };
        }

        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = _modelBody
                ? new Vector3((float)spec.BodyWidthM * 0.94f, 0.60f, (float)spec.BodyLengthM * 0.97f)
                : new Vector3(1.6f, 0.52f, 4.1f) },
            Position = new Vector3(0, _modelBody ? -0.10f : 0.12f, 0),
        });

        // Если в assets/models/cars лежит модель — берём её, иначе собираем заглушку.
        // Машина из деталей (ADR 0003): оболочка от арта вместо цельного кузова, если она есть.
        string specId = Session.Vehicle.Spec.Id;
        bool assembled = ModelOverride is null && CarAssemblyVisual.HasLayout(specId);
        var shell = assembled ? ModelLibrary.Instantiate($"res://assets/models/cars/{specId}_shell.glb") : null;

        var model = shell ?? ModelLibrary.CarBody(ModelOverride ?? specId);
        if (model is not null)
        {
            // Кузов — под корнем навесов: рессоры поднимают его вместе с ними, колёса — нет.
            _body = CarUpgradeVisuals.Create(ModelOverride is null ? Session.Vehicle.Spec.Id : string.Empty);
            AddChild(_body);
            _body.AddChild(model);
            _body.Sync(Session.Upgrades);
            if (PropFactory.Measure(model) is { } bounds)
            {
                BodyBounds = bounds;
                GD.Print($"[car] кузов {bounds.Size.X:F2} × {bounds.Size.Y:F2} × {bounds.Size.Z:F2} м, низ {bounds.Position.Y:F2}");
                if (assembled) _assembly = CarAssemblyVisual.Build(_body, specId, shell, bounds);
            }
        }
        else BuildProceduralShell();

        BuildWheels();

        AddHeadlights();

        _audio = new EngineAudio();
        AddChild(_audio);
    }

    /// <summary>Процедурный кузов: работает, пока модели нет.</summary>
    private void BuildProceduralShell()
    {
        var paint = new StandardMaterial3D { AlbedoColor = new Color(0.66f, 0.45f, 0.24f), Roughness = 0.7f, Metallic = 0.05f };
        var trim = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.16f, 0.17f), Roughness = 0.85f };
        var glass = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.11f, 0.13f), Roughness = 0.15f, Metallic = 0.4f };
        var chrome = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.72f, 0.7f), Metallic = 0.8f, Roughness = 0.35f };
        var lamp = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.95f, 0.8f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.92f, 0.7f),
            EmissionEnergyMultiplier = 1.2f,
        };
        var tail = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.1f, 0.08f),
            EmissionEnabled = true,
            Emission = new Color(0.7f, 0.05f, 0.03f),
            EmissionEnergyMultiplier = 0.8f,
        };

        // Кузов собирается из простых объёмов: низ, борта, капот, багажник, кабина.
        Part(new Vector3(1.62f, 0.34f, 4.02f), new Vector3(0, 0.02f, 0), paint);
        Part(new Vector3(1.66f, 0.30f, 3.9f), new Vector3(0, 0.26f, 0), paint);
        Part(new Vector3(1.5f, 0.12f, 1.1f), new Vector3(0, 0.45f, -1.32f), paint);
        Part(new Vector3(1.5f, 0.10f, 0.9f), new Vector3(0, 0.44f, 1.52f), paint);
        Part(new Vector3(1.44f, 0.5f, 1.86f), new Vector3(0, 0.66f, 0.12f), paint);
        Part(new Vector3(1.47f, 0.26f, 1.9f), new Vector3(0, 0.70f, 0.12f), glass);
        Part(new Vector3(1.3f, 0.07f, 1.7f), new Vector3(0, 0.9f, 0.12f), paint);

        Part(new Vector3(1.72f, 0.14f, 0.2f), new Vector3(0, 0.13f, -2.02f), trim);
        Part(new Vector3(1.72f, 0.14f, 0.2f), new Vector3(0, 0.13f, 2.02f), trim);
        Part(new Vector3(0.95f, 0.16f, 0.06f), new Vector3(0, 0.32f, -2.03f), trim);

        Part(new Vector3(0.3f, 0.15f, 0.05f), new Vector3(-0.56f, 0.36f, -2.03f), lamp);
        Part(new Vector3(0.3f, 0.15f, 0.05f), new Vector3(0.56f, 0.36f, -2.03f), lamp);
        Part(new Vector3(0.3f, 0.13f, 0.05f), new Vector3(-0.6f, 0.36f, 2.03f), tail);
        Part(new Vector3(0.3f, 0.13f, 0.05f), new Vector3(0.6f, 0.36f, 2.03f), tail);

        Part(new Vector3(0.36f, 0.12f, 0.02f), new Vector3(0, 0.16f, -2.04f), chrome);
        Part(new Vector3(0.12f, 0.09f, 0.16f), new Vector3(-0.85f, 0.66f, -0.7f), trim);
        Part(new Vector3(0.12f, 0.09f, 0.16f), new Vector3(0.85f, 0.66f, -0.7f), trim);

        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { Height = 0.18f, TopRadius = 0.055f, BottomRadius = 0.055f },
            Position = new Vector3(0.48f, 0.06f, 2.06f),
            Rotation = new Vector3(Mathf.Pi / 2, 0, 0),
            MaterialOverride = trim,
        });
    }

    private void BuildWheels()
    {
        var rubber = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.29f, 0.28f), Roughness = 1.0f };
        var hubMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.6f, 0.58f), Metallic = 0.5f, Roughness = 0.55f };

        for (int i = 0; i < Anchors.Length; i++)
        {
            var wheel = new Node3D
            {
                Name = $"Wheel{i}",
                Position = Anchors[i] - Vector3.Up * RestLength,
            };
            AddChild(wheel);
            var visual = new Node3D { Name = "RollingMesh" };
            wheel.AddChild(visual);
            var imported = _modelBody ? ModelLibrary.CarWheel(Session.Vehicle.Spec.Id) : null;
            if (imported is not null)
            {
                visual.AddChild(imported);
            }
            else
            {
                var tire = new MeshInstance3D
                {
                    Mesh = new CylinderMesh { Height = 0.24f, TopRadius = WheelRadius, BottomRadius = WheelRadius },
                    MaterialOverride = rubber,
                    Rotation = new Vector3(0, 0, Mathf.Pi / 2),
                };
                visual.AddChild(tire);
                tire.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { Height = 0.26f, TopRadius = 0.17f, BottomRadius = 0.17f },
                    MaterialOverride = hubMaterial,
                });
            }
            _wheels[i] = wheel;
            _wheelVisuals[i] = visual;
        }
    }

    private void Part(Vector3 size, Vector3 position, Material material)
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = material,
        });
    }

    private void AddHeadlights()
    {
        if (_modelBody)
        {
            var spec = Session.Vehicle.Spec;
            float x = (float)(spec.BodyWidthM * 0.34);
            float z = -(float)(spec.BodyLengthM / 2) - 0.06f;

            _headlightLeft = CreateHeadlight(new Vector3(-x, 0.06f, z));
            _headlightRight = CreateHeadlight(new Vector3(x, 0.06f, z));
            return;
        }

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

        // Дальность света — число из ядра: фары ставятся у верстака.
        bool upgraded = Session.Upgrades.Contains(Krntija.Core.Items.UpgradeCatalog.Headlights);
        float range = (float)Krntija.Core.UpgradeEffects.LightsRange(upgraded);

        if (_headlightLeft is not null)
        {
            _headlightLeft.LightEnergy = energy;
            _headlightLeft.SpotRange = range;
        }

        if (_headlightRight is not null)
        {
            _headlightRight.LightEnergy = energy;
            _headlightRight.SpotRange = range;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Session is null) return;

        float dt = (float)delta;
        _physicsFrames++;

        var input = ForcedInput
            ?? (Autopilot
                ? new DriveInput(Ignition: true, Throttle: 0.65, Steer: AutopilotSteer())
                : ReadInput());

        // Ручник — переключатель: нажал и стоит, нажал ещё раз — отпустил.
        input = input with { Handbrake = input.Handbrake || _handbrakeOn };

        // Удар: резкая потеря скорости за кадр — это износ кузова, а не торможение.
        float speedNow = LinearVelocity.Length();
        float speedDrop = _lastSpeed - speedNow;
        _lastSpeed = speedNow;
        if (speedDrop > Krntija.Core.CrashRules.ThresholdMps)
            Session.Systems.Impact(speedDrop);

        var sample = new VehicleSample(speedNow, AmbientC: 20);

        float forwardSpeed = LinearVelocity.Dot(-GlobalBasis.Z);
        HandbrakeActive = input.Handbrake;

        bool reversing = input.Brake > 0.5
                         && !input.Handbrake
                         && Session.Vehicle.EngineRunning
                         && forwardSpeed <= 0.6f;

        Session.Step(dt, sample, input, reversing);
        UpdateHeadlights();
        _body?.Sync(Session.Upgrades);
        _assembly?.Sync();

        if (_audio is not null)
        {
            _audio.Rpm = Session.DriveTrain.Rpm;
            _audio.SpeedMps = SpeedMps;
            _audio.Load = input.Throttle;
            Sfx.SetWind(0.16f + Mathf.Clamp(SpeedMps / 35f, 0f, 1f) * 0.5f);
            _audio.Running = Session.Vehicle.EngineRunning;
        }

        ApplyForces(dt, input, forwardSpeed, reversing);

        // Стояночный тормоз действительно держит: на уклоне машина не катится.
        float horizontal = new Vector2(LinearVelocity.X, LinearVelocity.Z).Length();
        if (_handbrakeOn && GroundedWheels > 0 && horizontal < 1.2f)
        {
            LinearVelocity = new Vector3(0, LinearVelocity.Y, 0);
            AngularVelocity = new Vector3(0, AngularVelocity.Y * 0.5f, 0);
        }

        SpinWheels(dt, forwardSpeed, (float)input.Steer);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Autopilot || ForcedInput is not null) return;
        if (@event.IsActionPressed("handbrake"))
            _handbrakeOn = !_handbrakeOn;
    }

    private DriveInput ReadInput() => Driven
        ? new DriveInput(
            Ignition: Input.IsActionJustPressed("ignition"),
            Throttle: Input.GetActionStrength("accelerate"),
            Brake: Input.GetActionStrength("brake"),
            Steer: Input.GetAxis("steer_left", "steer_right"))
        : default;

    private void ApplyForces(float dt, in DriveInput input, float forwardSpeed, bool reversing)
    {
        Vector3 up = GlobalBasis.Y.Normalized();
        Vector3 forward = -GlobalBasis.Z.Normalized();
        Vector3 right = GlobalBasis.X.Normalized();
        Vector3 com = GlobalPosition;

        float mass = Mass;
        float grip = 10f * (float)Session.Systems.GripFactor * GripScale;
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
            // Снятое колесо не держит свой угол: машина ложится на кузов.
            bool wheelOn = WheelInstalled(i);
            if (_wheels[i].Visible != wheelOn) _wheels[i].Visible = wheelOn;
            if (!wheelOn) continue;

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
            Vector3 point = PhysicsHit.Position(hit);
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

            // Ручник срывает заднюю ось в скольжение: боковое сцепление падает.
            float wheelGrip = input.Handbrake && i >= 2 ? grip * 0.28f : grip;
            float lateralForce = -lateralSpeed * wheelGrip * mass * 0.25f;
            lateralForce = Mathf.Clamp(lateralForce, -mass * 12f, mass * 12f);
            ApplyForce(wheelRight * lateralForce, point - com);

            // Тяга — на заднюю ось, но без пары сил: иначе машина встаёт на дыбы.
            if (i >= 2 && throttle > 0.01f && forwardSpeed < maxSpeed)
            {
                ApplyForce(forward * throttle * driveForce);
            }

            // Задний ход: та же задняя ось, сила в противоположную сторону.
            if (i >= 2 && reversing && forwardSpeed > -MaxReverseSpeed)
            {
                float reverseForce = Mathf.Min(powerW / Mathf.Max(Mathf.Abs(forwardSpeed), 4f), 5200f) * 0.75f;
                ApplyForce(-forward * reverseForce);
            }

            // Тормоз — только когда действительно катимся, и не в режиме заднего хода.
            if (!reversing && brake > 0.01f && Mathf.Abs(forwardSpeed) > 0.3f)
            {
                Vector3 back = -LinearVelocity.Normalized();
                ApplyForce(back * brake * MaxBrakeForce * 0.25f, point - com);
            }

            // Ручник держит задние колёса: тормозит и на ходу, и на последних сантиметрах.
            if (input.Handbrake && i >= 2 && SpeedMps > 0.05f)
            {
                Vector3 back = -LinearVelocity.Normalized();
                ApplyForce(back * 7000f, point - com);
            }

            _wheels[i].Position = ToLocal(point) + Vector3.Up * WheelRadius;
        }

        GroundedWheels = contacts;

        // Аэродинамическое сопротивление: без него с горки машина разгоняется
        // до 200 км/ч, потому что ограничитель стоит только на тяге.
        if (SpeedMps > 0.1f)
            ApplyForce(-LinearVelocity.Normalized() * (1.15f * SpeedMps * SpeedMps));

        // Небольшая прижимная сила на скорости — машина перестаёт козлить.
        ApplyForce(-up * Mathf.Min(SpeedMps * 45f, 2500f));

        if (contacts == 0)
            ApplyTorque(-AngularVelocity * mass * 0.05f);
    }

    /// <summary>Простое следование по осевой: смотрим на точку впереди и рулим к ней.</summary>
    private float AutopilotSteer()
    {
        if (PathTarget is not null)
            return Mathf.Clamp((GlobalTransform.AffineInverse() * PathTarget(GlobalPosition)).X * 0.12f, -1f, 1f);

        if (Path is null || Path.Count < 2) return 0;

        float best = float.MaxValue;
        int bestIndex = _pathIndex;

        int limit = Mathf.Min(_pathIndex + 60, Path.Count);
        for (int i = _pathIndex; i < limit; i++)
        {
            float distance = Path[i].DistanceSquaredTo(GlobalPosition);
            if (distance >= best) continue;

            best = distance;
            bestIndex = i;
        }

        _pathIndex = bestIndex;

        Vector3 target = Path[Mathf.Min(bestIndex + 5, Path.Count - 1)];
        Vector3 local = GlobalTransform.AffineInverse() * target;

        return Mathf.Clamp(local.X * 0.12f, -1f, 1f);
    }

    private void SpinWheels(float dt, float forwardSpeed, float steer)
    {
        float spin = forwardSpeed / WheelRadius * dt;
        for (int i = 0; i < _wheels.Length; i++)
        {
            _wheels[i].Rotation = new Vector3(0, i < 2 ? -steer * MaxSteerRad : 0, 0);
            _wheelVisuals[i].RotateX(-spin);
        }
    }
}
